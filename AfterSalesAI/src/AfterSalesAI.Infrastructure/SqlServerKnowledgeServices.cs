using System.Text.Json;
using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterSalesAI.Infrastructure;

public sealed class KnowledgeSourceOptions
{
    public const string SectionName = "KnowledgeSources";
    public string RootPath { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = 1_200;
}

public sealed class SqlServerKnowledgeIngestionService(
    AfterSalesAIDbContext dbContext,
    IOptions<KnowledgeSourceOptions> options,
    ILogger<SqlServerKnowledgeIngestionService> logger) : IKnowledgeIngestionService
{
    public async Task<KnowledgeIngestionResult> IngestAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || applicationId == Guid.Empty)
            throw new ArgumentException("tenantId and applicationId are required.");
        var rootPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            throw new DirectoryNotFoundException("KnowledgeSources:RootPath must identify an existing local directory.");
        var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
            return new KnowledgeIngestionResult(0, 0, 0, 0, ["No PDF documents were found. The existing index was not changed."]);
        var sources = new List<PdfKnowledgeSource>();
        var failures = new List<string>();
        foreach (var file in files)
        {
            try
            {
                sources.Add(PdfKnowledgeSourceReader.Read(rootPath, file, Math.Max(300, options.Value.ChunkSize), cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to ingest knowledge file {Path}.", file);
                failures.Add($"{Path.GetFileName(file)}: {exception.Message}");
            }
        }
        if (failures.Count > 0)
            return new KnowledgeIngestionResult(files.Length, 0, 0, 0, failures);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var documents = await dbContext.KnowledgeDocuments.Include(document => document.Chunks)
            .Where(document => document.TenantId == tenantId && document.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var added = 0;
        var updated = 0;
        var paths = sources.Select(source => source.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents.Where(document => document.DocumentType == "API_REFERENCE"
            || (document.SourcePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !paths.Contains(document.SourcePath))))
        {
            document.IsActive = false;
            document.UpdatedUtc = now;
        }
        foreach (var source in sources)
        {
            var document = documents.SingleOrDefault(document => document.SourcePath.Equals(source.RelativePath, StringComparison.OrdinalIgnoreCase));
            if (document is null)
            {
                document = new KnowledgeDocument
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ApplicationId = applicationId,
                    Version = "1", SourcePath = source.RelativePath, CreatedUtc = now
                };
                dbContext.KnowledgeDocuments.Add(document);
                added++;
            }
            else
            {
                dbContext.KnowledgeChunks.RemoveRange(document.Chunks);
                updated++;
            }
            document.Name = source.Title;
            document.DocumentType = source.Category;
            document.IsActive = true;
            document.UpdatedUtc = now;
        }
        // Delete old chunks before inserting their replacement indexes, within the same transaction.
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var source in sources)
        {
            var document = dbContext.KnowledgeDocuments.Local.Single(document => document.TenantId == tenantId
                && document.ApplicationId == applicationId && document.SourcePath.Equals(source.RelativePath, StringComparison.OrdinalIgnoreCase));
            foreach (var (chunk, index) in source.Chunks.Select((chunk, index) => (chunk, index)))
            {
                dbContext.KnowledgeChunks.Add(new KnowledgeChunk
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ApplicationId = applicationId, DocumentId = document.Id,
                    Content = chunk.Content, ChunkIndex = index, CreatedUtc = now,
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        relativePath = source.RelativePath, title = source.Title, documentType = source.Category,
                        sourceRoot = Path.GetFullPath(rootPath), pageNumber = chunk.PageNumber, chunkIndex = index
                    })
                });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Ingested {DocumentCount} PDF knowledge documents for tenant {TenantId} and application {ApplicationId}.", sources.Count, tenantId, applicationId);
        return new KnowledgeIngestionResult(files.Length, added, updated, sources.Sum(source => source.Chunks.Count), failures);
    }
}

public sealed class SqlServerKnowledgeRetriever(AfterSalesAIDbContext dbContext) : IKnowledgeRetriever
{
    public async Task<IReadOnlyCollection<KnowledgeSearchResult>> SearchAsync(Guid tenantId, Guid applicationId, string query, int maximumResults = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<KnowledgeSearchResult>();
        var terms = query.Split([' ', '?', ',', '.', ':', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var candidates = await dbContext.KnowledgeChunks.AsNoTracking()
            .Include(chunk => chunk.Document)
            .Where(chunk => chunk.TenantId == tenantId && chunk.ApplicationId == applicationId
                && chunk.Document.TenantId == tenantId && chunk.Document.ApplicationId == applicationId
                && chunk.Document.IsActive && chunk.Document.DocumentType != "API_REFERENCE")
            .ToListAsync(cancellationToken);
        return candidates.Select(chunk => new KnowledgeSearchResult(chunk.Document.Name, chunk.Document.SourcePath, chunk.Content, chunk.ChunkIndex,
                terms.Sum(term => CountOccurrences(chunk.Content, term))))
            .Where(result => result.Score > 0).OrderByDescending(result => result.Score).ThenBy(result => result.DocumentName)
            .Take(Math.Clamp(maximumResults, 1, 10)).ToArray();
    }

    private static int CountOccurrences(string value, string term)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0; index += term.Length) count++;
        return count;
    }
}

public sealed class SqlServerKnowledgeLibrary(AfterSalesAIDbContext dbContext) : IKnowledgeLibrary
{
    public async Task<IReadOnlyCollection<KnowledgeLibraryDocument>> GetDocumentsAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Include(document => document.Chunks.Where(chunk => chunk.TenantId == tenantId && chunk.ApplicationId == applicationId))
            .Where(document => document.TenantId == tenantId && document.ApplicationId == applicationId
                && document.IsActive && document.DocumentType != "API_REFERENCE")
            .OrderBy(document => document.Name).ToListAsync(cancellationToken);
        return documents.Select(document =>
        {
            var content = string.Join("\n\n", document.Chunks.OrderBy(chunk => chunk.ChunkIndex).Select(chunk => chunk.Content));
            return new KnowledgeLibraryDocument(document.Id, document.Name, document.DocumentType, document.SourcePath,
                content, content[..Math.Min(240, content.Length)]);
        }).ToArray();
    }
}
