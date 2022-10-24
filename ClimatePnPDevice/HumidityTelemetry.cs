using Common;

namespace ClimatePnPDevice;

public sealed class HumidityTelemetry : CanonicalTelemetry
{
    public override string Type => "humidity";
    public override string Unit => "percent";
}
