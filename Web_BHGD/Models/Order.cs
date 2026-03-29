using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Web_BHGD.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string? UserId { get; set; }

        public DateTime OrderDate { get; set; }

        [Range(0, 999999999, ErrorMessage = "Tổng giá phải nằm trong khoảng 0 - 999,999,999 VNĐ")]
        public decimal TotalPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        public string? Notes { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string CustomerEmail { get; set; }

        // ❗ BỎ REQUIRED — STATUS DO HỆ THỐNG TỰ GÁN
        public string Status { get; set; } = "Pending";

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD";

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser? ApplicationUser { get; set; }

        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        // ✔ biến ghi lại trạng thái đã thanh toán QR
        public bool IsPaid { get; set; } = false;
    }
}