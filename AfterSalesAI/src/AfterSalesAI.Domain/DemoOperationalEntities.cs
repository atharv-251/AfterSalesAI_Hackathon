namespace AfterSalesAI.Domain;

public sealed class DemoDealer
{
    public string DealerId { get; set; } = string.Empty;
    public string DealerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public decimal CreditLimitEur { get; set; }
    public DateOnly OnboardedDate { get; set; }
}

public sealed class DemoPart
{
    public string PartNo { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPriceEur { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int WarrantyMonths { get; set; }
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public bool HazmatFlag { get; set; }
    public string Status { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
}

public sealed class DemoPurchaseOrder
{
    public string PoNo { get; set; } = string.Empty;
    public string PoLineNo { get; set; } = string.Empty;
    public string DealerId { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public int OrderQuantity { get; set; }
    public decimal UnitPriceEur { get; set; }
    public decimal LineTotalEur { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly RequestedDeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ShipmentId { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}

public sealed class DemoShipment
{
    public string ShipmentId { get; set; } = string.Empty;
    public string PoNo { get; set; } = string.Empty;
    public string DealerId { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public DateOnly ShipDate { get; set; }
    public DateOnly EstimatedDeliveryDate { get; set; }
    public DateOnly? ActualDeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TrackingNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class DemoClaimRecord
{
    public string ClaimId { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public string DealerId { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public string PoNo { get; set; } = string.Empty;
    public int ClaimQuantity { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public DateOnly ClaimDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal ClaimAmountEur { get; set; }
    public string ResolutionCode { get; set; } = string.Empty;
}

public sealed class DemoInventoryItem
{
    public string InventoryId { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public string WarehouseLocation { get; set; } = string.Empty;
    public int OnHandQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public string BinLocation { get; set; } = string.Empty;
    public DateOnly LastCountDate { get; set; }
}

public sealed class DemoBomItem
{
    public string BomId { get; set; } = string.Empty;
    public string AssemblyPartNo { get; set; } = string.Empty;
    public string ComponentPartNo { get; set; } = string.Empty;
    public decimal QuantityPer { get; set; }
    public int Level { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class DemoKnowledgeArticle
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateOnly LastUpdated { get; set; }
    public string OwnerTeam { get; set; } = string.Empty;
}
