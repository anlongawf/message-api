namespace Messenger.DTO
{
    public class InviteToGroupDto
    {
        public int GroupChatId { get; set; }
        public int InviterId { get; set; } // Người mời
        public int InvitedUserId { get; set; } // Người được mời
    }
}

