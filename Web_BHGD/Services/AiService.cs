using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Web_BHGD.Services
{
    public class AiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public AiService(IConfiguration config)
        {
            _apiKey = config["OpenAI:ApiKey"];
            _http = new HttpClient();
        }

        public async Task<string> AskAsync(string question)
        {
            var url = "https://api.openai.com/v1/chat/completions";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "Bạn là trợ lý AI của BHGD Store." },
                    new { role = "user", content = question }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _http.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseText);

            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString();
        }
    }
}