using TelematicsPipeline.Simulator;
using Xunit;

namespace TelematicsPipeline.Simulator.Tests;

public class RouteTests
{
    [Fact]
    public void RequiresAtLeastTwoWaypoints()
    {
        Assert.Throws<ArgumentException>(() => new Route([new GeoPoint(43.68, -79.34)]));
    }

    [Fact]
    public void MeasuresDistanceGeographically()
    {
        // One degree of latitude is roughly 111 km anywhere on Earth.
        var metres = Route.DistanceMetres(new GeoPoint(43.0, -79.0), new GeoPoint(44.0, -79.0));

        Assert.InRange(metres, 110_000, 112_000);
    }

    [Fact]
    public void DistanceAccountsForLongitudeConvergingAtHigherLatitudes()
    {
        // A degree of longitude is much shorter than a degree of latitude at Toronto's
        // latitude. Interpolating raw degrees instead of real distance would make the
        // simulated vehicle cover ground at the wrong rate.
        var latitudeDegree = Route.DistanceMetres(new GeoPoint(43.0, -79.0), new GeoPoint(44.0, -79.0));
        var longitudeDegree = Route.DistanceMetres(new GeoPoint(43.0, -79.0), new GeoPoint(43.0, -78.0));

        Assert.True(longitudeDegree < latitudeDegree * 0.8, "longitude degrees should be visibly shorter here");
    }

    [Fact]
    public void ClampsToTheEndpointsOutsideTheRoute()
    {
        var route = Route.DonValleyParkway();

        var (beforeStart, _) = route.PositionAt(-100);
        var (afterEnd, _) = route.PositionAt(route.TotalMetres + 5_000);

        Assert.Equal(route.PositionAt(0).Position, beforeStart);
        Assert.Equal(route.PositionAt(route.TotalMetres).Position, afterEnd);
    }

    [Fact]
    public void AdvancesAlongTheRouteAsDistanceIncreases()
    {
        var route = Route.DonValleyParkway();

        var start = route.PositionAt(0).Position;
        var quarter = route.PositionAt(route.TotalMetres * 0.25).Position;
        var end = route.PositionAt(route.TotalMetres).Position;

        Assert.True(Route.DistanceMetres(start, quarter) < Route.DistanceMetres(start, end));
    }

    [Fact]
    public void HeadingChangesGraduallyRatherThanJumpingAtWaypoints()
    {
        // Guards the look-ahead heading fix. Taking the current segment's bearing made
        // heading constant within a segment and snap at each waypoint -- the yaw rate was
        // zero almost everywhere and spiked at the seams, so curves reported no lateral G.
        // The real invariant is that no single step turns the vehicle sharply.
        var route = Route.DonValleyParkway();
        var previous = route.PositionAt(0).HeadingDegrees;
        var largestStep = 0.0;

        for (var metres = 5.0; metres < route.TotalMetres; metres += 5)
        {
            var heading = route.PositionAt(metres).HeadingDegrees;
            var step = Math.Abs((heading - previous + 540) % 360 - 180);
            largestStep = Math.Max(largestStep, step);
            previous = heading;
        }

        Assert.True(largestStep > 0.01, "heading never changed at all -- curves would produce no lateral force");
        Assert.True(largestStep < 5, $"heading snapped {largestStep:F1} degrees in 5 metres, which is a jump, not a curve");
    }

    [Fact]
    public void BearingPointsInTheExpectedCompassDirection()
    {
        var due_north = Route.BearingDegrees(new GeoPoint(43.0, -79.0), new GeoPoint(44.0, -79.0));
        var due_east = Route.BearingDegrees(new GeoPoint(43.0, -79.0), new GeoPoint(43.0, -78.0));

        Assert.InRange(due_north, 0, 1);
        Assert.InRange(due_east, 89, 91);
    }
}
