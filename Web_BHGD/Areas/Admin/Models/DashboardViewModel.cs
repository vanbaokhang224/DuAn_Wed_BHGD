using System.ComponentModel.DataAnnotations;

namespace Web_BHGD.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        // Thống kê tổng quan
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public int TotalUsers { get; set; }

        // Thống kê đơn hàng theo trạng thái
        public int PendingOrders { get; set; }
        public int ConfirmedOrders { get; set; }
        public int ShippingOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }

        // Thống kê sản phẩm
        public int LowStockProducts { get; set; }
        public int OutOfStockProducts { get; set; }
        public int TotalProductsInStock { get; set; }
        public int TotalProductsSold { get; set; }

        // Thống kê theo thời gian
        public decimal TodayRevenue { get; set; }
        public int TodayOrders { get; set; }
        public decimal ThisWeekRevenue { get; set; }
        public int ThisWeekOrders { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public int ThisMonthOrders { get; set; }

        // Dữ liệu biểu đồ
        public List<MonthlyRevenueViewModel> MonthlyRevenue { get; set; } = new List<MonthlyRevenueViewModel>();
        public List<MonthlyOrderViewModel> MonthlyOrders { get; set; } = new List<MonthlyOrderViewModel>();
        public List<TopSellingProductViewModel> TopSellingProducts { get; set; } = new List<TopSellingProductViewModel>();
        public List<RecentOrderViewModel> RecentOrders { get; set; } = new List<RecentOrderViewModel>();
    }

    public class MonthlyRevenueViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
        public string MonthShort => new DateTime(Year, Month, 1).ToString("MM/yyyy");
    }

    public class MonthlyOrderViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
        public string MonthShort => new DateTime(Year, Month, 1).ToString("MM/yyyy");
        public decimal SuccessRate => TotalOrders > 0 ? (decimal)DeliveredOrders / TotalOrders * 100 : 0;
    }

    public class TopSellingProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public int SoldQuantity { get; set; }
        public decimal Price { get; set; }
        public decimal Revenue { get; set; }
        public int Stock { get; set; }

        public string StockStatus => Stock == 0 ? "Hết hàng" : Stock <= 10 ? "Sắp hết" : "Còn hàng";
        public string StockStatusClass => Stock == 0 ? "text-danger" : Stock <= 10 ? "text-warning" : "text-success";
    }

    public class RecentOrderViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }

        public string StatusClass => Status switch
        {
            "Chờ xác nhận" => "badge bg-warning",
            "Đã xác nhận" => "badge bg-info",
            "Đang giao hàng" => "badge bg-primary",
            "Đã giao hàng" => "badge bg-success",
            "Huỷ" => "badge bg-danger",
            _ => "badge bg-secondary"
        };

        public string PaymentMethodDisplay => PaymentMethod switch
        {
            "COD" => "Thanh toán khi nhận hàng",
            "BankTransfer" => "Chuyển khoản ngân hàng",
            "CreditCard" => "Thẻ tín dụng",
            _ => PaymentMethod
        };
    }
}