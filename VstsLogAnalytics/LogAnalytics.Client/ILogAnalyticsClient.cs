﻿using System.Threading.Tasks;

namespace VstsLogAnalytics.Client
{
    public interface ILogAnalyticsClient
    {
        Task AddCustomLogJsonAsync(string logName, object input, string timefield);
    }
}