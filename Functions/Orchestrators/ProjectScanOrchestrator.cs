using System;
using System.Collections.Generic;
using System.Linq;
using Functions.Activities;
using Functions.Helpers;
using Functions.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using SecurePipelineScan.Rules.Security;
using SecurePipelineScan.VstsService.Response;
using Task = System.Threading.Tasks.Task;

namespace Functions.Orchestrators
{
    public class ProjectScanOrchestrator
    {
        private static readonly IEnumerable<string> _scopes = new[]
        {
            RuleScopes.GlobalPermissions,
            RuleScopes.ReleasePipelines,
            RuleScopes.BuildPipelines,
            RuleScopes.Repositories
        };

        [FunctionName(nameof(ProjectScanOrchestrator))]
        public async Task RunAsync([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var (project, scope, scanDate) = context.GetInput<(Project, string, DateTime)>();

            IList<ProductionItem> releaseProductionItems = new List<ProductionItem>();
            IList<ProductionItem> buildProductionItems = new List<ProductionItem>();
            IList<ProductionItem> repositoryProductionItems = new List<ProductionItem>();

            if (MustRunScope(scope, RuleScopes.GlobalPermissions, RuleScopes.ReleasePipelines))
            {
                releaseProductionItems = await context.CallActivityWithRetryAsync<IList<ProductionItem>>(
                    nameof(GetDeploymentMethodsActivity), RetryHelper.ActivityRetryOptions, project.Id);
            }

            if (MustRunScope(scope, RuleScopes.GlobalPermissions))
            {
                await context.CallSubOrchestratorAsync(nameof(GlobalPermissionsOrchestrator),
                    OrchestrationHelper.CreateProjectScanScopeOrchestrationId(context.InstanceId,
                        RuleScopes.GlobalPermissions), (project, releaseProductionItems, scanDate));
            }

            if (MustRunScope(scope, RuleScopes.ReleasePipelines))
            {
                buildProductionItems =
                    await context.CallSubOrchestratorAsync<IList<ProductionItem>>(
                        nameof(ReleasePipelinesOrchestrator), OrchestrationHelper.CreateProjectScanScopeOrchestrationId(
                            context.InstanceId, RuleScopes.ReleasePipelines), (project, releaseProductionItems, scanDate));
            }

            if (MustRunScope(scope, RuleScopes.BuildPipelines))
            {
                repositoryProductionItems =
                    await context.CallSubOrchestratorAsync<IList<ProductionItem>>(
                        nameof(BuildPipelinesOrchestrator), OrchestrationHelper.CreateProjectScanScopeOrchestrationId(
                            context.InstanceId, RuleScopes.BuildPipelines), (project, buildProductionItems, scanDate));
            }

            if (MustRunScope(scope, RuleScopes.Repositories))
            {
                await context.CallSubOrchestratorAsync(nameof(RepositoriesOrchestrator),
                    OrchestrationHelper.CreateProjectScanScopeOrchestrationId(context.InstanceId,
                        RuleScopes.Repositories), (project, repositoryProductionItems, scanDate));
            }
        }

        private static bool MustRunScope(string scope, params string[] scopes)
        {
            if (scope == null)
            {
                return true;
            }

            if (!_scopes.Contains(scope))
            {
                throw new InvalidOperationException($"invalid scope: {scope}");
            }

            return scopes.Contains(scope);
        }
    }
}