namespace AfterSalesAI.Application;

public sealed class AssistantService
{
    public Task<AssistantResponse> HandleAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.", nameof(request.Message));

        // Deliberately deterministic in the scaffold. The real implementation will call
        // the LLMaaS orchestrator once the tool registry and RAG pipeline are wired.
        var decision = request.Message.Contains("how", StringComparison.OrdinalIgnoreCase)
            ? "RAG"
            : "API_OR_HYBRID_PENDING";

        var response = new AssistantResponse(
            "Project scaffold is running. The AI orchestrator is the next implementation step.",
            decision,
            Array.Empty<string>());

        return Task.FromResult(response);
    }
}
