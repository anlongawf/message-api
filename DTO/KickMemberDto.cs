namespace Messenger.DTO
{
    public class KickMemberDto
    {
        public int GroupChatId { get; set; }
        public int AdminId { get; set; } // Người thực hiện kick (phải là trưởng nhóm)
        public int MemberId { get; set; } // Người bị kick
    }
}

