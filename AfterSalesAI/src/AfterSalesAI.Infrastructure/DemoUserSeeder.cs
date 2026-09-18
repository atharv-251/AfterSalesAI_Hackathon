using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class DemoUserSeeder(AICoreDbContext db)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedAsync("tenant1.demo", "Tenant 1 Demo", AfterSalesAI.Application.DemoTenants.Tenant1, "Tenant1Demo!", cancellationToken);
        await SeedAsync("tenant2.demo", "Tenant 2 Demo", AfterSalesAI.Application.DemoTenants.Tenant2, "Tenant2Demo!", cancellationToken);
    }

    private async Task SeedAsync(string userName, string displayName, Guid tenantId, string password, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.UserName == userName, cancellationToken)) return;
        var credential = DemoAuthenticationService.Hash(password);
        db.Users.Add(new CoreDemoUser { UserId = Guid.NewGuid(), TenantId = tenantId, UserName = userName,
            DisplayName = displayName, PasswordSalt = credential.Salt, PasswordHash = credential.Hash,
            PasswordIterations = 100_000, IsActive = true, CreatedDate = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }
}
