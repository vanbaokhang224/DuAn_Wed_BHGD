using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_BHGD.Models;

namespace Web_BHGD.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class OrderDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderDetailsModel(ApplicationDbContext context, ILogger<OrderDetailsModel> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        public string StatusMessage { get; set; }
        public Order Order { get; set; }
        public List<OrderDetail> OrderItems { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Attempting to load order details for ID: {OrderId}", id);
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    StatusMessage = "Không tìm thấy thông tin người dùng.";
                    _logger.LogWarning("User not found.");
                    return RedirectToPage("./OrderHistory");
                }

                var userId = user.Id; // Sử dụng Id thay vì email
                _logger.LogInformation("User ID: {UserId}", userId);

                Order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .ThenInclude(oDetail => oDetail.Product)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (Order == null)
                {
                    StatusMessage = $"Không tìm thấy đơn hàng với ID {id} hoặc bạn không có quyền xem.";
                    _logger.LogWarning("Order with ID {OrderId} not found for user {UserId}", id, userId);
                    return RedirectToPage("./OrderHistory");
                }

                OrderItems = Order.OrderDetails?.ToList() ?? new List<OrderDetail>();
                _logger.LogInformation("Successfully loaded order with ID: {OrderId}", id);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order details for ID {OrderId}", id);
                StatusMessage = "Lỗi hệ thống khi tải chi tiết đơn hàng.";
                return RedirectToPage("./OrderHistory");
            }
        }

        // THÊM METHOD NÀY ĐỂ XỬ LÝ HỦY ĐỚN HÀNG
        [HttpPost]
        public async Task<IActionResult> OnPostCancelOrderAsync(int orderId)
        {
            _logger.LogInformation("Attempting to cancel order with ID: {OrderId}", orderId);

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found when trying to cancel order {OrderId}", orderId);
                    return new JsonResult(new { success = false, message = "Không tìm thấy người dùng" });
                }

                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id);

                if (order == null)
                {
                    _logger.LogWarning("Order with ID {OrderId} not found for user {UserId}", orderId, user.Id);
                    return new JsonResult(new { success = false, message = "Không tìm thấy đơn hàng hoặc bạn không có quyền thực hiện" });
                }

                // Kiểm tra trạng thái có thể hủy
                if (order.Status != "Chờ xác nhận" && order.Status != "Đã xác nhận")
                {
                    _logger.LogWarning("Cannot cancel order {OrderId} with status: {Status}", orderId, order.Status);
                    return new JsonResult(new { success = false, message = $"Không thể hủy đơn hàng ở trạng thái '{order.Status}'" });
                }

                // Cập nhật trạng thái đơn hàng
                order.Status = "Huỷ";
                // Nếu có trường UpdatedDate trong model Order
                // order.UpdatedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully cancelled order {OrderId} for user {UserId}", orderId, user.Id);
                return new JsonResult(new { success = true, message = "Đơn hàng đã được hủy thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                return new JsonResult(new { success = false, message = "Có lỗi xảy ra khi hủy đơn hàng: " + ex.Message });
            }
        }
    }
}