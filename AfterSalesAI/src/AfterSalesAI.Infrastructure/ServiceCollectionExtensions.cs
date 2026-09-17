using AfterSalesAI.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AfterSalesAI.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlServerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AfterSalesAI");
        services.Configure<LlmAasOptions>(options =>
        {
            options.BaseUrl = configuration["LlmAas:BaseUrl"] ?? options.BaseUrl;
            options.Model = configuration["LlmAas:Model"] ?? options.Model;
            options.TokenEndpoint = configuration["LlmAas:TokenEndpoint"] ?? options.TokenEndpoint;
            options.ClientId = configuration["LlmAas:ClientId"] ?? string.Empty;
            options.ClientSecret = configuration["LlmAas:ClientSecret"] ?? string.Empty;
            options.VirtualKey = configuration["LlmAas:VirtualKey"] ?? string.Empty;
            if (int.TryParse(configuration["LlmAas:RequestTimeoutSeconds"], out var requestTimeoutSeconds))
                options.RequestTimeoutSeconds = requestTimeoutSeconds;
            if (int.TryParse(configuration["LlmAas:MaxOutputTokens"], out var maxOutputTokens))
                options.MaxOutputTokens = maxOutputTokens;
        });
        services.AddHttpClient("LlmAasCloudIdp", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LlmAasOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });
        services.AddHttpClient<ILlmAnswerGenerator, LlmAasAnswerGenerator>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LlmAasOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<AfterSalesAIDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddScoped<IToolRegistry, SqlServerToolRegistry>();
            services.Configure<KnowledgeSourceOptions>(options =>
            {
                options.RootPath = configuration["KnowledgeSources:RootPath"] ?? string.Empty;
                if (int.TryParse(configuration["KnowledgeSources:ChunkSize"], out var chunkSize))
                    options.ChunkSize = chunkSize;
            });
            services.AddScoped<IKnowledgeIngestionService, SqlServerKnowledgeIngestionService>();
            services.AddScoped<IKnowledgeRetriever, SqlServerKnowledgeRetriever>();
            services.AddScoped<IKnowledgeLibrary, SqlServerKnowledgeLibrary>();
            services.AddScoped<IAfterSalesOperations, SqlServerAfterSalesOperations>();
            services.AddScoped<IDemoDatasetImportService, ExcelDemoDatasetImportService>();
        }
        else
        {
            services.AddSingleton<IToolRegistry, InMemoryToolRegistry>();
            services.AddSingleton<IAfterSalesOperations, DemoAfterSalesOperations>();
        }

        services.Configure<DemoDatasetOptions>(options =>
            options.WorkbookPath = configuration["DemoDataset:WorkbookPath"] ?? string.Empty);

        return services;
    }
}
