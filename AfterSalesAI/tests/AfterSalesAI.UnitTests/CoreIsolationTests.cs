using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using AfterSalesAI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AfterSalesAI.UnitTests;

public sealed class CoreIsolationTests
{
    [Fact]
    public async Task Guard_RejectsOperationOwnedByOtherTenant()
    {
        using var db = Create();
        Seed(db);
        var guard = new CoreTenantGuard(db);
        await Assert.ThrowsAsync<TenantAccessException>(() => guard.RequireAsync(DemoTenants.Tenant1, "service-overview"));
        await Assert.ThrowsAsync<TenantAccessException>(() => guard.RequireAsync(DemoTenants.Tenant2, "wrapper-service-overview"));
    }

    [Fact]
    public async Task Guard_RejectsInactiveOperationAndTenant()
    {
        using var db = Create();
        Seed(db);
        db.Operations.Single(x => x.OperationName == "service-overview").IsActive = false;
        db.Tenants.Single(x => x.TenantId == DemoTenants.Tenant1).IsActive = false;
        db.SaveChanges();
        var guard = new CoreTenantGuard(db);
        await Assert.ThrowsAsync<TenantAccessException>(() => guard.RequireAsync(DemoTenants.Tenant2, "service-overview"));
        await Assert.ThrowsAsync<TenantAccessException>(() => guard.RequireAsync(DemoTenants.Tenant1));
    }

    [Fact]
    public async Task Documents_NeverReturnOtherTenantChunks()
    {
        using var db = Create();
        Seed(db);
        var document = Guid.NewGuid();
        db.Documents.Add(new CoreDocument { DocumentId = document, TenantId = DemoTenants.Tenant2, FileName = "warranty.md", FileType = ".md", IsActive = true });
        db.Chunks.Add(new CoreDocumentChunk { ChunkId = Guid.NewGuid(), DocumentId = document, TenantId = DemoTenants.Tenant2, ChunkContent = "Warranty approved for Tenant 2 only." });
        db.SaveChanges();
        var retriever = new CoreKnowledgeRetriever(db, new CoreTenantGuard(db));
        Assert.Empty(await retriever.SearchAsync(DemoTenants.Tenant1, Guid.Empty, "warranty"));
        Assert.Single(await retriever.SearchAsync(DemoTenants.Tenant2, Guid.Empty, "warranty"));
    }

    [Fact]
    public async Task Chat_RejectsOtherTenantSessionBeforeCallingAssistant()
    {
        using var db = Create();
        Seed(db);
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new CoreChatSession { SessionId = sessionId, TenantId = DemoTenants.Tenant2 });
        db.SaveChanges();
        var chat = new CoreChatService(db, new CoreTenantGuard(db), null!);
        await Assert.ThrowsAsync<TenantAccessException>(() => chat.HandleAsync(new AssistantRequest(DemoTenants.Tenant1, null, "hello", sessionId), default));
        Assert.Empty(db.Messages);
    }

    private static AICoreDbContext Create() => new(new DbContextOptionsBuilder<AICoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Seed(AICoreDbContext db)
    {
        foreach (var id in new[] { DemoTenants.Tenant1, DemoTenants.Tenant2 })
        {
            var databaseId = Guid.NewGuid();
            db.Tenants.Add(new CoreTenant { TenantId = id, TenantCode = id.ToString(), IsActive = true,
                IsWrapperApiEnabled = id == DemoTenants.Tenant1, DatabaseConfigurationId = databaseId });
            db.Databases.Add(new CoreDatabaseConfiguration { DatabaseConfigurationId = databaseId, TenantId = id, IsActive = true });
            db.Operations.Add(new CoreOperation { OperationId = Guid.NewGuid(), TenantId = id, OperationName = "documents", IsActive = true });
        }
        db.Operations.Add(new CoreOperation { OperationId = Guid.NewGuid(), TenantId = DemoTenants.Tenant1, OperationName = "wrapper-service-overview", IsActive = true });
        db.Operations.Add(new CoreOperation { OperationId = Guid.NewGuid(), TenantId = DemoTenants.Tenant2, OperationName = "service-overview", IsActive = true });
        db.SaveChanges();
    }
}
