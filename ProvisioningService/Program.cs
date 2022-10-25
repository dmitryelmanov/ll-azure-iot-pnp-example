/*
 * Azure DPS is not able to provision a downstream device (device that connects to IoT Hub trough Edge device).
 * To be specific, it can't set a parent for a downstream device.
 * This service designed as a workaround for that issue.
 * This service receives notifications when a new devie has created, and,
 * if there is a tag 'edegeId', service will set that Edge device as a parent for created device.
 * Note: It only works with Provisioning Function as a Custom Allocation Policy in DPS.
 * Also this service is creating Digital Twin for a new device, 
 * and a relationship, if the device has a parent device (Edge device).
 */

using System.Text;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ProvisioningService;
using ProvisioningService.DigitalTwin;
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
var digitalTwinOptions = config.GetSection("DigitalTwin").Get<DigitalTwinOptions>();

logger.LogInformation("Start provisioning service.");
await using var consumerClient = new EventHubConsumerClient(
    hubOptions.ConsumerGroupName ?? EventHubConsumerClient.DefaultConsumerGroupName,
    hubOptions.EventHubConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(hubOptions.ConnectionString);

var credentials = new DefaultAzureCredential();
var digitalTwinsClient = new DigitalTwinsClient(new Uri(digitalTwinOptions.Uri), credentials);

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
                string? parentId = null;

                var deviceTwin = JsonConvert.DeserializeObject<Twin>(data);
                logger.LogTrace($"Twin: {JsonConvert.SerializeObject(deviceTwin, Formatting.Indented)}");

                try
                {
                    if (deviceTwin!.Tags.Contains("edgeId") && !string.IsNullOrEmpty((string)deviceTwin!.Tags["edgeId"]))
                    {
                        var edgeId = (string)deviceTwin!.Tags["edgeId"];
                        var edgeDevice = await registryManager.GetDeviceAsync(edgeId, cts.Token);
                        if (edgeDevice != null)
                        {
                            var device = await registryManager.GetDeviceAsync(deviceId, cts.Token);
                            device.Scope = edgeDevice.Scope;
                            await registryManager.UpdateDeviceAsync(device, cts.Token);
                            parentId = edgeDevice.Id;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to set device's parent.");
                    continue;
                }

                try 
                {
                    // Let's Create or Update Digital Twin for device
                    var modelId = (string)deviceTwin!.Tags["modelId"];
                    if (modelId != "dtmi:com:example:ClimateDevice;1")
                    {
                        logger.LogError($"Unknown model {modelId}");
                        continue;
                    }

                    var initialDigitalTwin = new ClimateDeviceTwin
                    {
                        Temperature = new TemperatureSensorTwin
                        {
                            TargetTemperatureRange = new TargetRangeProperty
                            {
                                Min = deviceTwin.Properties.Desired["temperature"]["targetTemperatureRange"]["min"],
                                Max = deviceTwin.Properties.Desired["temperature"]["targetTemperatureRange"]["max"],
                            },
                            Metadata = new TemperatureSensorMetadata(),
                        },
                        Humidity = new HumiditySensorTwin
                        {
                            TargetHumidityRange = new TargetRangeProperty
                            {
                                Min = deviceTwin.Properties.Desired["humidity"]["targetHumidityRange"]["min"],
                                Max = deviceTwin.Properties.Desired["humidity"]["targetHumidityRange"]["max"],
                            },
                            Metadata = new HumiditySensorMetadata(),
                        },
                        TelemetryInterval = (int)deviceTwin.Properties.Desired["telemetryInterval"],
                        Metadata = new ClimateDeviceMetadata
                        {
                            ModelId = modelId,
                        }
                    };

                    var digitalTwin = await digitalTwinsClient
                        .CreateOrReplaceDigitalTwinAsync(deviceId, initialDigitalTwin, cancellationToken: cts.Token);
                    logger.LogInformation("Digital Twin created successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create Digital Twin.");
                    continue;
                }

                if (parentId != null)
                {
                    var relationship = new BasicRelationship
                    {
                        TargetId = deviceId,
                        Name = "provides",
                    };

                    try
                    {
                        string relId = $"{parentId}-{relationship.Name}->{deviceId}";
                        await digitalTwinsClient.CreateOrReplaceRelationshipAsync(parentId, relId, relationship);
                        logger.LogInformation("Relationship created successfully.");
                    }
                    catch (RequestFailedException ex)
                    {
                        logger.LogError(ex, $"Create relationship error: {ex.Status}: {ex.Message}");
                    }
                }
            }
        }

        if (cts.IsCancellationRequested) break;
    }
}
catch (OperationCanceledException) { }
