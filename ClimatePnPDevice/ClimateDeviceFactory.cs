using AzureIoTDevice.Configuration;
using Microsoft.Extensions.Logging;

namespace ClimatePnPDevice;

internal static class ClimateDeviceFactory
{
    public static ClimateDevice Create(ClimateDeviceOptions options, ILogger? logger = null)
    {
        return new ClimateDevice(
            new ConnectConfiguration
            {
                TransportType = options.TransportType,
                DeviceConnectionString = options.DeviceConnectionString,
                ModelId = options.ModelId,
            },
            logger);
    }
}
