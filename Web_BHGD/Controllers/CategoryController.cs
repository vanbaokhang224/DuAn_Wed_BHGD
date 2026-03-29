using Web_BHGD.Models;
using Web_BHGD.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Web_BHGD.Controllers
{
    [AllowAnonymous]
    public class CategoryController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;

        public CategoryController(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // Hiển thị danh sách danh mục cho khách hàng
        public async Task<IActionResult> Index()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var products = await _productRepository.GetAllAsync();

            ViewBag.ProductCounts = categories.ToDictionary(c => c.Id, c => products.Count(p => p.CategoryId == c.Id));
            ViewBag.Categories = categories;

            return View(categories);
        }

        public async Task<IActionResult> Details(int id, string sortOrder, int page = 1)
        {
            // Lấy danh mục
            var category = id == 0 ? new Category { Id = 0, Name = "Tất cả danh mục" } : await _categoryRepository.GetByIdAsync(id);
            if (category == null && id != 0)
            {
                return NotFound();
            }

            // Lấy danh sách sản phẩm
            var products = id == 0 ? await _productRepository.GetAllAsync() : await _productRepository.GetByCategoryIdAsync(id);

            // Sắp xếp
            switch (sortOrder)
            {
                case "name_desc":
                    products = products.OrderByDescending(p => p.Name);
                    break;
                case "price":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                default:
                    products = products.OrderBy(p => p.Id);
                    break;
            }

            // Phân trang
            int pageSize = 12;
            int totalItems = products.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            products = products.Skip((page - 1) * pageSize).Take(pageSize);

            // Truyền ViewBag
            ViewBag.Category = category;
            ViewBag.Categories = await _categoryRepository.GetAllAsync(); // Đảm bảo truyền danh sách danh mục
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentCategoryId = id;

            return View(products);
        }

        // API để lấy danh sách danh mục (dùng cho dropdown, menu, v.v.)
        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return Json(categories.Select(c => new
            {
                id = c.Id,
                name = c.Name
            }));
        }

        // Hiển thị menu danh mục (partial view)
        public async Task<IActionResult> CategoryMenu()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return PartialView("_CategoryMenu", categories);
        }
    }
}