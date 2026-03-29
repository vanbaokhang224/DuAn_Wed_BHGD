namespace Web_BHGD.Areas.Admin.Models
{
    public class UserRolesViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }

        public List<string> AllRoles { get; set; } = new();
        public List<string> UserRoles { get; set; } = new();
    }
}