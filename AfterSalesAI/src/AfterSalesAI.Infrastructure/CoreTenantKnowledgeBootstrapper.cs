using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterSalesAI.Infrastructure;

public sealed class CoreTenantKnowledgeBootstrapper(
    AICoreDbContext db,
    IOptions<KnowledgeSourceOptions> options,
    ILogger<CoreTenantKnowledgeBootstrapper> logger)
{
    public async Task<int> SeedTenant2Async(CancellationToken cancellationToken = default)
    {
        var rootPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            logger.LogWarning("Tenant 2 business knowledge was not seeded because KnowledgeSources:RootPath does not exist.");
            return 0;
        }

        await new CoreTenantGuard(db).RequireAsync(DemoTenants.Tenant2, "documents", cancellationToken);
        var existingPaths = (await db.Documents.AsNoTracking()
            .Where(document => document.TenantId == DemoTenants.Tenant2 && document.FilePath.StartsWith("project-documents://"))
            .Select(document => document.FilePath)
            .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.pdf", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfKnowledgeSource source;
            try
            {
                source = PdfKnowledgeSourceReader.Read(rootPath, file, Math.Max(300, options.Value.ChunkSize), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Tenant 2 business knowledge file {FileName} could not be read.", Path.GetFileName(file));
                continue;
            }

            var sourcePath = $"project-documents://{source.RelativePath}";
            if (existingPaths.Contains(sourcePath)) continue;

            var now = DateTimeOffset.UtcNow;
            var documentId = Guid.NewGuid();
            db.Documents.Add(new CoreDocument
            {
                DocumentId = documentId,
                TenantId = DemoTenants.Tenant2,
                FileName = $"{source.Title}.pdf",
                FileType = ".pdf",
                FilePath = sourcePath,
                ExtractedText = string.Join("\n\n", source.Chunks.Select(chunk => chunk.Content)),
                IsActive = true,
                UploadedDate = now
            });
            db.Chunks.AddRange(source.Chunks.Select((chunk, index) => new CoreDocumentChunk
            {
                ChunkId = Guid.NewGuid(),
                TenantId = DemoTenants.Tenant2,
                DocumentId = documentId,
                ChunkSequence = index,
                ChunkContent = chunk.Content,
                CreatedDate = now
            }));
            existingPaths.Add(sourcePath);
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {DocumentCount} Tenant 2 business knowledge documents from Project Documents.", added);
        return added;
    }
}
