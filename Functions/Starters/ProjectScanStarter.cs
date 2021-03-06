using Functions.Orchestrators;
using Microsoft.Azure.WebJobs;
using SecurePipelineScan.VstsService;
using SecurePipelineScan.VstsService.Requests;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Functions.Starters
{
    public class ProjectScanStarter
    {
        private readonly IVstsRestClient _azuredo;

        public ProjectScanStarter(IVstsRestClient azuredo) => _azuredo = azuredo;

        [FunctionName(nameof(ProjectScanStarter))]
        public async Task RunAsync(
            [TimerTrigger("0 0 20 * * *", RunOnStartup=false)] TimerInfo timerInfo,
            [DurableClient] IDurableOrchestrationClient orchestrationClientBase)
        {
            if (orchestrationClientBase == null)
                throw new ArgumentNullException(nameof(orchestrationClientBase));

            var projects = _azuredo.Get(Project.Projects()).ToList();
            await orchestrationClientBase.StartNewAsync(nameof(ProjectScanSupervisor), projects);
        }
    }
}