using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AfterSalesAI.IntegrationTests;

public sealed class ApiHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiHealthTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
