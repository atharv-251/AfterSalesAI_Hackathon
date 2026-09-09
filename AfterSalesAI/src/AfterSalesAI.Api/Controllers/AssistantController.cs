using AfterSalesAI.Application;
using Microsoft.AspNetCore.Mvc;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(AssistantService assistantService, IToolRegistry toolRegistry) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantResponse>> Chat(
        [FromBody] AssistantRequest request,
        CancellationToken cancellationToken)
    {
        var response = await assistantService.HandleAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("tools/{tenantId:guid}/{applicationId:guid}")]
    public ActionResult<IReadOnlyCollection<AfterSalesAI.Domain.ToolDefinition>> GetTools(
        Guid tenantId,
        Guid applicationId)
        => Ok(toolRegistry.GetAvailableTools(tenantId, applicationId));
}
