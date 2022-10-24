using Microsoft.Azure.Devices.Client;

namespace AzureIoTDevice.Configuration;

#nullable disable warnings
public class ProvisionAndConnectConfiguration
    : IDpsConfiguration
    , IDeviceConfiguration
    , IEdgeConfiguration
    , IPlugAndPlayConfiguration
{
    public string GlobalDeviceEndpoint { get; set; }
    public string IdScope { get; set; }
    public string RegistrationId { get; set; }
    public string PrimaryKey { get; set; }
    public string? SecondaryKey { get; set; }
    /// <summary>
    /// Transport Type for provisioning
    /// </summary>
    public TransportType ProvisioningTransportType { get; set; }
    /// <summary>
    /// Transport Type for connecting to IoT Hub
    /// </summary>
    public TransportType ConnectingTransportType { get; set; }
    /// <summary>
    /// [optional] Id of an Edge Gateway device the device is connected trough
    /// </summary>
    public string? EdgeDeviceId { get; set; }
    /// <summary>
    /// [optional] HostName of an Edge Gateway device the device is connected trough
    /// </summary>
    public string? HostName { get; set; }
    /// <summary>
    /// [optional] Model Id of Plug'n'Play device
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Transport Type for provisioning
    /// </summary>
    TransportType IDpsConfiguration.TransportType
    {
        get => ProvisioningTransportType; 
        set => ProvisioningTransportType = value;
    }
    /// <summary>
    /// Transport Type for connecting to IoT Hub
    /// </summary>
    TransportType IDeviceConfiguration.TransportType
    {
        get => ConnectingTransportType;
        set => ConnectingTransportType = value;
    }
}
