using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Functions
{
    public class PoisonQueueFunction
    {
        private readonly EnvironmentConfig _config;

        public PoisonQueueFunction(EnvironmentConfig config)
        {
            _config = config;
        }

        [FunctionName(nameof(PoisonQueueFunction))]
        public async Task<HttpResponseMessage> Requeue(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "poison/requeue/{queue}")]HttpRequestMessage request,
            string queue,
            ILogger log)
        {
            if (string.IsNullOrEmpty(queue)) return new HttpResponseMessage(HttpStatusCode.BadRequest);

            log.LogInformation($"Requeue from: {queue}");

            var storage = CloudStorageAccount.Parse(_config.EventQueueStorageConnectionString);
            var client = storage.CreateCloudQueueClient();

            var requeuedPoisonMessages = await RequeuePoisonMessages(
                client.GetQueueReference(queue),
                client.GetQueueReference($"{queue}-poison"),
                log);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(new {requeuedMessages = requeuedPoisonMessages}), System.Text.Encoding.UTF8, "application/json")
            };
        }

        private async Task<IList<string>> RequeuePoisonMessages(CloudQueue queue, CloudQueue poison, ILogger log)
        {
            var requeuedMessageIds = new List<string>();
            
            var message = await poison.GetMessageAsync();

            while (message != null)
            {
                log.LogInformation($"Requeue message with id: {message.Id}");
                await queue.AddMessageAsync(new CloudQueueMessage(message.AsString));
                await poison.DeleteMessageAsync(message);
                requeuedMessageIds.Add(message.Id);

                message = await poison.GetMessageAsync();
            }

            log.LogInformation($"Done");
            return requeuedMessageIds;
        }
    }
}