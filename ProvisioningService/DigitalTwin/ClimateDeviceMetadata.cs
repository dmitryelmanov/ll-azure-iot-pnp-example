using System.Text.Json.Serialization;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class ClimateDeviceMetadata : DigitalTwinMetadata
{
    [JsonPropertyName("telemetryInterval")]
    public DigitalTwinPropertyMetadata TelemetryInterval { get; set; }
}
