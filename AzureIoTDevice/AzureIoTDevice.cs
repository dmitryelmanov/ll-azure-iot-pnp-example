using System.Text;
using AzureIoTDevice.Configuration;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MethodHandler = System.Func<
    byte[],
    System.Threading.CancellationToken,
    System.Threading.Tasks.Task<(int Status, string JsonPayload)>>;
using PropertyHandler = System.Func<
    dynamic,
    System.Threading.CancellationToken,
    System.Threading.Tasks.Task<dynamic>>;

namespace AzureIoTDevice;

public abstract class AzureIoTDevice : IDisposable
{
    protected readonly CancellationTokenSource _cancellationTokenSource;
    protected readonly ILogger? _logger;
    private readonly Task<DeviceRegistrationResult> _provisioningTask;
    private Task _connectingTask;
    private DeviceClient _deviceClient;
    private readonly IReadOnlyDictionary<string, PropertyHandler> _propertyHandlers;
    private readonly IReadOnlyDictionary<string, MethodHandler>? _methodHandlers;
    private readonly Func<string, Task>? _c2DMessageHandler;

    /// <summary>
    /// Provision device to IoT Hub and create Azure IoT Device client
    /// </summary>
    protected AzureIoTDevice(
        ProvisionAndConnectConfiguration configuration,
        ILogger? logger = null)
        : this(logger)
    {
        _provisioningTask = ProvisionAsync(configuration);
        _connectingTask = Task.Run(async () =>
        {
            var provisioningResult = await _provisioningTask;

            _logger?.LogInformation($"Device {provisioningResult.DeviceId} is assigned to {provisioningResult.AssignedHub}.");

            CreateClient(configuration, provisioningResult);

            await SetupHandlersAsync();
            await _deviceClient.OpenAsync(CancellationToken);
        });
    }

    /// <summary>
    /// Create Azure IoT Device client for already provisioned device
    /// </summary>
    protected AzureIoTDevice(
        ConnectConfiguration configuration,
        ILogger? logger = null)
        : this(logger)
    {
        _provisioningTask = Task.FromResult<DeviceRegistrationResult>(null!);
        _connectingTask = Task.Run(async () =>
        {
            await _provisioningTask;

            _logger?.LogDebug("Creating device client.");
            _deviceClient = DeviceClient.CreateFromConnectionString(
                configuration.DeviceConnectionString,
                configuration.TransportType,
                new ClientOptions
                {
                    ModelId = !string.IsNullOrWhiteSpace(configuration.ModelId) ? configuration.ModelId : null,
                });
            _logger?.LogDebug("Device client created.");

            await SetupHandlersAsync();
            await _deviceClient.OpenAsync(CancellationToken);
        });
    }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected async Task SendTelemetryAsync(string telemetry, CancellationToken cancellationToken = default)
    {
        await _connectingTask;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);

        _logger?.LogTrace($"Send telemetry: {telemetry}");
        var message = new Message(Encoding.UTF8.GetBytes(telemetry));
        await _deviceClient.SendEventAsync(message, cts.Token);
    }

    protected async Task UpdatePropertyAsync(string name, dynamic value)
    {
        await _connectingTask;

        _logger?.LogInformation($"Update {name} property to {value}.");
        var reported = new TwinCollection();
        reported[name] = value;
        await UpdateReportedPropertiesAsync(reported);
    }

    protected abstract IReadOnlyDictionary<string, PropertyHandler> SetPropertyHandlers();
    protected abstract IReadOnlyDictionary<string, MethodHandler> SetMethodHandlers();
    protected abstract Task HandleC2DMessageAsync(string message);

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _provisioningTask.Wait();
        _connectingTask.Wait();
        _deviceClient?.Dispose();
        _cancellationTokenSource.Dispose();
    }

    private AzureIoTDevice(
        ILogger? logger = null)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _logger = logger;
        _propertyHandlers = SetPropertyHandlers();
        _methodHandlers = SetMethodHandlers();
        _c2DMessageHandler = HandleC2DMessageAsync;
    }

    private async Task SetupHandlersAsync()
    {
        _logger?.LogDebug("Setup handlers.");
        SetConnectionStatusChangesHandler();
        await SetDesiredPropertyUpdateHandlerAsync();
        await SetMethodHandlersAsync();
        await SetReceiveMessageHandlerAsync();
    }

    private async Task<DeviceRegistrationResult> ProvisionAsync(ProvisionAndConnectConfiguration configuration)
    {
        _logger?.LogDebug("Creating provisioning client.");
        using var security = new SecurityProviderSymmetricKey(
            configuration.RegistrationId, configuration.PrimaryKey, configuration.SecondaryKey);
        using var transport = CreateTransportHandler(configuration.ProvisioningTransportType);
        var provisioningClient = ProvisioningDeviceClient.Create(
            configuration.GlobalDeviceEndpoint, configuration.IdScope, security, transport);
        _logger?.LogDebug("Provisioning client created.");

        string? provisioningPayload = null;
        if (!string.IsNullOrWhiteSpace(configuration.ModelId))
        {
            provisioningPayload = $"{{\"modelId\" : \"{configuration.ModelId}\"}}";
        }

        _logger?.LogDebug("Provisioning device.");
        var result = await provisioningClient.RegisterAsync(
            new ProvisioningRegistrationAdditionalData
            {
                JsonData = provisioningPayload
            },
            CancellationToken);

        if (result.Status != ProvisioningRegistrationStatusType.Assigned)
        {
            _logger?.LogError($"Device {configuration.RegistrationId} registration failed with status {result.Status}");
            throw new ApplicationException($"Device registration failed. Error {result.ErrorCode}: {result.ErrorMessage}");
        }

        return result;
    }

    private void CreateClient(
        ProvisionAndConnectConfiguration configuration,
        DeviceRegistrationResult registrationResult)
    {
        _logger?.LogDebug("Creating device client.");
        using var security = new SecurityProviderSymmetricKey(
            configuration.RegistrationId, configuration.PrimaryKey, configuration.SecondaryKey);
        var auth = new DeviceAuthenticationWithRegistrySymmetricKey(registrationResult.DeviceId, security.GetPrimaryKey());
        var clientOptions = new ClientOptions
        {
            ModelId = !string.IsNullOrWhiteSpace(configuration.ModelId) ? configuration.ModelId : null,
        };

        _deviceClient = !string.IsNullOrWhiteSpace(configuration.HostName)
            ? DeviceClient.Create(
                registrationResult.AssignedHub,
                configuration.HostName,
                auth,
                configuration.ConnectingTransportType,
                clientOptions)
            : DeviceClient.Create(
                registrationResult.AssignedHub,
                auth,
                configuration.ConnectingTransportType,
                clientOptions);
        _logger?.LogDebug("Device client created.");
    }

    private void SetConnectionStatusChangesHandler()
    {
        _deviceClient!.SetConnectionStatusChangesHandler(async (status, reason) =>
        {
            if (status == ConnectionStatus.Connected)
            {
                _logger?.LogInformation($"Connection status changed to {status} because of {reason}.");
                await SyncDevicePropertiesAsync();
            }
            else
            {
                _logger?.LogError($"Connection is {status} because of {reason}.");
            }
        });
    }

    private async Task SetDesiredPropertyUpdateHandlerAsync()
    {
        await _deviceClient!.SetDesiredPropertyUpdateCallbackAsync(
            async (props, userContext) =>
            {
                _logger?.LogInformation("Desired properties update received.");
                var reported = await HandleDesiredPropertiesAsync(props);
                await UpdateReportedPropertiesAsync(reported);
            },
            null);
    }

    private async Task SetMethodHandlersAsync()
    {
        foreach (var methodHandler in _methodHandlers ?? new Dictionary<string, MethodHandler>())
        {
            await _deviceClient!.SetMethodHandlerAsync(
                methodHandler.Key,
                async (request, userContext) =>
                {
                    _logger?.LogInformation($"{methodHandler.Key} method received.");
                    var (status, payload) = await methodHandler.Value(request.Data, CancellationToken);
                    return new MethodResponse(Encoding.UTF8.GetBytes(payload), status);
                },
                null);
        }
    }

    private async Task SetReceiveMessageHandlerAsync()
    {
        await _deviceClient!.SetReceiveMessageHandlerAsync(
            async (msg, userContext) =>
            {
                _logger?.LogInformation("C2D message received.");
                using var reader = new StreamReader(msg.BodyStream);
                var str = reader.ReadToEnd();
                if (_c2DMessageHandler != null)
                {
                    await _c2DMessageHandler(str);
                }

                await _deviceClient!.CompleteAsync(msg);
            },
            null);
    }

    private async Task SyncDevicePropertiesAsync()
    {
        _logger?.LogInformation("Sync device properties.");
        _logger?.LogDebug("Get device Twin.");
        var twin = await _deviceClient!.GetTwinAsync(CancellationToken);
        _logger?.LogTrace("Twin:");
        _logger?.LogTrace(twin?.ToJson(Formatting.Indented));

        var reported = await HandleDesiredPropertiesAsync(twin!.Properties.Desired);
        await UpdateReportedPropertiesAsync(reported);
    }

    private async Task<TwinCollection> HandleDesiredPropertiesAsync(TwinCollection desired)
    {
        _logger?.LogDebug("Handling desired properties.");
        var reported = new TwinCollection();
        foreach (KeyValuePair<string, dynamic> prop in desired)
        {
            if (_propertyHandlers.ContainsKey(prop.Key))
            {
                var updatedValue = await _propertyHandlers[prop.Key](prop.Value, CancellationToken);
                reported[prop.Key] = updatedValue;
                _logger?.LogTrace($"Property {prop.Key}: Desired value {prop.Value}. Reported value {updatedValue}.");
            }
        }

        return reported;
    }

    private async Task UpdateReportedPropertiesAsync(TwinCollection reported)
    {
        _logger?.LogInformation("Update reported properties.");
        await _deviceClient!.UpdateReportedPropertiesAsync(reported, CancellationToken);
    }

    private static ProvisioningTransportHandler CreateTransportHandler(TransportType transportType)
        => transportType switch
        {
            TransportType.Mqtt => new ProvisioningTransportHandlerMqtt(),
            TransportType.Mqtt_Tcp_Only => new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly),
            TransportType.Mqtt_WebSocket_Only => new ProvisioningTransportHandlerMqtt(TransportFallbackType.WebSocketOnly),
            TransportType.Amqp => new ProvisioningTransportHandlerAmqp(),
            TransportType.Amqp_Tcp_Only => new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly),
            TransportType.Amqp_WebSocket_Only => new ProvisioningTransportHandlerAmqp(TransportFallbackType.WebSocketOnly),
            TransportType.Http1 => new ProvisioningTransportHandlerHttp(),
            _ => throw new NotSupportedException($"Unsupported transport type {transportType}"),
        };
}
