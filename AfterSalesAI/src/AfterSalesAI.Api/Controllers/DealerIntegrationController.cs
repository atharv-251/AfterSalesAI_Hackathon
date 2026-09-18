using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class DealerIntegrationController(ITenant2DealerQueries tenant2, Tenant1WrapperService wrapper, CoreTenantGuard guard,
    ServiceIntegrationAuthorizer integrationAuthorizer) : ControllerBase
{
    [HttpGet("api/tenant2/dealers/{dealerId}/service-overview")]
    public async Task<ActionResult<Tenant2DealerResponse>> ServiceOverview(string dealerId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => await ReadTenant2(tenantId, dealerId, DealerOperations.Overview, null, cancellationToken);

    [HttpGet("api/tenant2/dealers/{dealerId}/repair-status")]
    public async Task<ActionResult<Tenant2DealerResponse>> RepairStatus(string dealerId, [FromQuery] Guid tenantId, [FromQuery] DateOnly? evaluationDate, CancellationToken cancellationToken)
        => await ReadTenant2(tenantId, dealerId, DealerOperations.Status, evaluationDate, cancellationToken);

    [HttpGet("api/tenant2/dealers/{dealerId}/warranty-summary")]
    public async Task<ActionResult<Tenant2DealerResponse>> WarrantySummary(string dealerId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => await ReadTenant2(tenantId, dealerId, DealerOperations.Warranty, null, cancellationToken);

    [HttpGet("api/tenant1/wrapper/dealers/{dealerId}/aftersales-overview")]
    public async Task<ActionResult<DealerWrapperResponse>> CombinedOverview(string dealerId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => await ReadWrapper(tenantId, dealerId, DealerOperations.Overview, cancellationToken);

    [HttpGet("api/tenant1/wrapper/dealers/{dealerId}/repair-readiness")]
    public async Task<ActionResult<DealerWrapperResponse>> RepairReadiness(string dealerId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => await ReadWrapper(tenantId, dealerId, DealerOperations.Status, cancellationToken);

    [HttpGet("api/tenant1/wrapper/dealers/{dealerId}/claims-warranty-summary")]
    public async Task<ActionResult<DealerWrapperResponse>> CombinedWarranty(string dealerId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => await ReadWrapper(tenantId, dealerId, DealerOperations.Warranty, cancellationToken);

    private async Task<ActionResult<Tenant2DealerResponse>> ReadTenant2(Guid tenantId, string dealerId, string operation, DateOnly? date, CancellationToken cancellationToken)
    {
        DemoTenants.Require(tenantId, DemoTenants.Tenant2);
        RequireAuthorizedCaller(tenantId);
        await guard.RequireAsync(tenantId, operation, cancellationToken);
        var result = await tenant2.GetAsync(tenantId, dealerId, operation, date, cancellationToken);
        return result is null ? NotFound(new { message = "Tenant 2 dealer was not found." }) : Ok(result);
    }

    private async Task<ActionResult<DealerWrapperResponse>> ReadWrapper(Guid tenantId, string dealerId, string operation, CancellationToken cancellationToken)
    {
        DemoTenants.Require(tenantId, DemoTenants.Tenant1);
        RequireAuthorizedCaller(tenantId);
        await guard.RequireAsync(tenantId, "wrapper-" + operation, cancellationToken);
        var result = await wrapper.GetAsync(tenantId, dealerId, operation, cancellationToken);
        return result is null ? NotFound(new { message = "Tenant 1 dealer was not found." }) : Ok(result);
    }

    private void RequireAuthorizedCaller(Guid tenantId)
    {
        if (!integrationAuthorizer.IsAuthorized(Request.Headers[ServiceIntegrationOptions.HeaderName].SingleOrDefault())
            && !DemoAuthenticationService.HasTenant(User, tenantId))
            throw new TenantAccessException();
    }
}
