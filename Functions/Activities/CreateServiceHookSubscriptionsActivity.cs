﻿using System.Collections.Generic;
using System.Linq;
using Functions.Helpers;
using Microsoft.Azure.WebJobs;
using SecurePipelineScan.VstsService;
using SecurePipelineScan.VstsService.Requests;
using SecurePipelineScan.VstsService.Response;
using Task = System.Threading.Tasks.Task;

namespace Functions.Activities
{
    public class CreateServiceHookSubscriptionsActivity
    {
        private readonly IVstsRestClient _vstsRestClient;
        private readonly EnvironmentConfig _config;

        public CreateServiceHookSubscriptionsActivity(EnvironmentConfig config, IVstsRestClient vstsRestClient)
        {
            _vstsRestClient = vstsRestClient;
            _config = config;
        }

        [FunctionName(nameof(CreateServiceHookSubscriptionsActivity))]
        public async Task Run([ActivityTrigger] CreateServiceHookSubscriptionsActivityRequest request)
        {
                await AddHookIfNotSubscribed(
                    Hooks.AddHookSubscription(),
                    Hooks.Add.BuildCompleted(_config.EventQueueStorageAccountName, _config.EventQueueStorageAccountKey,
                        StorageQueueNames.BuildCompletedQueueName, request.Project.Id), request.ExistingHooks);
                
                await AddHookIfNotSubscribed(
                    Hooks.AddHookSubscription(),
                    Hooks.Add.ReleaseDeploymentCompleted(_config.EventQueueStorageAccountName,
                        _config.EventQueueStorageAccountKey, StorageQueueNames.ReleaseDeploymentCompletedQueueName,
                        request.Project.Id), request.ExistingHooks);
        }

        private async Task AddHookIfNotSubscribed(IVstsRequest<Hooks.Add.Body, Hook> request, Hooks.Add.Body hook, IEnumerable<Hook> hooks)
        {
            if (!hooks.Any(h => h.EventType == hook.EventType &&
                                h.ConsumerInputs.AccountName == hook.ConsumerInputs.AccountName &&
                                h.PublisherInputs.ProjectId == hook.PublisherInputs.ProjectId))
            {
                await _vstsRestClient.PostAsync(request, hook);
            }
        }
    }
}