using Microsoft.AspNetCore.Mvc;
using Messenger.Data;
using Messenger.Models;
using Messenger.DTO;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace Messenger.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] User user)
    {
        if (await _context.Users.AnyAsync(u => u.Email == user.Email))
        {
            return BadRequest("Email đã tồn tài!");
        }
        
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đăng ký thành công!" });
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO login)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == login.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(login.Password, user.Password))
            return Unauthorized("Email hoặc mật khẩu không đúng!");

        return Ok(new { message = "Đăng nhập thành công!", user = user.UserName });
    }

    
}