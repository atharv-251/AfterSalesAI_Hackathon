using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AfterSalesAI.Application;

namespace AfterSalesAI.Infrastructure;

public sealed class TenantAssistantService(AssistantService tenant1Assistant, ITenant2DealerQueries tenant2,
    ITenant1DealerQueries tenant1, ILlmAnswerGenerator llm, CoreTenantGuard guard,
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
        var combinedQuestion = message.Contains("combined", StringComparison.OrdinalIgnoreCase)
            || message.Contains("both", StringComparison.OrdinalIgnoreCase)
            || mentionsTenant1 && mentionsTenant2;
        var targetTenant = mentionsTenant2 && !mentionsTenant1 ? DemoTenants.Tenant2 : DemoTenants.Tenant1;
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
        if (targetTenant == DemoTenants.Tenant1 && !combinedQuestion)
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
        await guard.RequireAsync(DemoTenants.Tenant2, "db-" + operation, cancellationToken);
        object? data;
        string source;
        if (combinedQuestion)
        {
            var tenant1Data = await tenant1.GetAsync(DemoTenants.Tenant1, dealerId, operation, cancellationToken);
            var tenant2Data = await tenant2.GetAsync(DemoTenants.Tenant2, dealerId, operation, cancellationToken: cancellationToken);
            data = new { DealerId = dealerId, Tenant1Data = tenant1Data, Tenant2Data = tenant2Data,
                Correlation = "DealerId correlates application accounts only; it does not transaction-match orders and repairs." };
            source = $"Approved multi-application data: Tenant 1 and Tenant 2 ({dealerId})";
        }
        else
        {
            source = $"Tenant 2 approved operation: {operation} ({dealerId})";
            data = await tenant2.GetAsync(DemoTenants.Tenant2, dealerId, operation, cancellationToken: cancellationToken);
        }
        if (data is null) return new AssistantResponse("No dealer record matched in the selected tenant.", "NO_MATCH", [], [operation]);
        var grounding = JsonSerializer.Serialize(data);
        var answer = llm.IsAvailable ? await llm.GenerateAsync(new LlmAnswerGenerationRequest(message,
            [new LlmGroundingSource(source, grounding)], LlmAnswerMode.OperationalResponse), cancellationToken) : null;
        return new AssistantResponse(string.IsNullOrWhiteSpace(answer) ? grounding : answer,
            combinedQuestion ? "MULTI_APPLICATION" : "TENANT2_DATA", [source], [combinedQuestion ? "multi_application_data" : operation]);
    }

    private static AssistantResponse Unavailable() => new("The approved wrapper service is unavailable. Please try again shortly.", "UNAVAILABLE", [], []);
}
