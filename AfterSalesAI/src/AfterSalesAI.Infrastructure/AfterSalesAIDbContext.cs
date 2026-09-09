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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfterSalesAIDbContext).Assembly);
    }
}
