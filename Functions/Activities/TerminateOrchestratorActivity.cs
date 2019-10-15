﻿using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Functions.Activities
{
    public class TerminateOrchestratorActivity
    {
        [FunctionName(nameof(TerminateOrchestratorActivity))]
        public async Task RunAsync([ActivityTrigger] string instanceId,
            [OrchestrationClient] DurableOrchestrationClientBase client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            await client.TerminateAsync(instanceId, "Orchestrator is already running multiple days")
                .ConfigureAwait(false);
        }
    }
}