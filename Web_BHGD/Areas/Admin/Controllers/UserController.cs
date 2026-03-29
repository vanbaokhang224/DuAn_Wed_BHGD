using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_BHGD.Areas.Admin.Models;
using Web_BHGD.Models;

namespace Web_BHGD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UserController> _logger;

        public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext, ILogger<UserController> logger)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        // Hiển thị danh sách người dùng
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Bắt đầu lấy danh sách người dùng");
            try
            {
                var rawUsers = await _dbContext.Users.ToListAsync();

                var users = rawUsers.Select(u =>
                {
                    int? parsedAge = null;
                    if (!string.IsNullOrEmpty(u.Age) && int.TryParse(u.Age, out int age))
                    {
                        parsedAge = age;
                    }

                    return new UserViewModel
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        Email = u.Email,
                        PhoneNumber = u.PhoneNumber,
                        Address = u.Address,
                        Age = parsedAge,
                        IsLocked = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow
                    };
                }).ToList();

                // Lấy tất cả roles trong một truy vấn
                var userIds = users.Select(u => u.Id).ToList();
                var userRoles = await _dbContext.UserRoles
                    .Where(ur => userIds.Contains(ur.UserId))
                    .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                    .GroupBy(ur => ur.UserId)
                    .ToDictionaryAsync(g => g.Key, g => g.Select(r => r.Name).ToList());

                foreach (var user in users)
                {
                    user.Roles = userRoles.ContainsKey(user.Id) ? userRoles[user.Id] : new List<string>();
                }

                _logger.LogInformation("Lấy danh sách người dùng thành công");
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách người dùng");
                TempData["Error"] = "Lỗi khi lấy danh sách người dùng: " + ex.Message;
                return View(new List<UserViewModel>());
            }
        }

        // Hiển thị chi tiết người dùng
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                _logger.LogWarning("ID người dùng không hợp lệ");
                return NotFound();
            }

            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng với ID: {UserId}", id);
                    return NotFound();
                }

                var roles = await _userManager.GetRolesAsync(user);
                var viewModel = new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    Age = string.IsNullOrEmpty(user.Age) ? (int?)null : int.TryParse(user.Age, out int age) ? age : (int?)null, // Chuyển đổi string sang int?
                    Roles = roles.ToList(),
                    IsLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
                };

                _logger.LogInformation("Lấy thông tin chi tiết người dùng thành công: {UserId}", id);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết người dùng: {UserId}", id);
                TempData["Error"] = $"Lỗi khi lấy chi tiết người dùng: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // Khóa tài khoản người dùng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            if (id == null)
            {
                _logger.LogWarning("ID người dùng không hợp lệ khi khóa tài khoản");
                return NotFound();
            }

            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để khóa: {UserId}", id);
                    return NotFound();
                }

                var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Khóa tài khoản thành công: {UserId}", id);
                    TempData["Success"] = $"Tài khoản {user.FullName} đã bị khóa.";
                }
                else
                {
                    _logger.LogError("Lỗi khi khóa tài khoản: {UserId}", id);
                    TempData["Error"] = "Có lỗi xảy ra khi khóa tài khoản.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi khóa tài khoản: {UserId}", id);
                TempData["Error"] = $"Lỗi khi khóa tài khoản: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }


        // Mở khóa tài khoản người dùng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            if (id == null)
            {
                _logger.LogWarning("ID người dùng không hợp lệ khi mở khóa tài khoản");
                return NotFound();
            }

            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Không tìm thấy người dùng để mở khóa: {UserId}", id);
                    return NotFound();
                }

                var result = await _userManager.SetLockoutEndDateAsync(user, null);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Mở khóa tài khoản thành công: {UserId}", id);
                    TempData["Success"] = $"Tài khoản {user.FullName} đã được mở khóa.";
                }
                else
                {
                    _logger.LogError("Lỗi khi mở khóa tài khoản: {UserId}", id);
                    TempData["Error"] = "Có lỗi xảy ra khi mở khóa tài khoản.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi mở khóa tài khoản: {UserId}", id);
                TempData["Error"] = $"Lỗi khi mở khóa tài khoản: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
        public async Task<IActionResult> EditRole(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("Yêu cầu chỉnh vai trò nhưng ID người dùng bị null hoặc rỗng.");
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với ID: {UserId}", id);
                return NotFound();
            }

            var allRoles = await _dbContext.Roles.Select(r => r.Name).ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;

            _logger.LogInformation("Tải trang chỉnh vai trò cho người dùng {UserId} thành công", id);
            var model = new UserRolesViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                AllRoles = allRoles,
                UserRoles = userRoles.ToList()
            };

            return View(model);
        }

        // Cập nhật vai trò người dùng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(string id, string newRole)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("ID người dùng bị null khi cập nhật vai trò.");
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với ID: {UserId}", id);
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Không thể xóa vai trò cũ của người dùng.";
                return RedirectToAction(nameof(Index));
            }

            var addResult = await _userManager.AddToRoleAsync(user, newRole);
            if (!addResult.Succeeded)
            {
                TempData["Error"] = "Không thể cập nhật vai trò mới.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Cập nhật vai trò thành công cho người dùng {UserId}", id);
            TempData["Success"] = "Cập nhật vai trò thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}