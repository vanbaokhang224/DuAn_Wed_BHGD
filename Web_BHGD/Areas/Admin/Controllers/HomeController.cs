using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_BHGD.Areas.Admin.Models;
using Web_BHGD.Models;

namespace Web_BHGD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Tính toán các giá trị date trước khi query
                var today = DateTime.Today;
                var startOfWeek = GetStartOfWeek(today);
                var endOfWeek = today.AddDays(1);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = today.AddDays(1);

                var dashboardData = new DashboardViewModel();

                // Thống kê tổng quan - tuần tự để tránh deadlock
                dashboardData.TotalRevenue = await CalculateTotalRevenue();
                dashboardData.TotalOrders = await _context.Orders.CountAsync();
                dashboardData.TotalProducts = await _context.Products.CountAsync();
                dashboardData.TotalUsers = await _context.Users.CountAsync();

                // Thống kê đơn hàng theo trạng thái
                dashboardData.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Chờ xác nhận");
                dashboardData.ConfirmedOrders = await _context.Orders.CountAsync(o => o.Status == "Đã xác nhận");
                dashboardData.ShippingOrders = await _context.Orders.CountAsync(o => o.Status == "Đang giao hàng");
                dashboardData.DeliveredOrders = await _context.Orders.CountAsync(o => o.Status == "Đã giao hàng");
                dashboardData.CancelledOrders = await _context.Orders.CountAsync(o => o.Status == "Huỷ");

                // Thống kê sản phẩm - FIX: Tránh infinite loop
                await LoadProductStats(dashboardData);

                // Thống kê theo thời gian
                dashboardData.TodayRevenue = await CalculateRevenueByDate(today);
                dashboardData.TodayOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date == today);

                dashboardData.ThisWeekRevenue = await CalculateRevenueByDateRange(startOfWeek, endOfWeek);
                dashboardData.ThisWeekOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date >= startOfWeek && o.OrderDate.Date < endOfWeek);

                dashboardData.ThisMonthRevenue = await CalculateRevenueByDateRange(startOfMonth, endOfMonth);
                dashboardData.ThisMonthOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date >= startOfMonth && o.OrderDate.Date < endOfMonth);

                // Load dữ liệu phức tạp
                await LoadChartData(dashboardData);

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải dashboard: " + ex.Message;
                return View(GetEmptyDashboard());
            }
        }

        private async Task LoadProductStats(DashboardViewModel dashboardData)
        {
            try
            {
                // Load từng thống kê riêng biệt để tránh conflict
                dashboardData.LowStockProducts = await _context.Products
                    .CountAsync(p => p.Stock > 0 && p.Stock <= 10);

                dashboardData.OutOfStockProducts = await _context.Products
                    .CountAsync(p => p.Stock == 0);

                // Sử dụng ToListAsync để tránh vấn đề với SumAsync
                var products = await _context.Products
                    .Select(p => new { p.Stock, p.SoldQuantity })
                    .ToListAsync();

                dashboardData.TotalProductsInStock = products.Sum(p => p.Stock);
                dashboardData.TotalProductsSold = products.Sum(p => p.SoldQuantity);
            }
            catch (Exception ex)
            {
                // Fallback values nếu có lỗi
                dashboardData.LowStockProducts = 0;
                dashboardData.OutOfStockProducts = 0;
                dashboardData.TotalProductsInStock = 0;
                dashboardData.TotalProductsSold = 0;
            }
        }

        private async Task LoadChartData(DashboardViewModel dashboardData)
        {
            try
            {
                dashboardData.MonthlyRevenue = await GetMonthlyRevenue();
            }
            catch
            {
                dashboardData.MonthlyRevenue = GetEmptyMonthlyRevenue();
            }

            try
            {
                dashboardData.MonthlyOrders = await GetMonthlyOrders();
            }
            catch
            {
                dashboardData.MonthlyOrders = GetEmptyMonthlyOrders();
            }

            try
            {
                dashboardData.TopSellingProducts = await GetTopSellingProducts();
            }
            catch
            {
                dashboardData.TopSellingProducts = new List<TopSellingProductViewModel>();
            }

            try
            {
                dashboardData.RecentOrders = await GetRecentOrders();
            }
            catch
            {
                dashboardData.RecentOrders = new List<RecentOrderViewModel>();
            }
        }

        private async Task<decimal> CalculateTotalRevenue()
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.Status == "Đã giao hàng")
                    .Select(o => o.TotalPrice)
                    .ToListAsync();

                return orders.Sum();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<decimal> CalculateRevenueByDate(DateTime date)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.Status == "Đã giao hàng" && o.OrderDate.Date == date.Date)
                    .Select(o => o.TotalPrice)
                    .ToListAsync();

                return orders.Sum();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<decimal> CalculateRevenueByDateRange(DateTime startDate, DateTime endDate)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => o.Status == "Đã giao hàng" && o.OrderDate.Date >= startDate.Date && o.OrderDate.Date < endDate.Date)
                    .Select(o => o.TotalPrice)
                    .ToListAsync();

                return orders.Sum();
            }
            catch
            {
                return 0;
            }
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private async Task<List<MonthlyRevenueViewModel>> GetMonthlyRevenue()
        {
            var sixMonthsAgo = DateTime.Today.AddMonths(-5);
            var firstDayOfSixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

            var orders = await _context.Orders
                .Where(o => o.Status == "Đã giao hàng" && o.OrderDate >= firstDayOfSixMonthsAgo)
                .Select(o => new { o.OrderDate.Year, o.OrderDate.Month, o.TotalPrice })
                .ToListAsync();

            var monthlyData = orders
                .GroupBy(o => new { o.Year, o.Month })
                .Select(g => new MonthlyRevenueViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(o => o.TotalPrice),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            return FillMissingMonths(monthlyData);
        }

        private async Task<List<MonthlyOrderViewModel>> GetMonthlyOrders()
        {
            var sixMonthsAgo = DateTime.Today.AddMonths(-5);
            var firstDayOfSixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

            var orders = await _context.Orders
                .Where(o => o.OrderDate >= firstDayOfSixMonthsAgo)
                .Select(o => new { o.OrderDate.Year, o.OrderDate.Month, o.Status })
                .ToListAsync();

            var monthlyData = orders
                .GroupBy(o => new { o.Year, o.Month })
                .Select(g => new MonthlyOrderViewModel
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.Status == "Đã giao hàng"),
                    CancelledOrders = g.Count(o => o.Status == "Huỷ")
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            return FillMissingMonthsOrders(monthlyData);
        }

        private async Task<List<TopSellingProductViewModel>> GetTopSellingProducts()
        {
            var products = await _context.Products
                .Where(p => p.SoldQuantity > 0)
                .OrderByDescending(p => p.SoldQuantity)
                .Take(10)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.SoldQuantity,
                    p.Price,
                    p.Stock,
                    CategoryId = p.CategoryId
                })
                .ToListAsync();

            var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
            var categories = await _context.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            return products.Select(p => new TopSellingProductViewModel
            {
                Id = p.Id,
                Name = p.Name ?? "N/A",
                CategoryName = categories.ContainsKey(p.CategoryId) ? categories[p.CategoryId] : "N/A",
                SoldQuantity = p.SoldQuantity,
                Price = p.Price,
                Revenue = p.SoldQuantity * p.Price,
                Stock = p.Stock
            }).ToList();
        }

        private async Task<List<RecentOrderViewModel>> GetRecentOrders()
        {
            return await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new RecentOrderViewModel
                {
                    Id = o.Id,
                    CustomerName = o.CustomerName ?? "N/A",
                    OrderDate = o.OrderDate,
                    TotalPrice = o.TotalPrice,
                    Status = o.Status ?? "N/A",
                    PaymentMethod = o.PaymentMethod ?? "N/A"
                })
                .ToListAsync();
        }

        private List<MonthlyRevenueViewModel> FillMissingMonths(List<MonthlyRevenueViewModel> monthlyData)
        {
            var result = new List<MonthlyRevenueViewModel>();
            for (int i = 0; i < 6; i++)
            {
                var targetDate = DateTime.Today.AddMonths(-5 + i);
                var existingData = monthlyData.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);

                result.Add(existingData ?? new MonthlyRevenueViewModel
                {
                    Year = targetDate.Year,
                    Month = targetDate.Month,
                    Revenue = 0,
                    OrderCount = 0
                });
            }
            return result;
        }

        private List<MonthlyOrderViewModel> FillMissingMonthsOrders(List<MonthlyOrderViewModel> monthlyData)
        {
            var result = new List<MonthlyOrderViewModel>();
            for (int i = 0; i < 6; i++)
            {
                var targetDate = DateTime.Today.AddMonths(-5 + i);
                var existingData = monthlyData.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);

                result.Add(existingData ?? new MonthlyOrderViewModel
                {
                    Year = targetDate.Year,
                    Month = targetDate.Month,
                    TotalOrders = 0,
                    DeliveredOrders = 0,
                    CancelledOrders = 0
                });
            }
            return result;
        }

        private DashboardViewModel GetEmptyDashboard()
        {
            return new DashboardViewModel
            {
                MonthlyRevenue = GetEmptyMonthlyRevenue(),
                MonthlyOrders = GetEmptyMonthlyOrders(),
                TopSellingProducts = new List<TopSellingProductViewModel>(),
                RecentOrders = new List<RecentOrderViewModel>()
            };
        }

        private List<MonthlyRevenueViewModel> GetEmptyMonthlyRevenue()
        {
            var result = new List<MonthlyRevenueViewModel>();
            for (int i = 0; i < 6; i++)
            {
                var targetDate = DateTime.Today.AddMonths(-5 + i);
                result.Add(new MonthlyRevenueViewModel
                {
                    Year = targetDate.Year,
                    Month = targetDate.Month,
                    Revenue = 0,
                    OrderCount = 0
                });
            }
            return result;
        }

        private List<MonthlyOrderViewModel> GetEmptyMonthlyOrders()
        {
            var result = new List<MonthlyOrderViewModel>();
            for (int i = 0; i < 6; i++)
            {
                var targetDate = DateTime.Today.AddMonths(-5 + i);
                result.Add(new MonthlyOrderViewModel
                {
                    Year = targetDate.Year,
                    Month = targetDate.Month,
                    TotalOrders = 0,
                    DeliveredOrders = 0,
                    CancelledOrders = 0
                });
            }
            return result;
        }
    }
}