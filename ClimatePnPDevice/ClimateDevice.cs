using AzureIoTDevice.Configuration;
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

namespace ClimatePnPDevice;

public sealed class ClimateDevice
    : AzureIoTDevice.AzureIoTDevice
{
    private readonly TemperatureSource _temperatureSource;
    private readonly HumiditySource _humiditySource;

    public ClimateDevice(ProvisionAndConnectConfiguration configuration, ILogger? logger = null)
        : base(configuration, logger)
    {
        _temperatureSource = new TemperatureSource(0, 100);
        _humiditySource = new HumiditySource(0, 100);
    }

    public ClimateDevice(ConnectConfiguration configuration, ILogger? logger = null)
        : base(configuration, logger)
    {
        _temperatureSource = new TemperatureSource(0, 100);
        _humiditySource = new HumiditySource(0, 100);
    }

    public TimeSpan TelemetryInterval { get; set; } = TimeSpan.FromSeconds(3);

    public async Task ExecAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(TelemetryLoopAsync(cancellationToken));
    }

    protected override IReadOnlyDictionary<string, PropertyHandler> SetPropertyHandlers() 
        => new Dictionary<string, PropertyHandler>
        {
            {
                "telemetryInterval",
                (value, cancellationToken) =>
                {
                    var ms = Convert.ToInt32(value);
                    TelemetryInterval = TimeSpan.FromMilliseconds(ms > 0 ? ms : TelemetryInterval.TotalMilliseconds);
                    return Task.FromResult((dynamic)TelemetryInterval.TotalMilliseconds);
                }
            },
            {
                "temperature",
                (value, cancellationToken) =>
                {
                    _logger?.LogTrace($"temperature: {value}");
                    return Task.FromResult(value);
                }
            }
        };

    protected override IReadOnlyDictionary<string, MethodHandler> SetMethodHandlers()
        => new Dictionary<string, MethodHandler>
        {
            {
                "reboot",
                (payload, cancellationToken) =>
                {
                    return Task.FromResult((202, "{}"));
                }
            }
        };

    protected override Task HandleC2DMessageAsync(string message)
    {
        return Task.CompletedTask;
    }

    private async Task TelemetryLoopAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
        while (!cts.IsCancellationRequested)
        {
            var telemetry = new
            {
                Temperature = await _temperatureSource.NextAsync(),
                Humidity = await _humiditySource.NextAsync(),
            };
            var json = JsonConvert.SerializeObject(telemetry);
            await SendTelemetryAsync(json, cancellationToken);
            await Task.Delay(TelemetryInterval, cts.Token);
        }
    }
}
