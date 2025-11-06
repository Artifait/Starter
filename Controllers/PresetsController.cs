using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Starter.Data;
using Starter.Hubs;
using Starter.Models;

namespace Starter.Controllers;

[ApiController]
[Route("api/v1/rooms/{roomId}/presets")]
public class PresetsController : ControllerBase
{
    private readonly StarterDbContext _db;
    private readonly IHubContext<RoomHub> _hub;

    public PresetsController(StarterDbContext db, IHubContext<RoomHub> hub)
    {
        _db = db; _hub = hub;
    }

    [HttpPost]
    public IActionResult CreatePreset(string roomId, [FromBody] CreatePresetDto dto)
    {
        var preset = new Preset
        {
            PresetId = Guid.NewGuid().ToString("N"),
            RoomId = roomId,
            Name = dto.Name,
            Command = dto.Command,
            ArgsJson = dto.Args == null ? null : System.Text.Json.JsonSerializer.Serialize(dto.Args),
            WorkDir = dto.WorkDir,
            RequiresConfirmation = dto.RequiresConfirmation,
            CreatedAt = DateTime.UtcNow
        };
        _db.Presets.Add(preset);
        _db.SaveChanges();

        // Notify PC clients in room about new preset (optional)
        _hub.Clients.Group(roomId).SendAsync("preset.created", new { presetId = preset.PresetId, name = preset.Name });

        return Created($"/api/v1/rooms/{roomId}/presets/{preset.PresetId}", new { presetId = preset.PresetId });
    }

    [HttpGet]
    public IActionResult GetPresets(string roomId)
    {
        // возвращаем все пресеты для комнаты
        var list = _db.Presets
            .Where(p => p.RoomId == roomId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.PresetId,
                p.RoomId,
                p.Name,
                p.Command,
                p.ArgsJson,
                p.WorkDir,
                p.RequiresConfirmation,
                p.CreatedAt
            })
            .ToList();

        return Ok(list);
    }

    [HttpGet("{presetId}")]
    public IActionResult GetPreset(string roomId, string presetId)
    {
        var preset = _db.Presets.FirstOrDefault(p => p.RoomId == roomId && p.PresetId == presetId);
        if (preset == null) return NotFound();
        return Ok(new
        {
            preset.PresetId,
            preset.RoomId,
            preset.Name,
            preset.Command,
            preset.ArgsJson,
            preset.WorkDir,
            preset.RequiresConfirmation,
            preset.CreatedAt
        });
    }

    [HttpPost("{presetId}/run")]
    public async Task<IActionResult> RunPreset(string roomId, string presetId, [FromBody] RunPresetDto? dto)
    {
        var preset = _db.Presets.FirstOrDefault(p => p.PresetId == presetId && p.RoomId == roomId);
        if (preset == null) return NotFound(new { error = "no_such_preset" });

        // Create execution record
        var exec = new Execution
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            PresetId = preset.PresetId,
            RoomId = roomId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.Executions.Add(exec);
        _db.SaveChanges();

        // Prepare payload to PC clients
        var payload = new
        {
            executionId = exec.ExecutionId,
            presetId = preset.PresetId,
            command = preset.Command,
            args = preset.ArgsJson == null ? new string[0] : System.Text.Json.JsonSerializer.Deserialize<string[]>(preset.ArgsJson),
            workDir = preset.WorkDir,
            meta = new { requestedBy = "mobile" }
        };

        // Send over SignalR to room group
        await _hub.Clients.Group(roomId).SendAsync("preset.run", payload);

        return Accepted(new { executionId = exec.ExecutionId });
    }
}

public record CreatePresetDto(string Name, string Command, string[]? Args, string? WorkDir, bool RequiresConfirmation);
public record RunPresetDto(string[]? Args, string? RequestId);