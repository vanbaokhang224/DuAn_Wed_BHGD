using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Web_BHGD.Models;
using Web_BHGD.Repositories;

namespace Web_BHGD.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;

        // 👉 CHỈ GIỮ LẠI 1 CONSTRUCTOR DUY NHẤT
        public ProductController(
            ApplicationDbContext context,
            IProductRepository productRepository,
            ICategoryRepository categoryRepository)
        {
            _context = context;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<IActionResult> Index(int? categoryId, string searchString, string sortOrder, int page = 1)
        {
            const int pageSize = 12;

            var (products, totalCount) = await _productRepository.GetFilteredAndPagedAsync(
                categoryId, searchString, sortOrder, page, pageSize);

            var categories = await _categoryRepository.GetAllAsync();

            var categoryList = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Tất cả danh mục" }
            };

            categoryList.AddRange(categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }));

            ViewBag.CategorySelectList = new SelectList(categoryList, "Value", "Text", categoryId?.ToString());
            ViewBag.CategoryList = categories;
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentCategory = categoryId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
             var reviews = _context.Reviews
             .Include(r => r.User)
             .Where(r => r.ProductId == id && r.IsApproved)
             .OrderByDescending(r => r.CreatedAt)
             .ToList();

            ViewBag.Reviews = reviews;

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            ViewBag.RelatedProducts =
                await _productRepository.GetRelatedProductsAsync(product.CategoryId, product.Id, 4);

            return View("Details", product);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsByCategory(int categoryId)
        {
            var products = await _productRepository.GetAllAsync();
            var filtered = products.Where(p => p.CategoryId == categoryId).ToList();

            return Json(filtered.Select(p => new {
                id = p.Id,
                name = p.Name,
                price = p.Price,
                imageUrl = p.ImageUrl,
                description = p.Description
            }));
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrEmpty(query))
                return Json(new List<object>());

            var products = await _productRepository.GetAllAsync();
            var searchResults = products
                .Where(p =>
                    p.Name.ToLower().Contains(query.ToLower()) ||
                    p.Description.ToLower().Contains(query.ToLower()))
                .Take(10)
                .ToList();

            return Json(searchResults.Select(p => new {
                id = p.Id,
                name = p.Name,
                price = p.Price,
                imageUrl = p.ImageUrl
            }));
        }
    }
}