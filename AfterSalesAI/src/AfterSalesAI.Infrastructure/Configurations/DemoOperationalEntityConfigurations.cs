using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterSalesAI.Infrastructure.Configurations;

internal sealed class DemoDealerConfiguration : IEntityTypeConfiguration<DemoDealer>
{
    public void Configure(EntityTypeBuilder<DemoDealer> builder)
    {
        builder.ToTable("Dealers", "demo");
        builder.HasKey(item => item.DealerId);
        builder.Property(item => item.DealerId).HasMaxLength(50);
        builder.Property(item => item.DealerName).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => item.Status);
    }
}

internal sealed class DemoPartConfiguration : IEntityTypeConfiguration<DemoPart>
{
    public void Configure(EntityTypeBuilder<DemoPart> builder)
    {
        builder.ToTable("Parts", "demo");
        builder.HasKey(item => item.PartNo);
        builder.Property(item => item.PartNo).HasMaxLength(50);
        builder.Property(item => item.UnitPriceEur).HasPrecision(18, 2);
        builder.HasIndex(item => item.Status);
    }
}

internal sealed class DemoPurchaseOrderConfiguration : IEntityTypeConfiguration<DemoPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<DemoPurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", "demo");
        builder.HasKey(item => new { item.PoNo, item.PoLineNo });
        builder.Property(item => item.LineTotalEur).HasPrecision(18, 2);
        builder.Property(item => item.UnitPriceEur).HasPrecision(18, 2);
        builder.HasIndex(item => item.DealerId);
        builder.HasIndex(item => item.PartNo);
        builder.HasIndex(item => item.ShipmentId);
    }
}

internal sealed class DemoShipmentConfiguration : IEntityTypeConfiguration<DemoShipment>
{
    public void Configure(EntityTypeBuilder<DemoShipment> builder)
    {
        builder.ToTable("Shipments", "demo");
        builder.HasKey(item => item.ShipmentId);
        builder.HasIndex(item => item.PoNo);
        builder.HasIndex(item => item.DealerId);
        builder.HasIndex(item => item.Status);
    }
}

internal sealed class DemoClaimRecordConfiguration : IEntityTypeConfiguration<DemoClaimRecord>
{
    public void Configure(EntityTypeBuilder<DemoClaimRecord> builder)
    {
        builder.ToTable("Claims", "demo");
        builder.HasKey(item => item.ClaimId);
        builder.Property(item => item.ClaimAmountEur).HasPrecision(18, 2);
        builder.HasIndex(item => item.PoNo);
        builder.HasIndex(item => item.DealerId);
        builder.HasIndex(item => item.PartNo);
    }
}

internal sealed class DemoInventoryItemConfiguration : IEntityTypeConfiguration<DemoInventoryItem>
{
    public void Configure(EntityTypeBuilder<DemoInventoryItem> builder)
    {
        builder.ToTable("Inventory", "demo");
        builder.HasKey(item => item.InventoryId);
        builder.HasIndex(item => item.PartNo);
    }
}

internal sealed class DemoBomItemConfiguration : IEntityTypeConfiguration<DemoBomItem>
{
    public void Configure(EntityTypeBuilder<DemoBomItem> builder)
    {
        builder.ToTable("Bom", "demo");
        builder.HasKey(item => item.BomId);
        builder.Property(item => item.QuantityPer).HasPrecision(18, 3);
        builder.HasIndex(item => item.AssemblyPartNo);
        builder.HasIndex(item => item.ComponentPartNo);
    }
}

internal sealed class DemoKnowledgeArticleConfiguration : IEntityTypeConfiguration<DemoKnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<DemoKnowledgeArticle> builder)
    {
        builder.ToTable("Knowledge", "demo");
        builder.HasKey(item => item.DocumentId);
        builder.HasIndex(item => item.ErrorCode);
        builder.HasIndex(item => item.Module);
    }
}
