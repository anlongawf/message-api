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
    public class GroupChatController : ControllerBase
    {
        private readonly DatabaseService _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public GroupChatController(DatabaseService db, IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var creator = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.CreatorId });
            if (creator == null)
                return BadRequest("Creator not found!");

            if (string.IsNullOrEmpty(dto.Name))
                return BadRequest("Group name is required!");

            // Insert group
            var groupSql = @"INSERT INTO GroupChats (Name, LeaderId, CreatedAt) 
                           VALUES (@Name, @LeaderId, @CreatedAt);
                           SELECT LAST_INSERT_ID();";
            var groupId = await conn.QuerySingleAsync<int>(groupSql, new
            {
                dto.Name,
                LeaderId = dto.CreatorId,
                CreatedAt = DateTime.UtcNow
            });

            // Insert creator as Admin
            var memberSql = @"INSERT INTO GroupMembers (GroupChatId, UserId, Role, JoinedAt) 
                            VALUES (@GroupChatId, @UserId, @Role, @JoinedAt)";
            await conn.ExecuteAsync(memberSql, new
            {
                GroupChatId = groupId,
                UserId = dto.CreatorId,
                Role = "Admin",
                JoinedAt = DateTime.UtcNow
            });

            return Ok(new { 
                Status = "Group created successfully", 
                GroupId = groupId,
                GroupName = dto.Name,
                LeaderId = dto.CreatorId
            });
        }

        [HttpPost("invite")]
        public async Task<IActionResult> InviteToGroup([FromBody] InviteToGroupDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = dto.GroupChatId });
            if (group == null)
                return BadRequest("Group not found!");

            var inviter = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.InviterId });
            var invitedUser = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.InvitedUserId });

            if (inviter == null || invitedUser == null)
                return BadRequest("Inviter or invited user not found!");

            // Kiểm tra inviter là member
            var inviterMember = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.InviterId });
            if (inviterMember == null)
                return BadRequest("Only group members can invite others!");

            // Kiểm tra đã là member chưa
            var existingMember = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.InvitedUserId });
            if (existingMember != null)
                return BadRequest("User is already a member of this group!");

            // Thêm member
            await conn.ExecuteAsync(
                "INSERT INTO GroupMembers (GroupChatId, UserId, Role, JoinedAt) VALUES (@GroupChatId, @UserId, @Role, @JoinedAt)",
                new { GroupChatId = dto.GroupChatId, UserId = dto.InvitedUserId, Role = "Member", JoinedAt = DateTime.UtcNow });

            // SignalR
            await _hubContext.Clients.User(dto.InvitedUserId.ToString())
                .SendAsync("GroupInvitation", new { 
                    GroupId = group.Id, 
                    GroupName = group.Name,
                    InviterName = inviter.UserName
                });

            return Ok(new { Status = "User invited successfully" });
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveGroup([FromBody] LeaveGroupDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = dto.GroupChatId });
            if (group == null)
                return BadRequest("Group not found!");

            if (group.LeaderId == dto.UserId)
                return BadRequest("Group leader cannot leave the group! Please transfer leadership first.");

            var member = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.UserId });
            if (member == null)
                return BadRequest("You are not a member of this group!");

            await conn.ExecuteAsync(
                "DELETE FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.UserId });

            await _hubContext.Clients.Group($"group_{dto.GroupChatId}")
                .SendAsync("MemberLeft", new { GroupId = dto.GroupChatId, UserId = dto.UserId });

            return Ok(new { Status = "Left group successfully" });
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendGroupMessage([FromBody] GroupMessageDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = dto.GroupChatId });
            if (group == null)
                return BadRequest("Group not found!");

            var sender = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = dto.SenderId });
            if (sender == null)
                return BadRequest("Sender not found!");

            var member = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.SenderId });
            if (member == null)
                return BadRequest("You must be a member to send messages!");

            if (string.IsNullOrEmpty(dto.Message) && string.IsNullOrEmpty(dto.FileUrl))
                return BadRequest("Message or file is required!");

            var sentAt = DateTime.UtcNow;
            var sql = @"INSERT INTO GroupMessages (GroupChatId, SenderId, Message, FileUrl, FileType, FileName, SentAt) 
                       VALUES (@GroupChatId, @SenderId, @Message, @FileUrl, @FileType, @FileName, @SentAt);
                       SELECT LAST_INSERT_ID();";

            var messageId = await conn.QuerySingleAsync<int>(sql, new
            {
                dto.GroupChatId,
                dto.SenderId,
                Message = dto.Message ?? (string?)null,
                FileUrl = dto.FileUrl ?? (string?)null,
                FileType = dto.FileType ?? (string?)null,
                FileName = dto.FileName ?? (string?)null,
                SentAt = sentAt
            });

            await _hubContext.Clients.Group($"group_{dto.GroupChatId}")
                .SendAsync("ReceiveGroupMessage", new {
                    GroupId = dto.GroupChatId,
                    Message = dto.Message,
                    SenderId = dto.SenderId,
                    SenderName = sender.UserName,
                    FileUrl = dto.FileUrl,
                    FileType = dto.FileType,
                    FileName = dto.FileName,
                    SentAt = sentAt
                });

            return Ok(new { Status = "Message sent", MessageId = messageId });
        }

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file, int groupChatId, int senderId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = groupChatId });
            if (group == null)
                return BadRequest("Group not found!");

            var sender = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = senderId });
            if (sender == null)
                return BadRequest("Sender not found!");

            var member = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = groupChatId, UserId = senderId });
            if (member == null)
                return BadRequest("You must be a member to send files!");

            // Kiểm tra file
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

            const long maxFileSize = 50 * 1024 * 1024;
            if (file.Length > maxFileSize)
                return BadRequest("File size exceeds 50MB limit");

            // Lưu file - tạo thư mục theo loại file
            var fileSubfolder = fileType == "audio" ? "audio" : "groups";
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

            var sql = @"INSERT INTO GroupMessages (GroupChatId, SenderId, FileUrl, FileType, FileName, SentAt) 
                       VALUES (@GroupChatId, @SenderId, @FileUrl, @FileType, @FileName, @SentAt);
                       SELECT LAST_INSERT_ID();";

            var messageId = await conn.QuerySingleAsync<int>(sql, new
            {
                GroupChatId = groupChatId,
                SenderId = senderId,
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = file.FileName,
                SentAt = sentAt
            });

            await _hubContext.Clients.Group($"group_{groupChatId}")
                .SendAsync("ReceiveGroupMessage", new {
                    GroupId = groupChatId,
                    Message = (string?)null,
                    SenderId = senderId,
                    SenderName = sender.UserName,
                    FileUrl = fileUrl,
                    FileType = fileType,
                    FileName = file.FileName,
                    SentAt = sentAt
                });

            return Ok(new { 
                Status = "File uploaded successfully", 
                FileUrl = fileUrl,
                FileType = fileType,
                FileName = file.FileName,
                MessageId = messageId
            });
        }

        [HttpPost("kick")]
        public async Task<IActionResult> KickMember([FromBody] KickMemberDto dto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = dto.GroupChatId });
            if (group == null)
                return BadRequest("Group not found!");

            if (group.LeaderId != dto.AdminId)
                return BadRequest("Only group leader can kick members!");

            if (dto.AdminId == dto.MemberId)
                return BadRequest("Cannot kick yourself!");

            var member = await conn.QueryFirstOrDefaultAsync<GroupMember>(
                "SELECT * FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.MemberId });
            if (member == null)
                return BadRequest("User is not a member of this group!");

            await conn.ExecuteAsync(
                "DELETE FROM GroupMembers WHERE GroupChatId = @GroupChatId AND UserId = @UserId",
                new { GroupChatId = dto.GroupChatId, UserId = dto.MemberId });

            await _hubContext.Clients.User(dto.MemberId.ToString())
                .SendAsync("KickedFromGroup", new { GroupId = dto.GroupChatId, GroupName = group.Name });

            await _hubContext.Clients.Group($"group_{dto.GroupChatId}")
                .SendAsync("MemberKicked", new { GroupId = dto.GroupChatId, KickedUserId = dto.MemberId });

            return Ok(new { Status = "Member kicked successfully" });
        }

        [HttpGet("members/{groupId}")]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = groupId });
            if (group == null)
                return BadRequest("Group not found!");

            var sql = @"SELECT gm.UserId, gm.Role, gm.JoinedAt, u.UserName, u.Email, u.AvatarUrl 
                       FROM GroupMembers gm 
                       INNER JOIN Users u ON gm.UserId = u.IdUser 
                       WHERE gm.GroupChatId = @GroupId
                       ORDER BY gm.JoinedAt";

            var members = await conn.QueryAsync(sql, new { GroupId = groupId });
            return Ok(new { 
                GroupId = groupId,
                GroupName = group.Name,
                LeaderId = group.LeaderId,
                Members = members 
            });
        }

        [HttpGet("messages/{groupId}")]
        public async Task<IActionResult> GetGroupMessages(int groupId)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var group = await conn.QueryFirstOrDefaultAsync<GroupChat>(
                "SELECT * FROM GroupChats WHERE Id = @Id", new { Id = groupId });
            if (group == null)
                return BadRequest("Group not found!");

            var sql = @"SELECT gm.Id, gm.Message, gm.SenderId, gm.FileUrl, gm.FileType, gm.FileName, gm.SentAt, u.UserName AS SenderName
                       FROM GroupMessages gm 
                       INNER JOIN Users u ON gm.SenderId = u.IdUser 
                       WHERE gm.GroupChatId = @GroupId 
                       ORDER BY gm.SentAt";

            var messages = await conn.QueryAsync(sql, new { GroupId = groupId });
            return Ok(new { 
                GroupId = groupId,
                GroupName = group.Name,
                Messages = messages 
            });
        }

        [HttpGet("user-groups/{userId}")]
        public async Task<IActionResult> GetUserGroups(int userId)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = userId });
            if (user == null)
                return BadRequest("User not found!");

            var sql = @"SELECT gc.Id AS GroupId, gc.Id, gc.Name, gc.Name AS GroupName, gc.LeaderId, 
                               gm.Role, gm.JoinedAt, leader.UserName AS LeaderName, gc.CreatedAt
                       FROM GroupMembers gm 
                       INNER JOIN GroupChats gc ON gm.GroupChatId = gc.Id 
                       INNER JOIN Users leader ON gc.LeaderId = leader.IdUser 
                       WHERE gm.UserId = @UserId
                       ORDER BY gc.CreatedAt DESC";

            var groups = await conn.QueryAsync(sql, new { UserId = userId });
            return Ok(groups);
        }
    }
}
