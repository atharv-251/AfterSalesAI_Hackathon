using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class AICoreDbContext(DbContextOptions<AICoreDbContext> options) : DbContext(options)
{
    public DbSet<CoreTenant> Tenants => Set<CoreTenant>();
    public DbSet<CoreDatabaseConfiguration> Databases => Set<CoreDatabaseConfiguration>();
    public DbSet<CoreOperation> Operations => Set<CoreOperation>();
    public DbSet<CoreDocument> Documents => Set<CoreDocument>();
    public DbSet<CoreDocumentChunk> Chunks => Set<CoreDocumentChunk>();
    public DbSet<CoreChatSession> Sessions => Set<CoreChatSession>();
    public DbSet<CoreChatMessage> Messages => Set<CoreChatMessage>();
    public DbSet<CoreDemoUser> Users => Set<CoreDemoUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<CoreTenant>();
        tenant.ToTable("CoreTenants"); tenant.HasKey(x => x.TenantId);
        tenant.Property(x => x.TenantCode).HasMaxLength(30);
        tenant.Property(x => x.TenantName).HasMaxLength(200);
        tenant.Property(x => x.ProductName).HasMaxLength(200);
        tenant.Property(x => x.Description).HasMaxLength(2000);
        tenant.HasIndex(x => x.TenantCode).IsUnique();
        tenant.HasIndex(x => x.DatabaseConfigurationId).IsUnique();
        var database = modelBuilder.Entity<CoreDatabaseConfiguration>();
        database.ToTable("CoreDatabaseConfigurations"); database.HasKey(x => x.DatabaseConfigurationId);
        database.HasAlternateKey(x => new { x.TenantId, x.DatabaseConfigurationId });
        database.HasIndex(x => x.TenantId).IsUnique();
        database.Property(x => x.ConfigurationReference).HasMaxLength(200);
        database.Property(x => x.DatabaseName).HasMaxLength(128);
        database.HasOne<CoreTenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        tenant.HasOne<CoreDatabaseConfiguration>().WithMany().HasForeignKey(x => new { x.TenantId, x.DatabaseConfigurationId })
            .HasPrincipalKey(x => new { x.TenantId, x.DatabaseConfigurationId }).OnDelete(DeleteBehavior.Restrict);
        var operation = modelBuilder.Entity<CoreOperation>();
        operation.ToTable("CoreOperations"); operation.HasKey(x => x.OperationId);
        operation.Property(x => x.OperationName).HasMaxLength(100);
        operation.Property(x => x.Description).HasMaxLength(2000);
        operation.Property(x => x.Kind).HasMaxLength(20);
        operation.Property(x => x.BaseUrl).HasMaxLength(500);
        operation.Property(x => x.RelativeUrl).HasMaxLength(500);
        operation.Property(x => x.HttpMethod).HasMaxLength(10);
        operation.Property(x => x.StoredProcedureName).HasMaxLength(200);
        operation.HasIndex(x => new { x.TenantId, x.OperationName }).IsUnique();
        operation.HasOne<CoreTenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        var document = modelBuilder.Entity<CoreDocument>();
        document.ToTable("CoreDocuments"); document.HasKey(x => x.DocumentId);
        document.HasAlternateKey(x => new { x.TenantId, x.DocumentId });
        document.Property(x => x.FileName).HasMaxLength(500);
        document.Property(x => x.FileType).HasMaxLength(20);
        document.Property(x => x.FilePath).HasMaxLength(1000);
        document.HasOne<CoreTenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        var chunk = modelBuilder.Entity<CoreDocumentChunk>();
        chunk.ToTable("CoreDocumentChunks"); chunk.HasKey(x => x.ChunkId);
        chunk.HasIndex(x => new { x.TenantId, x.DocumentId, x.ChunkSequence }).IsUnique();
        chunk.HasOne<CoreDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId })
            .HasPrincipalKey(x => new { x.TenantId, x.DocumentId }).OnDelete(DeleteBehavior.Restrict);
        var session = modelBuilder.Entity<CoreChatSession>();
        session.ToTable("CoreChatSessions"); session.HasKey(x => x.SessionId);
        session.HasAlternateKey(x => new { x.TenantId, x.SessionId });
        session.HasOne<CoreTenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        var message = modelBuilder.Entity<CoreChatMessage>();
        message.ToTable("CoreChatMessages"); message.HasKey(x => x.MessageId);
        message.Property(x => x.Role).HasMaxLength(20);
        message.HasIndex(x => new { x.TenantId, x.SessionId, x.CreatedDate });
        message.HasOne<CoreChatSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.SessionId })
            .HasPrincipalKey(x => new { x.TenantId, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
        var user = modelBuilder.Entity<CoreDemoUser>();
        user.ToTable("CoreDemoUsers"); user.HasKey(x => x.UserId);
        user.Property(x => x.UserName).HasMaxLength(100);
        user.Property(x => x.DisplayName).HasMaxLength(200);
        user.Property(x => x.PasswordHash).HasMaxLength(100);
        user.Property(x => x.PasswordSalt).HasMaxLength(100);
        user.HasIndex(x => x.UserName).IsUnique();
        user.HasOne<CoreTenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CoreTenantGuard(AICoreDbContext db)
{
    public async Task<CoreTenant> RequireAsync(Guid tenantId, string? operation = null, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        if (tenant is null || !await db.Databases.AnyAsync(x => x.TenantId == tenantId && x.DatabaseConfigurationId == tenant.DatabaseConfigurationId && x.IsActive, cancellationToken))
            throw new AfterSalesAI.Application.TenantAccessException();
        if (operation is not null && !await db.Operations.AnyAsync(x => x.TenantId == tenantId && x.OperationName == operation && x.IsActive, cancellationToken))
            throw new AfterSalesAI.Application.TenantAccessException();
        if (operation?.StartsWith("wrapper-", StringComparison.Ordinal) == true && !tenant.IsWrapperApiEnabled)
            throw new AfterSalesAI.Application.TenantAccessException();
        return tenant;
    }
}
