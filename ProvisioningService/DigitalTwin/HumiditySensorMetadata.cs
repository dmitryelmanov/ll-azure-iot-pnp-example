using System.Text.Json.Serialization;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class HumiditySensorMetadata
{
    [JsonPropertyName("targetHumidityRange")]
    public DigitalTwinPropertyMetadata TargetHumidityRange { get; set; }
}
