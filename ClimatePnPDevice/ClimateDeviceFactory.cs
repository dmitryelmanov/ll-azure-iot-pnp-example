using AzureIoTDevice.Configuration;
using Microsoft.Extensions.Logging;

namespace ClimatePnPDevice;

internal static class ClimateDeviceFactory
{
    public static ClimateDevice Create(ClimateDeviceOptions options, ILogger? logger = null)
    {
        return !string.IsNullOrWhiteSpace(options.PrimaryKey)
            ? new ClimateDevice(
                new ProvisionAndConnectConfiguration
                {
                    GlobalDeviceEndpoint = options.GlobalDeviceEndpoint!,
                    IdScope = options.IdScope!,
                    RegistrationId = options.RegistrationId!,
                    PrimaryKey = options.PrimaryKey!,
                    ProvisioningTransportType = options.TransportType,
                    ModelId = options.ModelId,
                    ConnectingTransportType = options.TransportType,
                    EdgeDeviceId = options.EdgeDeviceId,
                    HostName = options.EdgeDeviceHostName,
                },
                logger)
            : new ClimateDevice(
                new ConnectConfiguration
                {
                    TransportType = options.TransportType,
                    DeviceConnectionString = options.DeviceConnectionString!,
                    ModelId = options.ModelId,
                },
                logger);
    }
}
