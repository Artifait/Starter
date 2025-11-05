namespace Starter.Models;


public class Client
{
    public string ClientId { get; set; } = null!; // e.g. pc-01 or GUID
    public string RoomId { get; set; } = null!;


    // access token (random GUID) used for authenticating to WS/REST (store hashed in prod)
    public string AccessToken { get; set; } = null!;


    public DateTime LastSeen { get; set; }
}