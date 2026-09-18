using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api/core/database-operations")]
public sealed class ApprovedDatabaseController(AICoreDbContext core, CoreTenantGuard guard,
    ITenant1DealerQueries tenant1, ITenant2DealerQueries tenant2) : ControllerBase
{
    [HttpPost("{operationId:guid}/execute")]
    public async Task<IActionResult> Execute(Guid operationId, [FromQuery] Guid tenantId,
        [FromBody] DealerDatabaseRequest request, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        var operation = await core.Operations.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId
            && x.TenantId == tenantId && x.Kind == "Database" && x.IsActive, cancellationToken);
        if (operation is null) throw new TenantAccessException();
        DemoTenants.ValidateDealerId(request.DealerId);
        if (tenantId == DemoTenants.Tenant1 && operation.OperationName == "local")
        {
            var result = await tenant1.GetAsync(tenantId, request.DealerId, DealerOperations.Overview, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        if (tenantId == DemoTenants.Tenant2)
        {
            var name = operation.OperationName switch
            {
                "db-service-overview" => DealerOperations.Overview,
                "db-repair-status" => DealerOperations.Status,
                "db-warranty-summary" => DealerOperations.Warranty,
                _ => throw new TenantAccessException()
            };
            var result = await tenant2.GetAsync(tenantId, request.DealerId, name, cancellationToken: cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        throw new TenantAccessException();
    }
}

public sealed record DealerDatabaseRequest(string DealerId);
