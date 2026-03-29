using System;
using System.ComponentModel.DataAnnotations;

namespace Web_BHGD.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        public string OrderId { get; set; } // hoặc int OrderId theo dự án
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // "QR-BANK" hoặc "COD" ...
        public string QrPayload { get; set; } // chuỗi payload cho QR (iso20022/vietqr)
        public string QrImagePath { get; set; } // lưu file png nếu muốn
        public bool IsPaid { get; set; } = false;
        public string ProofImagePath { get; set; } // user upload xác nhận
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; }

        public string Status { get; set; } = "Pending";  // trạng thái thanh toán
    }
}