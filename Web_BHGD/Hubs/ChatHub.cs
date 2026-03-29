using Microsoft.AspNetCore.SignalR;
using Web_BHGD.Models;
using Microsoft.EntityFrameworkCore;
using Web_BHGD.Services;

public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;
    private readonly AiService _ai;

    public ChatHub(ApplicationDbContext db, AiService ai)
    {
        _db = db;
        _ai = ai;
    }

    public override async Task OnConnectedAsync()
    {
        string tempId = Context.ConnectionId;
        await Clients.Caller.SendAsync("ReceiveUserId", tempId);
        await base.OnConnectedAsync();
    }

    public async Task Send(string userId, string userName, string message, bool isAdmin)
    {
        var safeId = userId ?? ("guest-" + userName);

        var msg = new ChatMessage
        {
            UserId = safeId,
            UserName = userName,
            Message = message,
            IsAdmin = isAdmin,
            CreatedAt = DateTime.Now
        };

        _db.ChatMessages.Add(msg);
        _db.SaveChanges();

        // Gửi tin nhắn của khách về client
        await Clients.All.SendAsync("addNewMessage", safeId, message, isAdmin);

        // ===========================
        // 🔥 TÍCH HỢP AI TRẢ LỜI
        // ===========================
        if (!isAdmin)
        {
            string aiReply = await _ai.AskAsync(message);

            var aiMsg = new ChatMessage
            {
                UserId = safeId,
                UserName = "AI",
                Message = aiReply,
                IsAdmin = true,
                CreatedAt = DateTime.Now
            };

            _db.ChatMessages.Add(aiMsg);
            _db.SaveChanges();

            // Gửi trả lời AI về KH
            await Clients.Caller.SendAsync("addNewMessage", "AI", aiReply, true);
        }
    }

    public async Task LoadHistory(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            userId = Context.ConnectionId;

        var history = await _db.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        await Clients.Caller.SendAsync("loadChatHistory", history);
    }
}