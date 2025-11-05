namespace Starter.Models;


public class Execution
{
    public string ExecutionId { get; set; } = null!; // GUID
    public string PresetId { get; set; } = null!;
    public string RoomId { get; set; } = null!;
    public string? ClientId { get; set; }
    public string Status { get; set; } = "pending"; // pending|running|exited|failed
    public int? Pid { get; set; }
    public int? ExitCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}