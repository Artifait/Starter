using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<PresetsController> _logger;

    public PresetsController(StarterDbContext db, IHubContext<RoomHub> hub, ILogger<PresetsController> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePreset(string roomId, [FromBody] CreatePresetDto dto)
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

        // Notify PC clients in room about new preset (optional) — логируем отправку
        try
        {
            await _hub.Clients.Group(roomId).SendAsync("preset.created", new { presetId = preset.PresetId, name = preset.Name });
            _logger.LogInformation("preset.created sent to group {roomId} for preset {presetId}", roomId, preset.PresetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send preset.created to group {roomId} for preset {presetId}", roomId, preset.PresetId);
        }

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
            meta = new { requestedBy = "mobile", requestId = dto?.RequestId }
        };

        // Send over SignalR to room group and log result
        try
        {
            await _hub.Clients.Group(roomId).SendAsync("preset.run", payload);
            _logger.LogInformation("preset.run sent to group {roomId} for exec {execId} (preset {presetId})", roomId, exec.ExecutionId, preset.PresetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send preset.run to group {roomId} for exec {execId}", roomId, exec.ExecutionId);
            // We still return Accepted because execution record created; consider queueing or marking failed if desired
        }

        return Accepted(new { executionId = exec.ExecutionId });
    }

    // Debug endpoint: отправить тестовое сообщение в группу (полезно для проверки delivery)
    [HttpPost("debug/broadcast")]
    public async Task<IActionResult> BroadcastTest(string roomId)
    {
        var payload = new { test = "hello", ts = DateTime.UtcNow, from = "admin" };
        try
        {
            await _hub.Clients.Group(roomId).SendAsync("preset.run", payload);
            _logger.LogInformation("Debug broadcast sent to group {roomId}", roomId);
            return Ok(new { sent = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed debug broadcast to group {roomId}", roomId);
            return StatusCode(500, new { error = "broadcast_failed", message = ex.Message });
        }
    }
}

public record CreatePresetDto(string Name, string Command, string[]? Args, string? WorkDir, bool RequiresConfirmation);
public record RunPresetDto(string[]? Args, string? RequestId);
