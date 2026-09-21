using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.Persistence;

public sealed class TelematicsDbContext(DbContextOptions<TelematicsDbContext> options) : DbContext(options)
{
    public DbSet<TelematicsRecord> TelematicsRecords => Set<TelematicsRecord>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceApiKey> DeviceApiKeys => Set<DeviceApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<DeviceApiKey>()
            .HasIndex(d => d.DeviceId)
            .IsUnique();
    }
}
