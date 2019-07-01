using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Functions.Orchestrators;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using SecurePipelineScan.VstsService;
using SecurePipelineScan.VstsService.Requests;

namespace Functions.Starters
{
    public class ProjectScanHttpStarter
    {
        private readonly ITokenizer _tokenizer;
        private readonly IVstsRestClient _azuredo;

        private static readonly IDictionary<string, string> Scopes = new Dictionary<string, string>
        {
            ["globalpermissions"] = nameof(GlobalPermissionsOrchestration),
            ["repository"] = nameof(RepositoriesOrchestration),
            ["buildpipelines"] = nameof(BuildPipelinesOrchestration),
            ["releasepipelines"] = nameof(ReleasePipelinesOrchestration)
        };

        public ProjectScanHttpStarter(ITokenizer tokenizer, IVstsRestClient azuredo)
        {
            _tokenizer = tokenizer;
            _azuredo = azuredo;
        }

        [FunctionName(nameof(ProjectScanHttpStarter))]
        public async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "scan/{organization}/{project}/{scope}")]
            HttpRequestMessage request,
            string organization,
            string project,
            string scope,
            [OrchestrationClient] DurableOrchestrationClientBase starter)
        {
            if (_tokenizer.IdentifierFromClaim(request) == null)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var projectObject = await _azuredo.GetAsync(Project.ProjectByName(project));

            if(projectObject == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var instanceId = await starter.StartNewAsync(Orchestration(scope), projectObject);
            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, TimeSpan.FromSeconds(180));
        }

        private static string Orchestration(string scope) => 
            Scopes.TryGetValue(scope, out var value) ? value : throw new ArgumentException(nameof(scope));

        public static string RescanUrl(EnvironmentConfig environmentConfig, string project, string scope) => 
            $"https://{environmentConfig.FunctionAppHostname}/api/scan/{environmentConfig.Organization}/{project}/{scope}";
    }
}
