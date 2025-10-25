using Microsoft.EntityFrameworkCore;
using Messenger.Data;

var builder = WebApplication.CreateBuilder(args);

// Lấy connection string từ appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Cấu hình DbContext với MariaDB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

// Thêm controller
builder.Services.AddControllers();

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
app.UseAuthorization();

// Map controller endpoints
app.MapControllers();

app.Run();