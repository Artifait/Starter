using Microsoft.EntityFrameworkCore;
using Starter.Data;
using Starter.Hubs;
using Starter.Models;


var builder = WebApplication.CreateBuilder(args);


// Configuration: SQLite connection string from env or default file
var connectionString = builder.Configuration.GetValue<string>("DATA_SOURCE") ?? "Data Source=starter.db";


// Add EF DbContext
builder.Services.AddDbContext<StarterDbContext>(options =>
options.UseSqlite(connectionString));


// Add SignalR
builder.Services.AddSignalR();


// Add controllers
builder.Services.AddControllers();


// Allow simple CORS for development (adjust for prod)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().AllowAnyOrigin();
    });
});


var app = builder.Build();


// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
    db.Database.Migrate();
}


app.UseRouting();
app.UseCors();
app.MapControllers();


// Minimal health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));


// SignalR hub
app.MapHub<RoomHub>("/ws/roomhub");


app.Run();