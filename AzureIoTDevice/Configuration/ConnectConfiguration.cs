using Microsoft.Azure.Devices.Client;

namespace AzureIoTDevice.Configuration;

#nullable disable warnings
public class ConnectConfiguration
    : IDeviceConnectionString
    , IDeviceConfiguration
    , IPlugAndPlayConfiguration
{
    public string DeviceConnectionString { get; set; }
    public TransportType TransportType { get; set; }
    /// <summary>
    /// [optional] Model Id of Plug'n'Play device
    /// </summary>
    public string? ModelId { get; set; }
}
