using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AfterSalesAI.Application;

namespace AfterSalesAI.Infrastructure;

public sealed class TenantAssistantService(AssistantService tenant1Assistant, Tenant1WrapperService tenant1Wrapper,
    ITenant1WrapperApiClient tenant1WrapperApi, ITenant2DealerQueries tenant2, ILlmAnswerGenerator llm, CoreTenantGuard guard,
    CoreKnowledgeRetriever documents, TenantLlmContext llmContext)
{
    public async Task<AssistantResponse> HandleAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        if (request.TenantId != DemoTenants.Tenant1 && request.TenantId != DemoTenants.Tenant2)
            throw new TenantAccessException();
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
            throw new ArgumentException("Message must contain 1-4000 characters.");
        var signedInTenant = request.TenantId;
        var signedInApplication = Guid.Parse(signedInTenant == DemoTenants.Tenant1 ? "20000000-0000-0000-0000-000000000001" : "20000000-0000-0000-0000-000000000002");
        if (request.ApplicationId.HasValue && request.ApplicationId != signedInApplication)
            throw new TenantAccessException();
        var message = request.Message;
        await guard.RequireAsync(signedInTenant, cancellationToken: cancellationToken);
        var mentionsTenant2 = new[] { "tenant 2", "tenant2", "repair", "workshop", "warranty", "vehicle", "service" }
            .Any(x => message.Contains(x, StringComparison.OrdinalIgnoreCase));
        var mentionsTenant1 = new[] { "tenant 1", "tenant1", "order", "shipment", "delivery", "inventory", "part", "claim" }
            .Any(x => message.Contains(x, StringComparison.OrdinalIgnoreCase));
        var linkedQuestion = message.Contains("combined", StringComparison.OrdinalIgnoreCase)
            || message.Contains("both", StringComparison.OrdinalIgnoreCase)
            || message.Contains("linked", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cross tenant", StringComparison.OrdinalIgnoreCase)
            || signedInTenant == DemoTenants.Tenant1 && mentionsTenant2
            || signedInTenant == DemoTenants.Tenant2 && mentionsTenant1;
        var targetTenant = signedInTenant;
        var targetApplication = targetTenant == DemoTenants.Tenant1
            ? Guid.Parse("20000000-0000-0000-0000-000000000001") : Guid.Parse("20000000-0000-0000-0000-000000000002");
        llmContext.SystemPrompt = (await guard.RequireAsync(targetTenant, cancellationToken: cancellationToken)).TenantSystemPrompt;
        var documentQuestion = new[] { "procedure", "process", "policy", "document", "how to", "sop", "workflow" }
            .Any(x => message.Contains(x, StringComparison.OrdinalIgnoreCase));
        if (documentQuestion)
        {
            var matches = await documents.SearchAsync(targetTenant, targetApplication, message, cancellationToken: cancellationToken);
            if (matches.Count > 0)
            {
                var generated = llm.IsAvailable ? await llm.GenerateAsync(new LlmAnswerGenerationRequest(message,
                    matches.Select(x => new LlmGroundingSource(x.DocumentName, x.Content)).ToArray()), cancellationToken) : null;
                return new AssistantResponse(string.IsNullOrWhiteSpace(generated) ? string.Join("\n\n", matches.Select(x => x.Content)) : generated,
                    "KNOWLEDGE", matches.Select(x => x.DocumentName).Distinct().ToArray(), ["documents"]);
            }
            if (targetTenant == DemoTenants.Tenant2) return new AssistantResponse("No matching Tenant 2 document was found.", "NO_MATCH", [], ["documents"]);
        }
        if (targetTenant == DemoTenants.Tenant1 && !linkedQuestion)
        {
            await guard.RequireAsync(targetTenant, "local", cancellationToken);
            return await tenant1Assistant.HandleAsync(request with { TenantId = targetTenant, ApplicationId = targetApplication }, cancellationToken);
        }
        var keys = Regex.Matches(message.ToUpperInvariant(), @"\b(?:D\d{3}|T2ONLY-\d{3})\b")
            .Select(x => x.Value).Distinct().ToArray();
        if (keys.Length != 1)
            return new AssistantResponse("Please specify one dealer key, for example D001, D002, D004 or T2ONLY-001.", "NEEDS_DEALER", [], []);
        var dealerId = keys[0];
        var operation = message.Contains("warranty", StringComparison.OrdinalIgnoreCase) || message.Contains("claim", StringComparison.OrdinalIgnoreCase)
            ? DealerOperations.Warranty : new[] { "status", "overdue", "readiness", "awaiting", "delay" }.Any(x => message.Contains(x, StringComparison.OrdinalIgnoreCase))
                ? DealerOperations.Status : DealerOperations.Overview;
        if (linkedQuestion)
        {
            DealerWrapperResponse? integration;
            if (signedInTenant == DemoTenants.Tenant1)
            {
                await guard.RequireAsync(DemoTenants.Tenant1, "wrapper-" + operation, cancellationToken);
                integration = await tenant1Wrapper.GetAsync(DemoTenants.Tenant1, dealerId, operation, cancellationToken);
            }
            else
            {
                await guard.RequireAsync(DemoTenants.Tenant2, "db-" + operation, cancellationToken);
                integration = (await tenant1WrapperApi.GetAsync(dealerId, operation, cancellationToken)).Data;
            }
            if (integration is null) return new AssistantResponse("No linked dealer record matched in the approved integration.", "NO_MATCH", [], ["multi_application_data"]);
            var linkedSource = $"Approved linked Tenant 1 and Tenant 2 data ({dealerId})";
            var linkedGrounding = CreateLinkedGrounding(integration, operation);
            var linkedAnswer = llm.IsAvailable ? await llm.GenerateAsync(new LlmAnswerGenerationRequest(message,
                [new LlmGroundingSource(linkedSource, linkedGrounding)], LlmAnswerMode.OperationalResponse,
                AssistantIntent.StatusOrException, ResponseLength.Concise), cancellationToken) : null;
            return new AssistantResponse(string.IsNullOrWhiteSpace(linkedAnswer)
                    ? "I couldn't generate the linked-data answer right now. Please try again shortly."
                    : linkedAnswer,
                "MULTI_APPLICATION", [linkedSource], ["multi_application_data"]);
        }
        await guard.RequireAsync(DemoTenants.Tenant2, "db-" + operation, cancellationToken);
        var source = $"Tenant 2 approved operation: {operation} ({dealerId})";
        var data = await tenant2.GetAsync(DemoTenants.Tenant2, dealerId, operation, cancellationToken: cancellationToken);
        if (data is null) return new AssistantResponse("No dealer record matched in the selected tenant.", "NO_MATCH", [], [operation]);
        var grounding = JsonSerializer.Serialize(data);
        var answer = llm.IsAvailable ? await llm.GenerateAsync(new LlmAnswerGenerationRequest(message,
            [new LlmGroundingSource(source, grounding)], LlmAnswerMode.OperationalResponse), cancellationToken) : null;
        return new AssistantResponse(string.IsNullOrWhiteSpace(answer) ? grounding : answer,
            "TENANT2_DATA", [source], [operation]);
    }

    private static AssistantResponse Unavailable() => new("The approved wrapper service is unavailable. Please try again shortly.", "UNAVAILABLE", [], []);

    private static string CreateLinkedGrounding(DealerWrapperResponse integration, string operation)
    {
        var tenant2 = integration.Tenant2Data;
        return JsonSerializer.Serialize(new
        {
            DealerId = integration.DealerId,
            integration.IntegrationStatus,
            Tenant1 = new
            {
                integration.Tenant1Data.DealerName,
                integration.Tenant1Data.Status,
                Orders = operation == DealerOperations.Warranty ? [] : integration.Tenant1Data.Orders.Take(5).Select(order => new
                {
                    order.OrderNumber, order.PartNumber, order.Quantity, order.Status, order.RequestedDeliveryDate
                }),
                Shipments = operation == DealerOperations.Warranty ? [] : integration.Tenant1Data.Shipments.Take(5).Select(shipment => new
                {
                    shipment.ShipmentId, shipment.OrderNumber, shipment.Status, shipment.EstimatedDeliveryDate, shipment.ActualDeliveryDate
                }),
                Claims = operation == DealerOperations.Warranty ? integration.Tenant1Data.Claims.Take(5) : []
            },
            Tenant2 = tenant2 is null ? null : new
            {
                tenant2.EvaluationDate,
                Repairs = operation == DealerOperations.Warranty ? [] : tenant2.Repairs
                    .Where(repair => repair.Status != "Completed" && repair.Status != "Cancelled").Take(8).Select(repair => new
                    {
                        repair.RepairOrderNumber, repair.Complaint, repair.Status, repair.Priority, repair.PromisedDate, repair.IsOverdue
                    }),
                Events = operation == DealerOperations.Status ? tenant2.Events.Take(5) : [],
                WarrantySummary = operation == DealerOperations.Warranty ? tenant2.WarrantySummary : null,
                WarrantyCases = operation == DealerOperations.Warranty ? tenant2.WarrantyCases.Take(5) : []
            },
            Correlation = "DealerId only. Do not infer order-to-repair matches."
        });
    }
}
