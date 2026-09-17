using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;
using AfterSalesApplication = AfterSalesAI.Domain.Application;

namespace AfterSalesAI.Infrastructure;

public sealed class AfterSalesAIDbContext(DbContextOptions<AfterSalesAIDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AfterSalesApplication> Applications => Set<AfterSalesApplication>();
    public DbSet<IntegrationSource> IntegrationSources => Set<IntegrationSource>();
    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<DemoDealer> DemoDealers => Set<DemoDealer>();
    public DbSet<DemoPart> DemoParts => Set<DemoPart>();
    public DbSet<DemoPurchaseOrder> DemoPurchaseOrders => Set<DemoPurchaseOrder>();
    public DbSet<DemoShipment> DemoShipments => Set<DemoShipment>();
    public DbSet<DemoClaimRecord> DemoClaims => Set<DemoClaimRecord>();
    public DbSet<DemoInventoryItem> DemoInventory => Set<DemoInventoryItem>();
    public DbSet<DemoBomItem> DemoBom => Set<DemoBomItem>();
    public DbSet<DemoKnowledgeArticle> DemoKnowledge => Set<DemoKnowledgeArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfterSalesAIDbContext).Assembly);
    }
}
