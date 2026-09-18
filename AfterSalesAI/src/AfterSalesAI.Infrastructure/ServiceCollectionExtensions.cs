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
        var coreConnection = configuration.GetConnectionString("AICore")
            ?? throw new InvalidOperationException("ConnectionStrings:AICore is required.");
        if (!new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(coreConnection).InitialCatalog.Equals("MultiTenantAICoreDb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI Core must use MultiTenantAICoreDb.");
        services.AddDbContext<AICoreDbContext>(options => options.UseSqlServer(coreConnection,
            sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
        services.AddScoped<CoreTenantGuard>();
        services.AddScoped<CoreChatService>();
        services.AddScoped<DemoAuthenticationService>();
        services.AddScoped<DemoUserSeeder>();
        services.AddScoped<CoreTenantKnowledgeBootstrapper>();
        services.AddScoped<CoreKnowledgeRetriever>();
        services.AddScoped<LegacyKnowledgeTransfer>();
        services.AddScoped<TenantLlmContext>();
        var tenant2Connection = configuration.GetConnectionString("Tenant2")
            ?? throw new InvalidOperationException("ConnectionStrings:Tenant2 is required.");
        var tenant2Target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(tenant2Connection);
        if (!tenant2Target.InitialCatalog.Equals("Tenant2DemoDb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Tenant 2 must use Tenant2DemoDb.");
        if (!string.IsNullOrWhiteSpace(connectionString)
            && new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog.Equals("Tenant2DemoDb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Tenant 1 cannot use the Tenant 2 database.");
        services.AddDbContext<Tenant2DbContext>(options => options.UseSqlServer(tenant2Connection,
            sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
        services.AddScoped<ITenant2DealerQueries, Tenant2DealerQueries>();
        services.AddScoped<ITenant1DealerQueries, Tenant1DealerQueries>();
        services.AddScoped<Tenant1WrapperService>();
        services.AddScoped<TenantAssistantService>();
        services.Configure<ServiceIntegrationOptions>(options =>
            options.ApiKey = configuration["ServiceIntegration:ApiKey"] ?? string.Empty);
        services.AddScoped<ServiceIntegrationAuthorizer>();
        var wrapperUrl = configuration["Tenant1WrapperApi:BaseUrl"] ?? "http://127.0.0.1:5088/";
        if (!Uri.TryCreate(wrapperUrl, UriKind.Absolute, out var wrapperUri)
            || wrapperUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(wrapperUri.UserInfo)
            || !string.IsNullOrEmpty(wrapperUri.Query) || !string.IsNullOrEmpty(wrapperUri.Fragment))
            throw new InvalidOperationException("Tenant1WrapperApi:BaseUrl must be a server-configured HTTP address.");
        services.AddHttpClient("Tenant1Wrapper", client =>
        {
            client.BaseAddress = new Uri(wrapperUri.AbsoluteUri.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<ITenant1WrapperApiClient, Tenant1WrapperApiClient>((serviceProvider, client) =>
        {
            var key = serviceProvider.GetRequiredService<IOptions<ServiceIntegrationOptions>>().Value.ApiKey;
            client.BaseAddress = new Uri(wrapperUri.AbsoluteUri.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add(ServiceIntegrationOptions.HeaderName, key);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        var tenant2BaseUrl = configuration["Tenant2Api:BaseUrl"] ?? "http://127.0.0.1:5088/";
        if (!Uri.TryCreate(tenant2BaseUrl, UriKind.Absolute, out var tenant2Uri)
            || tenant2Uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(tenant2Uri.UserInfo)
            || !string.IsNullOrEmpty(tenant2Uri.Query) || !string.IsNullOrEmpty(tenant2Uri.Fragment))
            throw new InvalidOperationException("Tenant2Api:BaseUrl must be a server-configured HTTP address without credentials, query or fragment.");
        services.AddHttpClient<ITenant2ApiClient, Tenant2ApiClient>((serviceProvider, client) =>
        {
            var key = serviceProvider.GetRequiredService<IOptions<ServiceIntegrationOptions>>().Value.ApiKey;
            client.BaseAddress = new Uri(tenant2Uri.AbsoluteUri.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add(ServiceIntegrationOptions.HeaderName, key);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
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
                options.UseSqlServer(connectionString,
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
            services.AddScoped<IToolRegistry, SqlServerToolRegistry>();
            services.Configure<KnowledgeSourceOptions>(options =>
            {
                options.RootPath = configuration["KnowledgeSources:RootPath"] ?? string.Empty;
                if (int.TryParse(configuration["KnowledgeSources:ChunkSize"], out var chunkSize))
                    options.ChunkSize = chunkSize;
            });
            services.AddScoped<IKnowledgeIngestionService, SqlServerKnowledgeIngestionService>();
            services.AddScoped<IKnowledgeRetriever, CoreKnowledgeRetriever>();
            services.AddScoped<IKnowledgeLibrary, CoreKnowledgeLibrary>();
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
