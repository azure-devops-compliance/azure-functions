using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Functions
{
    public class MonitorQueueLengthsFunction
    {
        private readonly CloudQueueClient _cloudQueueClient;

        public MonitorQueueLengthsFunction(CloudQueueClient cloudQueueClient)
        {
            _cloudQueueClient = cloudQueueClient;
        }

        [FunctionName("MonitorQueueLengthsFunction")]
        public async Task RunAsync([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            // List the queues for this storage account 
            QueueContinuationToken token = null;
            var cloudQueueList = new List<CloudQueue>();

            do
            {
                var segment = await _cloudQueueClient.ListQueuesSegmentedAsync(token);
                token = segment.ContinuationToken;
                cloudQueueList.AddRange(segment.Results);
            }
            while (token != null);

            foreach (var queue in cloudQueueList)
            {
                // You need to explicitly fetch attributes before you can query the queue length
                await queue.FetchAttributesAsync();
                var length = queue.ApproximateMessageCount;

                if (length != null)
                    log.LogMetric($"Queue length - {queue.Name}", (double) length);
            }
        }
    }
}
