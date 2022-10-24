using ClimatePnPDevice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

const string ENV_PREFIX = "IOT_CLIMATE_DEVICE_";

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(ENV_PREFIX)
    .AddUserSecrets<ClimateDevice>()
    .AddCommandLine(Environment.GetCommandLineArgs())
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();

var logger = new SerilogLoggerFactory(Log.Logger)
    .CreateLogger("ClimateDevice");

var deviceOptions = config.GetSection("ClimateDevice").Get<ClimateDeviceOptions>();

logger.LogInformation("Initializing Climate IoT Device");
using var climateDevice = ClimateDeviceFactory.Create(deviceOptions, logger);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
Console.WriteLine("Press Control+C to quit");

try
{
    await climateDevice.ExecAsync(cts.Token);
}
catch (OperationCanceledException) { }

logger.LogInformation("Exit Universal IoT Device");
