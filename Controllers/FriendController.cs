using Microsoft.AspNetCore.Mvc;
using Messenger.Models;
using Messenger.Services;
using Dapper;

namespace Messenger.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FriendController : ControllerBase
{
    private readonly DatabaseService _db;

    public FriendController(DatabaseService db)
    {
        _db = db;
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendFriendRequest(string username, string friendUsername)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE UserName = @UserName", new { UserName = username });
        var friend = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE UserName = @UserName", new { UserName = friendUsername });

        if (user == null || friend == null)
            return NotFound("User not found");

        if (user.IdUser == friend.IdUser)
            return BadRequest("Cannot add yourself");

        // Kiểm tra đã tồn tại friend request chưa accepted
        var existing = await conn.QueryFirstOrDefaultAsync<Friend>(
            @"SELECT * FROM Friends 
              WHERE ((UserId = @UserId AND FriendId = @FriendId) OR (UserId = @FriendId AND FriendId = @UserId)) 
              AND Accepted = 0",
            new { UserId = user.IdUser, FriendId = friend.IdUser });
        if (existing != null)
            return BadRequest("Friend request already exists");

        await conn.ExecuteAsync(
            "INSERT INTO Friends (UserId, FriendId, Accepted, CreatedAt) VALUES (@UserId, @FriendId, @Accepted, @CreatedAt)",
            new { UserId = user.IdUser, FriendId = friend.IdUser, Accepted = false, CreatedAt = DateTime.UtcNow });

        return Ok(new { message = "Friend request sent" });
    }

    [HttpPost("accept")]
    public async Task<IActionResult> AcceptFriendRequest(int userId, int friendId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var request = await conn.QueryFirstOrDefaultAsync<Friend>(
            "SELECT * FROM Friends WHERE UserId = @FriendId AND FriendId = @UserId AND Accepted = 0",
            new { UserId = userId, FriendId = friendId });

        if (request == null)
            return BadRequest("Friend request not found");

        await conn.ExecuteAsync(
            "UPDATE Friends SET Accepted = 1 WHERE Id = @Id",
            new { Id = request.Id });

        return Ok(new { message = "Friend request accepted" });
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetFriends(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var sql = @"SELECT 
                    CASE 
                        WHEN f.UserId = @UserId THEN f.FriendId
                        ELSE f.UserId
                    END AS IdUser,
                    CASE 
                        WHEN f.UserId = @UserId THEN friend.UserName
                        ELSE u.UserName
                    END AS UserName,
                    CASE 
                        WHEN f.UserId = @UserId THEN friend.AvatarUrl
                        ELSE u.AvatarUrl
                    END AS AvatarUrl
                   FROM Friends f
                   LEFT JOIN Users u ON f.UserId = u.IdUser
                   LEFT JOIN Users friend ON f.FriendId = friend.IdUser
                   WHERE (f.UserId = @UserId OR f.FriendId = @UserId) AND f.Accepted = 1";

        var friends = await conn.QueryAsync(sql, new { UserId = userId });
        return Ok(friends);
    }

    [HttpGet("pending/{userId}")]
    public async Task<IActionResult> GetPendingRequests(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var sql = @"SELECT f.Id, f.UserId, f.FriendId, f.Accepted, f.CreatedAt, 
                           f.FriendId AS IdUser, u.UserName, u.AvatarUrl
                   FROM Friends f
                   INNER JOIN Users u ON f.FriendId = u.IdUser
                   WHERE f.UserId = @UserId AND f.Accepted = 0";

        var requests = await conn.QueryAsync(sql, new { UserId = userId });
        return Ok(requests);
    }

    [HttpGet("received/{userId}")]
    public async Task<IActionResult> GetReceivedRequests(int userId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var sql = @"SELECT f.Id, f.UserId, f.FriendId, f.Accepted, f.CreatedAt, 
                           f.UserId AS IdUser, u.UserName, u.AvatarUrl
                   FROM Friends f
                   INNER JOIN Users u ON f.UserId = u.IdUser
                   WHERE f.FriendId = @UserId AND f.Accepted = 0";

        var requests = await conn.QueryAsync(sql, new { UserId = userId });
        return Ok(requests);
    }
}
