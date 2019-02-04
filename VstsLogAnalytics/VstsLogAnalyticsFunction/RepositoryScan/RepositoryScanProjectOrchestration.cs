﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Rules.Reports;
using SecurePipelineScan.VstsService;
using Response = SecurePipelineScan.VstsService.Response;
using VstsLogAnalytics.Common;
using VstsLogAnalyticsFunction.SecurityScan.Activites;

namespace VstsLogAnalyticsFunction.RepositoryScan
{
    public static class RepositoryScanProjectOrchestration
    {
        [FunctionName(nameof(RepositoryScanProjectOrchestration))]
        public static async Task<IEnumerable<RepositoryReport>> Run(
            [OrchestrationTrigger] DurableOrchestrationContextBase context,
            ILogger log
        )

        {
            var projects = context.GetInput<Response.Multiple<Response.Project>>();

            var newList50Projects = (from project in projects
                            orderby project.Name select project).Take(50);
                                
                

            log.LogInformation($"Creating tasks for every project total amount of projects {projects.Count()}");

            var tasks = new List<Task<IEnumerable<RepositoryReport>>>();
            
            
            
            foreach (var project in newList50Projects)
            {
                tasks.Add(
                    context.CallActivityAsync<IEnumerable<RepositoryReport>>(
                        nameof(RepositoryScanProjectActivity),
                        project)
                );
            }

            await Task.WhenAll(tasks);

            return tasks.SelectMany(task => task.Result).ToList();
        }
    }
}