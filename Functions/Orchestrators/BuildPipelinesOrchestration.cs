using System.Collections.Generic;
using AzDoCompliancy.CustomStatus;
using Functions.Activities;
using Functions.Helpers;
using Functions.Model;
using Functions.Starters;
using Microsoft.Azure.WebJobs;
using SecurePipelineScan.VstsService.Response;
using Task = System.Threading.Tasks.Task;

namespace Functions.Orchestrators
{
    public class BuildPipelinesOrchestration
    {
        private readonly EnvironmentConfig _config;

        public BuildPipelinesOrchestration(EnvironmentConfig config)
        {
            _config = config;
        }

        [FunctionName(nameof(BuildPipelinesOrchestration))]
        public async Task Run([OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var project = context.GetInput<Project>();

            context.SetCustomStatus(new ScanOrchestrationStatus
                {Project = project.Name, Scope = RuleScopes.BuildPipelines});

            var buildDefinitions =
                await context.CallActivityWithRetryAsync<List<BuildDefinition>>(nameof(BuildDefinitionsActivity),
                    RetryHelper.ActivityRetryOptions,
                    project);
            
            //Not using select so we can run the activities each at a time
            var reports = new List<ItemExtensionData>();
            foreach (var buildDefinition in buildDefinitions)
            {
                var itemExtensionData = await context.CallActivityWithRetryAsync<ItemExtensionData>(nameof(BuildPipelinesScanActivity),
                    RetryHelper.ActivityRetryOptions, buildDefinition);
                reports.Add(itemExtensionData);
            }

            var data = new ItemsExtensionData
            {
                Id = project.Name,
                Date = context.CurrentUtcDateTime,
                RescanUrl = ProjectScanHttpStarter.RescanUrl(_config, project.Name, RuleScopes.BuildPipelines),
                HasReconcilePermissionUrl = ReconcileFunction.HasReconcilePermissionUrl(_config, project.Id),
                Reports = reports
            };

            await context.CallActivityAsync(nameof(LogAnalyticsUploadActivity),
                new LogAnalyticsUploadActivityRequest
                    {PreventiveLogItems = data.Flatten(RuleScopes.BuildPipelines, context.InstanceId)});

            await context.CallActivityAsync(nameof(ExtensionDataUploadActivity),
                (buildPipelines: data, RuleScopes.BuildPipelines));
        }
    }
}