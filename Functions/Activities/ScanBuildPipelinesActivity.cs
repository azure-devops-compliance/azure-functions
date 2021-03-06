﻿using Functions.Model;
using Microsoft.Azure.WebJobs;
using Response = SecurePipelineScan.VstsService.Response;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using AzureDevOps.Compliance.Rules;

namespace Functions.Activities
{
    public class ScanBuildPipelinesActivity
    {
        private readonly IEnumerable<IBuildPipelineRule> _rules;
        private readonly EnvironmentConfig _config;

        public ScanBuildPipelinesActivity(EnvironmentConfig config,
            IEnumerable<IBuildPipelineRule> rules)
        {
            _config = config;
            _rules = rules;
        }

        [FunctionName(nameof(ScanBuildPipelinesActivity))]
        public async Task<ItemExtensionData> RunAsync(
            [ActivityTrigger] (Response.Project, Response.BuildDefinition) input)
        {
            if (input.Item1 == null || input.Item2 == null)
                throw new ArgumentNullException(nameof(input));

            var project = input.Item1;
            var buildPipeline = input.Item2;

            return new ItemExtensionData
            {
                Item = buildPipeline.Name,
                ItemId = buildPipeline.Id,
                Rules = await Task.WhenAll(_rules.Select(async rule =>
                    {
                        var ruleName = rule.GetType().Name;
                        return new EvaluatedRule
                        {
                            Name = ruleName,
                            Description = rule.Description,
                            Link = rule.Link,
                            Status = await rule.EvaluateAsync(project, buildPipeline)
                                .ConfigureAwait(false),
                            Reconcile = ReconcileFunction.ReconcileFromRule(rule as IReconcile, _config,
                                project.Id, RuleScopes.BuildPipelines, buildPipeline.Id)
                        };
                    })
                    .ToList())
                    .ConfigureAwait(false)
            };
        }
    }
}