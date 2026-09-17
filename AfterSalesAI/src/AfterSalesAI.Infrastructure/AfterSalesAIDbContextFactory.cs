using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AfterSalesAI.Infrastructure;

public sealed class AfterSalesAIDbContextFactory : IDesignTimeDbContextFactory<AfterSalesAIDbContext>
{
    public AfterSalesAIDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AfterSalesAI")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=AfterSalesAI_Demo;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AfterSalesAIDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AfterSalesAIDbContext(options);
    }
}
