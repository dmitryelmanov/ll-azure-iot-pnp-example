namespace AzureIoTDevice.Configuration;

/// <summary>
/// Configuration of Edge Gateway device your Leaf device is connected through.
/// Needed only with Provisioning scenario (DPS).
/// </summary>
public interface IGatewayConfiguration
{
    string HostName { get; set; }
}
