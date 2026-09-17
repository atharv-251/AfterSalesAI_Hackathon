using AfterSalesAI.Domain;
using System.Text.RegularExpressions;

namespace AfterSalesAI.Application;

public interface IToolRegistry
{
    IReadOnlyCollection<ToolDefinition> GetAvailableTools(Guid tenantId, Guid applicationId);
}

public interface IKnowledgeVectorStore
{
    Task UpsertAsync(KnowledgeVectorRecord vector, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeVectorSearchResult>> SearchAsync(
        Guid tenantId,
        Guid applicationId,
        ReadOnlyMemory<float> embedding,
        int maximumResults,
        CancellationToken cancellationToken = default);
}

public sealed record KnowledgeVectorRecord(Guid KnowledgeChunkId, ReadOnlyMemory<float> Embedding);

public sealed record KnowledgeVectorSearchResult(Guid KnowledgeChunkId, float Distance);

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> IngestAsync(
        Guid tenantId,
        Guid applicationId,
        CancellationToken cancellationToken = default);
}

public interface IKnowledgeLibrary
{
    Task<IReadOnlyCollection<KnowledgeLibraryDocument>> GetDocumentsAsync(
        Guid tenantId,
        Guid applicationId,
        CancellationToken cancellationToken = default);
}

public sealed record KnowledgeLibraryDocument(Guid Id, string Title, string Category, string SourcePath, string Content, string Summary);

public interface IKnowledgeRetriever
{
    Task<IReadOnlyCollection<KnowledgeSearchResult>> SearchAsync(
        Guid tenantId,
        Guid applicationId,
        string query,
        int maximumResults = 5,
        CancellationToken cancellationToken = default);
}

public sealed record KnowledgeIngestionResult(int FilesDiscovered, int DocumentsAdded, int DocumentsUpdated, int ChunksCreated, IReadOnlyCollection<string> Failures);

public sealed record KnowledgeSearchResult(string DocumentName, string SourcePath, string Content, int ChunkIndex, int Score);

public sealed record AssistantRequest(
    Guid TenantId,
    Guid? ApplicationId,
    string Message);

public sealed record AssistantResponse(
    string Answer,
    string Decision,
    IReadOnlyCollection<string> Sources,
    IReadOnlyCollection<string>? ToolsExecuted = null);

public sealed record OperationalSearchRecord(string Category, string Source, string Content);

public sealed record OperationalSearchResult(IReadOnlyCollection<OperationalSearchRecord> Records);

public static class OperationalSearchMatcher
{
    private static readonly Regex PurchaseOrderPattern = new(@"\bPO-\d{4}-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PartNumberPattern = new(@"\bP-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlySet<string> ExtractPurchaseOrderIds(string query) => PurchaseOrderPattern.Matches(query)
        .Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> ExtractPartNumbers(string query) => PartNumberPattern.Matches(query)
        .Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool Matches(string query, params string?[] values)
    {
        var candidates = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.Trim('.', ',', '?', '!', ':', ';', '\"', '\''))
            .Where(word => word.Length >= 3 || word.Any(char.IsDigit)).ToArray();

        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Any(value =>
        {
            var normalizedValue = value!.Trim();
            for (var start = 0; start < candidates.Length; start++)
            {
                var candidate = string.Empty;
                for (var length = 0; length < Math.Min(6, candidates.Length - start); length++)
                {
                    candidate = string.IsNullOrEmpty(candidate) ? candidates[start + length] : $"{candidate} {candidates[start + length]}";
                    if (normalizedValue.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                        || candidate.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        });
    }
}

public interface IAfterSalesOperations
{
    IReadOnlyCollection<DemoOrder> GetOrders();
    IReadOnlyCollection<DemoClaim> GetClaims();
    IReadOnlyCollection<DemoPartStock> GetInventory();
    DemoDashboard GetDashboard();
    OperationalSearchResult Search(string query);
}

public sealed record DemoOrder(
    string OrderId,
    string Customer,
    string Status,
    string DeliveryStatus,
    DateOnly ExpectedDelivery,
    string PartNumber,
    string Alert,
    string? Carrier = null,
    string? TrackingNumber = null,
    DateOnly? ShipmentEstimatedDelivery = null);
public sealed record DemoClaim(string ClaimId, string OrderId, string Status, string Reason, DateOnly CreatedOn);
public sealed record DemoPartStock(string PartNumber, string Description, string Plant, int AvailableQuantity, int ReorderLevel);
public sealed record DemoDashboard(int OpenOrders, int OrdersInDelivery, int DeliveryExceptions, int OpenClaims, int PartsInStock, int LowStockParts, IReadOnlyCollection<string> RecentAlerts);

public interface ILlmAnswerGenerator
{
    bool IsAvailable { get; }

    Task<string?> GenerateAsync(
        LlmAnswerGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public enum LlmAnswerMode
{
    TheoreticalGuidance,
    DocumentSummary,
    OperationalResponse
}

public enum AssistantIntent
{
    Factual,
    EntityLookup,
    StatusOrException,
    Comparison,
    ProcessGuidance
}

public enum ResponseLength
{
    Concise,
    Medium,
    Detailed
}

public sealed record LlmAnswerGenerationRequest(
    string UserMessage,
    IReadOnlyCollection<LlmGroundingSource> Sources,
    LlmAnswerMode Mode = LlmAnswerMode.TheoreticalGuidance,
    AssistantIntent Intent = AssistantIntent.Factual,
    ResponseLength ResponseLength = ResponseLength.Medium,
    int TotalRecordsFound = 0);

public sealed record LlmGroundingSource(string Name, string Content);

public interface IDemoDatasetImportService
{
    Task<DemoDatasetImportResult> ImportAsync(CancellationToken cancellationToken = default);
}

public sealed record DemoDatasetImportResult(IReadOnlyDictionary<string, int> ImportedRows);
