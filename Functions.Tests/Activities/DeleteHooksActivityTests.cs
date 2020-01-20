using AutoFixture;
using Functions.Activities;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Moq;
using SecurePipelineScan.VstsService;
using SecurePipelineScan.VstsService.Response;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Functions.Tests.Activities
{
    public class DeleteHooksActivityTests
    {
        private readonly Fixture _fixture;

        public DeleteHooksActivityTests() => _fixture = new Fixture();

        [Fact]
        public async Task ShouldDeleteServiceHookSubscription()
        {
            // Arrange
            var client = new Mock<IVstsRestClient>();
            var context = new Mock<IDurableActivityContext>();
            var hook = _fixture.Create<Hook>();
            context.Setup(c => c.GetInput<Hook>()).Returns(hook);
            
            // Act
            var fun = new DeleteHooksActivity(client.Object);
            await fun.RunAsync(context.Object);
            
            // Assert
            client.Verify(c => c.DeleteAsync(It.IsAny<IVstsRequest<Hook>>()), Times.Once);
        }
    }
}