using System.Text.Json.Serialization;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class HumiditySensorTwin
{
    [JsonPropertyName("targetHumidityRange")]
    public TargetRangeProperty TargetHumidityRange { get; set; }
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata)]
    public HumiditySensorMetadata Metadata { get; set; }
}
