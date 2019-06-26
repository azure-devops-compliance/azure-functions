﻿using System.Threading.Tasks;
using Functions.Model;
using Microsoft.Azure.WebJobs;
using SecurePipelineScan.VstsService;
using SecurePipelineScan.VstsService.Requests;

namespace Functions.Activities
{
    public class ExtensionDataUploadActivity
    {
        private readonly IVstsRestClient _azuredo;
        private readonly EnvironmentConfig _config;

        public ExtensionDataUploadActivity(IVstsRestClient azuredo,
            EnvironmentConfig config)
        {
            _azuredo = azuredo;
            _config = config;
        }

        [FunctionName(nameof(ExtensionDataUploadActivity))]
        public async Task Run([ActivityTrigger] DurableActivityContext inputs)
        {
            var (data, scope) = inputs.GetInput<(ItemsExtensionData, string)>();
            await _azuredo.PutAsync(ExtensionManagement.ExtensionData<ExtensionDataReports>("tas", _config.ExtensionName, scope), data);
        }
    }
}
