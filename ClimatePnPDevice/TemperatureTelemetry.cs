using Common;

namespace ClimatePnPDevice;

public class TemperatureTelemetry : CanonicalTelemetry
{
    public override string Type => "temperature";
    public override string Unit => "degreeCelsius";
}
