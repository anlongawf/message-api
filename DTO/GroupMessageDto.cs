namespace Messenger.DTO
{
    public class GroupMessageDto
    {
        public int GroupChatId { get; set; }
        public int SenderId { get; set; }
        public string? Message { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public string? FileName { get; set; }
    }
}

