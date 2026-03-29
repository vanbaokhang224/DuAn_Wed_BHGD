using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web_BHGD.Models;

namespace Web_BHGD.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc")]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; }

            // Bỏ Required, chỉ giữ EmailAddress để validate format
            [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            // Không bắt buộc nhập số điện thoại - để nullable
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }

            // Không bắt buộc nhập địa chỉ - để nullable
            [Display(Name = "Địa chỉ")]
            public string Address { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var email = await _userManager.GetEmailAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            var fullName = user.FullName;
            var address = user.Address;

            Input = new InputModel
            {
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                Address = address
            };
        }

        public async Task<IActionResult> OnGetAsync(bool editMode = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            ViewData["EditMode"] = editMode.ToString().ToLower();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            // Đảm bảo Email luôn có giá trị từ database
            if (string.IsNullOrEmpty(Input.Email))
            {
                Input.Email = await _userManager.GetEmailAsync(user);
            }

            // Loại bỏ validation cho các trường không bắt buộc
            ModelState.Remove("Input.Email");
            ModelState.Remove("Input.PhoneNumber");
            ModelState.Remove("Input.Address");

            // Chỉ validate FullName
            if (string.IsNullOrWhiteSpace(Input.FullName))
            {
                ModelState.AddModelError("Input.FullName", "Họ và tên là bắt buộc");
            }

            // Validate PhoneNumber format chỉ khi có giá trị
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                // Kiểm tra format số điện thoại đơn giản
                if (!System.Text.RegularExpressions.Regex.IsMatch(Input.PhoneNumber, @"^[\d\s\-\+\(\)]*$"))
                {
                    ModelState.AddModelError("Input.PhoneNumber", "Số điện thoại không đúng định dạng");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                ViewData["EditMode"] = "true";
                return Page();
            }

            // Cập nhật số điện thoại (có thể null hoặc empty)
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Lỗi không mong muốn khi cập nhật số điện thoại.";
                    ViewData["EditMode"] = "true";
                    return Page();
                }
            }

            // Cập nhật họ và tên
            if (Input.FullName != user.FullName)
            {
                user.FullName = Input.FullName;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Lỗi không mong muốn khi cập nhật họ và tên.";
                    ViewData["EditMode"] = "true";
                    return Page();
                }
            }

            // Cập nhật địa chỉ (có thể null hoặc empty)
            if (Input.Address != user.Address)
            {
                user.Address = Input.Address;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Lỗi không mong muốn khi cập nhật địa chỉ.";
                    ViewData["EditMode"] = "true";
                    return Page();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật thành công.";
            return RedirectToPage(new { editMode = false });
        }
    }
}