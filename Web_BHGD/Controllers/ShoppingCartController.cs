using Web_BHGD.Models;
using Web_BHGD.Repositories;
using Web_BHGD.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Web_BHGD.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShoppingCartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProductRepository productRepository)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
        }

        // ========================== GIỎ HÀNG ==========================
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            ViewBag.TotalAmount = cart.Items.Sum(i => i.Price * i.Quantity);
            ViewBag.TotalItems = cart.Items.Sum(i => i.Quantity);

            return View(cart);
        }

        // ========================== THÊM GIỎ ==========================
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (productId <= 0)
                return JsonError("ID sản phẩm không hợp lệ.");

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return JsonError("Sản phẩm không tồn tại.");

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            cart.AddItem(new CartItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Quantity = quantity
            });

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new
            {
                success = true,
                itemCount = cart.Items.Sum(i => i.Quantity),
                message = $"Đã thêm {product.Name} vào giỏ hàng."
            });
        }

        // ========================== API: XÁC NHẬN THANH TOÁN ==========================
        [HttpPost]
        public IActionResult ConfirmPayment()
        {
            // Không cần thêm logic — JS gọi API này để xác nhận
            return Json(new { success = true });
        }

        // ========================== GET SỐ LƯỢNG GIỎ ==========================
        [HttpGet]
        public IActionResult GetCartItemCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            return Json(new { count = cart?.Items.Sum(i => i.Quantity) ?? 0 });
        }

        // ========================== UPDATE SỐ LƯỢNG ==========================
        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
                return JsonError("Sản phẩm không tồn tại trong giỏ.");

            if (quantity <= 0)
                return RemoveFromCart(productId);

            item.Quantity = quantity;

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new
            {
                success = true,
                itemTotal = (item.Price * item.Quantity).ToString("#,##0"),
                totalPrice = cart.Items.Sum(i => i.Price * i.Quantity).ToString("#,##0")
            });
        }

        // ========================== XOÁ SẢN PHẨM ==========================
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
                return JsonError("Sản phẩm không có trong giỏ.");

            cart.RemoveItem(productId);

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new
            {
                success = true,
                totalPrice = cart.Items.Sum(i => i.Price * i.Quantity).ToString("#,##0"),
                message = $"Đã xóa {item.Name} khỏi giỏ."
            });
        }

        // ========================== XOÁ GIỎ ==========================
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return Json(new { success = true });
        }

        // ========================== TRANG CHECKOUT ==========================
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");

            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);

            var order = new Order
            {
                UserId = user.Id,
                CustomerName = user.FullName,
                CustomerPhone = user.PhoneNumber,
                CustomerEmail = user.Email,
                OrderDate = DateTime.Now,
                TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity),
                PaymentMethod = "COD",
                Status = "Chờ xác nhận",
                IsPaid = false
            };

            ViewBag.CartItems = cart.Items;

            return View(order);
        }

        // ========================== XỬ LÝ ĐẶT HÀNG ==========================
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Checkout(Order order, bool isQrPaymentComplete = false)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");

            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng rỗng.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cart.Items;
                return View(order);
            }

            // =================== BẮT BUỘC PHẢI XÁC NHẬN THANH TOÁN ===================
            if ((order.PaymentMethod == "MoMo" || order.PaymentMethod == "Bank") && isQrPaymentComplete == false)
            {
                TempData["Error"] = "Bạn cần quét mã QR và nhấn 'Hoàn tất thanh toán'.";
                ViewBag.CartItems = cart.Items;
                return View(order);
            }

            var user = await _userManager.GetUserAsync(User);

            order.UserId = user.Id;
            order.OrderDate = DateTime.Now;
            order.Status = "Chờ xác nhận";
            order.TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity);
            order.IsPaid = isQrPaymentComplete; // ⬅ lưu trạng thái đã thanh toán

            order.OrderDetails = cart.Items.Select(i => new OrderDetail
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList();

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("Cart");

            return RedirectToAction("Success", new { id = order.Id });
        }

        public IActionResult Success(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        // ========================== TIỆN ÍCH ==========================
        private JsonResult JsonError(string msg)
        {
            return Json(new { success = false, message = msg });
        }
    }
}