using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_BHGD.Models;

namespace Web_BHGD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Hiển thị danh sách đơn hàng
        public async Task<IActionResult> Index()
        {
            if (_context.Orders == null)
            {
                TempData["Error"] = "Không thể truy cập dữ liệu đơn hàng.";
                return View(new List<Order>());
            }

            var orders = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.ApplicationUser)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // Xem chi tiết đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            if (_context.Orders == null)
            {
                TempData["Error"] = "Không thể truy cập dữ liệu đơn hàng.";
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = $"Không tìm thấy đơn hàng #{id}.";
                return NotFound();
            }

            return View(order);
        }

        // Cập nhật trạng thái đơn hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (_context.Orders == null)
            {
                TempData["Error"] = "Không thể truy cập dữ liệu đơn hàng.";
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = $"Không tìm thấy đơn hàng #{id}.";
                return NotFound();
            }

            var validStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang giao hàng", "Đã giao hàng", "Huỷ" };
            if (!validStatuses.Contains(status))
            {
                TempData["Error"] = "Trạng thái không hợp lệ.";
                return RedirectToAction("Details", new { id });
            }

            var oldStatus = order.Status;

            // Kiểm tra logic chuyển trạng thái
            if (!IsValidStatusTransition(oldStatus, status))
            {
                TempData["Error"] = $"Không thể chuyển từ trạng thái '{oldStatus}' sang '{status}'.";
                return RedirectToAction("Details", new { id });
            }

            // Xử lý logic khi chuyển sang "Đã xác nhận" - áp dụng cho mọi phương thức thanh toán
            if (status == "Đã xác nhận" && oldStatus != "Đã xác nhận")
            {
                // Kiểm tra số lượng tồn kho cho tất cả sản phẩm
                foreach (var detail in order.OrderDetails)
                {
                    var product = detail.Product;
                    if (product.Stock < detail.Quantity)
                    {
                        TempData["Error"] = $"Sản phẩm {product.Name} không đủ tồn kho. Hiện có: {product.Stock}, cần: {detail.Quantity}";
                        return RedirectToAction("Details", new { id });
                    }
                }

                // Cập nhật tồn kho và số lượng đã bán
                foreach (var detail in order.OrderDetails)
                {
                    var product = detail.Product;
                    product.Stock -= detail.Quantity;
                    product.SoldQuantity += detail.Quantity;
                    _context.Products.Update(product);
                }
            }

            // Xử lý logic khi hủy đơn hàng
            if (status == "Huỷ" && (oldStatus == "Đã xác nhận" || oldStatus == "Đang giao hàng"))
            {
                // Hoàn lại tồn kho và giảm số lượng đã bán
                foreach (var detail in order.OrderDetails)
                {
                    var product = detail.Product;
                    product.Stock += detail.Quantity;
                    product.SoldQuantity = Math.Max(0, product.SoldQuantity - detail.Quantity);
                    _context.Products.Update(product);
                }
            }

            order.Status = status;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật trạng thái đơn hàng #{id} thành '{status}'.";
            return RedirectToAction("Details", new { id });
        }

        // Hủy đơn hàng (method riêng cho nút hủy)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCancel(int id)
        {
            if (_context.Orders == null)
            {
                TempData["Error"] = "Không thể truy cập dữ liệu đơn hàng.";
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = $"Không tìm thấy đơn hàng #{id}.";
                return NotFound();
            }

            if (order.Status == "Huỷ")
            {
                TempData["Error"] = "Đơn hàng này đã được hủy trước đó.";
                return RedirectToAction("Details", new { id });
            }

            if (order.Status == "Đã giao hàng")
            {
                TempData["Error"] = "Không thể hủy đơn hàng đã giao thành công.";
                return RedirectToAction("Details", new { id });
            }

            var oldStatus = order.Status;

            // Hoàn lại tồn kho nếu đơn hàng đã được xác nhận
            if (oldStatus == "Đã xác nhận" || oldStatus == "Đang giao hàng")
            {
                foreach (var detail in order.OrderDetails)
                {
                    var product = detail.Product;
                    product.Stock += detail.Quantity;
                    product.SoldQuantity = Math.Max(0, product.SoldQuantity - detail.Quantity);
                    _context.Products.Update(product);
                }
            }

            order.Status = "Huỷ";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã hủy đơn hàng #{id}.";
            return RedirectToAction("Index");
        }

        // Tạo hóa đơn HTML
        public async Task<IActionResult> GenerateInvoice(int id)
        {
            if (_context.Orders == null)
            {
                TempData["Error"] = "Không thể truy cập dữ liệu đơn hàng.";
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product) // Chỉ cần load Product
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = $"Không tìm thấy đơn hàng #{id}.";
                return NotFound();
            }

            return View("Invoice", order);
        }

        // Kiểm tra tính hợp lệ của việc chuyển trạng thái
        private bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            // Định nghĩa các chuyển đổi trạng thái hợp lệ
            var validTransitions = new Dictionary<string, string[]>
            {
                ["Chờ xác nhận"] = new[] { "Đã xác nhận","Đang giao hàng", "Huỷ" },
                ["Đã xác nhận"] = new[] { "Đang giao hàng", "Đã giao hàng", "Huỷ" },
                ["Đang giao hàng"] = new[] { "Đã giao hàng", "Huỷ" },
                ["Đã giao hàng"] = new string[] { },
                ["Huỷ"] = new string[] { } 
            };

            if (currentStatus == newStatus)
                return true; // Cho phép giữ nguyên trạng thái

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(newStatus);
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "Chờ xác nhận" => "Chờ xác nhận",
                "Đã xác nhận" => "Đã xác nhận",
                "Đang giao hàng" => "Đang giao hàng",
                "Đã giao hàng" => "Đã giao hàng",
                "Huỷ" => "Huỷ",
                _ => status
            };
        }
    }
}