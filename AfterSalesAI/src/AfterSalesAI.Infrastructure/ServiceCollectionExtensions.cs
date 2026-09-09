using AfterSalesAI.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AfterSalesAI");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<AfterSalesAIDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));
            services.AddScoped<IToolRegistry, PostgresToolRegistry>();
        }
        else
        {
            services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
        }

        return services;
    }
}
