using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AfterSalesApplication = AfterSalesAI.Domain.Application;

namespace AfterSalesAI.Infrastructure.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
        builder.Property(tenant => tenant.Region).HasMaxLength(100).IsRequired();
        builder.HasIndex(tenant => tenant.Name).IsUnique();

        builder.HasData(new Tenant
        {
            Id = RegistrySeedData.TenantId,
            Name = "Demo After-Sales Tenant",
            Region = "EU",
            IsActive = true
        });
    }
}

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<AfterSalesApplication>
{
    public void Configure(EntityTypeBuilder<AfterSalesApplication> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Name).HasMaxLength(200).IsRequired();
        builder.Property(application => application.Description).HasMaxLength(2_000).IsRequired();
        builder.HasIndex(application => new { application.TenantId, application.Name }).IsUnique();
        builder.HasIndex(application => new { application.TenantId, application.Id }).IsUnique();
        builder.HasOne(application => application.Tenant)
            .WithMany(tenant => tenant.Applications)
            .HasForeignKey(application => application.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new AfterSalesApplication
        {
            Id = RegistrySeedData.ApplicationId,
            TenantId = RegistrySeedData.TenantId,
            Name = "Demo After-Sales Portal",
            Description = "Demonstration application for after-sales capability registration.",
            IsActive = true
        });
    }
}

internal sealed class IntegrationSourceConfiguration : IEntityTypeConfiguration<IntegrationSource>
{
    public void Configure(EntityTypeBuilder<IntegrationSource> builder)
    {
        builder.ToTable("integration_sources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Protocol).HasMaxLength(100).IsRequired();
        builder.Property(source => source.Name).HasMaxLength(200).IsRequired();
        builder.Property(source => source.ConfigurationReference).HasMaxLength(500).IsRequired();
        builder.HasIndex(source => new { source.TenantId, source.ApplicationId, source.Name }).IsUnique();
        builder.HasIndex(source => new { source.TenantId, source.ApplicationId });
        builder.HasOne(source => source.Tenant)
            .WithMany()
            .HasForeignKey(source => source.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.Application)
            .WithMany(application => application.IntegrationSources)
            .HasForeignKey(source => source.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new IntegrationSource
            {
                Id = RegistrySeedData.ApiIntegrationSourceId,
                TenantId = RegistrySeedData.TenantId,
                ApplicationId = RegistrySeedData.ApplicationId,
                Type = IntegrationSourceType.Api,
                Protocol = "REST",
                Name = "Demo Order API",
                ConfigurationReference = "IntegrationSources__DemoOrderApi",
                IsReadOnly = true,
                IsActive = true
            },
            new IntegrationSource
            {
                Id = RegistrySeedData.DatabaseIntegrationSourceId,
                TenantId = RegistrySeedData.TenantId,
                ApplicationId = RegistrySeedData.ApplicationId,
                Type = IntegrationSourceType.Database,
                Protocol = "SQLServer",
                Name = "Demo Knowledge Database",
                ConfigurationReference = "IntegrationSources__DemoKnowledgeDatabase",
                IsReadOnly = true,
                IsActive = true
            });
    }
}

internal sealed class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> builder)
    {
        builder.ToTable("tool_definitions");
        builder.HasKey(tool => tool.Id);
        builder.Property(tool => tool.Name).HasMaxLength(200).IsRequired();
        builder.Property(tool => tool.Description).HasMaxLength(2_000).IsRequired();
        builder.Property(tool => tool.InputSchemaJson).HasColumnType("jsonb").IsRequired();
        builder.Property(tool => tool.OutputSchemaJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(tool => new { tool.TenantId, tool.ApplicationId, tool.Name }).IsUnique();
        builder.HasIndex(tool => new { tool.TenantId, tool.ApplicationId, tool.IsActive });
        builder.HasOne(tool => tool.Tenant)
            .WithMany()
            .HasForeignKey(tool => tool.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(tool => tool.Application)
            .WithMany(application => application.ToolDefinitions)
            .HasForeignKey(tool => tool.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(tool => tool.IntegrationSource)
            .WithMany(source => source.ToolDefinitions)
            .HasForeignKey(tool => tool.IntegrationSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            RegistrySeedData.CreateTool(RegistrySeedData.OrderStatusToolId, RegistrySeedData.ApiIntegrationSourceId, "get_order_status", "Retrieve the current status of an order from an approved live system."),
            RegistrySeedData.CreateTool(RegistrySeedData.DeliveryStatusToolId, RegistrySeedData.ApiIntegrationSourceId, "get_delivery_status", "Retrieve the current delivery status for an order."),
            RegistrySeedData.CreateTool(RegistrySeedData.SearchSopToolId, RegistrySeedData.DatabaseIntegrationSourceId, "search_sop", "Search approved SOP and business process knowledge."));
    }
}

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Name).HasMaxLength(500).IsRequired();
        builder.Property(document => document.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(document => document.Version).HasMaxLength(100).IsRequired();
        builder.HasIndex(document => new { document.TenantId, document.ApplicationId, document.Name, document.Version }).IsUnique();
        builder.HasOne(document => document.Tenant)
            .WithMany(tenant => tenant.KnowledgeDocuments)
            .HasForeignKey(document => document.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(document => document.Application)
            .WithMany(application => application.KnowledgeDocuments)
            .HasForeignKey(document => document.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");
        builder.HasKey(chunk => chunk.Id);
        builder.Property(chunk => chunk.Content).IsRequired();
        builder.Property(chunk => chunk.MetadataJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(chunk => new { chunk.TenantId, chunk.ApplicationId, chunk.DocumentId });
        builder.HasOne(chunk => chunk.Tenant)
            .WithMany(tenant => tenant.KnowledgeChunks)
            .HasForeignKey(chunk => chunk.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(chunk => chunk.Application)
            .WithMany(application => application.KnowledgeChunks)
            .HasForeignKey(chunk => chunk.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(chunk => chunk.Document)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal static class RegistrySeedData
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ApplicationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ApiIntegrationSourceId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid DatabaseIntegrationSourceId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid OrderStatusToolId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid DeliveryStatusToolId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SearchSopToolId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static ToolDefinition CreateTool(Guid id, Guid integrationSourceId, string name, string description) => new()
    {
        Id = id,
        TenantId = TenantId,
        ApplicationId = ApplicationId,
        IntegrationSourceId = integrationSourceId,
        Name = name,
        Description = description,
        InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}",
        OutputSchemaJson = "{\"type\":\"object\"}",
        IsActive = true
    };
}
