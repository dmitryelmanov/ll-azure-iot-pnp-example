﻿using System.Text;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ProvisioningService;
using Serilog;
using Serilog.Extensions.Logging;

const string ENV_PREFIX = "IOT_PROVISIONING_SERVICE_";

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
    EventHubConsumerClient.DefaultConsumerGroupName,
    hubOptions.EventHubConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(hubOptions.ConnectionString);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
Console.WriteLine("Press Control+C to quit");

logger.LogInformation("Listening for messages on all partitions.");
await foreach (var partitionEvent in consumerClient.ReadEventsAsync(new ReadEventOptions
{
    MaximumWaitTime = TimeSpan.FromMilliseconds(500),
}))
{
    if (partitionEvent.Data != null)
    {
        logger.LogInformation("Message received on partition {0}:", partitionEvent.Partition.PartitionId);

        string data = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());
        logger.LogTrace($"Data:\r\n\t{data}");

        logger.LogDebug("Application properties:");
        foreach (var prop in partitionEvent.Data.Properties)
        {
            logger.LogDebug("\t{0}: {1}", prop.Key, prop.Value);
        }

        logger.LogDebug("System properties:");
        foreach (var prop in partitionEvent.Data.SystemProperties)
        {
            logger.LogDebug("\t{0}: {1}", prop.Key, prop.Value);
        }

        if (partitionEvent.Data.Properties.TryGetValue("iothub-message-schema", out var schema)
            && schema.Equals("deviceLifecycleNotification")
            && partitionEvent.Data.Properties.TryGetValue("opType", out var opType)
            && opType.Equals("createDeviceIdentity"))
        {
            var deviceId = (string)partitionEvent.Data.Properties["deviceId"];
            try
            {
                var twin = await registryManager.GetTwinAsync(deviceId);
                logger.LogTrace($"Twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get Device Twin.");
            }
        }
    }

    if (cts.IsCancellationRequested) break;
}