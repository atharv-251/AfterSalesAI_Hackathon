using System.Data;
using System.Text;
using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterSalesAI.Infrastructure;

public sealed class DemoDatasetOptions
{
    public const string SectionName = "DemoDataset";
    public string WorkbookPath { get; set; } = string.Empty;
}

public sealed class ExcelDemoDatasetImportService(
    AfterSalesAIDbContext dbContext,
    IOptions<DemoDatasetOptions> options) : IDemoDatasetImportService
{
    private static readonly string[] RequiredSheets = ["Dealers", "Parts", "PurchaseOrders", "Shipments", "Claims", "Inventory", "BOM", "Knowledge"];

    public async Task<DemoDatasetImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var workbookPath = options.Value.WorkbookPath;
        if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            throw new InvalidOperationException("The configured demo dataset workbook was not found.");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = File.Open(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration { ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true } });
        if (RequiredSheets.Any(sheet => !dataSet.Tables.Contains(sheet)))
            throw new InvalidOperationException("The workbook does not contain all required approved business sheets.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [demo].[Knowledge]; DELETE FROM [demo].[Bom]; DELETE FROM [demo].[Inventory]; DELETE FROM [demo].[Claims]; DELETE FROM [demo].[Shipments]; DELETE FROM [demo].[PurchaseOrders]; DELETE FROM [demo].[Parts]; DELETE FROM [demo].[Dealers];", cancellationToken);

        var counts = new Dictionary<string, int>();
        await ImportAsync(dataSet.Tables["Dealers"]!, dbContext.DemoDealers, row => new DemoDealer { DealerId = Text(row, "DealerID"), DealerName = Text(row, "DealerName"), Region = Text(row, "Region"), Country = Text(row, "Country"), City = Text(row, "City"), Tier = Text(row, "Tier"), Status = Text(row, "Status"), ContactEmail = Text(row, "ContactEmail"), CreditLimitEur = Decimal(row, "CreditLimitEUR"), OnboardedDate = Date(row, "OnboardedDate") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["Parts"]!, dbContext.DemoParts, row => new DemoPart { PartNo = Text(row, "PartNo"), PartName = Text(row, "PartName"), Category = Text(row, "Category"), UnitPriceEur = Decimal(row, "UnitPriceEUR"), Currency = Text(row, "Currency"), WarrantyMonths = Int(row, "WarrantyMonths"), SupplierId = Text(row, "SupplierID"), SupplierName = Text(row, "SupplierName"), HazmatFlag = Text(row, "HazmatFlag").Equals("Y", StringComparison.OrdinalIgnoreCase), Status = Text(row, "Status"), LeadTimeDays = Int(row, "LeadTimeDays") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["PurchaseOrders"]!, dbContext.DemoPurchaseOrders, row => new DemoPurchaseOrder { PoNo = Text(row, "PO_No"), PoLineNo = Text(row, "PO_LineNo"), DealerId = Text(row, "DealerID"), PartNo = Text(row, "PartNo"), OrderQuantity = Int(row, "OrderQty"), UnitPriceEur = Decimal(row, "UnitPriceEUR"), LineTotalEur = Decimal(row, "LineTotalEUR"), OrderDate = Date(row, "OrderDate"), RequestedDeliveryDate = Date(row, "RequestedDeliveryDate"), Status = Text(row, "POStatus"), ShipmentId = Text(row, "ShipmentID"), Currency = Text(row, "Currency") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["Shipments"]!, dbContext.DemoShipments, row => new DemoShipment { ShipmentId = Text(row, "ShipmentID"), PoNo = Text(row, "PO_No"), DealerId = Text(row, "DealerID"), Carrier = Text(row, "Carrier"), ShipDate = Date(row, "ShipDate"), EstimatedDeliveryDate = Date(row, "EstDeliveryDate"), ActualDeliveryDate = OptionalDate(row, "ActualDeliveryDate"), Status = Text(row, "ShipmentStatus"), TrackingNo = Text(row, "TrackingNo"), Quantity = Int(row, "Qty") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["Claims"]!, dbContext.DemoClaims, row => new DemoClaimRecord { ClaimId = Text(row, "ClaimID"), ClaimType = Text(row, "ClaimType"), DealerId = Text(row, "DealerID"), PartNo = Text(row, "PartNo"), PoNo = Text(row, "PO_No"), ClaimQuantity = Int(row, "ClaimQty"), PurchaseDate = Date(row, "PurchaseDate"), ClaimDate = Date(row, "ClaimDate"), Status = Text(row, "ClaimStatus"), Reason = Text(row, "Reason"), ClaimAmountEur = Decimal(row, "ClaimAmountEUR"), ResolutionCode = Text(row, "ResolutionCode") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["Inventory"]!, dbContext.DemoInventory, row => new DemoInventoryItem { InventoryId = Text(row, "InventoryID"), PartNo = Text(row, "PartNo"), WarehouseLocation = Text(row, "WarehouseLoc"), OnHandQuantity = Int(row, "OnHandQty"), ReservedQuantity = Int(row, "ReservedQty"), AvailableQuantity = Int(row, "AvailableQty"), ReorderPoint = Int(row, "ReorderPoint"), BinLocation = Text(row, "BinLocation"), LastCountDate = Date(row, "LastCountDate") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["BOM"]!, dbContext.DemoBom, row => new DemoBomItem { BomId = Text(row, "BOMID"), AssemblyPartNo = Text(row, "AssemblyPartNo"), ComponentPartNo = Text(row, "ComponentPartNo"), QuantityPer = Decimal(row, "QtyPer"), Level = Int(row, "Level"), Notes = Text(row, "Notes") }, counts, cancellationToken);
        await ImportAsync(dataSet.Tables["Knowledge"]!, dbContext.DemoKnowledge, row => new DemoKnowledgeArticle { DocumentId = Text(row, "DocID"), DocumentType = Text(row, "DocType"), Title = Text(row, "Title"), Module = Text(row, "Module"), ErrorCode = Text(row, "ErrorCode"), Summary = Text(row, "Summary"), LastUpdated = Date(row, "LastUpdated"), OwnerTeam = Text(row, "OwnerTeam") }, counts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return new DemoDatasetImportResult(counts);
    }

    private static async Task ImportAsync<T>(DataTable table, DbSet<T> set, Func<DataRow, T> map, IDictionary<string, int> counts, CancellationToken token) where T : class { var items = table.Rows.Cast<DataRow>().Where(row => !row.ItemArray.All(value => value is DBNull || string.IsNullOrWhiteSpace(value?.ToString()))).Select(map).ToArray(); await set.AddRangeAsync(items, token); counts[table.TableName] = items.Length; }
    private static string Text(DataRow row, string column) => row[column]?.ToString()?.Trim() ?? string.Empty;
    private static int Int(DataRow row, string column) => Convert.ToInt32(row[column]);
    private static decimal Decimal(DataRow row, string column) => Convert.ToDecimal(row[column]);
    private static DateOnly Date(DataRow row, string column) => DateOnly.FromDateTime(Convert.ToDateTime(row[column]));
    private static DateOnly? OptionalDate(DataRow row, string column) => row.IsNull(column) || string.IsNullOrWhiteSpace(Text(row, column)) ? null : Date(row, column);
}
