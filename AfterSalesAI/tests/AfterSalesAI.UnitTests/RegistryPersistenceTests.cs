using AfterSalesAI.Domain;
using AfterSalesAI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AfterSalesApplication = AfterSalesAI.Domain.Application;

namespace AfterSalesAI.UnitTests;

public sealed class RegistryPersistenceTests
{
    [Fact]
    public void Model_CreatesTenantApplicationAndIntegrationRelationships()
    {
        using var dbContext = CreateDbContext();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A", Region = "EU" };
        var application = new AfterSalesApplication
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Application A",
            Description = "Test application"
        };
        var integrationSource = new IntegrationSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ApplicationId = application.Id,
            Type = IntegrationSourceType.Api,
            Protocol = "REST",
            Name = "API A",
            ConfigurationReference = "IntegrationSources__ApiA",
            IsReadOnly = true
        };

        dbContext.AddRange(tenant, application, integrationSource);
        dbContext.SaveChanges();

        var storedApplication = dbContext.Applications.Include(item => item.Tenant).Single();
        var storedIntegration = dbContext.IntegrationSources.Include(item => item.Application).Single();

        Assert.Equal(tenant.Id, storedApplication.Tenant.Id);
        Assert.Equal(application.Id, storedIntegration.Application.Id);
        Assert.True(storedIntegration.IsReadOnly);
    }

    [Fact]
    public void ToolRegistry_ReturnsOnlyActiveToolsForRequestedTenantAndApplication()
    {
        using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var activeSource = new IntegrationSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Type = IntegrationSourceType.Api,
            Protocol = "REST",
            Name = "Active API",
            ConfigurationReference = "IntegrationSources__ActiveApi",
            IsReadOnly = true,
            IsActive = true
        };
        var inactiveSource = new IntegrationSource
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Type = IntegrationSourceType.Api,
            Protocol = "REST",
            Name = "Inactive API",
            ConfigurationReference = "IntegrationSources__InactiveApi",
            IsReadOnly = true,
            IsActive = false
        };

        dbContext.AddRange(
            activeSource,
            inactiveSource,
            CreateTool(tenantId, applicationId, activeSource, "included", true),
            CreateTool(tenantId, applicationId, activeSource, "inactive-tool", false),
            CreateTool(tenantId, applicationId, inactiveSource, "inactive-source", true),
            CreateTool(Guid.NewGuid(), applicationId, activeSource, "other-tenant", true));
        dbContext.SaveChanges();

        var tools = new SqlServerToolRegistry(dbContext).GetAvailableTools(tenantId, applicationId);

        var tool = Assert.Single(tools);
        Assert.Equal("included", tool.Name);
    }

    private static AfterSalesAIDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AfterSalesAIDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AfterSalesAIDbContext(options);
    }

    private static ToolDefinition CreateTool(
        Guid tenantId,
        Guid applicationId,
        IntegrationSource integrationSource,
        string name,
        bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ApplicationId = applicationId,
        IntegrationSourceId = integrationSource.Id,
        IntegrationSource = integrationSource,
        Name = name,
        Description = "Test tool",
        IsActive = isActive
    };
}
