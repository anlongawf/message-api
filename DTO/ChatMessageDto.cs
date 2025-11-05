namespace Messenger.DTO;

public class ChatMessageDto
{
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string? Message { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public string? FileName { get; set; }
}
