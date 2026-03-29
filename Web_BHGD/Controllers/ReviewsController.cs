using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_BHGD.Models;

namespace Web_BHGD.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ReviewsController(ApplicationDbContext context) => _context = context;

        [HttpPost]
        public IActionResult Create(int productId, int rating, string comment)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var review = new Review
            {
                ProductId = productId,
                UserId = userId,
                Rating = rating,
                Comment = comment
            };

            _context.Reviews.Add(review);
            _context.SaveChanges();

            TempData["msg"] = "Đánh giá đã gửi, chờ admin duyệt!";
            return RedirectToAction("Details", "Product", new { id = productId });
        }
    }
}