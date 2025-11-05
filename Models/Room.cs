namespace Starter.Models;


public class Room
{
    // short human-friendly id
    public string RoomId { get; set; } = null!;


    // hashed secret (bcrypt)
    public string SecretHash { get; set; } = null!;


    public DateTime CreatedAt { get; set; }
}