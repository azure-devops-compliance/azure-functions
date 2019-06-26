using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SecurePipelineScan.Rules.Events;
using SecurePipelineScan.Rules.Reports;
using SecurePipelineScan.VstsService;
using LogAnalytics.Client;
using Requests = SecurePipelineScan.VstsService.Requests;
using Functions.Helpers;
using Functions.Model;

namespace Functions
{
    public class BuildCompletedFunction
    {
        private readonly ILogAnalyticsClient _client;
        private readonly IServiceHookScan<BuildScanReport> _scan;
        private readonly IVstsRestClient _azuredo;
        private readonly EnvironmentConfig _config;

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
        public async Task Run(
            [QueueTrigger("buildcompleted", Connection = "connectionString")]string data, 
            ILogger log)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (log == null) throw new ArgumentNullException(nameof(log));
            
            var report = await _scan.Completed(JObject.Parse(data));
            if (report != null)
            {
                await _client.AddCustomLogJsonAsync(nameof(BuildCompletedFunction), report, "Date");
                await RetryHelper.ExecuteInvalidDocumentVersionPolicy(_config.Organization,() => UpdateExtensionData(report));
            }
        }

        private async Task UpdateExtensionData(BuildScanReport report)
        {
            var reports = await _azuredo.GetAsync(
                              Requests.ExtensionManagement.ExtensionData<ExtensionDataReports<BuildScanReport>>(
                                  "tas",
                                  _config.ExtensionName,
                                  "BuildReports",
                                  report.Project)) ??
                          new ExtensionDataReports<BuildScanReport>
                              {Id = report.Project, Reports = new List<BuildScanReport>()};

            reports.Reports = reports
                .Reports
                .Concat(new[]{report})
                .OrderByDescending(x => x.CreatedDate)
                .Take(50)
                .ToList();

            await _azuredo.PutAsync(
                Requests.ExtensionManagement.ExtensionData<ExtensionDataReports<BuildScanReport>>(
                    "tas", _config.ExtensionName,
                    "BuildReports", report.Project), reports);
        }
    }
}