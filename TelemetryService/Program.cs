using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Configuration;
using Serilog.Extensions.Logging;
using Serilog;
using System.Text;
using TelemetryService;
using Microsoft.Extensions.Logging;

const string ENV_PREFIX = "IOT_TELEMETRY_SERVICE_";

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(ENV_PREFIX)
    .AddUserSecrets<IoTHubOptions>()
    .AddCommandLine(Environment.GetCommandLineArgs())
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();

var logger = new SerilogLoggerFactory(Log.Logger)
    .CreateLogger("ClimateDevice");

var hubOptions = config.GetSection("IoTHub").Get<IoTHubOptions>();

logger.LogInformation("Start provisioning service.");
await using var consumerClient = new EventHubConsumerClient(
    hubOptions.ConsumerGroupName ?? EventHubConsumerClient.DefaultConsumerGroupName,
    hubOptions.EventHubConnectionString);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
Console.WriteLine("Press Control+C to quit");

logger.LogInformation("Listening for messages on all partitions.");
try
{
    await foreach (var partitionEvent in consumerClient.ReadEventsAsync(
        new ReadEventOptions
        {
            MaximumWaitTime = TimeSpan.FromMilliseconds(500),
        },
        cts.Token))
    {
        if (partitionEvent.Data != null
            && partitionEvent.Data.SystemProperties.TryGetValue("iothub-message-source", out var source)
            && source.Equals("Telemetry"))
        {
            var deviceId = (string)partitionEvent.Data.SystemProperties["iothub-connection-device-id"];
            if (partitionEvent.Data.SystemProperties.TryGetValue("dt-dataschema", out var schema))
            {
                logger.LogInformation($"Telemetry received fot device {deviceId} with schema {schema}.");
            }
            else
            {
                logger.LogInformation($"Telemetry received fot device {deviceId}.");

            }

            string data = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());
            logger.LogTrace($"Data:\r\n\t{data}");

            // TODO: Store data anywhere, or send it somewhere, or do whatever you want.
        }

        if (cts.IsCancellationRequested) break;
    }
}
catch (OperationCanceledException) { }
