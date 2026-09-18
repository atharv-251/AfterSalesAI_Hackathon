using System.Text.RegularExpressions;

namespace AfterSalesAI.Application;

public static class DemoTenants
{
    public static readonly Guid Tenant1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Tenant2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static void Require(Guid actual, Guid expected)
    {
        if (actual != expected) throw new TenantAccessException();
    }

    public static void ValidateDealerId(string dealerId)
    {
        if (string.IsNullOrEmpty(dealerId) || !Regex.IsMatch(dealerId, "\\A[A-Z0-9-]{1,50}\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("DealerId must contain 1-50 uppercase letters, digits or hyphens.");
    }
}

public sealed class TenantAccessException : Exception
{
    public TenantAccessException() : base("The operation is not assigned to the selected tenant.") { }
}

public static class DealerOperations
{
    public const string Overview = "service-overview";
    public const string Status = "repair-status";
    public const string Warranty = "warranty-summary";

    public static void Validate(string operation)
    {
        if (operation is not (Overview or Status or Warranty))
            throw new ArgumentException("The operation is not approved.");
    }
}

public sealed record ServiceDealerDto(string DealerId, string DealerName, bool IsActive);
public sealed record RepairDto(string RepairOrderNumber, string VehicleReference, string ModelName,
    string Complaint, string Status, string Priority, DateOnly OpenedDate, DateOnly PromisedDate,
    DateOnly? ClosedDate, decimal EstimatedAmountEur, bool IsOverdue);
public sealed record RepairLineDto(string RepairOrderNumber, short LineNumber, string OperationCode,
    string Description, decimal LabourHours, decimal LabourRateEur, decimal MaterialsAmountEur,
    decimal LineTotalEur, string Status);
public sealed record RepairEventDto(string RepairOrderNumber, short EventSequence, DateTime OccurredUtc,
    string Status, string PublicNote);
public sealed record WarrantyDto(string RepairOrderNumber, string CaseNumber, string Status,
    string Reason, DateOnly SubmittedDate, DateOnly? DecisionDate, decimal ClaimedAmountEur, decimal ApprovedAmountEur);
public sealed record WarrantySummaryDto(int TotalCases, int PendingCases, decimal ClaimedAmountEur, decimal ApprovedAmountEur);
public sealed record Tenant2DealerResponse(Guid TenantId, string DealerId, string Operation,
    ServiceDealerDto Dealer, DateOnly EvaluationDate, IReadOnlyList<RepairDto> Repairs,
    IReadOnlyList<RepairLineDto> Lines, IReadOnlyList<RepairEventDto> Events,
    IReadOnlyList<WarrantyDto> WarrantyCases, WarrantySummaryDto? WarrantySummary);

public sealed record DealerOrderDto(string OrderNumber, string LineNumber, string PartNumber, int Quantity,
    string Status, DateOnly RequestedDeliveryDate);
public sealed record DealerShipmentDto(string ShipmentId, string OrderNumber, string Status,
    DateOnly EstimatedDeliveryDate, DateOnly? ActualDeliveryDate);
public sealed record DealerClaimDto(string ClaimId, string OrderNumber, string PartNumber,
    string Status, string Reason, decimal ClaimAmountEur);
public sealed record Tenant1DealerData(string DealerId, string DealerName, string Status,
    IReadOnlyList<DealerOrderDto> Orders, IReadOnlyList<DealerShipmentDto> Shipments, IReadOnlyList<DealerClaimDto> Claims);
public sealed record Tenant2FetchResult(string Status, Tenant2DealerResponse? Data);
public sealed record DealerWrapperResponse(Guid TenantId, string DealerId, string IntegrationStatus,
    Tenant1DealerData Tenant1Data, Tenant2DealerResponse? Tenant2Data, string CombinedSummary);

public interface ITenant2DealerQueries
{
    Task<Tenant2DealerResponse?> GetAsync(Guid tenantId, string dealerId, string operation,
        DateOnly? evaluationDate = null, CancellationToken cancellationToken = default);
}

public interface ITenant1DealerQueries
{
    Task<Tenant1DealerData?> GetAsync(Guid tenantId, string dealerId, string operation, CancellationToken cancellationToken = default);
}

public interface ITenant2ApiClient
{
    Task<Tenant2FetchResult> GetAsync(string dealerId, string operation, CancellationToken cancellationToken = default);
}

public sealed record Tenant1WrapperFetchResult(string Status, DealerWrapperResponse? Data);

public interface ITenant1WrapperApiClient
{
    Task<Tenant1WrapperFetchResult> GetAsync(string dealerId, string operation, CancellationToken cancellationToken = default);
}

public sealed class Tenant1WrapperService(ITenant1DealerQueries tenant1, ITenant2ApiClient tenant2)
{
    public async Task<DealerWrapperResponse?> GetAsync(Guid tenantId, string dealerId, string operation, CancellationToken cancellationToken = default)
    {
        DemoTenants.Require(tenantId, DemoTenants.Tenant1);
        DemoTenants.ValidateDealerId(dealerId);
        DealerOperations.Validate(operation);
        var local = await tenant1.GetAsync(tenantId, dealerId, operation, cancellationToken);
        if (local is null) return null;
        var remote = await tenant2.GetAsync(local.DealerId, operation, cancellationToken);
        return new DealerWrapperResponse(tenantId, local.DealerId, remote.Status, local, remote.Data,
            remote.Data is null
                ? $"Tenant 1 data is available. Tenant 2 integration status: {remote.Status}."
                : "Sources are correlated by DealerId only. Parts orders and repair orders are not transaction-matched; claims and warranty amounts must not be combined as a single liability.");
    }
}
