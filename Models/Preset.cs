namespace Starter.Models;


public class Preset
{
    public string PresetId { get; set; } = null!; // GUID
    public string RoomId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Command { get; set; } = null!;
    public string? ArgsJson { get; set; }
    public string? WorkDir { get; set; }
    public bool RequiresConfirmation { get; set; }
    public DateTime CreatedAt { get; set; }
}