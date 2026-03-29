using Microsoft.AspNetCore.Mvc;

namespace Web_BHGD.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string? UserId { get; set; }  // Nếu đã đăng nhập
        public string? UserName { get; set; } // Nếu chưa đăng nhập => Guest
        public string Message { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
