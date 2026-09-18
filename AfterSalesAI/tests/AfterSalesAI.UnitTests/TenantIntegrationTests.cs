using System.Net;
using System.Net.Http.Json;
using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AfterSalesAI.UnitTests;

public sealed class TenantIntegrationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("bad/key")]
    [InlineData("D001 ")]
    [InlineData("d001")]
    public void DealerKey_RejectsInvalidInput(string key) => Assert.Throws<ArgumentException>(() => DemoTenants.ValidateDealerId(key));

    [Fact]
    public async Task Wrapper_RejectsTenant2BeforeAnyQuery()
    {
        var local = new LocalQuery();
        var remote = new RemoteClient();
        await Assert.ThrowsAsync<TenantAccessException>(() => new Tenant1WrapperService(local, remote)
            .GetAsync(DemoTenants.Tenant2, "D001", DealerOperations.Overview));
        Assert.Equal(0, local.Calls);
        Assert.Equal(0, remote.Calls);
    }

    [Fact]
    public async Task Wrapper_MissingLocalDealerDoesNotCallTenant2()
    {
        var remote = new RemoteClient();
        var result = await new Tenant1WrapperService(new LocalQuery { Missing = true }, remote)
            .GetAsync(DemoTenants.Tenant1, "D001", DealerOperations.Overview);
        Assert.Null(result);
        Assert.Equal(0, remote.Calls);
    }

    [Theory]
    [InlineData("NotFound")]
    [InlineData("Unavailable")]
    [InlineData("Timeout")]
    [InlineData("InvalidResponse")]
    public async Task Wrapper_PreservesLocalDataOnRemoteFailure(string status)
    {
        var remote = new RemoteClient { Result = new Tenant2FetchResult(status, null) };
        var result = await new Tenant1WrapperService(new LocalQuery(), remote)
            .GetAsync(DemoTenants.Tenant1, "D001", DealerOperations.Overview);
        Assert.NotNull(result);
        Assert.Equal("D001", result.Tenant1Data.DealerId);
        Assert.Equal(status, result.IntegrationStatus);
        Assert.Null(result.Tenant2Data);
    }

    [Fact]
    public async Task Wrapper_CombinesApprovedResponseByDealerKey()
    {
        var remote = new RemoteClient { Result = new Tenant2FetchResult("Available", Response()) };
        var result = await new Tenant1WrapperService(new LocalQuery(), remote)
            .GetAsync(DemoTenants.Tenant1, "D001", DealerOperations.Overview);
        Assert.Equal("D001", remote.Key);
        Assert.Equal(DemoTenants.Tenant1, result!.TenantId);
        Assert.Equal(DemoTenants.Tenant2, result.Tenant2Data!.TenantId);
        Assert.Contains("not transaction-matched", result.CombinedSummary);
    }

    [Theory]
    [InlineData(404, "NotFound")]
    [InlineData(500, "Unavailable")]
    [InlineData(302, "Unavailable")]
    public async Task HttpClient_MapsStatusWithoutExposingBody(int code, string expected)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)
        { Content = new StringContent("private upstream details") }))) { BaseAddress = new Uri("http://localhost/") };
        var result = await new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance)
            .GetAsync("D001", DealerOperations.Overview);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task HttpClient_UsesFixedRouteAndTenant2Context()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal($"http://localhost/api/tenant2/dealers/D001/service-overview?tenantId={DemoTenants.Tenant2:D}", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Response()) });
        })) { BaseAddress = new Uri("http://localhost/") };
        var result = await new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance).GetAsync("D001", DealerOperations.Overview);
        Assert.Equal("Available", result.Status);
    }

    [Fact]
    public async Task Tenant1WrapperClient_UsesFixedRouteAndTenant1Context()
    {
        var wrapper = new DealerWrapperResponse(DemoTenants.Tenant1, "D001", "Available",
            new Tenant1DealerData("D001", "Demo dealer", "Active", [], [], []),
            Response() with { Operation = DealerOperations.Status }, "Correlated by DealerId only.");
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal($"http://localhost/api/tenant1/wrapper/dealers/D001/repair-readiness?tenantId={DemoTenants.Tenant1:D}", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(wrapper) });
        })) { BaseAddress = new Uri("http://localhost/") };
        var result = await new Tenant1WrapperApiClient(http, NullLogger<Tenant1WrapperApiClient>.Instance)
            .GetAsync("D001", DealerOperations.Status);
        Assert.Equal("Available", result.Status);
        Assert.Equal("D001", result.Data!.Tenant1Data.DealerId);
    }

    [Fact]
    public void ServiceIntegrationAuthorizer_AcceptsOnlyConfiguredKey()
    {
        var authorizer = new ServiceIntegrationAuthorizer(Options.Create(new ServiceIntegrationOptions { ApiKey = "test-key" }));
        Assert.True(authorizer.IsAuthorized("test-key"));
        Assert.False(authorizer.IsAuthorized("other-key"));
        Assert.False(authorizer.IsAuthorized(null));
    }

    [Fact]
    public async Task HttpClient_RejectsMismatchedDealer()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(Response() with { DealerId = "D002" }) }))) { BaseAddress = new Uri("http://localhost/") };
        var result = await new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance).GetAsync("D001", DealerOperations.Overview);
        Assert.Equal("InvalidResponse", result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task HttpClient_RejectsMalformedResponse()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json") }))) { BaseAddress = new Uri("http://localhost/") };
        Assert.Equal("InvalidResponse", (await new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance)
            .GetAsync("D001", DealerOperations.Overview)).Status);
    }

    [Fact]
    public async Task HttpClient_MapsTimeout()
    {
        using var http = new HttpClient(new Handler(_ => throw new TaskCanceledException())) { BaseAddress = new Uri("http://localhost/") };
        Assert.Equal("Timeout", (await new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance)
            .GetAsync("D001", DealerOperations.Overview)).Status);
    }

    [Fact]
    public async Task HttpClient_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var http = new HttpClient(new Handler(_ => throw new TaskCanceledException())) { BaseAddress = new Uri("http://localhost/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Tenant2ApiClient(http, NullLogger<Tenant2ApiClient>.Instance)
            .GetAsync("D001", DealerOperations.Overview, cancellation.Token));
    }

    [Fact]
    public async Task Tenant2Query_RejectsTenant1BeforeDatabaseAccess()
    {
        using var db = new Tenant2DbContext(new DbContextOptionsBuilder<Tenant2DbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await Assert.ThrowsAsync<TenantAccessException>(() => new Tenant2DealerQueries(db)
            .GetAsync(DemoTenants.Tenant1, "D001", DealerOperations.Overview));
    }

    [Fact]
    public void Tenant2Model_ContainsOnlySevenAftersalesEntities()
    {
        using var db = new Tenant2DbContext(new DbContextOptionsBuilder<Tenant2DbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Tenant2DemoDb;Integrated Security=True").Options);
        Assert.Equal(7, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), entity => Assert.Equal("aftersales", entity.GetSchema()));
    }

    private static Tenant2DealerResponse Response() => new(DemoTenants.Tenant2, "D001", DealerOperations.Overview,
        new ServiceDealerDto("D001", "Demo dealer", true), new DateOnly(2026, 9, 18), [], [], [], [], null);

    private sealed class LocalQuery : ITenant1DealerQueries
    {
        public int Calls { get; private set; }
        public bool Missing { get; init; }
        public Task<Tenant1DealerData?> GetAsync(Guid tenantId, string dealerId, string operation, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<Tenant1DealerData?>(Missing ? null : new Tenant1DealerData(dealerId, "Demo dealer", "Active", [], [], []));
        }
    }

    private sealed class RemoteClient : ITenant2ApiClient
    {
        public int Calls { get; private set; }
        public string? Key { get; private set; }
        public Tenant2FetchResult Result { get; init; } = new("NotFound", null);
        public Task<Tenant2FetchResult> GetAsync(string dealerId, string operation, CancellationToken cancellationToken = default)
        {
            Calls++;
            Key = dealerId;
            return Task.FromResult(Result);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
