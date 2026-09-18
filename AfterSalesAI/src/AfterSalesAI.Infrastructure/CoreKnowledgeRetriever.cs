using AfterSalesAI.Application;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class CoreKnowledgeRetriever(AICoreDbContext db, CoreTenantGuard guard) : IKnowledgeRetriever
{
    public async Task<IReadOnlyCollection<KnowledgeSearchResult>> SearchAsync(Guid tenantId, Guid applicationId,
        string query, int maximumResults = 5, CancellationToken cancellationToken = default)
    {
        await guard.RequireAsync(tenantId, "documents", cancellationToken);
        var expectedApplication = tenantId == DemoTenants.Tenant1
            ? Guid.Parse("20000000-0000-0000-0000-000000000001") : Guid.Parse("20000000-0000-0000-0000-000000000002");
        if (applicationId != Guid.Empty && applicationId != expectedApplication) throw new TenantAccessException();
        var terms = query.Split([' ', '.', ',', '?', ':', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
        var rows = await (from c in db.Chunks.AsNoTracking()
            join d in db.Documents.AsNoTracking() on new { c.TenantId, c.DocumentId } equals new { d.TenantId, d.DocumentId }
            where c.TenantId == tenantId && d.IsActive
            select new { d.FileName, d.FilePath, c.ChunkContent, c.ChunkSequence }).ToListAsync(cancellationToken);
        return rows.Select(x => new KnowledgeSearchResult(x.FileName, x.FilePath, x.ChunkContent, x.ChunkSequence,
            terms.Count(t => x.ChunkContent.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).Take(Math.Clamp(maximumResults, 1, 10)).ToArray();
    }
}

public sealed class TenantLlmContext
{
    public string SystemPrompt { get; set; } = string.Empty;
    public IReadOnlyList<(string Role, string Content)> History { get; set; } = [];
}
