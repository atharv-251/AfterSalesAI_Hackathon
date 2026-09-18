using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api/core")]
public sealed class CoreManagementController(AICoreDbContext db, CoreTenantGuard guard) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId)) throw new TenantAccessException();
        return Ok(await db.Tenants.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.TenantId, x.TenantCode, x.TenantName, x.ProductName, x.Description, x.IsWrapperApiEnabled }).ToArrayAsync(cancellationToken));
    }

    [HttpGet("tenant")]
    public async Task<IActionResult> Tenant([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        return Ok(new { tenant.TenantId, tenant.TenantCode, tenant.TenantName, tenant.ProductName, tenant.Description, tenant.TenantSystemPrompt, tenant.IsWrapperApiEnabled });
    }

    [HttpPut("tenant")]
    public async Task<IActionResult> UpdateTenant([FromQuery] Guid tenantId, [FromBody] TenantPromptUpdate update, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(update.SystemPrompt) || update.SystemPrompt.Length > 4000) return BadRequest("A prompt of 1-4000 characters is required.");
        var tenant = await db.Tenants.SingleAsync(x => x.TenantId == tenantId, cancellationToken);
        tenant.TenantSystemPrompt = update.SystemPrompt;
        tenant.UpdatedDate = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("operations")]
    public async Task<IActionResult> Operations([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        return Ok(await db.Operations.AsNoTracking().Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.OperationName).Select(x => new { x.OperationId, x.OperationName, x.Description, x.Kind,
                x.RelativeUrl, x.HttpMethod, x.TimeoutSeconds, x.IsWrapperApi, x.IsActive }).ToArrayAsync(cancellationToken));
    }

    [HttpPut("operations/{operationId:guid}")]
    public async Task<IActionResult> SetOperation(Guid operationId, [FromQuery] Guid tenantId, [FromBody] OperationStateUpdate update, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        var operation = await db.Operations.SingleOrDefaultAsync(x => x.OperationId == operationId && x.TenantId == tenantId, cancellationToken);
        if (operation is null) return NotFound();
        operation.IsActive = update.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("documents")]
    public async Task<IActionResult> Documents([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        return Ok(await db.Documents.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.DocumentId, x.FileName, x.FileType, x.UploadedDate }).ToArrayAsync(cancellationToken));
    }

    [HttpGet("documents/{documentId:guid}")]
    public async Task<IActionResult> Document(Guid documentId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        var document = await db.Documents.AsNoTracking().Where(x => x.TenantId == tenantId && x.DocumentId == documentId && x.IsActive)
            .Select(x => new { x.DocumentId, x.FileName, x.FileType, x.ExtractedText }).SingleOrDefaultAsync(cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeactivateDocument(Guid documentId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        var document = await db.Documents.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentId == documentId, cancellationToken);
        if (document is null) return NotFound();
        document.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("documents")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromQuery] Guid tenantId, IFormFile file, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        if (file.Length is <= 0 or > 5 * 1024 * 1024) return BadRequest("Upload a document up to 5 MB.");
        var name = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (name.Length > 500 || extension is not (".pdf" or ".docx" or ".txt" or ".md")) return BadRequest("Supported types: PDF, DOCX, TXT and Markdown.");
        string text;
        try
        {
            await using var input = file.OpenReadStream();
            if (extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(input);
                var builder = new System.Text.StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AppendLine(page.Text);
                    if (builder.Length > 1_000_000) return BadRequest("Extracted document is too large.");
                }
                text = builder.ToString();
            }
            else if (extension == ".docx")
            {
                using var archive = new ZipArchive(input, ZipArchiveMode.Read);
                var entry = archive.GetEntry("word/document.xml");
                if (entry is null || entry.Length > 5 * 1024 * 1024) return BadRequest("Invalid or oversized DOCX content.");
                using var xmlStream = entry.Open();
                using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5 * 1024 * 1024 });
                var xml = XDocument.Load(reader);
                XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                text = string.Join("\n", xml.Descendants(word + "p").Select(p => string.Concat(p.Descendants(word + "t").Select(t => t.Value))));
            }
            else
            {
                using var reader = new StreamReader(input);
                text = await reader.ReadToEndAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return UnprocessableEntity("Document extraction failed. Use a readable, unencrypted document; scanned PDF OCR is not supported."); }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1_000_000) return UnprocessableEntity("Document must contain readable text of at most one million characters.");
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Documents.Add(new CoreDocument { DocumentId = id, TenantId = tenantId, FileName = name, FileType = extension,
            FilePath = $"core://{tenantId:D}/{id:D}", ExtractedText = text, IsActive = true, UploadedDate = now });
        for (var start = 0; start < text.Length; start += 1200)
            db.Chunks.Add(new CoreDocumentChunk { ChunkId = Guid.NewGuid(), TenantId = tenantId, DocumentId = id,
                ChunkSequence = start / 1200, ChunkContent = text.Substring(start, Math.Min(1200, text.Length - start)), CreatedDate = now });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { documentId = id, fileName = name });
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        return Ok(await db.Sessions.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.CreatedDate)
            .Take(100).Select(x => new { x.SessionId, x.CreatedDate }).ToArrayAsync(cancellationToken));
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid sessionId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(tenantId, cancellationToken: cancellationToken);
        if (!await db.Sessions.AnyAsync(x => x.TenantId == tenantId && x.SessionId == sessionId, cancellationToken)) return NotFound();
        return Ok(await db.Messages.AsNoTracking().Where(x => x.TenantId == tenantId && x.SessionId == sessionId)
            .OrderBy(x => x.CreatedDate).Select(x => new { x.MessageId, x.Role, x.Content, x.CreatedDate }).ToArrayAsync(cancellationToken));
    }
}

public sealed record TenantPromptUpdate(string SystemPrompt);
public sealed record OperationStateUpdate(bool IsActive);
