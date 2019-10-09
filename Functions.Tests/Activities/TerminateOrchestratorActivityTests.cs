﻿using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using NSubstitute;
using Xunit;
using Functions.Activities;

namespace Functions.Tests.Activities
{
    public class TerminateOrchestratorActivityTests
    {
        [Fact]
        public async Task ShouldSendTerminateCall()
        {
            //Arrange
            var client = Substitute.For<DurableOrchestrationClientBase>();

            //Act
            var func = new TerminateOrchestratorActivity();
            await func.RunAsync("instanceId", client);

            //Assert
            await client.Received().TerminateAsync("instanceId", Arg.Any<string>());
        }
    }
}