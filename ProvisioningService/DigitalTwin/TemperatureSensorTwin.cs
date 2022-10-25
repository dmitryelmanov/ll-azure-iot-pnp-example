using System.Text.Json.Serialization;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class TemperatureSensorTwin
{
    [JsonPropertyName("targetTemperatureRange")]
    public TargetRangeProperty TargetTemperatureRange { get; set; }
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata)]
    public TemperatureSensorMetadata Metadata { get; set; }
}
