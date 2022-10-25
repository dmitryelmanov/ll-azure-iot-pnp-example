namespace TelemetryService;

#nullable disable warnings
public sealed class IoTHubOptions
{
    public string EventHubConnectionString { get; set; }
    public string? ConsumerGroupName { get; set; }
}
