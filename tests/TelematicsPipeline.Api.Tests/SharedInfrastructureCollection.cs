using Xunit;

namespace TelematicsPipeline.Api.Tests;

/// <summary>
/// Groups every test class that hits the real Postgres test database or the real Redis test
/// database (db 1) into one xUnit collection, so they run sequentially against each other
/// instead of in parallel. Without this, two classes' IAsyncLifetime setup (each does a
/// DELETE FROM "TelematicsRecords" / leaderboard key reset before its own tests) can race:
/// one class's cleanup wipes rows or leaderboard state a concurrently-running class just wrote,
/// producing intermittent failures that have nothing to do with the code under test.
/// </summary>
[CollectionDefinition("Shared Postgres/Redis")]
public class SharedInfrastructureCollection;
