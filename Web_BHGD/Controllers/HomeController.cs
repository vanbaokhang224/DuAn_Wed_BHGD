using System.Diagnostics;
using Web_BHGD.Models;
using Web_BHGD.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Web_BHGD.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;

        public HomeController(ILogger<HomeController> logger,
                            IProductRepository productRepository,
                            ICategoryRepository categoryRepository)
        {
            _logger = logger;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy tất cả sản phẩm
            var allProducts = await _productRepository.GetAllAsync();

            // Lấy 8 sản phẩm nổi bật (có thể tùy chỉnh theo tiêu chí như mới nhất, bán chạy, v.v.)
            var featuredProducts = allProducts.Take(8).ToList();

            // Lấy tất cả danh mục
            var categories = await _categoryRepository.GetAllAsync();

            // Tính số lượng sản phẩm trong mỗi danh mục
            var productCounts = allProducts
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Truyền dữ liệu vào ViewBag
            ViewBag.Categories = categories;
            ViewBag.ProductCounts = productCounts;
            ViewBag.FeaturedProducts = featuredProducts;

            // Trả về danh sách tất cả sản phẩm làm model
            return View(allProducts.ToList());
        }

        // Trang giới thiệu về cửa hàng
        public IActionResult About()
        {
            return View();
        }

        // Trang liên hệ
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Tìm kiếm nhanh trên trang chủ
        [HttpPost]
        public async Task<IActionResult> QuickSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return RedirectToAction("Index");
            }

            var products = await _productRepository.GetAllAsync();
            var searchResults = products
                .Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()) ||
                           p.Description.ToLower().Contains(searchTerm.ToLower()))
                .ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SearchResults = searchResults;

            return View("SearchResults", searchResults);
        }
    }
}