using Functions.Helpers;
using Functions.Model;
using LogAnalytics.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SecurePipelineScan.Rules.Events;
using SecurePipelineScan.Rules.Reports;
using SecurePipelineScan.VstsService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Requests = SecurePipelineScan.VstsService.Requests;

namespace Functions
{
    public class BuildCompletedFunction
    {
        private readonly ILogAnalyticsClient _client;
        private readonly IServiceHookScan<BuildScanReport> _scan;
        private readonly IVstsRestClient _azuredo;
        private readonly EnvironmentConfig _config;

        private const int ReportsToDisplay = 50;

        public BuildCompletedFunction(
            ILogAnalyticsClient client,
            IServiceHookScan<BuildScanReport> scan,
            IVstsRestClient azuredo,
            EnvironmentConfig config)
        {
            _client = client;
            _scan = scan;
            _azuredo = azuredo;
            _config = config;
        }

        [FunctionName(nameof(BuildCompletedFunction))]
        public Task RunAsync(
            [QueueTrigger("buildcompleted", Connection = "eventQueueStorageConnectionString")]string data,
            ILogger log)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            return RunInternalAsync(data);
        }

        private async Task RunInternalAsync(string data)
        {
            var report = await _scan.GetCompletedReportAsync(JObject.Parse(data));
            if (report != null)
            {
                await _client.AddCustomLogJsonAsync(nameof(BuildCompletedFunction), report, "Date");
                await RetryHelper.ExecuteInvalidDocumentVersionPolicyAsync(_config.Organization, () => UpdateExtensionDataAsync(report));
            }
        }

        private async Task UpdateExtensionDataAsync(BuildScanReport report)
        {
            var reports = 
                await _azuredo.GetAsync(Requests.ExtensionManagement.ExtensionData<ExtensionDataReports<BuildScanReport>>(
                    "tas", _config.ExtensionName, "BuildReports", report.Project)) ??
                new ExtensionDataReports<BuildScanReport>
                {
                    Id = report.Project,
                    Reports = new List<BuildScanReport>()
                };

            reports.Reports = reports
                .Reports
                .Concat(new[] { report })
                .OrderByDescending(x => x.CreatedDate)
                .Take(ReportsToDisplay)
                .ToList();

            await _azuredo.PutAsync(Requests.ExtensionManagement.ExtensionData<ExtensionDataReports<BuildScanReport>>(
                "tas", _config.ExtensionName, "BuildReports", report.Project), reports);
        }
    }
}