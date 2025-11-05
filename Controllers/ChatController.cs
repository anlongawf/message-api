using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Messenger.Hubs;
using Messenger.Models;
using Messenger.DTO;
using Messenger.Services;
using Dapper;

namespace Messenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(DatabaseService db, IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        // Gửi tin nhắn text
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            // Kiểm tra sender và receiver
            var sender = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.SenderId });
            var receiver = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.ReceiverId });

            if (sender == null || receiver == null)
                return BadRequest("Sender or Receiver not found!");

            if (string.IsNullOrEmpty(dto.Message) && string.IsNullOrEmpty(dto.FileUrl))
                return BadRequest("Message or file is required");

            var sentAt = DateTime.UtcNow;

            // Insert message
            var sql = @"INSERT INTO Messages (SenderId, ReceiverId, Message, FileUrl, FileType, FileName, SentAt) 
                       VALUES (@SenderId, @ReceiverId, @Message, @FileUrl, @FileType, @FileName, @SentAt);
                       SELECT LAST_INSERT_ID();";

            var messageId = await conn.QuerySingleAsync<int>(sql, new
            {
                dto.SenderId,
                dto.ReceiverId,
                Message = dto.Message ?? (string?)null,
                FileUrl = dto.FileUrl ?? (string?)null,
                FileType = dto.FileType ?? (string?)null,
                FileName = dto.FileName ?? (string?)null,
                SentAt = sentAt
            });

            // Gửi message qua SignalR
            var messageData = new
            {
                Id = messageId,
                Message = dto.Message,
                SenderId = dto.SenderId,
                SenderName = sender.UserName,
                SenderAvatarUrl = sender.AvatarUrl,
                ReceiverId = dto.ReceiverId,
                FileUrl = dto.FileUrl,
                FileType = dto.FileType,
                FileName = dto.FileName,
                SentAt = sentAt
            };

            await _hubContext.Clients.User(sender.IdUser.ToString())
                .SendAsync("ReceiveMessage", messageData);

            await _hubContext.Clients.User(receiver.IdUser.ToString())
                .SendAsync("ReceiveMessage", messageData);

            return Ok(new { 
                Status = "Message sent", 
                MessageId = messageId,
                SentAt = sentAt
            });
        }

        // Upload file (ảnh, video, audio) cho chat 1-1
        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file, int senderId, int receiverId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var sender = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = senderId });
            var receiver = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = receiverId });

            if (sender == null || receiver == null)
                return BadRequest("Sender or Receiver not found!");

            // Kiểm tra loại file
            var allowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            var allowedVideoTypes = new[] { "video/mp4", "video/avi", "video/mov", "video/quicktime", "video/webm" };
            var allowedAudioTypes = new[] { "audio/mpeg", "audio/mp3", "audio/wav", "audio/ogg", "audio/aac", "audio/m4a", "audio/webm", "audio/x-m4a" };

            var contentType = file.ContentType.ToLower();
            string? fileType = null;

            if (allowedImageTypes.Contains(contentType))
                fileType = "image";
            else if (allowedVideoTypes.Contains(contentType))
                fileType = "video";
            else if (allowedAudioTypes.Contains(contentType))
                fileType = "audio";
            else
                return BadRequest("File type not supported. Only images, videos, and audio files are allowed.");

            // Kiểm tra kích thước file (max 50MB)
            const long maxFileSize = 50 * 1024 * 1024;
            if (file.Length > maxFileSize)
                return BadRequest("File size exceeds 50MB limit");

            // Lưu file - tạo thư mục theo loại file
            var fileSubfolder = fileType == "audio" ? "audio" : "messages";
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileSubfolder);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{fileSubfolder}/{fileName}";
            var sentAt = DateTime.UtcNow;

            // Lưu vào database
            var sql = @"INSERT INTO Messages (SenderId, ReceiverId, FileUrl, FileType, FileName, SentAt) 
                       VALUES (@SenderId, @ReceiverId, @FileUrl, @FileType, @FileName, @SentAt);
                       SELECT LAST_INSERT_ID();";

            var messageId = await conn.QuerySingleAsync<int>(sql, new
            {
                senderId,
                receiverId,
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = file.FileName,
                SentAt = sentAt
            });

            // Gửi qua SignalR
            var messageData = new
            {
                Id = messageId,
                Message = (string?)null,
                SenderId = senderId,
                SenderName = sender.UserName,
                SenderAvatarUrl = sender.AvatarUrl,
                ReceiverId = receiverId,
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = file.FileName,
                SentAt = sentAt
            };

            await _hubContext.Clients.User(senderId.ToString())
                .SendAsync("ReceiveMessage", messageData);

            await _hubContext.Clients.User(receiverId.ToString())
                .SendAsync("ReceiveMessage", messageData);

            return Ok(new { 
                Status = "File uploaded successfully", 
                MessageId = messageId,
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = file.FileName,
                SentAt = sentAt
            });
        }

        // Lấy toàn bộ cuộc trò chuyện giữa 2 user
        [HttpGet("conversation/{user1Id}/{user2Id}")]
        public async Task<IActionResult> GetConversation(int user1Id, int user2Id)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            // Kiểm tra user tồn tại
            var user1 = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = user1Id });
            var user2 = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = user2Id });

            if (user1 == null || user2 == null)
                return BadRequest("User not found!");

            var sql = @"SELECT m.IdMessage AS Id, m.SenderId, m.ReceiverId, m.Message, 
                              m.FileUrl, m.FileType, m.FileName, m.SentAt,
                              s.UserName AS SenderName, s.AvatarUrl AS SenderAvatarUrl,
                              r.UserName AS ReceiverName, r.AvatarUrl AS ReceiverAvatarUrl
                       FROM Messages m
                       INNER JOIN Users s ON m.SenderId = s.IdUser
                       INNER JOIN Users r ON m.ReceiverId = r.IdUser
                       WHERE (m.SenderId = @User1Id AND m.ReceiverId = @User2Id) 
                          OR (m.SenderId = @User2Id AND m.ReceiverId = @User1Id)
                       ORDER BY m.SentAt";

            var messages = await conn.QueryAsync(sql, new { User1Id = user1Id, User2Id = user2Id });
            
            return Ok(new {
                User1Id = user1Id,
                User1Name = user1.UserName,
                User1AvatarUrl = user1.AvatarUrl,
                User2Id = user2Id,
                User2Name = user2.UserName,
                User2AvatarUrl = user2.AvatarUrl,
                Messages = messages
            });
        }

        // Lấy danh sách các cuộc trò chuyện của một user (các user đã nhắn tin với)
        [HttpGet("conversations/{userId}")]
        public async Task<IActionResult> GetConversations(int userId)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = userId });
            if (user == null)
                return BadRequest("User not found!");

            // Lấy danh sách các user đã nhắn tin với và tin nhắn cuối cùng
            var sql = @"SELECT 
                            other_user.IdUser AS OtherUserId,
                            other_user.UserName AS OtherUserName,
                            other_user.AvatarUrl AS OtherUserAvatarUrl,
                            (SELECT m.Message FROM Messages m 
                             WHERE (m.SenderId = @UserId AND m.ReceiverId = other_user.IdUser)
                                OR (m.ReceiverId = @UserId AND m.SenderId = other_user.IdUser)
                             ORDER BY m.SentAt DESC LIMIT 1) AS LastMessage,
                            (SELECT m.FileUrl FROM Messages m 
                             WHERE (m.SenderId = @UserId AND m.ReceiverId = other_user.IdUser)
                                OR (m.ReceiverId = @UserId AND m.SenderId = other_user.IdUser)
                             ORDER BY m.SentAt DESC LIMIT 1) AS LastFileUrl,
                            (SELECT m.FileType FROM Messages m 
                             WHERE (m.SenderId = @UserId AND m.ReceiverId = other_user.IdUser)
                                OR (m.ReceiverId = @UserId AND m.SenderId = other_user.IdUser)
                             ORDER BY m.SentAt DESC LIMIT 1) AS LastFileType,
                            (SELECT m.FileName FROM Messages m 
                             WHERE (m.SenderId = @UserId AND m.ReceiverId = other_user.IdUser)
                                OR (m.ReceiverId = @UserId AND m.SenderId = other_user.IdUser)
                             ORDER BY m.SentAt DESC LIMIT 1) AS LastFileName,
                            (SELECT m.SentAt FROM Messages m 
                             WHERE (m.SenderId = @UserId AND m.ReceiverId = other_user.IdUser)
                                OR (m.ReceiverId = @UserId AND m.SenderId = other_user.IdUser)
                             ORDER BY m.SentAt DESC LIMIT 1) AS LastMessageTime
                       FROM (
                           SELECT DISTINCT
                               CASE WHEN m.SenderId = @UserId THEN m.ReceiverId ELSE m.SenderId END AS OtherUserId
                           FROM Messages m
                           WHERE m.SenderId = @UserId OR m.ReceiverId = @UserId
                       ) AS distinct_users
                       INNER JOIN Users other_user ON distinct_users.OtherUserId = other_user.IdUser
                       ORDER BY LastMessageTime DESC";

            var conversations = await conn.QueryAsync(sql, new { UserId = userId });
            
            return Ok(conversations);
        }
    }
}
