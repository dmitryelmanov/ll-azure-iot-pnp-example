using Microsoft.Azure.Devices.Client;

namespace ClimatePnPDevice;

#nullable disable warnings
public class ClimateDeviceOptions
{
    public TransportType TransportType { get; set; }
    public string DeviceConnectionString { get; set; }
    public string ModelId { get; set; }
}
