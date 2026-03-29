using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using System.IO;
using Web_BHGD.Models;
using System.Threading.Tasks;

namespace Web_BHGD.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public PaymentsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // Bước 1: Hiển thị form xác nhận thanh toán
        [HttpGet]
        public IActionResult CreateQrPayment(string orderId, decimal amount)
        {
            var model = new Payment
            {
                OrderId = orderId,
                Amount = amount,
                PaymentMethod = "QR-BANK"
            };

            return View(model);
        }

        // Bước 2: Tạo payment, tạo payload và chuyển sang trang hiển thị QR
        [HttpPost]
        public async Task<IActionResult> CreateQrPaymentConfirm(string orderId, decimal amount)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var payload = $"PAYTO|{orderId}|{amount}|Web_BHGD";

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = amount,
                PaymentMethod = "QR-BANK",
                QrPayload = payload,
                UserId = userId,
                Status = "Pending"
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ShowQr), new { id = payment.Id });
        }

        // Bước 3: Hiển thị giao diện QR
        [HttpGet]
        public async Task<IActionResult> ShowQr(int id)
        {
            var payment = await _db.Payments.FindAsync(id);
            if (payment == null) return NotFound();

            // Generate QR code
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrData = qrGenerator.CreateQrCode(payment.QrPayload, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrData))
                {
                    var qrBytes = qrCode.GetGraphic(20);

                    ViewBag.QrBase64 = Convert.ToBase64String(qrBytes);
                }
            }

            return View("ShowQrPayment", payment);
        }

        // Bước 4: Upload ảnh chuyển khoản
        [HttpPost]
        public async Task<IActionResult> UploadProof(int paymentId, IFormFile proof)
        {
            var p = await _db.Payments.FindAsync(paymentId);
            if (p == null) return NotFound();

            if (proof != null && proof.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "payments");
                Directory.CreateDirectory(uploads);

                var fname = $"{Guid.NewGuid()}{Path.GetExtension(proof.FileName)}";
                var fpath = Path.Combine(uploads, fname);

                using (var stream = System.IO.File.Create(fpath))
                    await proof.CopyToAsync(stream);

                p.ProofImagePath = $"/uploads/payments/{fname}";
                p.Status = "Waiting-Confirm";

                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", "Orders", new { id = p.OrderId });
        }
    }
}