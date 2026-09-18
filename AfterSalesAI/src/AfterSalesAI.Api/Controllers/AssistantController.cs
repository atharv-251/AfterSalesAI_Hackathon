using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(CoreChatService assistantService, AICoreDbContext db, CoreTenantGuard guard) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantResponse>> Chat(
        [FromBody] AssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (!DemoAuthenticationService.HasTenant(User, request.TenantId)) throw new TenantAccessException();
        var response = await assistantService.HandleAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("tools/{tenantId:guid}/{applicationId:guid}")]
    public async Task<IActionResult> GetTools(
        Guid tenantId,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        if (!DemoAuthenticationService.HasTenant(User, tenantId)) throw new TenantAccessException();
        var expected = tenantId == DemoTenants.Tenant1 ? Guid.Parse("20000000-0000-0000-0000-000000000001")
            : Guid.Parse("20000000-0000-0000-0000-000000000002");
        if (applicationId != expected) throw new TenantAccessException();
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        return Ok(await db.Operations.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { name = x.OperationName, x.Description, x.Kind }).ToArrayAsync(cancellationToken));
    }
}
