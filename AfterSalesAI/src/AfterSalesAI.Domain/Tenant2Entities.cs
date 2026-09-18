namespace AfterSalesAI.Domain;

public sealed class ServiceDealerAccount
{
    public string DealerId { get; set; } = string.Empty;
    public string DealerName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ServiceVehicle
{
    public int VehicleId { get; set; }
    public string VehicleReference { get; set; } = string.Empty;
    public string DealerId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public short ModelYear { get; set; }
    public DateOnly WarrantyStart { get; set; }
    public DateOnly WarrantyEnd { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ServiceOperation
{
    public string OperationCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal StandardHours { get; set; }
    public bool IsActive { get; set; }
}

public sealed class RepairOrder
{
    public int RepairOrderId { get; set; }
    public string RepairOrderNumber { get; set; } = string.Empty;
    public string DealerId { get; set; } = string.Empty;
    public int VehicleId { get; set; }
    public string Complaint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateOnly OpenedDate { get; set; }
    public DateOnly PromisedDate { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public int MileageKm { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class RepairLine
{
    public int RepairOrderId { get; set; }
    public short LineNumber { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public decimal LabourHours { get; set; }
    public decimal LabourRateEur { get; set; }
    public decimal MaterialsAmountEur { get; set; }
    public decimal LineTotalEur { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class WarrantyCase
{
    public int WarrantyCaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int RepairOrderId { get; set; }
    public DateOnly SubmittedDate { get; set; }
    public DateOnly? DecisionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal ClaimedAmountEur { get; set; }
    public decimal ApprovedAmountEur { get; set; }
}

public sealed class RepairStatusEvent
{
    public int RepairOrderId { get; set; }
    public short EventSequence { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PublicNote { get; set; } = string.Empty;
}
