﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs;
using CompletenessCheckFunction.Requests;
using DurableFunctionsAdministration.Client.Response;

namespace CompletenessCheckFunction.Activities
{
    public class FilterOrchestratorsForParentIdActivity
    {
        private const int IdPartsToSubstract = 2;

        [FunctionName(nameof(FilterOrchestratorsForParentIdActivity))]
        public IList<OrchestrationInstance> Run([ActivityTrigger] FilterOrchestratorsForParentIdActivityRequest request)
        {
            return request.InstancesToFilter.Where(i => GetParentId(i.InstanceId) == request.ParentId).ToList();
        }

        private static string GetParentId(string instanceId)
        {
            var idParts = instanceId.Split(':');
            return idParts.Length > 1 ? idParts[idParts.Length - IdPartsToSubstract] : string.Empty;
        }
    }
}


