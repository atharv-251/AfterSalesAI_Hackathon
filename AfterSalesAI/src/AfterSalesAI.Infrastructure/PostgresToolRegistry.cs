using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class PostgresToolRegistry(AfterSalesAIDbContext dbContext) : IToolRegistry
{
    public IReadOnlyCollection<ToolDefinition> GetAvailableTools(Guid tenantId, Guid applicationId) =>
        dbContext.ToolDefinitions
            .AsNoTracking()
            .Where(tool => tool.TenantId == tenantId
                && tool.ApplicationId == applicationId
                && tool.IsActive
                && tool.IntegrationSource.IsActive)
            .OrderBy(tool => tool.Name)
            .ToArray();
}
