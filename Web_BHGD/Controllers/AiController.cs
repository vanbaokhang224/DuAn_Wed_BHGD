using Microsoft.AspNetCore.Mvc;
using Web_BHGD.Services;

[Route("api/test-ai")]
public class TestController : Controller
{
    private readonly AiService _ai;

    public TestController(AiService ai)
    {
        _ai = ai;
    }

    [HttpGet]
    public async Task<string> Get(string q = "Xin chào, bạn là ai?")
    {
        return await _ai.AskAsync(q);
    }
}