using System.Text.Json.Serialization;

namespace ProvisioningService.DigitalTwin;

internal class TargetRangeProperty
{
    [JsonPropertyName("min")]
    public double? Min { get; set; }
    [JsonPropertyName("max")]
    public double? Max { get; set; }
}
