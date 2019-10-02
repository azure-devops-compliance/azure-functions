using AutoFixture;
using AutoFixture.AutoMoq;
using Functions.Activities;
using Functions.Model;
using Moq;
using SecurePipelineScan.Rules.Security;
using SecurePipelineScan.VstsService;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Functions.Tests.Activities
{
    public class GlobalPermissionsScanActivityTests
    {
        [Fact]
        public async Task RunShouldCallIProjectRuleEvaluate()
        {
            //Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization());

            var rule = new Mock<IProjectRule>();
            rule
                .Setup(x => x.EvaluateAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(true));

            var ruleSets = new Mock<IRulesProvider>();
            ruleSets
                .Setup(x => x.GlobalPermissions(It.IsAny<IVstsRestClient>()))
                .Returns(new[] { rule.Object, rule.Object });

            //Act
            var fun = new GlobalPermissionsScanActivity(
                fixture.Create<IVstsRestClient>(),
                fixture.Create<EnvironmentConfig>(),
                ruleSets.Object);

            await fun.RunAsync(
                fixture.Create<ItemOrchestratorRequest>());

            //Assert
            rule.Verify(x => x.EvaluateAsync(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task
            GivenGlobalPermissionsAreScanned_WhenReportsArePutInExtensionDataStorage_ThenItShouldHaveReconcileUrls()
        {
            //Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var config = fixture.Create<EnvironmentConfig>();
            var dummyreq = fixture.Create<ItemOrchestratorRequest>();
            var clientMock = new Mock<IVstsRestClient>();

            var rule = new Mock<IProjectRule>();
            rule
                .As<IProjectReconcile>()
                .Setup(x => x.Impact)
                .Returns(new[] { "just some action" });

            var rulesProvider = new Mock<IRulesProvider>();
            rulesProvider
                .Setup(x => x.GlobalPermissions(It.IsAny<IVstsRestClient>()))
                .Returns(new[] { rule.Object });

            //Act
            var fun = new GlobalPermissionsScanActivity(
                clientMock.Object,
                config,
                rulesProvider.Object);
            var result = await fun.RunAsync(
                dummyreq);

            var ruleName = rule.Object.GetType().Name;

            // Assert
            result.Rules.ShouldContain(r => r.Reconcile != null);
            result.Rules.ShouldContain(r => r.Reconcile.Impact.Any());
            result.Rules.ShouldContain(r => r.Reconcile.Url == new Uri($"https://{config.FunctionAppHostname}" +
                $"/api/reconcile/{config.Organization}/{dummyreq.Project.Id}/globalpermissions/{ruleName}"));
        }
    }
}