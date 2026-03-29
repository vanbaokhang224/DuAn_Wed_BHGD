using Microsoft.AspNetCore.Mvc;

namespace Web_BHGD.Areas.Admin.Models
{
    public class EditUserRolesViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public List<UserRoleSelection> Roles { get; set; } = new();
    }

    public class UserRoleSelection
    {
        public string RoleName { get; set; }
        public bool IsSelected { get; set; }
    }
}
