using Microsoft.EntityFrameworkCore;
using TelematicsPipeline.Api.Models;

namespace TelematicsPipeline.Api.Persistence;

public sealed class TelematicsDbContext(DbContextOptions<TelematicsDbContext> options) : DbContext(options)
{
    public DbSet<TelematicsRecord> TelematicsRecords => Set<TelematicsRecord>();
}
