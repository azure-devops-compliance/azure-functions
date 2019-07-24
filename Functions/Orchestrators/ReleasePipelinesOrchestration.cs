using AzDoCompliancy.CustomStatus;
using Functions.Activities;
using Functions.Model;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;
using Response = SecurePipelineScan.VstsService.Response;

namespace Functions.Orchestrators
{
    public static class ReleasePipelinesOrchestration
    {
        [FunctionName(nameof(ReleasePipelinesOrchestration))]
        public static async Task Run([OrchestrationTrigger]DurableOrchestrationContextBase context)
        {
            var project = context.GetInput<Response.Project>();
            context.SetCustomStatus(new ScanOrchestrationStatus { Project = project.Name, Scope = RuleScopes.ReleasePipelines });

            var data = await context.CallActivityAsync<ItemsExtensionData>(
                nameof(ReleasePipelinesScanActivity), project);

            await context.CallActivityAsync(nameof(LogAnalyticsUploadActivity),
                new LogAnalyticsUploadActivityRequest { PreventiveLogItems = data.Flatten(RuleScopes.ReleasePipelines) });

            await context.CallActivityAsync(nameof(ExtensionDataUploadActivity),
                (releasePipelines: data, RuleScopes.ReleasePipelines));
        }
    }
}