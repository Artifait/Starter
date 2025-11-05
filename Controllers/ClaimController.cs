using Microsoft.AspNetCore.Mvc;
using Starter.Data;
using Starter.Models;
using BCrypt.Net;


namespace Starter.Controllers;


[ApiController]
[Route("api/v1/rooms/{roomId}/claim")]
public class ClaimController : ControllerBase
{
    private readonly StarterDbContext _db;


    public ClaimController(StarterDbContext db) { _db = db; }


    [HttpPost]
    public IActionResult Claim(string roomId, [FromBody] ClaimDto dto)
    {
        // room secret should come in header X-Room-Secret for simplicity
        var secret = Request.Headers["X-Room-Secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(secret)) return Unauthorized(new { error = "missing_room_secret" });


        var room = _db.Rooms.Find(roomId);
        if (room == null) return NotFound(new { error = "no_such_room" });


        if (!BCrypt.Net.BCrypt.Verify(secret, room.SecretHash))
            return Unauthorized(new { error = "invalid_room_secret" });


        var clientId = dto.ClientId ?? Guid.NewGuid().ToString();
        var accessToken = Guid.NewGuid().ToString("N");


        var client = new Client { ClientId = clientId, RoomId = roomId, AccessToken = accessToken, LastSeen = DateTime.UtcNow };
        _db.Clients.Add(client);
        _db.SaveChanges();


        return Ok(new { accessToken, clientId, expiresIn = 3600 });
    }
}


public record ClaimDto(string? ClientId, object? ClientInfo);