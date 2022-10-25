using System.Text.Json.Serialization;
using Azure;
using Azure.DigitalTwins.Core;

namespace ProvisioningService.DigitalTwin;

#nullable disable warnings
internal class ClimateDeviceTwin
{
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinId)]
    public string Id { get; set; }
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinETag)]
    public ETag ETag { get; set; }
    [JsonPropertyName("temperature")]
    public TemperatureSensorTwin Temperature { get; set; }
    [JsonPropertyName("humidity")]
    public HumiditySensorTwin Humidity { get; set; }
    [JsonPropertyName("telemetryInterval")]
    public int TelemetryInterval { get; set; }
    [JsonPropertyName(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata)]
    public ClimateDeviceMetadata Metadata { get; set; }
}
