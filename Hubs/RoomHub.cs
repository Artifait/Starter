using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Starter.Data;
using Starter.Models;

namespace Starter.Hubs;

public class RoomHub : Hub
{
    private readonly ILogger<RoomHub> _logger;

    public RoomHub(ILogger<RoomHub> logger)
    {
        _logger = logger;
    }

    // Clients will call Register(accessToken) after connecting to identify themselves
    public async Task Register(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await Clients.Caller.SendAsync("error", new { code = "missing_token" });
            _logger.LogWarning("Register called with empty token. Connection {conn} aborted.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
        var client = db.Clients.FirstOrDefault(c => c.AccessToken == accessToken);
        if (client == null)
        {
            await Clients.Caller.SendAsync("error", new { code = "invalid_token" });
            _logger.LogWarning("Invalid accessToken on Register. Connection {conn} aborted.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        // логируем найденного клиента, connectionId и room
        _logger.LogInformation("Register: connection={conn}, clientId={client}, room={room}", Context.ConnectionId, client.ClientId, client.RoomId);

        try
        {
            // add connection to group
            await Groups.AddToGroupAsync(Context.ConnectionId, client.RoomId);
            client.LastSeen = DateTime.UtcNow;
            db.SaveChanges();

            _logger.LogInformation("Added connection {conn} to group {room}", Context.ConnectionId, client.RoomId);
            await Clients.Caller.SendAsync("registered", new { roomId = client.RoomId, clientId = client.ClientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register connection {conn} for client {client}", Context.ConnectionId, client.ClientId);
            await Clients.Caller.SendAsync("error", new { code = "register_failed", message = ex.Message });
            Context.Abort();
        }
    }

    // Server-side helper: send run command to room group
    public async Task SendPresetRun(string roomId, object payload)
    {
        try
        {
            await Clients.Group(roomId).SendAsync("preset.run", payload);
            _logger.LogInformation("SendPresetRun: payload sent to group {roomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendPresetRun: failed to send payload to group {roomId}", roomId);
        }
    }

    public async Task ExecutionStarted(string executionId, string clientId, int pid, DateTime startedAt)
    {
        // сохраняем в БД (если нужно) — пример:
        try
        {
            using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
            var exec = db.Executions.FirstOrDefault(e => e.ExecutionId == executionId);
            if (exec != null)
            {
                exec.Status = "running";
                exec.Pid = pid;
                exec.StartedAt = startedAt;
                db.SaveChanges();
                _logger.LogInformation("ExecutionStarted: exec={execId} client={clientId} pid={pid}", executionId, clientId, pid);
            }

            // оповещаем всех мобильных клиентов в комнате(ах) этого клиента
            // Лучше отправлять в комнату, которую хранит запись exec (если есть)
            var roomToNotify = exec?.RoomId;
            if (!string.IsNullOrEmpty(roomToNotify))
            {
                await Clients.Group(roomToNotify).SendAsync("execution.started", new { executionId, clientId, pid, startedAt });
                _logger.LogInformation("Broadcasted execution.started to group {room}", roomToNotify);
            }
            else
            {
                // fallback: find all rooms for this client and notify them
                var rooms = db.Clients.Where(c => c.ClientId == clientId).Select(c => c.RoomId).Distinct().ToList();
                foreach (var r in rooms)
                {
                    await Clients.Group(r).SendAsync("execution.started", new { executionId, clientId, pid, startedAt });
                    _logger.LogInformation("Fallback broadcast execution.started to group {room}", r);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecutionStarted: error handling exec={execId}", executionId);
        }
    }

    public async Task ExecutionFinished(string executionId, string clientId, int exitCode, DateTime finishedAt)
    {
        try
        {
            using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
            var exec = db.Executions.FirstOrDefault(e => e.ExecutionId == executionId);
            if (exec != null)
            {
                exec.Status = "exited";
                exec.ExitCode = exitCode;
                exec.FinishedAt = finishedAt;
                db.SaveChanges();
                _logger.LogInformation("ExecutionFinished: exec={execId} client={clientId} exitCode={code}", executionId, clientId, exitCode);
            }

            var roomId = exec?.RoomId;
            if (!string.IsNullOrEmpty(roomId))
            {
                await Clients.Group(roomId).SendAsync("execution.finished", new { executionId, clientId, exitCode, finishedAt });
                _logger.LogInformation("Broadcasted execution.finished to group {room}", roomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecutionFinished: error handling exec={execId}", executionId);
        }
    }
}
