using Microsoft.Azure.Devices.Client;

namespace AzureIoTDevice.Configuration;

public interface IDeviceConfiguration
{
    TransportType TransportType { get; set; }
}
