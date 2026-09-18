using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class DemoAuthenticationService(AICoreDbContext db)
{
    public async Task<DemoAuthenticatedUser?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || userName.Length > 100 || password.Length > 200)
            return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == userName && x.IsActive, cancellationToken);
        if (user is null || !Verify(password, user.PasswordSalt, user.PasswordHash, user.PasswordIterations)) return null;
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == user.TenantId && x.IsActive, cancellationToken);
        return tenant is null ? null : new DemoAuthenticatedUser(user.UserId, user.UserName, user.DisplayName, tenant.TenantId, tenant.TenantName, tenant.ProductName);
    }

    public static ClaimsPrincipal Principal(DemoAuthenticatedUser user) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString("D")), new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.GivenName, user.DisplayName),
        new Claim("tenant_id", user.TenantId.ToString("D")), new Claim("tenant_name", user.TenantName),
        new Claim("product_name", user.ProductName)], "DemoCookie"));

    public static bool HasTenant(ClaimsPrincipal user, Guid tenantId) => Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var assigned) && assigned == tenantId;

    public static (string Salt, string Hash) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    private static bool Verify(string password, string salt, string hash, int iterations)
    {
        try
        {
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(hash));
        }
        catch (FormatException) { return false; }
    }
}

public sealed record DemoAuthenticatedUser(Guid UserId, string UserName, string DisplayName, Guid TenantId, string TenantName, string ProductName);
