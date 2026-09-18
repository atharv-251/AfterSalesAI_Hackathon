using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "AfterSalesAI.Demo";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().Build());
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
              .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                  && uri.Host is "localhost" or "127.0.0.1");
    });
});

builder.Services.AddScoped<AssistantService>();
builder.Services.AddSqlServerInfrastructure(builder.Configuration);

await using var app = builder.Build();

if (builder.Configuration.GetValue<bool>("copy-legacy-knowledge"))
{
    using var scope = app.Services.CreateScope();
    var count = await scope.ServiceProvider.GetRequiredService<LegacyKnowledgeTransfer>().CopyAsync();
    app.Logger.LogInformation("Copied {Count} previously untransferred Tenant 1 knowledge documents into Core. Originals were preserved.", count);
    return;
}

if (builder.Configuration.GetValue<bool>("ingest-knowledge"))
{
    if (!Guid.TryParse(builder.Configuration["tenant-id"], out var tenantId) || tenantId == Guid.Empty
        || !Guid.TryParse(builder.Configuration["application-id"], out var applicationId) || applicationId == Guid.Empty
        || string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("AfterSalesAI")))
    {
        app.Logger.LogError("Offline ingestion requires --tenant-id, --application-id and a SQL Server connection string.");
        Environment.ExitCode = 1;
        return;
    }
    try
    {
        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IKnowledgeIngestionService>().IngestAsync(tenantId, applicationId);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
        if (result.Failures.Count > 0) Environment.ExitCode = 1;
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Offline knowledge ingestion failed.");
        Environment.ExitCode = 1;
    }
    return;
}

var connectionString = builder.Configuration.GetConnectionString("AfterSalesAI");
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var databases = new List<(string Name, DbContext Context)>
    {
        ("AI Core", scope.ServiceProvider.GetRequiredService<AICoreDbContext>()),
        ("Tenant 2", scope.ServiceProvider.GetRequiredService<Tenant2DbContext>())
    };
    if (!string.IsNullOrWhiteSpace(connectionString))
        databases.Add(("AfterSalesAI", scope.ServiceProvider.GetRequiredService<AfterSalesAIDbContext>()));

    foreach (var (name, database) in databases)
    {
        try
        {
            if (await database.Database.CanConnectAsync())
                logger.LogInformation("Connected to {DatabaseName} database.", name);
            else
                logger.LogError("Could not connect to {DatabaseName} database.", name);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not connect to {DatabaseName} database.", name);
        }
    }
}
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var coreDatabase = scope.ServiceProvider.GetRequiredService<AICoreDbContext>().Database;
        await coreDatabase.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<DemoUserSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<CoreTenantKnowledgeBootstrapper>().SeedTenant2Async();
        logger.LogInformation("AI Core database initialization and demo-user seeding completed.");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "AI Core database initialization failed; the API will continue, but Core-backed requests may fail until SQL Server is available.");
    }
}
if (!string.IsNullOrWhiteSpace(connectionString))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await scope.ServiceProvider.GetRequiredService<AfterSalesAIDbContext>().Database.MigrateAsync();
        logger.LogInformation("AfterSalesAI database migrations completed.");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "AfterSalesAI database migrations failed; the API will continue, but database-backed registry requests may fail until the database is available.");
    }
}
else
{
    app.Logger.LogWarning("No AfterSalesAI connection string is configured; using the in-memory tool registry.");
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    try
    {
        var path = context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant() ?? string.Empty;
        if (path == "/api/knowledge/ingest")
        {
            await Results.Problem(statusCode: 410, detail: "Use the tenant-scoped Core document upload endpoint.").ExecuteAsync(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated == true && path.StartsWith("/api/", StringComparison.Ordinal)
            && !path.StartsWith("/api/auth/", StringComparison.Ordinal) && path != "/api/health")
        {
            var assigned = context.User.FindFirst("tenant_id")?.Value;
            if (!Guid.TryParse(assigned, out var assignedTenant)) throw new TenantAccessException();
            if (Guid.TryParse(context.Request.Query["tenantId"], out var requestedTenant) && requestedTenant != assignedTenant)
                throw new TenantAccessException();
        }
        if (path is "/api/dashboard" or "/api/orders" or "/api/claims" or "/api/inventory" or "/api/demo-data/import" or "/api/knowledge/ingest" or "/api/knowledge/documents"
            || path.StartsWith("/api/orders/", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(context.Request.Query["tenantId"], out var tenantId))
                throw new ArgumentException("TenantId is required.");
            DemoTenants.Require(tenantId, DemoTenants.Tenant1);
            await context.RequestServices.GetRequiredService<CoreTenantGuard>().RequireAsync(tenantId,
                path.StartsWith("/api/knowledge/", StringComparison.Ordinal) ? "documents" : "local", context.RequestAborted);
        }
        await next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        context.Response.StatusCode = 499;
    }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        var status = exception is TenantAccessException ? 403 : exception is ArgumentException ? 400 : 503;
        var detail = status == 403 ? "The operation is not assigned to the selected tenant."
            : status == 400 ? "The request contains an invalid parameter."
            : "The requested service is temporarily unavailable.";
        app.Logger.LogWarning("Request failed with status {StatusCode}; error type {ErrorType}; trace {TraceId}.",
            status, exception.GetType().Name, context.TraceIdentifier);
        context.Response.Clear();
        await Results.Problem(statusCode: status, detail: detail).ExecuteAsync(context);
    }
});
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    service = "AfterSalesAI.Api",
    timestampUtc = DateTimeOffset.UtcNow
})).AllowAnonymous();

    app.Run();

public partial class Program { }
