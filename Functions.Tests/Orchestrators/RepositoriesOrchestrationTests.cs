using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using AzDoCompliancy.CustomStatus;
using Functions.Activities;
using Functions.Model;
using Functions.Orchestrators;
using Microsoft.Azure.WebJobs;
using Moq;
using SecurePipelineScan.VstsService.Response;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Functions.Tests.Orchestrators
{
    public class RepositoriesOrchestrationTests
    {
        [Fact]
        public async Task ShouldCallActivityAsyncForProject()
        {
            //Arrange
            var fixture = new Fixture();
            var mocks = new MockRepository(MockBehavior.Strict);

            var starter = mocks.Create<DurableOrchestrationContextBase>();
            starter
                .Setup(x => x.GetInput<ItemOrchestratorRequest>())
                .Returns(fixture.Create<ItemOrchestratorRequest>());

            starter
                .Setup(x => x.InstanceId)
                .Returns(fixture.Create<string>());

            starter
                .Setup(x => x.SetCustomStatus(It.Is<ScanOrchestrationStatus>(s => 
                    s.Scope == RuleScopes.Repositories)))
                .Verifiable();

            starter
                .Setup(x => x.CallActivityWithRetryAsync<ItemExtensionData>(
                    nameof(RepositoriesScanActivity), It.IsAny<RetryOptions>(), 
                    It.IsAny<RepositoriesScanActivityRequest>()))
                .ReturnsAsync(fixture.Create<ItemExtensionData>())
                .Verifiable();
            
            starter
                .Setup(x => x.CallActivityWithRetryAsync<List<Repository>>(
                    nameof(RepositoriesForProjectActivity), It.IsAny<RetryOptions>(),
                    It.IsAny<Project>()))
                .ReturnsAsync(fixture.CreateMany<Repository>().ToList())
                .Verifiable();
            
            starter
                .Setup(x => x.CurrentUtcDateTime).Returns(new DateTime());
            
            starter
                .Setup(x => x.CallActivityAsync(nameof(ExtensionDataUploadActivity),
                    It.Is<(ItemsExtensionData data, string scope)>(t => 
                    t.scope == RuleScopes.Repositories)))
                .Returns(Task.CompletedTask);

            starter
                .Setup(x => x.CallActivityAsync(nameof(LogAnalyticsUploadActivity),
                    It.Is<LogAnalyticsUploadActivityRequest>(l =>
                    l.PreventiveLogItems.All(p => p.Scope == RuleScopes.Repositories))))
                .Returns(Task.CompletedTask);
            
            var environmentConfig = fixture.Create<EnvironmentConfig>();

            //Act
            var function = new RepositoriesOrchestration(environmentConfig);
            await function.RunAsync(starter.Object);

            //Assert           
            mocks.VerifyAll();
        }
    }
}