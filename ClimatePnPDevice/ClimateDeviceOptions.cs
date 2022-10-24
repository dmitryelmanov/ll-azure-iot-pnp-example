using Microsoft.Azure.Devices.Client;

namespace ClimatePnPDevice;

#nullable disable warnings
public class ClimateDeviceOptions
{
    public TransportType TransportType { get; set; }
    public string? DeviceConnectionString { get; set; }
    public string ModelId { get; set; }
    // DPS configuration
    public string? GlobalDeviceEndpoint { get; set; }
    public string? IdScope { get; set; }
    public string? RegistrationId { get; set; }
    public string? PrimaryKey { get; set; }
    // Edge device
    public string? EdgeDeviceId { get; set; }
    public string? EdgeDeviceHostName { get; set; }
}
