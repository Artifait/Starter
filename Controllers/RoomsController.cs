using Microsoft.AspNetCore.Mvc;
using Starter.Data;
using Starter.Models;
using BCrypt.Net;


namespace Starter.Controllers;


[ApiController]
[Route("api/v1/rooms")]
public class RoomsController : ControllerBase
{
    private readonly StarterDbContext _db;


    public RoomsController(StarterDbContext db) { _db = db; }


    [HttpPost]
    public IActionResult CreateRoom([FromBody] CreateRoomDto dto)
    {
        var roomId = GenerateShortId(6);
        var secret = Guid.NewGuid().ToString("N") + RandomString(12);
        var hash = BCrypt.Net.BCrypt.HashPassword(secret);


        var room = new Room { RoomId = roomId, SecretHash = hash, CreatedAt = DateTime.UtcNow };
        _db.Rooms.Add(room);
        _db.SaveChanges();


        return Created($"/api/v1/rooms/{roomId}", new { roomId, roomSecret = secret, createdAt = room.CreatedAt });
    }


    [HttpGet("{roomId}")]
    public IActionResult GetRoom(string roomId)
    {
        var room = _db.Rooms.Find(roomId);
        if (room == null) return NotFound();
        var hasPc = _db.Clients.Any(c => c.RoomId == roomId);
        var presetsCount = _db.Presets.Count(p => p.RoomId == roomId);
        return Ok(new { roomId = room.RoomId, hasPc, presetsCount });
    }


    // Helpers
    private static string GenerateShortId(int len)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var sb = new System.Text.StringBuilder(len);
        var rng = Random.Shared;
        for (int i = 0; i < len; i++) sb.Append(alphabet[rng.Next(alphabet.Length)]);
        return sb.ToString();
    }


    private static string RandomString(int len)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new System.Text.StringBuilder(len);
        var rng = Random.Shared;
        for (int i = 0; i < len; i++) sb.Append(alphabet[rng.Next(alphabet.Length)]);
        return sb.ToString();
    }
}


public record CreateRoomDto(string? Name, int? TtlSeconds);