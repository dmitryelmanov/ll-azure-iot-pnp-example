using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Shared;               // For TwinCollection
using Microsoft.Azure.Devices.Provisioning.Service; // For TwinState
using System;
using System.Collections.Generic;

namespace ProvisioningFunction
{
    public static class ProvisionDevice
    {
        [FunctionName("ProvisionDevice")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            log.LogInformation("Request.Body:...");
            log.LogInformation(requestBody);

            // Get registration ID of the device
            string regId = data?.deviceRuntimeContext?.registrationId;
            log.LogInformation("RegistrationId: {0}", regId);

            ResponseObj response = null;

            if (regId == null)
            {
                log.LogInformation("Registration ID : NULL");
                return new BadRequestObjectResult("Registration ID not provided for the device.");
            }
            else
            {
                string[] hubs = data?.linkedHubs?.ToObject<string[]>();

                // Must have hubs selected on the enrollment
                if (hubs == null)
                {
                    log.LogInformation("linkedHubs : NULL");
                    return new BadRequestObjectResult("No hub group defined for the enrollment.");
                }
                else
                {
                    response = new ResponseObj();
                    response.iotHubHostName = hubs[0];
                    log.LogInformation("Hub: {0}", response.iotHubHostName);

                    // Specify the initial tags for the device.
                    TwinCollection tags = new TwinCollection();

                    // Specify the initial desired properties for the device.
                    TwinCollection properties = new TwinCollection();

                    // Find or create twin based on the provided registration ID and model ID
                    dynamic payload = data?.deviceRuntimeContext?.payload;
                    if (payload != null)
                    {
                        log.LogInformation("{0}", (object)payload.GetType());
                        var modelId = (string)payload.modelId;
                        if (!string.IsNullOrEmpty(modelId))
                        {
                            log.LogInformation("ModelId: {0}", modelId);
                            tags["modelId"] = modelId;

                            // Here we can initialise Device Twin based on the Model.
                            // It might seem a good idea to parse a Model and create Device Twin dynamiclly,
                            // but you need to figure out what initial values for each property.
                            // So, it seems that the better solution would be to get Device Twin from some DB,
                            // using ModelId or combinations of ModelId and DeviceId as a key.
                            // For now we will get this from configuration.
                            var twinJson = Environment.GetEnvironmentVariable(modelId, EnvironmentVariableTarget.Process);
                            if (twinJson != null)
                            {
                                var twin = JsonConvert.DeserializeObject<Twin>(twinJson);
                                foreach (KeyValuePair<string, dynamic> prop in twin.Properties.Desired)
                                {
                                    properties[prop.Key] = prop.Value;
                                }
                            }
                        }

                        var edgeId = (string)payload.edgeId;
                        if (!string.IsNullOrEmpty(edgeId))
                        {
                            log.LogInformation("EdgeId: {0}", edgeId);
                            tags["edgeId"] = edgeId;
                        }
                    }

                    // Add the initial twin state to the response.
                    TwinState twinState = new TwinState(tags, properties);
                    response.initialTwin = twinState;
                }
            }

            log.LogInformation("\nResponse");
            log.LogInformation(JsonConvert.SerializeObject(response));

            return new OkObjectResult(response);
        }

        public class ResponseObj
        {
            public string iotHubHostName { get; set; }
            public TwinState initialTwin { get; set; }
        }
    }
}
