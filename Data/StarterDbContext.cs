using Microsoft.EntityFrameworkCore;
using Starter.Models;


namespace Starter.Data;


public class StarterDbContext : DbContext
{
    public StarterDbContext(DbContextOptions<StarterDbContext> options) : base(options) { }


    public DbSet<Room> Rooms { get; set; } = null!;
    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Preset> Presets { get; set; } = null!;
    public DbSet<Execution> Executions { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>().HasKey(r => r.RoomId);
        modelBuilder.Entity<Client>().HasKey(c => c.ClientId);
        modelBuilder.Entity<Preset>().HasKey(p => p.PresetId);
        modelBuilder.Entity<Execution>().HasKey(e => e.ExecutionId);


        modelBuilder.Entity<Room>().Property(r => r.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<Execution>().Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");


        base.OnModelCreating(modelBuilder);
    }
}