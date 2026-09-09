using AfterSalesAI.Application;
using AfterSalesAI.Domain;

namespace AfterSalesAI.Infrastructure;

public sealed class InMemoryToolRegistry : IToolRegistry
{
    private readonly IReadOnlyCollection<ToolDefinition> _tools =
    [
        new ToolDefinition
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ApplicationId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            IntegrationSourceId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "get_order_status",
            Description = "Retrieve the current status of an order from an approved live system.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"orderId\":{\"type\":\"string\"}},\"required\":[\"orderId\"]}"
        },
        new ToolDefinition
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            ApplicationId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            IntegrationSourceId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "get_delivery_status",
            Description = "Retrieve the current delivery status for an order.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"orderId\":{\"type\":\"string\"}},\"required\":[\"orderId\"]}"
        },
        new ToolDefinition
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            ApplicationId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            IntegrationSourceId = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            Name = "search_sop",
            Description = "Search approved SOP and business process knowledge.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}"
        }
    ];

    public IReadOnlyCollection<ToolDefinition> GetAvailableTools(Guid tenantId, Guid applicationId) => _tools;
}
