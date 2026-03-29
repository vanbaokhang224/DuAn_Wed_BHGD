namespace Web_BHGD.Areas.Admin.Models
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public int? Age { get; set; }
        public List<string> Roles { get; set; }
        public bool IsLocked { get; set; }
    }
}
