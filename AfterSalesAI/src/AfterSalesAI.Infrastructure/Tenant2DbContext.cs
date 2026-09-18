using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class Tenant2DbContext(DbContextOptions<Tenant2DbContext> options) : DbContext(options)
{
    public DbSet<ServiceDealerAccount> Dealers => Set<ServiceDealerAccount>();
    public DbSet<ServiceVehicle> Vehicles => Set<ServiceVehicle>();
    public DbSet<ServiceOperation> Operations => Set<ServiceOperation>();
    public DbSet<RepairOrder> Repairs => Set<RepairOrder>();
    public DbSet<RepairLine> Lines => Set<RepairLine>();
    public DbSet<WarrantyCase> Warranties => Set<WarrantyCase>();
    public DbSet<RepairStatusEvent> Events => Set<RepairStatusEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dealer = modelBuilder.Entity<ServiceDealerAccount>();
        dealer.ToTable("DealerAccounts", "aftersales");
        dealer.HasKey(x => x.DealerId);
        dealer.Property(x => x.DealerId).HasMaxLength(50);
        dealer.Property(x => x.DealerName).HasMaxLength(200);
        dealer.Property(x => x.CreatedUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        dealer.Property(x => x.IsActive).HasDefaultValue(true);

        var vehicle = modelBuilder.Entity<ServiceVehicle>();
        vehicle.ToTable("Vehicles", "aftersales");
        vehicle.HasKey(x => x.VehicleId);
        vehicle.HasAlternateKey(x => new { x.VehicleId, x.DealerId });
        vehicle.Property(x => x.DealerId).HasMaxLength(50);
        vehicle.Property(x => x.VehicleReference).HasMaxLength(30);
        vehicle.Property(x => x.ModelName).HasMaxLength(100);
        vehicle.Property(x => x.IsActive).HasDefaultValue(true);
        vehicle.HasIndex(x => x.VehicleReference).IsUnique();
        vehicle.HasIndex(x => new { x.DealerId, x.IsActive });
        vehicle.HasOne<ServiceDealerAccount>().WithMany().HasForeignKey(x => x.DealerId).OnDelete(DeleteBehavior.Restrict);

        var operation = modelBuilder.Entity<ServiceOperation>();
        operation.ToTable("ServiceOperations", "aftersales");
        operation.HasKey(x => x.OperationCode);
        operation.Property(x => x.OperationCode).HasMaxLength(30);
        operation.Property(x => x.Description).HasMaxLength(200);
        operation.Property(x => x.Category).HasMaxLength(30);
        operation.Property(x => x.StandardHours).HasPrecision(5, 2);
        operation.Property(x => x.IsActive).HasDefaultValue(true);

        var repair = modelBuilder.Entity<RepairOrder>();
        repair.ToTable("RepairOrders", "aftersales");
        repair.HasKey(x => x.RepairOrderId);
        repair.Property(x => x.RepairOrderNumber).HasMaxLength(30);
        repair.Property(x => x.DealerId).HasMaxLength(50);
        repair.Property(x => x.Complaint).HasMaxLength(500);
        repair.Property(x => x.Status).HasMaxLength(30).HasDefaultValue("Booked");
        repair.Property(x => x.Priority).HasMaxLength(10).HasDefaultValue("Normal");
        repair.Property(x => x.CreatedUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        repair.HasIndex(x => x.RepairOrderNumber).IsUnique();
        repair.HasIndex(x => new { x.DealerId, x.Status });
        repair.HasOne<ServiceVehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.DealerId })
            .HasPrincipalKey(x => new { x.VehicleId, x.DealerId }).OnDelete(DeleteBehavior.Restrict);

        var line = modelBuilder.Entity<RepairLine>();
        line.ToTable("RepairLines", "aftersales");
        line.HasKey(x => new { x.RepairOrderId, x.LineNumber });
        line.Property(x => x.OperationCode).HasMaxLength(30);
        line.Property(x => x.LabourHours).HasPrecision(6, 2);
        line.Property(x => x.LabourRateEur).HasPrecision(10, 2);
        line.Property(x => x.MaterialsAmountEur).HasPrecision(12, 2).HasDefaultValue(0m);
        line.Property(x => x.LineTotalEur).HasPrecision(18, 2)
            .HasComputedColumnSql("CONVERT(decimal(18,2), LabourHours * LabourRateEur + MaterialsAmountEur)", stored: true);
        line.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Planned");
        line.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Restrict);
        line.HasOne<ServiceOperation>().WithMany().HasForeignKey(x => x.OperationCode).OnDelete(DeleteBehavior.Restrict);

        var warranty = modelBuilder.Entity<WarrantyCase>();
        warranty.ToTable("WarrantyCases", "aftersales");
        warranty.HasKey(x => x.WarrantyCaseId);
        warranty.Property(x => x.CaseNumber).HasMaxLength(30);
        warranty.HasIndex(x => x.CaseNumber).IsUnique();
        warranty.HasIndex(x => new { x.RepairOrderId, x.Status });
        warranty.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Submitted");
        warranty.Property(x => x.Reason).HasMaxLength(300);
        warranty.Property(x => x.ClaimedAmountEur).HasPrecision(12, 2);
        warranty.Property(x => x.ApprovedAmountEur).HasPrecision(12, 2).HasDefaultValue(0m);
        warranty.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Restrict);

        var statusEvent = modelBuilder.Entity<RepairStatusEvent>();
        statusEvent.ToTable("RepairStatusEvents", "aftersales");
        statusEvent.HasKey(x => new { x.RepairOrderId, x.EventSequence });
        statusEvent.Property(x => x.OccurredUtc).HasColumnType("datetime2(0)");
        statusEvent.Property(x => x.Status).HasMaxLength(30);
        statusEvent.Property(x => x.PublicNote).HasMaxLength(300);
        statusEvent.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
