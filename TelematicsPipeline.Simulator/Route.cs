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
    /// Along a real stretch of the Don Valley Parkway in Toronto -- fetched once from the
    /// Mapbox Directions API (driving profile), not a hand-picked handful of points
    /// connected by straight lines. Of all the clean (no interchange-ramp jumps, no
    /// unrealistic derived G-force) windows along the fetched route, this is the one with
    /// the most total turning -- the DVP follows the Don River valley, so there's real
    /// s-curve geometry to pick from rather than settling for the straightest stretch.
    /// </summary>
    public static Route DonValleyParkway() => new(
    [
        new GeoPoint(43.704948, -79.331899),
        new GeoPoint(43.704824, -79.331925),
        new GeoPoint(43.704369, -79.332030),
        new GeoPoint(43.704093, -79.332108),
        new GeoPoint(43.703814, -79.332210),
        new GeoPoint(43.703605, -79.332304),
        new GeoPoint(43.703451, -79.332379),
        new GeoPoint(43.703085, -79.332621),
        new GeoPoint(43.702938, -79.332729),
        new GeoPoint(43.702840, -79.332804),
        new GeoPoint(43.702663, -79.332972),
        new GeoPoint(43.702477, -79.333155),
        new GeoPoint(43.702252, -79.333421),
        new GeoPoint(43.702073, -79.333675),
        new GeoPoint(43.701941, -79.333871),
        new GeoPoint(43.701794, -79.334135),
        new GeoPoint(43.701636, -79.334447),
        new GeoPoint(43.701491, -79.334782),
        new GeoPoint(43.701331, -79.335194),
        new GeoPoint(43.701131, -79.335725),
        new GeoPoint(43.700925, -79.336268),
        new GeoPoint(43.700733, -79.336773),
        new GeoPoint(43.700596, -79.337082),
        new GeoPoint(43.700388, -79.337511),
        new GeoPoint(43.700177, -79.337876),
        new GeoPoint(43.699995, -79.338181),
        new GeoPoint(43.699750, -79.338502),
        new GeoPoint(43.699422, -79.338930),
        new GeoPoint(43.699227, -79.339179),
        new GeoPoint(43.698924, -79.339574),
        new GeoPoint(43.698717, -79.339850),
        new GeoPoint(43.698626, -79.339975),
        new GeoPoint(43.698552, -79.340073),
        new GeoPoint(43.698364, -79.340370),
        new GeoPoint(43.698199, -79.340668),
        new GeoPoint(43.698028, -79.341043),
        new GeoPoint(43.697846, -79.341516),
        new GeoPoint(43.697718, -79.341937),
        new GeoPoint(43.697671, -79.342139),
        new GeoPoint(43.697605, -79.342469),
        new GeoPoint(43.697567, -79.342751),
        new GeoPoint(43.697538, -79.343017),
        new GeoPoint(43.697502, -79.343461),
        new GeoPoint(43.697499, -79.343824),
        new GeoPoint(43.697511, -79.344194),
        new GeoPoint(43.697597, -79.345015),
        new GeoPoint(43.697795, -79.346380),
        new GeoPoint(43.697900, -79.347086),
        new GeoPoint(43.697997, -79.347749),
        new GeoPoint(43.698049, -79.348157),
        new GeoPoint(43.698086, -79.348575),
        new GeoPoint(43.698106, -79.349010),
        new GeoPoint(43.698098, -79.349417),
        new GeoPoint(43.698077, -79.349951),
        new GeoPoint(43.698042, -79.350230),
        new GeoPoint(43.697984, -79.350519),
        new GeoPoint(43.697912, -79.350806),
        new GeoPoint(43.697790, -79.351235),
        new GeoPoint(43.697675, -79.351585),
        new GeoPoint(43.697581, -79.351832),
        new GeoPoint(43.697545, -79.351925),
        new GeoPoint(43.697412, -79.352204),
        new GeoPoint(43.697225, -79.352555),
        new GeoPoint(43.697014, -79.352904),
        new GeoPoint(43.696787, -79.353212),
        new GeoPoint(43.696661, -79.353373),
        new GeoPoint(43.696424, -79.353652),
        new GeoPoint(43.696016, -79.354146),
    ]);

    /// <summary>Along a real, curviest-available stretch of the Gardiner Expressway in
    /// Toronto, chosen and validated the same way as <see cref="DonValleyParkway"/>.</summary>
    public static Route GardinerExpressway() => new(
    [
        new GeoPoint(43.638103, -79.455048),
        new GeoPoint(43.638100, -79.454989),
        new GeoPoint(43.638092, -79.454709),
        new GeoPoint(43.637987, -79.453567),
        new GeoPoint(43.637942, -79.453099),
        new GeoPoint(43.637929, -79.453007),
        new GeoPoint(43.637775, -79.451828),
        new GeoPoint(43.637748, -79.451614),
        new GeoPoint(43.637486, -79.450133),
        new GeoPoint(43.637262, -79.448955),
        new GeoPoint(43.637240, -79.448854),
        new GeoPoint(43.637204, -79.448654),
        new GeoPoint(43.637117, -79.448161),
        new GeoPoint(43.637093, -79.447988),
        new GeoPoint(43.637049, -79.447661),
        new GeoPoint(43.637034, -79.447486),
        new GeoPoint(43.637022, -79.447306),
        new GeoPoint(43.637019, -79.447183),
        new GeoPoint(43.637015, -79.446843),
        new GeoPoint(43.637016, -79.446790),
        new GeoPoint(43.637025, -79.446677),
        new GeoPoint(43.637047, -79.446404),
        new GeoPoint(43.637070, -79.446102),
        new GeoPoint(43.637071, -79.445943),
        new GeoPoint(43.637068, -79.445813),
        new GeoPoint(43.637050, -79.445655),
        new GeoPoint(43.637023, -79.445523),
        new GeoPoint(43.636971, -79.445333),
        new GeoPoint(43.636913, -79.445172),
        new GeoPoint(43.636343, -79.443860),
        new GeoPoint(43.636130, -79.443370),
        new GeoPoint(43.636106, -79.443311),
        new GeoPoint(43.636081, -79.443245),
        new GeoPoint(43.635969, -79.442922),
        new GeoPoint(43.635716, -79.442088),
        new GeoPoint(43.635407, -79.441220),
        new GeoPoint(43.635365, -79.441004),
        new GeoPoint(43.635010, -79.440197),
        new GeoPoint(43.634591, -79.439244),
        new GeoPoint(43.634412, -79.438827),
        new GeoPoint(43.634245, -79.438439),
        new GeoPoint(43.634087, -79.438091),
        new GeoPoint(43.634019, -79.437944),
        new GeoPoint(43.633898, -79.437791),
        new GeoPoint(43.633795, -79.437643),
        new GeoPoint(43.633711, -79.437505),
        new GeoPoint(43.633644, -79.437404),
        new GeoPoint(43.633565, -79.437290),
        new GeoPoint(43.633405, -79.437077),
        new GeoPoint(43.632978, -79.436548),
        new GeoPoint(43.632961, -79.436531),
        new GeoPoint(43.632844, -79.436357),
        new GeoPoint(43.632746, -79.436190),
        new GeoPoint(43.632656, -79.436004),
        new GeoPoint(43.632532, -79.435720),
        new GeoPoint(43.632427, -79.435441),
        new GeoPoint(43.632315, -79.435125),
        new GeoPoint(43.632197, -79.434733),
        new GeoPoint(43.632116, -79.434397),
        new GeoPoint(43.632011, -79.433762),
        new GeoPoint(43.631990, -79.433651),
        new GeoPoint(43.631930, -79.433323),
        new GeoPoint(43.631880, -79.432989),
        new GeoPoint(43.631893, -79.432501),
        new GeoPoint(43.631911, -79.432372),
        new GeoPoint(43.631935, -79.432262),
        new GeoPoint(43.631970, -79.432136),
        new GeoPoint(43.632019, -79.432023),
        new GeoPoint(43.632260, -79.431554),
        new GeoPoint(43.632311, -79.431449),
        new GeoPoint(43.632344, -79.431340),
        new GeoPoint(43.632420, -79.430991),
        new GeoPoint(43.632496, -79.430146),
        new GeoPoint(43.632572, -79.429782),
    ]);

    /// <summary>Along a real, curviest-available stretch of Allen Road in Toronto, chosen
    /// and validated the same way as <see cref="DonValleyParkway"/>.</summary>
    public static Route AllenRoad() => new(
    [
        new GeoPoint(43.735891, -79.434600),
        new GeoPoint(43.735734, -79.434854),
        new GeoPoint(43.734627, -79.436725),
        new GeoPoint(43.734406, -79.437161),
        new GeoPoint(43.734213, -79.437366),
        new GeoPoint(43.733870, -79.438020),
        new GeoPoint(43.733377, -79.439010),
        new GeoPoint(43.733093, -79.439560),
        new GeoPoint(43.732803, -79.440117),
        new GeoPoint(43.732654, -79.440476),
        new GeoPoint(43.732530, -79.440836),
        new GeoPoint(43.732367, -79.441994),
        new GeoPoint(43.732344, -79.442164),
        new GeoPoint(43.732275, -79.442554),
        new GeoPoint(43.732201, -79.442868),
        new GeoPoint(43.731973, -79.443501),
        new GeoPoint(43.731686, -79.444202),
        new GeoPoint(43.731529, -79.444591),
        new GeoPoint(43.731067, -79.445817),
        new GeoPoint(43.730999, -79.446024),
        new GeoPoint(43.730950, -79.446247),
        new GeoPoint(43.730930, -79.446353),
        new GeoPoint(43.730912, -79.446462),
        new GeoPoint(43.730899, -79.446553),
        new GeoPoint(43.730887, -79.446650),
        new GeoPoint(43.730874, -79.446759),
        new GeoPoint(43.730865, -79.446877),
        new GeoPoint(43.730855, -79.447021),
        new GeoPoint(43.730848, -79.447173),
        new GeoPoint(43.730846, -79.447327),
        new GeoPoint(43.730850, -79.447478),
        new GeoPoint(43.730861, -79.447632),
        new GeoPoint(43.730879, -79.447786),
        new GeoPoint(43.730925, -79.448079),
        new GeoPoint(43.731057, -79.448864),
        new GeoPoint(43.731101, -79.449153),
        new GeoPoint(43.731134, -79.449435),
        new GeoPoint(43.731137, -79.449638),
        new GeoPoint(43.731129, -79.449832),
        new GeoPoint(43.731105, -79.450042),
        new GeoPoint(43.731071, -79.450225),
        new GeoPoint(43.731026, -79.450391),
        new GeoPoint(43.730985, -79.450519),
        new GeoPoint(43.730942, -79.450633),
        new GeoPoint(43.730890, -79.450748),
        new GeoPoint(43.730839, -79.450849),
        new GeoPoint(43.730783, -79.450940),
        new GeoPoint(43.730728, -79.451024),
        new GeoPoint(43.730665, -79.451104),
        new GeoPoint(43.730602, -79.451178),
        new GeoPoint(43.730486, -79.451289),
        new GeoPoint(43.730371, -79.451376),
        new GeoPoint(43.730284, -79.451432),
        new GeoPoint(43.730190, -79.451480),
        new GeoPoint(43.730113, -79.451513),
        new GeoPoint(43.729974, -79.451557),
        new GeoPoint(43.729837, -79.451583),
        new GeoPoint(43.729734, -79.451589),
        new GeoPoint(43.729633, -79.451586),
        new GeoPoint(43.729528, -79.451573),
        new GeoPoint(43.729425, -79.451549),
        new GeoPoint(43.729316, -79.451512),
        new GeoPoint(43.729208, -79.451462),
        new GeoPoint(43.729117, -79.451412),
        new GeoPoint(43.729025, -79.451346),
        new GeoPoint(43.728908, -79.451249),
        new GeoPoint(43.728823, -79.451166),
        new GeoPoint(43.728739, -79.451072),
        new GeoPoint(43.728646, -79.450939),
        new GeoPoint(43.728553, -79.450787),
        new GeoPoint(43.728479, -79.450654),
        new GeoPoint(43.728405, -79.450509),
        new GeoPoint(43.728255, -79.450184),
        new GeoPoint(43.728083, -79.449801),
        new GeoPoint(43.728030, -79.449693),
        new GeoPoint(43.727970, -79.449580),
        new GeoPoint(43.727913, -79.449488),
        new GeoPoint(43.727857, -79.449405),
        new GeoPoint(43.727783, -79.449313),
        new GeoPoint(43.727700, -79.449224),
        new GeoPoint(43.727677, -79.449201),
        new GeoPoint(43.727589, -79.449124),
        new GeoPoint(43.727490, -79.449057),
        new GeoPoint(43.727431, -79.449018),
        new GeoPoint(43.727126, -79.448838),
        new GeoPoint(43.726982, -79.448776),
        new GeoPoint(43.726569, -79.448588),
        new GeoPoint(43.726195, -79.448401),
        new GeoPoint(43.725873, -79.448274),
        new GeoPoint(43.725722, -79.448157),
        new GeoPoint(43.725472, -79.448046),
        new GeoPoint(43.723798, -79.447415),
    ]);
}
