using AutoFixture;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SecurePipelineScan.Rules.Security;
using SecurePipelineScan.VstsService;
using Shouldly;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Xunit;
using Response = SecurePipelineScan.VstsService.Response;

namespace Functions.Tests
{
    public class ReconcileTests
    {
        [Fact]
        public async Task ExistingRuleExecutedWhenReconcile()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var rule = new Mock<IProjectRule>(MockBehavior.Strict);
            rule
                .As<IProjectReconcile>()
                .Setup(x => x.ReconcileAsync("TAS"))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var ruleProvider = new Mock<IRulesProvider>();
            ruleProvider
                .Setup(x => x.GlobalPermissions(It.IsAny<IVstsRestClient>()))
                .Returns(new[] { rule.Object });

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.IsAny<IVstsRequest<Response.PermissionsProjectId>>()))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()));
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();
            var config = new EnvironmentConfig {Organization = "raboweb" };

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object, ruleProvider.Object, tokenizer.Object);
            (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                RuleScopes.GlobalPermissions,
                rule.Object.GetType().Name)).ShouldBeOfType<OkResult>();

            rule.Verify();
        }

        [Fact]
        public async Task ExistingRepositoryRuleExecutedWhenReconcile()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var rule = new Mock<IRepositoryRule>(MockBehavior.Strict);
            rule
                .As<IReconcile>()
                .Setup(x => x.ReconcileAsync("TAS", "repository-id", null))
                .Returns(Task.CompletedTask)
                .Verifiable();
            rule
                .As<IReconcile>()
                .Setup(x => x.RequiresStageId)
                .Returns(false)
                .Verifiable();

            var ruleProvider = new Mock<IRulesProvider>();
            ruleProvider
                .Setup(x => x.RepositoryRules(It.IsAny<IVstsRestClient>()))
                .Returns(new[] {rule.Object});

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.IsAny<IVstsRequest<Response.PermissionsProjectId>>()))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()));

            var config = new EnvironmentConfig {Organization = "raboweb"};
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object, ruleProvider.Object,
                tokenizer.Object);
            (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                RuleScopes.Repositories,
                rule.Object.GetType().Name,
                "repository-id")).ShouldBeOfType<OkResult>();

            rule.Verify();
        }

        [Fact]
        public async Task RuleNotFound()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var ruleProvider = new Mock<IRulesProvider>();
            ruleProvider
                .Setup(x => x.GlobalPermissions(It.IsAny<IVstsRestClient>()))
                .Returns(Enumerable.Empty<IProjectRule>());

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.IsAny<IVstsRequest<Response.PermissionsProjectId>>()))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object, ruleProvider.Object,
                tokenizer.Object);
            var result = (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                RuleScopes.GlobalPermissions,
                "some-non-existing-rule")).ShouldBeOfType<NotFoundObjectResult>();

            result
                .Value
                .ToString()
                .ShouldContain("Rule not found");
        }

        [Fact]
        public async Task ScopeNotFound()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);


            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.IsAny<IVstsRequest<Response.PermissionsProjectId>>()))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            var result = (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                "non-existing-scope",
                "some-non-existing-rule")).ShouldBeOfType<NotFoundObjectResult>();

            result.Value.ShouldBe("non-existing-scope");
        }

        [Fact]
        public async Task CanCheckPermissionsForUserWithUnknownVsIInTokenAndValidUserId()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ab84d5a2-4b8d-68df-9ad3-cc9c8884270c"))))
                .Returns(Task.FromResult<Response.PermissionsProjectId>(null))
                .Verifiable();

            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ef2e3683-8fb5-439d-9dc9-53af732e6387"))))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");
            request.RequestUri = new System.Uri("https://dev.azure.com/reconcile/raboweb/TAS/haspermissions?userId=ef2e3683-8fb5-439d-9dc9-53af732e6387");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<OkObjectResult>()
                .Value
                .ShouldBe(true);
            vstsClient.Verify();
        }

        [Fact]
        public async Task CanCheckPermissionsForUserWithUnknownVsIInTokenAndInvalidUserId()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ab84d5a2-4b8d-68df-9ad3-cc9c8884270c"))))
                .Returns(Task.FromResult<Response.PermissionsProjectId>(null))
                .Verifiable();

            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ef2e3683-8fb5-439d-9dc9-53af732e6387"))))
                .Returns(Task.FromResult<Response.PermissionsProjectId>(null))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");
            request.RequestUri =
                new System.Uri(
                    "https://dev.azure.com/reconcile/raboweb/TAS/haspermissions?userId=ef2e3683-8fb5-439d-9dc9-53af732e6387");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<OkObjectResult>()
                .Value
                .ShouldBe(false);
            vstsClient.Verify();
        }

        [Fact]
        public async Task UnauthorizedWithoutHeaderWhenReconcile()
        {
            var ruleProvider = new Mock<IRulesProvider>();
            var request = new HttpRequestMessage();
            request.Headers.Authorization = null;

            var config = new EnvironmentConfig {Organization = "raboweb"};
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var function = new ReconcileFunction(config, tableClient, null, ruleProvider.Object,
                new Mock<ITokenizer>().Object);
            (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                RuleScopes.GlobalPermissions,
                "some-non-existing-rule")).ShouldBeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task UnauthorizedWithoutNameClaimWhenReconcile()
        {
            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(new ClaimsPrincipal());

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var function = new ReconcileFunction(config, tableClient, null, new Mock<IRulesProvider>().Object,
                tokenizer.Object);
            (await function.ReconcileAsync(request,
                "raboweb",
                "TAS",
                RuleScopes.GlobalPermissions,
                "some-non-existing-rule")).ShouldBeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task UnauthorizedWithoutPermissionWhenReconcile()
        {
            var fixture = new Fixture();
            fixture.Customize<Response.Permission>(ctx =>
                ctx.With(x => x.DisplayName, "Manage project properties"));

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ab84d5a2-4b8d-68df-9ad3-cc9c8884270c"))))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            (await function.ReconcileAsync(request,
                    "raboweb",
                    "TAS",
                    RuleScopes.GlobalPermissions,
                    "some-non-existing-rule"))
                .ShouldBeOfType<UnauthorizedResult>();

            vstsClient.Verify();
        }

        [Fact]
        public async Task UnauthorizedWithoutHeaderWhenHasPermission()
        {
            var request = new HttpRequestMessage();
            request.Headers.Authorization = null;

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var function = new ReconcileFunction(config, tableClient, null, new Mock<IRulesProvider>().Object,
                new Mock<ITokenizer>().Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task UnauthorizedWithoutNameClaimWhenHasPermission()
        {
            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(new ClaimsPrincipal());

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var function = new ReconcileFunction(config, tableClient, null, new Mock<IRulesProvider>().Object,
                tokenizer.Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task WithoutPermission()
        {
            var fixture = new Fixture();
            fixture.Customize<Response.Permission>(ctx =>
                ctx.With(x => x.DisplayName, "Manage project properties"));

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ab84d5a2-4b8d-68df-9ad3-cc9c8884270c"))))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<OkObjectResult>()
                .Value
                .ShouldBe(false);
            vstsClient.Verify();
        }

        [Fact]
        public async Task WithPermission()
        {
            var fixture = new Fixture();
            ManageProjectPropertiesPermission(fixture);

            var tokenizer = new Mock<ITokenizer>();
            tokenizer
                .Setup(x => x.Principal(It.IsAny<string>()))
                .Returns(PrincipalWithClaims());

            var vstsClient = new Mock<IVstsRestClient>();
            vstsClient
                .Setup(x => x.GetAsync(It.Is<IVstsRequest<Response.PermissionsProjectId>>(req =>
                    req.QueryParams.Values.Contains("ab84d5a2-4b8d-68df-9ad3-cc9c8884270c"))))
                .Returns(Task.FromResult(fixture.Create<Response.PermissionsProjectId>()))
                .Verifiable();

            var config = new EnvironmentConfig { Organization = "raboweb" };
            var tableClient = CloudStorageAccount.Parse("UseDevelopmentStorage=true").CreateCloudTableClient();

            var request = new HttpRequestMessage();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "");

            var function = new ReconcileFunction(config, tableClient, vstsClient.Object,
                new Mock<IRulesProvider>().Object, tokenizer.Object);
            (await function
                    .HasPermissionAsync(request, "raboweb", "TAS"))
                .ShouldBeOfType<OkObjectResult>()
                .Value
                .ShouldBe(true);
            vstsClient.Verify();
        }

        private static void ManageProjectPropertiesPermission(IFixture fixture)
        {
            fixture.Customize<Response.Permission>(ctx => ctx
                .With(x => x.DisplayName, "Manage project properties")
                .With(x => x.PermissionId, 3));
        }

        private static ClaimsPrincipal PrincipalWithClaims() =>
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "ab84d5a2-4b8d-68df-9ad3-cc9c8884270c")
            }));
    }
}