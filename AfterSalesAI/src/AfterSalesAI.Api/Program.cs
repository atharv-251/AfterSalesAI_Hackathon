using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

builder.Services.AddScoped<AssistantService>();
builder.Services.AddSqlServerInfrastructure(builder.Configuration);

await using var app = builder.Build();

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
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    service = "AfterSalesAI.Api",
    timestampUtc = DateTimeOffset.UtcNow
}));

app.Run();

public partial class Program { }
