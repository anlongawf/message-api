    using Microsoft.AspNetCore.Mvc;
    using Messenger.Models;
    using Messenger.DTO;
    using Messenger.Services;
    using Dapper;
    using BCrypt.Net;

    namespace Messenger.Controllers;

    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DatabaseService _db;

        public AuthController(DatabaseService db)
        {
            _db = db;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] User user)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var existing = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { user.Email });
            if (existing != null)
                return BadRequest("Email đã tồn tại!");

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            var sql = @"INSERT INTO Users (Email, UserName, Password, AvatarUrl) 
                       VALUES (@Email, @UserName, @Password, @AvatarUrl);
                       SELECT LAST_INSERT_ID();";

            var userId = await conn.QuerySingleAsync<int>(sql, new
            {
                user.Email,
                user.UserName,
                user.Password,
                AvatarUrl = user.AvatarUrl ?? (string?)null
            });

            return Ok(new { message = "Đăng ký thành công!", userId });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO login)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { login.Email });

            if (user == null || !BCrypt.Net.BCrypt.Verify(login.Password, user.Password))
                return Unauthorized("Email hoặc mật khẩu không đúng!");

            return Ok(new { message = "Đăng nhập thành công!", user = user.UserName, userId = user.IdUser, avatarUrl = user.AvatarUrl });
        }

        [HttpGet("users")]
        public async Task<ActionResult> GetAllUsers()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var users = await conn.QueryAsync("SELECT IdUser, UserName, Email, AvatarUrl FROM Users");
            return Ok(users);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO changePasswordDto)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = changePasswordDto.UserId });

            if (user == null)
                return NotFound("Người dùng không tồn tại!");

            if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.OldPassword, user.Password))
                return Unauthorized("Mật khẩu cũ không đúng!");

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);

            await conn.ExecuteAsync(
                "UPDATE Users SET Password = @Password WHERE IdUser = @Id",
                new { Password = newPasswordHash, Id = changePasswordDto.UserId });

            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { message = "Đăng xuất thành công!" });
        }

        // Upload avatar cho user
        [HttpPost("upload-avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file, int userId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = userId });
            if (user == null)
                return NotFound("User not found!");

            // Kiểm tra loại file (chỉ cho phép ảnh)
            var allowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            var contentType = file.ContentType.ToLower();

            if (!allowedImageTypes.Contains(contentType))
                return BadRequest("File type not supported. Only images are allowed.");

            // Kiểm tra kích thước file (max 5MB cho avatar)
            const long maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
                return BadRequest("File size exceeds 5MB limit");

            // Tạo thư mục lưu trữ nếu chưa tồn tại
            var avatarsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            if (!Directory.Exists(avatarsFolder))
                Directory.CreateDirectory(avatarsFolder);

            // Xóa avatar cũ nếu có
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                    catch { } // Ignore nếu không xóa được
                }
            }

            // Tạo tên file unique
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{userId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(avatarsFolder, fileName);

            // Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Tạo URL để truy cập file
            var avatarUrl = $"/uploads/avatars/{fileName}";

            // Cập nhật database
            await conn.ExecuteAsync(
                "UPDATE Users SET AvatarUrl = @AvatarUrl WHERE IdUser = @Id",
                new { AvatarUrl = avatarUrl, Id = userId });

            return Ok(new { 
                Status = "Avatar uploaded successfully", 
                AvatarUrl = avatarUrl,
                UserId = userId
            });
        }

        // Lấy thông tin user (bao gồm avatar)
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUser(int userId)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE IdUser = @Id", new { Id = userId });
            
            if (user == null)
                return NotFound("User not found!");

            return Ok(new {
                userId = user.IdUser,
                userName = user.UserName,
                email = user.Email,
                avatarUrl = user.AvatarUrl
            });
        }
    }
