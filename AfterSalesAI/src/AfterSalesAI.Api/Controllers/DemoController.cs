using AfterSalesAI.Application;
using Microsoft.AspNetCore.Mvc;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class DemoController(
    IAfterSalesOperations operations,
    IKnowledgeIngestionService knowledgeIngestionService,
    IKnowledgeLibrary knowledgeLibrary,
    IDemoDatasetImportService? demoDatasetImportService = null) : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<DemoDashboard> GetDashboard() => Ok(operations.GetDashboard());

    [HttpGet("orders")]
    public ActionResult<IReadOnlyCollection<DemoOrder>> GetOrders() => Ok(operations.GetOrders());

    [HttpGet("orders/{orderId}")]
    public ActionResult<DemoOrder> GetOrder(string orderId) => operations.GetOrders().FirstOrDefault(item => item.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase)) is { } order ? Ok(order) : NotFound();

    [HttpGet("claims")]
    public ActionResult<IReadOnlyCollection<DemoClaim>> GetClaims() => Ok(operations.GetClaims());

    [HttpGet("inventory")]
    public ActionResult<IReadOnlyCollection<DemoPartStock>> GetInventory() => Ok(operations.GetInventory());

    [HttpGet("knowledge/documents")]
    public async Task<ActionResult<IReadOnlyCollection<KnowledgeLibraryDocument>>> GetKnowledgeDocuments([FromQuery] Guid tenantId, [FromQuery] Guid applicationId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || applicationId == Guid.Empty) return BadRequest("tenantId and applicationId are required.");
        return Ok(await knowledgeLibrary.GetDocumentsAsync(tenantId, applicationId, cancellationToken));
    }

    [HttpPost("knowledge/ingest")]
    public async Task<ActionResult<KnowledgeIngestionResult>> IngestKnowledge([FromQuery] Guid tenantId, [FromQuery] Guid applicationId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || applicationId == Guid.Empty) return BadRequest("tenantId and applicationId are required.");
        var result = await knowledgeIngestionService.IngestAsync(tenantId, applicationId, cancellationToken);
        return result.Failures.Count > 0 ? UnprocessableEntity(result) : Ok(result);
    }

    [HttpPost("demo-data/import")]
    public async Task<ActionResult<DemoDatasetImportResult>> ImportDemoData(CancellationToken cancellationToken)
    {
        if (demoDatasetImportService is null) return Conflict("A SQL Server connection is required to import the demo dataset.");
        return Ok(await demoDatasetImportService.ImportAsync(cancellationToken));
    }
}
