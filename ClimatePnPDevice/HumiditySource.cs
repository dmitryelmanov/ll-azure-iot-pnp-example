using Common;

namespace ClimatePnPDevice;

public class HumiditySource : ITelemetrySource
{
    private readonly Random _random;
    private readonly static int MIN = 0;
    private readonly static int MAX = 100;

    public HumiditySource(double min, double max)
    {
        _random = new Random();
        Min = Math.Max(Math.Min(min, MAX), MIN);
        Max = Math.Min(Math.Max(max, MIN), MAX);
    }

    public double Min { get; set; }
    public double Max { get; set; }

    public Task<CanonicalTelemetry> NextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((CanonicalTelemetry)new HumidityTelemetry
        {
            Timestamp = DateTimeOffset.Now,
            Value = _random.NextDouble() * (Max - Min) + Min,
        });
}
