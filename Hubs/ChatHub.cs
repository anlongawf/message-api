using Microsoft.AspNetCore.SignalR;

namespace Messenger.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // Tham gia vào một nhóm chat
        public async Task JoinGroup(int groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
            await Clients.Group($"group_{groupId}").SendAsync("UserJoined", new { GroupId = groupId, ConnectionId = Context.ConnectionId });
        }

        // Rời khỏi nhóm chat
        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
            await Clients.Group($"group_{groupId}").SendAsync("UserLeft", new { GroupId = groupId, ConnectionId = Context.ConnectionId });
        }

        // Gửi tin nhắn đến nhóm (đã được xử lý ở controller, method này chỉ để tham khảo)
        public async Task SendGroupMessage(int groupId, string user, string message)
        {
            await Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", new { GroupId = groupId, User = user, Message = message });
        }
    }
}