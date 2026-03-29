using Web_BHGD.Models;
using Web_BHGD.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.IO;
using Web_BHGD.Areas.Admin.Models;

namespace Web_BHGD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductController> _logger;
        private readonly string _imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
        private const string DefaultImagePath = "/images/default_product.png";
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ProductController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            ApplicationDbContext context,
            ILogger<ProductController> logger)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync(); // Lấy danh mục
            ViewBag.Categories = new SelectList(categories, "Id", "Name"); // Truyền vào ViewBag

            // Thống kê chi tiết hơn
            ViewBag.TotalProducts = products.Count();
            ViewBag.LowStockCount = products.Count(p => p.Stock > 0 && p.Stock <= 10);
            ViewBag.OutOfStockCount = products.Count(p => p.Stock == 0);
            ViewBag.HighStockCount = products.Count(p => p.Stock > 50);
            ViewBag.TotalValue = products.Sum(p => p.Stock * p.Price);
            ViewBag.TotalSold = products.Sum(p => p.SoldQuantity);

            return View(products);
        }

        // Các action khác giữ nguyên
        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.StockStatus = GetStockStatus(product.Stock);
            ViewBag.StockStatusClass = GetStockStatusClass(product.Stock);

            var totalInitialQuantity = product.Stock + product.SoldQuantity;
            ViewBag.SalesPercentage = totalInitialQuantity > 0
                ? Math.Round((double)product.SoldQuantity / totalInitialQuantity * 100, 1)
                : 0;

            ViewBag.StockValue = product.Stock * product.Price;
            ViewBag.Revenue = product.SoldQuantity * product.Price;
            ViewBag.PerformanceStatus = GetPerformanceStatus(product.SoldQuantity, product.Stock);

            return View(product);
        }

        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(new Product());
        }

        [HttpPost]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl)
        {
            _logger.LogInformation("Bắt đầu thêm sản phẩm: {ProductName}, CategoryId: {CategoryId}, Stock: {Stock}",
                product.Name, product.CategoryId, product.Stock);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ: {Errors}",
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name");
                return View(product);
            }

            if (product.Stock < 0)
            {
                _logger.LogWarning("Số lượng tồn kho không hợp lệ: {Stock}", product.Stock);
                ModelState.AddModelError("Stock", "Số lượng tồn kho phải lớn hơn hoặc bằng 0");
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name");
                return View(product);
            }

            try
            {
                product.SoldQuantity = 0;
                product.ImageUrl = imageUrl != null && imageUrl.Length > 0
                    ? await SaveImage(imageUrl)
                    : DefaultImagePath;

                await _productRepository.AddAsync(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Thêm sản phẩm thành công: {ProductName} - Tồn kho: {Stock}",
                    product.Name, product.Stock);

                TempData["Success"] = $"Thêm sản phẩm '{product.Name}' thành công với {product.Stock} đơn vị tồn kho";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi thêm sản phẩm: {ProductName}", product.Name);
                ModelState.AddModelError("", $"Lỗi cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name");
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi thêm sản phẩm: {ProductName}", product.Name);
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name");
                return View(product);
            }
        }

        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            ViewBag.CurrentStock = product.Stock;
            ViewBag.SoldQuantity = product.SoldQuantity;
            ViewBag.StockValue = product.Stock * product.Price;
            ViewBag.Revenue = product.SoldQuantity * product.Price;

            return View(product);
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl, string ExistingImageUrl)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Xóa validation error cho ImageUrl nếu là update và không có ảnh mới
            if (product.Id > 0 && (imageUrl == null || imageUrl.Length == 0))
            {
                ModelState.Remove("ImageUrl");
            }

            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
                ViewBag.CurrentStock = product.Stock;
                ViewBag.SoldQuantity = product.SoldQuantity;
                return View(product);
            }

            if (product.Stock < 0)
            {
                ModelState.AddModelError("Stock", "Số lượng tồn kho phải lớn hơn hoặc bằng 0");
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
                return View(product);
            }

            try
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                var oldStock = existingProduct.Stock;
                var oldImageUrl = existingProduct.ImageUrl;

                // Xử lý ảnh được cải thiện
                if (imageUrl != null && imageUrl.Length > 0)
                {
                    // Có ảnh mới được upload
                    existingProduct.ImageUrl = await SaveImage(imageUrl);

                    // Xóa ảnh cũ nếu không phải ảnh mặc định
                    if (!string.IsNullOrEmpty(oldImageUrl) &&
                        oldImageUrl != DefaultImagePath &&
                        oldImageUrl.StartsWith("/images/"))
                    {
                        var oldImagePath = Path.Combine("wwwroot", oldImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Không thể xóa ảnh cũ: {OldImagePath}", oldImagePath);
                            }
                        }
                    }
                }
                else
                {
                    // Không có ảnh mới, giữ ảnh hiện tại
                    // Ưu tiên: ExistingImageUrl -> existingProduct.ImageUrl -> DefaultImagePath
                    if (!string.IsNullOrWhiteSpace(ExistingImageUrl))
                    {
                        existingProduct.ImageUrl = ExistingImageUrl;
                    }
                    else if (string.IsNullOrWhiteSpace(existingProduct.ImageUrl))
                    {
                        existingProduct.ImageUrl = DefaultImagePath;
                    }
                    // Nếu existingProduct.ImageUrl đã có giá trị, giữ nguyên
                }

                // Cập nhật các thuộc tính khác
                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Stock = product.Stock;

                await _productRepository.UpdateAsync(existingProduct);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cập nhật sản phẩm thành công: {ProductId} - Tồn kho từ {OldStock} thành {NewStock}",
                    id, oldStock, product.Stock);

                TempData["Success"] = $"Cập nhật sản phẩm '{product.Name}' thành công. Tồn kho hiện tại: {product.Stock}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật sản phẩm: {ProductId}", id);
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
                return View(product);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Không tìm thấy sản phẩm";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra đơn hàng chưa hoàn thành
                var pendingOrders = await _context.OrderDetails
                    .Include(od => od.Order)
                    .Where(od => od.ProductId == id &&
                                (od.Order.Status == "Chờ xác nhận" ||
                                 od.Order.Status == "Đã xác nhận" ||
                                 od.Order.Status == "Đang giao hàng"))
                    .CountAsync();

                if (pendingOrders > 0)
                {
                    ViewBag.DeleteWarning = $"Cảnh báo: Sản phẩm này đang có {pendingOrders} đơn hàng chưa hoàn thành!";
                    ViewBag.CanDelete = false;
                }
                else
                {
                    ViewBag.CanDelete = true;
                    if (product.Stock > 0)
                    {
                        ViewBag.StockWarning = $"Sản phẩm còn {product.Stock} đơn vị trong kho (giá trị: {(product.Stock * product.Price):N0} đ)";
                    }
                    if (product.SoldQuantity > 0)
                    {
                        ViewBag.SalesInfo = $"Sản phẩm đã bán được {product.SoldQuantity} đơn vị (doanh thu: {(product.SoldQuantity * product.Price):N0} đ)";
                    }
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải trang xóa sản phẩm: {ProductId}", id);
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin sản phẩm";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                _logger.LogInformation("Bắt đầu xóa sản phẩm với ID: {ProductId}", id);

                // Lấy sản phẩm với tracking để có thể xóa
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    _logger.LogWarning("Không tìm thấy sản phẩm với ID: {ProductId}", id);
                    TempData["Error"] = "Không tìm thấy sản phẩm cần xóa";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra lại đơn hàng chưa hoàn thành
                var pendingOrders = await _context.OrderDetails
                    .Include(od => od.Order)
                    .Where(od => od.ProductId == id &&
                                (od.Order.Status == "Chờ xác nhận" ||
                                 od.Order.Status == "Đã xác nhận" ||
                                 od.Order.Status == "Đang giao hàng"))
                    .CountAsync();

                if (pendingOrders > 0)
                {
                    _logger.LogWarning("Không thể xóa sản phẩm {ProductId} vì còn {PendingOrders} đơn hàng chưa hoàn thành",
                        id, pendingOrders);
                    TempData["Error"] = $"Không thể xóa sản phẩm vì còn {pendingOrders} đơn hàng chưa hoàn thành";
                    return RedirectToAction(nameof(Index));
                }

                // Lưu thông tin để log
                var productName = product.Name;
                var productStock = product.Stock;
                var productSoldQuantity = product.SoldQuantity;
                var imageUrl = product.ImageUrl;

                // Xóa sản phẩm từ database trước
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã xóa sản phẩm khỏi database: {ProductName} - Tồn kho: {Stock}, Đã bán: {SoldQuantity}",
                    productName, productStock, productSoldQuantity);

                // Sau khi xóa thành công từ database, mới xóa file ảnh
                await DeleteProductImage(imageUrl);

                TempData["Success"] = $"Xóa sản phẩm '{productName}' thành công";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi xóa sản phẩm: {ProductId}", id);
                TempData["Error"] = "Không thể xóa sản phẩm do có ràng buộc dữ liệu. Vui lòng kiểm tra lại các đơn hàng liên quan.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi xóa sản phẩm: {ProductId}", id);
                TempData["Error"] = $"Lỗi khi xóa sản phẩm: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task DeleteProductImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || imageUrl == DefaultImagePath)
                {
                    return; // Không cần xóa ảnh mặc định
                }

                // Chỉ xóa ảnh trong thư mục images của ứng dụng
                if (imageUrl.StartsWith("/images/"))
                {
                    var imagePath = Path.Combine("wwwroot", imageUrl.TrimStart('/'));

                    if (System.IO.File.Exists(imagePath))
                    {
                        // Đợi một chút để đảm bảo file không bị lock
                        await Task.Delay(100);

                        // Thử xóa file
                        System.IO.File.Delete(imagePath);
                        _logger.LogInformation("Đã xóa file ảnh: {ImagePath}", imagePath);
                    }
                    else
                    {
                        _logger.LogWarning("File ảnh không tồn tại: {ImagePath}", imagePath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng không throw exception vì đã xóa thành công khỏi database
                _logger.LogWarning(ex, "Không thể xóa file ảnh: {ImageUrl}", imageUrl);
            }
        }

        // Thêm method kiểm tra ràng buộc dữ liệu
        private async Task<(bool CanDelete, string Reason, int Count)> CheckProductConstraints(int productId)
        {
            try
            {
                // Kiểm tra đơn hàng chưa hoàn thành
                var pendingOrdersCount = await _context.OrderDetails
                    .Include(od => od.Order)
                    .Where(od => od.ProductId == productId &&
                                (od.Order.Status == "Chờ xác nhận" ||
                                 od.Order.Status == "Đã xác nhận" ||
                                 od.Order.Status == "Đang giao hàng"))
                    .CountAsync();

                if (pendingOrdersCount > 0)
                {
                    return (false, "Có đơn hàng chưa hoàn thành", pendingOrdersCount);
                }

                // Có thể thêm các kiểm tra khác ở đây
                // Ví dụ: kiểm tra trong giỏ hàng, wishlist, v.v.

                return (true, string.Empty, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra ràng buộc cho sản phẩm: {ProductId}", productId);
                return (false, "Lỗi hệ thống", 0);
            }
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            _logger.LogInformation("Bắt đầu lưu ảnh: {FileName}, Kích thước: {FileSize} bytes", image.FileName, image.Length);

            if (image == null || image.Length == 0)
            {
                _logger.LogError("File ảnh không hợp lệ: null hoặc rỗng.");
                throw new ArgumentException("File ảnh không hợp lệ.");
            }

            if (image.Length > MaxFileSize)
            {
                _logger.LogError("File ảnh vượt quá kích thước tối đa: {FileSize} bytes.", image.Length);
                throw new ArgumentException("File ảnh vượt quá kích thước tối đa (5MB).");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogError("Phần mở rộng không hợp lệ: {Extension}", extension);
                throw new ArgumentException("Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .gif).");
            }

            try
            {
                using var stream = image.OpenReadStream();
                using var img = SixLabors.ImageSharp.Image.Load(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File không phải là ảnh hợp lệ: {FileName}", image.FileName);
                throw new ArgumentException("File không phải là ảnh hợp lệ.");
            }

            if (!Directory.Exists(_imagePath))
            {
                Directory.CreateDirectory(_imagePath);
            }

            var fileName = $"{Guid.NewGuid()}_{DateTime.UtcNow.Ticks}{extension}";
            var savePath = Path.Combine(_imagePath, fileName);

            try
            {
                using var stream = image.OpenReadStream();
                using var img = SixLabors.ImageSharp.Image.Load(stream);
                img.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(800, 800),
                    Mode = ResizeMode.Max
                }));
                await img.SaveAsync(savePath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 75 });
                return "/images/" + fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu ảnh: {FileName}", image.FileName);
                throw new InvalidOperationException($"Lỗi khi lưu file ảnh: {ex.Message}", ex);
            }
        }

        private string GetStockStatus(int stock)
        {
            return stock switch
            {
                0 => "Hết hàng",
                <= 5 => "Sắp hết hàng",
                <= 10 => "Tồn kho thấp",
                <= 50 => "Tồn kho bình thường",
                _ => "Tồn kho cao"
            };
        }

        private string GetStockStatusClass(int stock)
        {
            return stock switch
            {
                0 => "stock-out",
                <= 5 => "stock-critical",
                <= 10 => "stock-low",
                <= 50 => "stock-normal",
                _ => "stock-high"
            };
        }

        private string GetPerformanceStatus(int soldQuantity, int stock)
        {
            var totalQuantity = soldQuantity + stock;
            if (totalQuantity == 0) return "Chưa có dữ liệu";

            var salesRate = (double)soldQuantity / totalQuantity;

            return salesRate switch
            {
                >= 0.8 => "Bán chạy",
                >= 0.5 => "Bán tốt",
                >= 0.2 => "Bán chậm",
                _ => "Bán rất chậm"
            };
        }
    }
}