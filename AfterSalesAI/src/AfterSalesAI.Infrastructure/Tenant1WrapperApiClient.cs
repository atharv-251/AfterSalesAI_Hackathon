using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterSalesAI.Application;
using Microsoft.Extensions.Logging;

namespace AfterSalesAI.Infrastructure;

public sealed class Tenant1WrapperApiClient(HttpClient client, ILogger<Tenant1WrapperApiClient> logger) : ITenant1WrapperApiClient
{
    public async Task<Tenant1WrapperFetchResult> GetAsync(string dealerId, string operation, CancellationToken cancellationToken = default)
    {
        DemoTenants.ValidateDealerId(dealerId);
        DealerOperations.Validate(operation);
        var route = operation switch
        {
            DealerOperations.Overview => "aftersales-overview",
            DealerOperations.Status => "repair-readiness",
            _ => "claims-warranty-summary"
        };

        try
        {
            using var response = await client.GetAsync(
                $"api/tenant1/wrapper/dealers/{Uri.EscapeDataString(dealerId)}/{route}?tenantId={DemoTenants.Tenant1:D}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return new Tenant1WrapperFetchResult("NotFound", null);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tenant 1 wrapper operation {Operation} returned HTTP {StatusCode}.", operation, (int)response.StatusCode);
                return new Tenant1WrapperFetchResult("Unavailable", null);
            }

            var data = await response.Content.ReadFromJsonAsync<DealerWrapperResponse>(cancellationToken);
            if (data is null || data.TenantId != DemoTenants.Tenant1 || data.DealerId != dealerId
                || data.Tenant1Data is null || data.Tenant1Data.DealerId != dealerId
                || data.Tenant2Data is not null && (data.Tenant2Data.TenantId != DemoTenants.Tenant2
                    || data.Tenant2Data.DealerId != dealerId || data.Tenant2Data.Operation != operation))
            {
                logger.LogWarning("Tenant 1 wrapper operation {Operation} returned an invalid response.", operation);
                return new Tenant1WrapperFetchResult("InvalidResponse", null);
            }

            return new Tenant1WrapperFetchResult(data.IntegrationStatus, data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Tenant 1 wrapper operation {Operation} timed out.", operation);
            return new Tenant1WrapperFetchResult("Timeout", null);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Tenant 1 wrapper operation {Operation} is unavailable.", operation);
            return new Tenant1WrapperFetchResult("Unavailable", null);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning("Tenant 1 wrapper operation {Operation} returned an unreadable response.", operation);
            return new Tenant1WrapperFetchResult("InvalidResponse", null);
        }
    }
}
