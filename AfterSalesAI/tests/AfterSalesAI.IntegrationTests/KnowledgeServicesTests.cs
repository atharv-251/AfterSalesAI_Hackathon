using System.Text.Json;
using AfterSalesAI.Api.Controllers;
using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using AfterSalesApplication = AfterSalesAI.Domain.Application;

namespace AfterSalesAI.IntegrationTests;

public sealed class KnowledgeServicesTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ApplicationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"AfterSalesAI_KnowledgeTests_{Guid.NewGuid():N}");
    private readonly AfterSalesAIDbContext _dbContext;

    public KnowledgeServicesTests()
    {
        var connectionString = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("AFTERSALESAI_TEST_SQL_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True")
        {
            InitialCatalog = $"AfterSalesAI_KnowledgeTests_{Guid.NewGuid():N}"
        };
        _dbContext = new AfterSalesAIDbContext(new DbContextOptionsBuilder<AfterSalesAIDbContext>()
            .UseSqlServer(connectionString.ConnectionString).Options);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_rootPath);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "TestDocuments"), "*.pdf"))
            File.Copy(file, Path.Combine(_rootPath, Path.GetFileName(file)));
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _dbContext.Database.EnsureDeletedAsync();
        }
        finally
        {
            await _dbContext.DisposeAsync();
            if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, true);
        }
    }

    [Fact]
    public async Task Ingest_ExtractsSevenBusinessPdfsWithCategoriesAndPageMetadata()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "api-reference.md"), "# Wrong API reference");

        var result = await CreateIngestionService().IngestAsync(TenantId, ApplicationId);
        var documents = await new SqlServerKnowledgeLibrary(_dbContext).GetDocumentsAsync(TenantId, ApplicationId);
        var chunks = await _dbContext.KnowledgeChunks.AsNoTracking().ToArrayAsync();

        Assert.Empty(result.Failures);
        Assert.Equal(7, result.FilesDiscovered);
        Assert.Equal(7, result.DocumentsAdded);
        Assert.Equal(7, documents.Count);
        Assert.Contains(documents, document => document.SourcePath == "Standard_Operating_Procedure.pdf" && document.Category == "SOP");
        Assert.Contains(documents, document => document.SourcePath == "Knowledge_Base.pdf" && document.Category == "KNOWLEDGE_BASE");
        Assert.All(documents, document =>
        {
            Assert.EndsWith(".pdf", document.SourcePath);
            Assert.NotEqual("API_REFERENCE", document.Category);
            Assert.False(string.IsNullOrWhiteSpace(document.Content));
        });
        Assert.Equal(result.ChunksCreated, chunks.Length);
        Assert.All(chunks, chunk =>
        {
            Assert.InRange(chunk.Content.Length, 1, 1200);
            using var metadata = JsonDocument.Parse(chunk.MetadataJson);
            Assert.True(metadata.RootElement.GetProperty("pageNumber").GetInt32() > 0);
            Assert.Equal(_rootPath, metadata.RootElement.GetProperty("sourceRoot").GetString());
        });
    }

    [Fact]
    public async Task Ingest_RetiresApiReferencesOnlyInRequestedScopeAndPreservesOriginalChunks()
    {
        var otherApplicationId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantApplicationId = Guid.NewGuid();
        _dbContext.Applications.Add(new AfterSalesApplication { Id = otherApplicationId, TenantId = TenantId, Name = "Other application" });
        _dbContext.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other tenant", Region = "EU" });
        _dbContext.Applications.Add(new AfterSalesApplication { Id = otherTenantApplicationId, TenantId = otherTenantId, Name = "Private application" });
        var obsolete = CreateDocument("delivery/api.md", "API_REFERENCE", "Old delivery API structure");
        var otherApplication = CreateDocument("delivery/api.md", "API_REFERENCE", "Other application API structure", applicationId: otherApplicationId);
        var otherTenant = CreateDocument("delivery/api.md", "API_REFERENCE", "Other tenant API structure", tenantId: otherTenantId, applicationId: otherTenantApplicationId);
        _dbContext.AddRange(obsolete, otherApplication, otherTenant);
        await _dbContext.SaveChangesAsync();

        var result = await CreateIngestionService().IngestAsync(TenantId, ApplicationId);

        Assert.Empty(result.Failures);
        Assert.False(obsolete.IsActive);
        Assert.True(otherApplication.IsActive);
        Assert.True(otherTenant.IsActive);
        Assert.Equal("Old delivery API structure", (await _dbContext.KnowledgeChunks.SingleAsync(chunk => chunk.DocumentId == obsolete.Id)).Content);
        Assert.Equal(7, await _dbContext.KnowledgeDocuments.CountAsync(document => document.TenantId == TenantId && document.ApplicationId == ApplicationId && document.IsActive));
    }

    [Fact]
    public async Task Ingest_ReplacesChunksWithoutDuplicatesAndReactivatesCurrentPdfs()
    {
        var service = CreateIngestionService();
        var first = await service.IngestAsync(TenantId, ApplicationId);
        var documentIds = await _dbContext.KnowledgeDocuments.Select(document => document.Id).OrderBy(id => id).ToArrayAsync();
        var oldChunkIds = await _dbContext.KnowledgeChunks.Select(chunk => chunk.Id).ToArrayAsync();
        var sop = await _dbContext.KnowledgeDocuments.SingleAsync(document => document.DocumentType == "SOP");
        sop.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var second = await service.IngestAsync(TenantId, ApplicationId);

        Assert.Empty(second.Failures);
        Assert.Equal(0, second.DocumentsAdded);
        Assert.Equal(7, second.DocumentsUpdated);
        Assert.Equal(first.ChunksCreated, second.ChunksCreated);
        Assert.Equal(second.ChunksCreated, await _dbContext.KnowledgeChunks.CountAsync());
        Assert.Equal(documentIds, await _dbContext.KnowledgeDocuments.Select(document => document.Id).OrderBy(id => id).ToArrayAsync());
        Assert.False(await _dbContext.KnowledgeChunks.AnyAsync(chunk => oldChunkIds.Contains(chunk.Id)));
        Assert.True(sop.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ingest_LeavesExistingIndexUnchangedWhenSourceIsEmptyOrUnreadable(bool corruptPdf)
    {
        var existing = CreateDocument("delivery/api.md", "API_REFERENCE", "Preserve existing text");
        _dbContext.Add(existing);
        await _dbContext.SaveChangesAsync();
        if (corruptPdf)
            await File.WriteAllTextAsync(Path.Combine(_rootPath, "Corrupt.pdf"), "This is not a PDF.");
        else
            foreach (var file in Directory.EnumerateFiles(_rootPath)) File.Delete(file);

        var result = await CreateIngestionService().IngestAsync(TenantId, ApplicationId);

        Assert.NotEmpty(result.Failures);
        Assert.Equal(0, result.DocumentsAdded);
        Assert.Equal(0, result.ChunksCreated);
        Assert.True(existing.IsActive);
        Assert.Equal(1, await _dbContext.KnowledgeDocuments.CountAsync());
        Assert.Equal("Preserve existing text", (await _dbContext.KnowledgeChunks.SingleAsync()).Content);
    }

    [Fact]
    public async Task Ingest_RollsBackRetirementWhenSqlWriteFails()
    {
        var existing = CreateDocument("delivery/api.md", "API_REFERENCE", "Preserve existing text");
        _dbContext.Add(existing);
        await _dbContext.SaveChangesAsync();
        await _dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE knowledge_chunks ADD CONSTRAINT CK_KnowledgeTest_RejectPdf CHECK (MetadataJson NOT LIKE '%pageNumber%')");

        await Assert.ThrowsAsync<DbUpdateException>(() => CreateIngestionService().IngestAsync(TenantId, ApplicationId));
        _dbContext.ChangeTracker.Clear();

        var stored = await _dbContext.KnowledgeDocuments.SingleAsync();
        Assert.True(stored.IsActive);
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("Preserve existing text", (await _dbContext.KnowledgeChunks.SingleAsync()).Content);
    }

    [Fact]
    public async Task LibraryAndSearch_ExcludeInactiveApiAndOtherScopeDocuments()
    {
        var otherTenantId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        _dbContext.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other tenant", Region = "EU" });
        _dbContext.Applications.Add(new AfterSalesApplication { Id = otherApplicationId, TenantId = otherTenantId, Name = "Other application" });
        var sop = CreateDocument("Standard_Operating_Procedure.pdf", "SOP", "Shipment escalation procedure");
        var inactive = CreateDocument("Retired.pdf", "SOP", "Shipment escalation procedure");
        inactive.IsActive = false;
        _dbContext.AddRange(sop, inactive,
            CreateDocument("delivery/api.md", "API_REFERENCE", "Shipment escalation procedure"),
            CreateDocument("Private.pdf", "SOP", "Shipment escalation procedure", otherTenantId, otherApplicationId));
        await _dbContext.SaveChangesAsync();

        var library = new SqlServerKnowledgeLibrary(_dbContext);
        var retriever = new SqlServerKnowledgeRetriever(_dbContext);
        var documents = await library.GetDocumentsAsync(TenantId, ApplicationId);
        var results = await retriever.SearchAsync(TenantId, ApplicationId, "Shipment escalation");

        Assert.Equal(sop.SourcePath, Assert.Single(documents).SourcePath);
        Assert.Equal(sop.SourcePath, Assert.Single(results).SourcePath);
        Assert.Empty(await library.GetDocumentsAsync(TenantId, otherApplicationId));
        Assert.Empty(await retriever.SearchAsync(TenantId, otherApplicationId, "Shipment escalation"));
        Assert.Empty(await retriever.SearchAsync(TenantId, ApplicationId, " "));
    }

    [Fact]
    public async Task KnowledgeEndpoint_ReturnsIndexedPdfTextAndValidatesScope()
    {
        var service = CreateIngestionService();
        var ingestion = await service.IngestAsync(TenantId, ApplicationId);
        Assert.Empty(ingestion.Failures);
        var controller = new DemoController(new SqlServerAfterSalesOperations(_dbContext), service, new SqlServerKnowledgeLibrary(_dbContext));

        var response = await controller.GetKnowledgeDocuments(TenantId, ApplicationId, CancellationToken.None);
        var documents = Assert.IsAssignableFrom<IReadOnlyCollection<KnowledgeLibraryDocument>>(Assert.IsType<OkObjectResult>(response.Result).Value);

        Assert.Equal(7, documents.Count);
        Assert.IsType<BadRequestObjectResult>((await controller.GetKnowledgeDocuments(Guid.Empty, ApplicationId, CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetKnowledgeDocuments(TenantId, Guid.Empty, CancellationToken.None)).Result);
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Corrupt.pdf"), "Not a PDF");
        Assert.IsType<UnprocessableEntityObjectResult>((await controller.IngestKnowledge(TenantId, ApplicationId, CancellationToken.None)).Result);
    }

    private SqlServerKnowledgeIngestionService CreateIngestionService() => new(_dbContext,
        Options.Create(new KnowledgeSourceOptions { RootPath = _rootPath, ChunkSize = 1200 }),
        NullLogger<SqlServerKnowledgeIngestionService>.Instance);

    private static KnowledgeDocument CreateDocument(string path, string category, string content, Guid? tenantId = null, Guid? applicationId = null)
    {
        var document = new KnowledgeDocument
        {
            Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, ApplicationId = applicationId ?? ApplicationId,
            Name = Path.GetFileNameWithoutExtension(path), SourcePath = path, DocumentType = category,
            Version = "1", CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow
        };
        document.Chunks.Add(new KnowledgeChunk
        {
            Id = Guid.NewGuid(), DocumentId = document.Id, TenantId = document.TenantId, ApplicationId = document.ApplicationId,
            Content = content, ChunkIndex = 0, CreatedUtc = DateTimeOffset.UtcNow
        });
        return document;
    }
}
