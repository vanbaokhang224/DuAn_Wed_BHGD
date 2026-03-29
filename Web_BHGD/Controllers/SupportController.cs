using Microsoft.AspNetCore.Mvc;
using Web_BHGD.Models;
using Web_BHGD.Areas.Admin.Models;
using Web_BHGD.Services;
using System.Linq;

namespace Web_BHGD.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class SupportController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AiService _aiService;

        public SupportController(ApplicationDbContext db, AiService aiService)
        {
            _db = db;
            _aiService = aiService;
        }
       
        public IActionResult ReturnPolicy() => View();

        public IActionResult WarrantyPolicy() => View();

        public IActionResult ShoppingGuide() => View();

        public IActionResult Chat(string userId)
        {
            // Nếu chưa đăng nhập → dùng ID guest
            if (string.IsNullOrEmpty(userId))
            {
                if (!Request.Cookies.ContainsKey("GuestId"))
                {
                    Response.Cookies.Append("GuestId", "guest-" + Guid.NewGuid().ToString());
                }

                userId = Request.Cookies["GuestId"];
            }

            ViewBag.UserId = userId;

            var messages = _db.ChatMessages
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            return View(messages);
        }

        public IActionResult Index()
        {
            var conversations = _db.ChatMessages
                .Select(x => new
                {
                    SafeUserId = x.UserId ?? ("guest-" + x.UserName),
                    x.Message,
                    x.CreatedAt,
                    x.IsAdmin
                })
                .GroupBy(x => x.SafeUserId)
                .Select(g => new ChatSummary
                {
                    UserId = g.Key,
                    LastMessage = g.OrderByDescending(x => x.CreatedAt).First().Message,
                    LastTime = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(x => x.LastTime)
                .ToList();

            return View(conversations);
        }
        [HttpGet]
        public IActionResult GetMessages(string userId)
        {
            var list = _db.ChatMessages
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new {
                    message = x.Message,
                    isAdmin = x.IsAdmin
                })
                .ToList();

            return Json(list);
        }
        [HttpPost]
        public IActionResult SendMessage(string userId, string message)
        {
            var msg = new ChatMessage
            {
                UserId = userId,
                Message = message,
                IsAdmin = false,
                CreatedAt = DateTime.Now
            };

            _db.ChatMessages.Add(msg);
            _db.SaveChanges();

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> AskAI(string question)
        {
            try
            {
                var reply = await _aiService.AskAsync(question);
                return Json(reply);
            }
            catch (Exception ex)
            {
                return Json("LỖI: " + ex.Message);
            }
        }

    }
}