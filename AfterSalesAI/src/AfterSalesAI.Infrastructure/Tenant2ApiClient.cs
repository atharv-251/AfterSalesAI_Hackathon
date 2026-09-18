using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterSalesAI.Application;
using Microsoft.Extensions.Logging;

namespace AfterSalesAI.Infrastructure;

public sealed class Tenant2ApiClient(HttpClient client, ILogger<Tenant2ApiClient> logger) : ITenant2ApiClient
{
    public async Task<Tenant2FetchResult> GetAsync(string dealerId, string operation, CancellationToken cancellationToken = default)
    {
        DemoTenants.ValidateDealerId(dealerId);
        DealerOperations.Validate(operation);
        try
        {
            using var response = await client.GetAsync(
                $"api/tenant2/dealers/{Uri.EscapeDataString(dealerId)}/{operation}?tenantId={DemoTenants.Tenant2:D}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new Tenant2FetchResult("NotFound", null);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tenant 2 operation {Operation} returned HTTP {StatusCode}.", operation, (int)response.StatusCode);
                return new Tenant2FetchResult("Unavailable", null);
            }
            var data = await response.Content.ReadFromJsonAsync<Tenant2DealerResponse>(cancellationToken);
            if (data is null || data.TenantId != DemoTenants.Tenant2 || data.DealerId != dealerId
                || data.Operation != operation || data.Dealer is null || data.Dealer.DealerId != dealerId
                || data.Repairs is null || data.Lines is null || data.Events is null || data.WarrantyCases is null
                || data.Repairs.Any(x => x is null) || data.Lines.Any(x => x is null)
                || data.Events.Any(x => x is null) || data.WarrantyCases.Any(x => x is null)
                || operation == DealerOperations.Warranty && data.WarrantySummary is null)
            {
                logger.LogWarning("Tenant 2 operation {Operation} returned an invalid response.", operation);
                return new Tenant2FetchResult("InvalidResponse", null);
            }
            return new Tenant2FetchResult("Available", data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Tenant 2 operation {Operation} timed out.", operation);
            return new Tenant2FetchResult("Timeout", null);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Tenant 2 operation {Operation} is unavailable.", operation);
            return new Tenant2FetchResult("Unavailable", null);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning("Tenant 2 operation {Operation} returned an unreadable response.", operation);
            return new Tenant2FetchResult("InvalidResponse", null);
        }
    }
}
