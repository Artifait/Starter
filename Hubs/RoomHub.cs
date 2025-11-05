using Microsoft.AspNetCore.SignalR;
using Starter.Data;
using Starter.Models;


namespace Starter.Hubs;


public class RoomHub : Hub
{
    // Clients will call Register(accessToken) after connecting to identify themselves
    public async Task Register(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await Clients.Caller.SendAsync("error", new { code = "missing_token" });
            Context.Abort();
            return;
        }


        using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
        var client = db.Clients.FirstOrDefault(c => c.AccessToken == accessToken);
        if (client == null)
        {
            await Clients.Caller.SendAsync("error", new { code = "invalid_token" });
            Context.Abort();
            return;
        }


        // add connection to group for the room
        await Groups.AddToGroupAsync(Context.ConnectionId, client.RoomId);
        client.LastSeen = DateTime.UtcNow;
        db.SaveChanges();


        await Clients.Caller.SendAsync("registered", new { roomId = client.RoomId, clientId = client.ClientId });
    }


    // Server-side helper: send run command to room group
    public async Task SendPresetRun(string roomId, object payload)
    {
        await Clients.Group(roomId).SendAsync("preset.run", payload);
    }

    public async Task ExecutionStarted(string executionId, string clientId, int pid, DateTime startedAt)
    {
        // сохраняем в БД (если нужно) — пример:
        using var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
        var exec = db.Executions.FirstOrDefault(e => e.ExecutionId == executionId);
        if (exec != null)
        {
            exec.Status = "running";
            exec.Pid = pid;
            exec.StartedAt = startedAt;
            db.SaveChanges();
        }

        // оповещаем всех мобильных клиентов в группе (room)
        var groups = db.Clients.Where(c => c.ClientId == clientId).Select(c => c.RoomId).Distinct();
        foreach (var roomId in groups)
        {
            await Clients.Group(roomId).SendAsync("execution.started", new { executionId, clientId, pid, startedAt });
        }
    }

    public async Task ExecutionFinished(string executionId, string clientId, int exitCode, DateTime finishedAt)
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
        }

        // оповестить мобильных
        var roomId = exec?.RoomId;
        if (!string.IsNullOrEmpty(roomId))
            await Clients.Group(roomId).SendAsync("execution.finished", new { executionId, clientId, exitCode, finishedAt });
    }
}