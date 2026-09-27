namespace TelematicsPipeline.Simulator;

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// A path made of lat/lon waypoints, with the geometry needed to place a vehicle
/// part-way along it. Distances use the haversine formula so a simulated vehicle
/// covers ground at a realistic rate rather than interpolating raw degrees
/// (a degree of longitude is much shorter than a degree of latitude at this latitude).
/// </summary>
public sealed class Route
{
    private const double EarthRadiusMetres = 6_371_000;

    private readonly IReadOnlyList<GeoPoint> _waypoints;
    private readonly double[] _cumulativeMetres;

    public Route(IReadOnlyList<GeoPoint> waypoints)
    {
        if (waypoints.Count < 2)
        {
            throw new ArgumentException("A route needs at least two waypoints.", nameof(waypoints));
        }

        _waypoints = waypoints;
        _cumulativeMetres = new double[waypoints.Count];

        for (var i = 1; i < waypoints.Count; i++)
        {
            _cumulativeMetres[i] = _cumulativeMetres[i - 1] + DistanceMetres(waypoints[i - 1], waypoints[i]);
        }
    }

    public double TotalMetres => _cumulativeMetres[^1];

    /// <summary>Look-ahead used to smooth heading through curves.</summary>
    private const double HeadingLookAheadMetres = 40;

    /// <summary>
    /// Position and compass heading at a given distance along the route.
    /// Past the end, the vehicle stays at the final waypoint.
    ///
    /// Heading is taken toward a point a little further along rather than from the
    /// current segment's bearing. Segment bearing is constant within a segment, so it
    /// would make the vehicle appear to turn instantly at each waypoint and drive
    /// perfectly straight in between -- yielding zero lateral G through the curves.
    /// </summary>
    public (GeoPoint Position, double HeadingDegrees) PositionAt(double metresTravelled)
    {
        var position = InterpolateAt(metresTravelled);
        var ahead = InterpolateAt(Math.Min(metresTravelled + HeadingLookAheadMetres, TotalMetres));

        var heading = DistanceMetres(position, ahead) < 0.5
            ? BearingDegrees(_waypoints[^2], _waypoints[^1])
            : BearingDegrees(position, ahead);

        return (position, heading);
    }

    private GeoPoint InterpolateAt(double metresTravelled)
    {
        if (metresTravelled <= 0)
        {
            return _waypoints[0];
        }

        if (metresTravelled >= TotalMetres)
        {
            return _waypoints[^1];
        }

        var segment = 1;
        while (_cumulativeMetres[segment] < metresTravelled)
        {
            segment++;
        }

        var start = _waypoints[segment - 1];
        var end = _waypoints[segment];
        var segmentLength = _cumulativeMetres[segment] - _cumulativeMetres[segment - 1];
        var fraction = segmentLength <= 0 ? 0 : (metresTravelled - _cumulativeMetres[segment - 1]) / segmentLength;

        return new GeoPoint(
            start.Latitude + (end.Latitude - start.Latitude) * fraction,
            start.Longitude + (end.Longitude - start.Longitude) * fraction);
    }

    public static double DistanceMetres(GeoPoint from, GeoPoint to)
    {
        var lat1 = DegreesToRadians(from.Latitude);
        var lat2 = DegreesToRadians(to.Latitude);
        var deltaLat = DegreesToRadians(to.Latitude - from.Latitude);
        var deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        return EarthRadiusMetres * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static double BearingDegrees(GeoPoint from, GeoPoint to)
    {
        var lat1 = DegreesToRadians(from.Latitude);
        var lat2 = DegreesToRadians(to.Latitude);
        var deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

        var y = Math.Sin(deltaLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon);

        return (RadiansToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    /// <summary>
    /// Southbound along a real mainline stretch of the Don Valley Parkway in Toronto --
    /// fetched once from the Mapbox Directions API (driving profile), not a hand-picked
    /// handful of points connected by straight lines. This specific window was chosen by
    /// searching the fetched route for a stretch clear of both interchange ramps (no
    /// waypoint-to-waypoint heading jump above the smoothing tolerance) and unrealistic
    /// derived G-force (replaying this trip's speed profile through the route's real
    /// curvature never exceeds what a genuine highway curve produces at that speed).
    /// </summary>
    public static Route DonValleyParkway() => new(
    [
        new GeoPoint(43.721164, -79.329901),
        new GeoPoint(43.721100, -79.329831),
        new GeoPoint(43.720937, -79.329724),
        new GeoPoint(43.720802, -79.329655),
        new GeoPoint(43.720629, -79.329473),
        new GeoPoint(43.720402, -79.329360),
        new GeoPoint(43.719668, -79.328925),
        new GeoPoint(43.719201, -79.328602),
        new GeoPoint(43.719020, -79.328478),
        new GeoPoint(43.718975, -79.328441),
        new GeoPoint(43.718229, -79.327866),
        new GeoPoint(43.717582, -79.327362),
        new GeoPoint(43.716896, -79.326796),
        new GeoPoint(43.716188, -79.326230),
        new GeoPoint(43.715798, -79.325970),
        new GeoPoint(43.715424, -79.325776),
        new GeoPoint(43.715021, -79.325624),
        new GeoPoint(43.714610, -79.325527),
        new GeoPoint(43.714242, -79.325500),
        new GeoPoint(43.713873, -79.325514),
        new GeoPoint(43.713489, -79.325575),
        new GeoPoint(43.713086, -79.325693),
        new GeoPoint(43.712807, -79.325801),
        new GeoPoint(43.712499, -79.325967),
        new GeoPoint(43.712113, -79.326238),
        new GeoPoint(43.711721, -79.326584),
        new GeoPoint(43.711454, -79.326868),
        new GeoPoint(43.711065, -79.327345),
        new GeoPoint(43.710644, -79.327943),
        new GeoPoint(43.710181, -79.328568),
        new GeoPoint(43.709776, -79.329134),
        new GeoPoint(43.709371, -79.329670),
        new GeoPoint(43.709016, -79.330118),
        new GeoPoint(43.708744, -79.330424),
        new GeoPoint(43.708421, -79.330727),
        new GeoPoint(43.708023, -79.331009),
        new GeoPoint(43.707529, -79.331269),
        new GeoPoint(43.707075, -79.331427),
        new GeoPoint(43.706648, -79.331538),
        new GeoPoint(43.706164, -79.331630),
        new GeoPoint(43.705704, -79.331734),
        new GeoPoint(43.705585, -79.331763),
        new GeoPoint(43.704824, -79.331925),
    ]);
}
