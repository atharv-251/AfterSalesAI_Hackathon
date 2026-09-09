using AfterSalesAI.Application;
using Xunit;

namespace AfterSalesAI.UnitTests;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task HandleAsync_ReturnsScaffoldResponse()
    {
        var service = new AssistantService();
        var request = new AssistantRequest(Guid.NewGuid(), null, "How do I raise a claim?");

        var response = await service.HandleAsync(request);

        Assert.Equal("RAG", response.Decision);
        Assert.NotNull(response.Answer);
    }
}
