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
    /// Picks one of the named routes for a device, deterministically -- the same device ID
    /// always gets the same route (reproducible runs), but different devices get visibly
    /// different paths on the map instead of every simulated vehicle retracing the exact
    /// same trail. Uses a hand-rolled hash rather than string.GetHashCode(), which .NET
    /// randomizes per-process by design and would make the mapping change between runs.
    /// </summary>
    public static Route ForDevice(string deviceId)
    {
        Route[] routes = [DonValleyParkway(), GardinerExpressway(), AllenRoad()];

        var hash = 0;
        foreach (var c in deviceId)
        {
            hash = hash * 31 + c;
        }

        return routes[Math.Abs(hash) % routes.Length];
    }

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

    /// <summary>Eastbound along a real mainline stretch of the Gardiner Expressway in
    /// Toronto, validated the same way as <see cref="DonValleyParkway"/>.</summary>
    public static Route GardinerExpressway() => new(
    [
        new GeoPoint(43.630320, -79.476899),
        new GeoPoint(43.630361, -79.476840),
        new GeoPoint(43.630513, -79.476601),
        new GeoPoint(43.630741, -79.476235),
        new GeoPoint(43.630883, -79.476001),
        new GeoPoint(43.631004, -79.475841),
        new GeoPoint(43.631202, -79.475650),
        new GeoPoint(43.631241, -79.475596),
        new GeoPoint(43.631259, -79.475558),
        new GeoPoint(43.631656, -79.475200),
        new GeoPoint(43.631893, -79.474954),
        new GeoPoint(43.632165, -79.474587),
        new GeoPoint(43.632371, -79.474273),
        new GeoPoint(43.632514, -79.474053),
        new GeoPoint(43.633234, -79.472586),
        new GeoPoint(43.633359, -79.472301),
        new GeoPoint(43.633407, -79.472191),
        new GeoPoint(43.633433, -79.472120),
        new GeoPoint(43.633500, -79.471943),
        new GeoPoint(43.633664, -79.471356),
        new GeoPoint(43.633691, -79.471236),
        new GeoPoint(43.633720, -79.471072),
        new GeoPoint(43.633786, -79.470707),
        new GeoPoint(43.633846, -79.470430),
        new GeoPoint(43.633912, -79.470152),
        new GeoPoint(43.633955, -79.470002),
        new GeoPoint(43.634005, -79.469850),
        new GeoPoint(43.634059, -79.469697),
        new GeoPoint(43.634123, -79.469540),
        new GeoPoint(43.634237, -79.469308),
        new GeoPoint(43.634488, -79.468894),
        new GeoPoint(43.634720, -79.468487),
        new GeoPoint(43.634899, -79.468153),
        new GeoPoint(43.634912, -79.468127),
        new GeoPoint(43.634975, -79.467986),
        new GeoPoint(43.635235, -79.467453),
        new GeoPoint(43.635497, -79.466854),
        new GeoPoint(43.635742, -79.466258),
        new GeoPoint(43.635832, -79.466041),
        new GeoPoint(43.635957, -79.465698),
        new GeoPoint(43.636088, -79.465324),
        new GeoPoint(43.636134, -79.465180),
        new GeoPoint(43.636356, -79.464420),
        new GeoPoint(43.636404, -79.464282),
        new GeoPoint(43.636487, -79.464041),
        new GeoPoint(43.636551, -79.463902),
        new GeoPoint(43.636630, -79.463737),
        new GeoPoint(43.636873, -79.463348),
        new GeoPoint(43.636990, -79.463140),
        new GeoPoint(43.637091, -79.462935),
        new GeoPoint(43.637218, -79.462631),
        new GeoPoint(43.637273, -79.462469),
        new GeoPoint(43.637320, -79.462303),
        new GeoPoint(43.637374, -79.462088),
        new GeoPoint(43.637420, -79.461845),
        new GeoPoint(43.637451, -79.461605),
        new GeoPoint(43.637475, -79.461346),
        new GeoPoint(43.637477, -79.461129),
        new GeoPoint(43.637470, -79.460607),
        new GeoPoint(43.637478, -79.460365),
        new GeoPoint(43.637513, -79.460054),
        new GeoPoint(43.637531, -79.459947),
        new GeoPoint(43.637554, -79.459813),
        new GeoPoint(43.637606, -79.459570),
        new GeoPoint(43.637699, -79.459091),
        new GeoPoint(43.637733, -79.458926),
        new GeoPoint(43.637830, -79.458414),
        new GeoPoint(43.637931, -79.457909),
        new GeoPoint(43.637965, -79.457673),
        new GeoPoint(43.638004, -79.457394),
        new GeoPoint(43.638038, -79.457098),
        new GeoPoint(43.638061, -79.456850),
        new GeoPoint(43.638091, -79.456524),
        new GeoPoint(43.638105, -79.456271),
        new GeoPoint(43.638118, -79.455803),
        new GeoPoint(43.638114, -79.455441),
        new GeoPoint(43.638111, -79.455226),
    ]);

    /// <summary>Southbound along a real stretch of Allen Road in Toronto, validated the
    /// same way as <see cref="DonValleyParkway"/>.</summary>
    public static Route AllenRoad() => new(
    [
        new GeoPoint(43.729952, -79.442954),
        new GeoPoint(43.729988, -79.442882),
        new GeoPoint(43.730164, -79.442551),
        new GeoPoint(43.730436, -79.442133),
        new GeoPoint(43.730708, -79.441741),
        new GeoPoint(43.731460, -79.440562),
        new GeoPoint(43.731744, -79.440142),
        new GeoPoint(43.732029, -79.439734),
        new GeoPoint(43.732652, -79.438930),
        new GeoPoint(43.732883, -79.438547),
        new GeoPoint(43.733514, -79.437501),
        new GeoPoint(43.734447, -79.435868),
        new GeoPoint(43.735289, -79.434454),
        new GeoPoint(43.735691, -79.433776),
        new GeoPoint(43.735848, -79.433521),
        new GeoPoint(43.735874, -79.433477),
        new GeoPoint(43.736731, -79.432009),
        new GeoPoint(43.737528, -79.430645),
        new GeoPoint(43.737866, -79.430063),
        new GeoPoint(43.738004, -79.429806),
        new GeoPoint(43.738640, -79.428729),
        new GeoPoint(43.740303, -79.425942),
        new GeoPoint(43.740339, -79.425889),
        new GeoPoint(43.740408, -79.425619),
        new GeoPoint(43.740847, -79.424767),
        new GeoPoint(43.741045, -79.424256),
        new GeoPoint(43.741094, -79.423898),
        new GeoPoint(43.741093, -79.423625),
        new GeoPoint(43.741105, -79.423390),
    ]);
}
