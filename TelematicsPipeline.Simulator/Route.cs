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

    /// <summary>Southbound along the Don Valley Parkway corridor in Toronto.</summary>
    public static Route DonValleyParkway() => new(
    [
        new GeoPoint(43.70120, -79.35480),
        new GeoPoint(43.69540, -79.35060),
        new GeoPoint(43.68960, -79.34820),
        new GeoPoint(43.68655, -79.34518),
        new GeoPoint(43.68240, -79.34390),
        new GeoPoint(43.67810, -79.34460),
        new GeoPoint(43.67350, -79.34760),
        new GeoPoint(43.66930, -79.35170),
        new GeoPoint(43.66540, -79.35520),
    ]);
}
