using System.Text.Json.Serialization;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class TemperatureSensorMetadata
{
    [JsonPropertyName("targetTemperatureRange")]
    public DigitalTwinPropertyMetadata TargetTemperatureRange { get; set; }
}
