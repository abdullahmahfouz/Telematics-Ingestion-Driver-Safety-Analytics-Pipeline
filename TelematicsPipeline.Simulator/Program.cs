using System.Net.Http.Json;
using TelematicsPipeline.Simulator;

var deviceId = GetOption("--device") ?? "b2A83F1";
var apiBaseUrl = GetOption("--api") ?? "http://localhost:5231/api/telematics";
var apiKey = GetOption("--api-key");
var speedMultiplier = double.TryParse(GetOption("--speed"), out var parsed) ? Math.Max(0.1, parsed) : 1.0;
var dryRun = args.Contains("--dry-run");

if (!dryRun && string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("error: --api-key is required (the API now authenticates ingestion). Provision one with:");
    Console.WriteLine("  scripts/provision-device.sh <deviceId>");
    return 1;
}

var route = Route.DonValleyParkway();
var phases = TripScript.RushHourCommute();
var simulator = new TripSimulator(route, phases, deviceId, seed: 42);

// Materialise once so the run and the expected-detection counts describe the same drive.
var readings = simulator.Run(DateTimeOffset.UtcNow).ToList();
var (expectedBraking, expectedAcceleration, expectedCornering) = TripScript.ExpectedDetections(readings);
var totalSeconds = phases.Sum(p => p.Seconds);

Console.WriteLine($"Device          {deviceId}");
Console.WriteLine($"Route           {route.TotalMetres / 1000:F2} km, {totalSeconds}s trip at 1Hz");
Console.WriteLine($"Playback        {speedMultiplier}x{(dryRun ? " (dry run -- nothing will be sent)" : $" -> {apiBaseUrl}/ingest")}");
Console.WriteLine($"Expected        {expectedBraking} harsh braking, {expectedAcceleration} harsh acceleration, {expectedCornering} harsh cornering");
Console.WriteLine("                (the API's counts should match these once the run finishes)");
Console.WriteLine();

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
if (!string.IsNullOrWhiteSpace(apiKey))
{
    http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
}

var sent = 0;
var failed = 0;
var flagged = 0;

foreach (var reading in readings)
{
    var isEventPhase = reading.Phase is PhaseKind.HarshBraking or PhaseKind.HarshAcceleration or PhaseKind.HarshCornering;
    if (isEventPhase)
    {
        flagged++;
    }

    if (!dryRun)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"{apiBaseUrl}/ingest", ToPayload(reading));
            if (response.IsSuccessStatusCode)
            {
                sent++;
            }
            else
            {
                failed++;
                Console.WriteLine($"  ingest failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"  ingest error: {ex.Message}");
        }
    }

    var marker = isEventPhase ? "<<" : "  ";
    Console.WriteLine(
        $"{reading.Timestamp:HH:mm:ss}  {reading.SpeedKmh,5:F1} km/h  " +
        $"X {reading.AccelerationXG,6:F2}g  Y {reading.AccelerationYG,6:F2}g  " +
        $"{reading.Phase,-18} {marker}");

    if (!dryRun && speedMultiplier > 0)
    {
        await Task.Delay(TimeSpan.FromSeconds(1 / speedMultiplier));
    }
}

Console.WriteLine();
Console.WriteLine(dryRun
    ? $"Dry run complete. {flagged} readings fell inside a scripted event phase."
    : $"Done. Sent {sent}, failed {failed}. {flagged} readings fell inside a scripted event phase.");

return failed == 0 ? 0 : 1;

static object ToPayload(SimulatedReading r) => new
{
    deviceId = r.DeviceId,
    timestamp = r.Timestamp,
    latitude = r.Latitude,
    longitude = r.Longitude,
    speedKmh = r.SpeedKmh,
    headingDegrees = r.HeadingDegrees,
    engineRpm = r.EngineRpm,
    engineCoolantTempC = r.EngineCoolantTempC,
    fuelLevelPercent = r.FuelLevelPercent,
    odometerKm = r.OdometerKm,
    isIdling = r.IsIdling,
    isIgnitionOn = r.IsIgnitionOn,
    accelerationXG = r.AccelerationXG,
    accelerationYG = r.AccelerationYG,
    accelerationZG = r.AccelerationZG,
};

string? GetOption(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
