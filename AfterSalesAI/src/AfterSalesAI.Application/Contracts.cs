using AfterSalesAI.Domain;

namespace AfterSalesAI.Application;

public interface IToolRegistry
{
    IReadOnlyCollection<ToolDefinition> GetAvailableTools(Guid tenantId, Guid applicationId);
}

public sealed record AssistantRequest(
    Guid TenantId,
    Guid? ApplicationId,
    string Message);

public sealed record AssistantResponse(
    string Answer,
    string Decision,
    IReadOnlyCollection<string> Sources);
