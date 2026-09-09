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

builder.Services.AddSingleton<AssistantService>();
builder.Services.AddPostgresInfrastructure(builder.Configuration);

var app = builder.Build();

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
