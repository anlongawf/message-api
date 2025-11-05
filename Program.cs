using Messenger.Hubs;
using Messenger.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký DatabaseService
builder.Services.AddSingleton<DatabaseService>();

// Thêm controller
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Thêm CORS để cho phép client app gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Thêm Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Đảm bảo thư mục wwwroot tồn tại
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

// Tạo các thư mục con cho uploads nếu chưa tồn tại
var uploadsMessagesPath = Path.Combine(wwwrootPath, "uploads", "messages");
var uploadsGroupsPath = Path.Combine(wwwrootPath, "uploads", "groups");
var uploadsAudioPath = Path.Combine(wwwrootPath, "uploads", "audio");
var uploadsAvatarsPath = Path.Combine(wwwrootPath, "uploads", "avatars");
if (!Directory.Exists(uploadsMessagesPath))
    Directory.CreateDirectory(uploadsMessagesPath);
if (!Directory.Exists(uploadsGroupsPath))
    Directory.CreateDirectory(uploadsGroupsPath);
if (!Directory.Exists(uploadsAudioPath))
    Directory.CreateDirectory(uploadsAudioPath);
if (!Directory.Exists(uploadsAvatarsPath))
    Directory.CreateDirectory(uploadsAvatarsPath);

// Cấu hình static files để serve ảnh/video từ wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

app.UseRouting();

// Sử dụng CORS
app.UseCors("AllowAll");
    
app.UseAuthorization();

// Map endpoints
app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();