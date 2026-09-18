using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class CoreKnowledgeLibrary(AICoreDbContext db, CoreTenantGuard guard) : IKnowledgeLibrary
{
    public async Task<IReadOnlyCollection<KnowledgeLibraryDocument>> GetDocumentsAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        var expectedApplication = tenantId == DemoTenants.Tenant1
            ? Guid.Parse("20000000-0000-0000-0000-000000000001") : Guid.Parse("20000000-0000-0000-0000-000000000002");
        if (applicationId != expectedApplication) throw new TenantAccessException();
        var rows = await db.Documents.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.FileName).ToArrayAsync(cancellationToken);
        return rows.Select(x => new KnowledgeLibraryDocument(x.DocumentId, x.FileName, x.FileType, x.FilePath,
            x.ExtractedText, x.ExtractedText[..Math.Min(240, x.ExtractedText.Length)])).ToArray();
    }
}

public sealed class LegacyKnowledgeTransfer(AfterSalesAIDbContext legacy, AICoreDbContext core, CoreTenantGuard guard)
{
    public async Task<int> CopyAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = DemoTenants.Tenant1;
        var applicationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        var documents = await legacy.KnowledgeDocuments.AsNoTracking()
            .Include(x => x.Chunks.Where(c => c.TenantId == tenantId && c.ApplicationId == applicationId))
            .Where(x => x.TenantId == tenantId && x.ApplicationId == applicationId && x.IsActive && x.DocumentType != "API_REFERENCE")
            .ToArrayAsync(cancellationToken);
        var existing = await core.Documents.Where(x => x.TenantId == tenantId).Select(x => x.DocumentId).ToArrayAsync(cancellationToken);
        var added = 0;
        foreach (var document in documents.Where(x => !existing.Contains(x.Id)))
        {
            var text = string.Join("\n\n", document.Chunks.OrderBy(x => x.ChunkIndex).Select(x => x.Content));
            if (string.IsNullOrWhiteSpace(text)) continue;
            core.Documents.Add(new CoreDocument { DocumentId = document.Id, TenantId = tenantId,
                FileName = document.Name, FileType = Path.GetExtension(document.SourcePath).ToLowerInvariant(),
                FilePath = $"core://{tenantId:D}/{document.Id:D}", ExtractedText = text, IsActive = true, UploadedDate = DateTimeOffset.UtcNow });
            var sequence = 0;
            foreach (var chunk in document.Chunks.OrderBy(x => x.ChunkIndex))
                core.Chunks.Add(new CoreDocumentChunk { ChunkId = Guid.NewGuid(), TenantId = tenantId, DocumentId = document.Id,
                    ChunkSequence = sequence++, ChunkContent = chunk.Content, CreatedDate = DateTimeOffset.UtcNow });
            added++;
        }
        await core.SaveChangesAsync(cancellationToken);
        return added;
    }
}
