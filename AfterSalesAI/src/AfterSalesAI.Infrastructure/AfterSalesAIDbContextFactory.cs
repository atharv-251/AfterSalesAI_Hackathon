using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class AfterSalesAIDbContextFactory : IDesignTimeDbContextFactory<AfterSalesAIDbContext>
{
    public AfterSalesAIDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AfterSalesAI")
            ?? "Host=localhost;Database=aftersalesai;Username=aftersales";

        var options = new DbContextOptionsBuilder<AfterSalesAIDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new AfterSalesAIDbContext(options);
    }
}
