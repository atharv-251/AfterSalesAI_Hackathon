using AfterSalesAI.Application;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class SqlServerAfterSalesOperations(AfterSalesAIDbContext dbContext) : IAfterSalesOperations
{
    public IReadOnlyCollection<DemoOrder> GetOrders() =>
        (from order in dbContext.DemoPurchaseOrders.AsNoTracking()
         join dealer in dbContext.DemoDealers.AsNoTracking() on order.DealerId equals dealer.DealerId
         join shipment in dbContext.DemoShipments.AsNoTracking() on order.ShipmentId equals shipment.ShipmentId into shipments
         from shipment in shipments.DefaultIfEmpty()
         orderby order.OrderDate descending
         select new DemoOrder(
             order.PoNo,
             dealer.DealerName,
             order.Status,
             shipment == null ? "Not shipped" : shipment.Status,
             order.RequestedDeliveryDate,
             order.PartNo,
              shipment != null && shipment.Status != "Delivered" ? $"Shipment {shipment.ShipmentId} is {shipment.Status}." : string.Empty,
              shipment == null ? null : shipment.Carrier,
              shipment == null ? null : shipment.TrackingNo,
              shipment == null ? null : shipment.EstimatedDeliveryDate))
        .ToArray();

    public IReadOnlyCollection<DemoClaim> GetClaims() =>
        dbContext.DemoClaims.AsNoTracking()
            .OrderByDescending(claim => claim.ClaimDate)
            .Select(claim => new DemoClaim(claim.ClaimId, claim.PoNo, claim.Status, claim.Reason, claim.ClaimDate))
            .ToArray();

    public IReadOnlyCollection<DemoPartStock> GetInventory() =>
        (from inventory in dbContext.DemoInventory.AsNoTracking()
         join part in dbContext.DemoParts.AsNoTracking() on inventory.PartNo equals part.PartNo
         select new DemoPartStock(inventory.PartNo, part.PartName, inventory.WarehouseLocation, inventory.AvailableQuantity, inventory.ReorderPoint))
        .ToArray();

    public DemoDashboard GetDashboard()
    {
        var orders = GetOrders();
        var claims = GetClaims();
        var inventory = GetInventory();
        var alerts = orders.Where(order => !string.IsNullOrWhiteSpace(order.Alert)).Select(order => order.Alert)
            .Concat(inventory.Where(item => item.AvailableQuantity <= item.ReorderLevel).Select(item => $"Part {item.PartNumber} is at or below its reorder level."))
            .Take(10)
            .ToArray();

        return new DemoDashboard(
            orders.Count(order => order.Status.Equals("Open", StringComparison.OrdinalIgnoreCase) || order.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)),
            orders.Count(order => order.DeliveryStatus.Contains("delivery", StringComparison.OrdinalIgnoreCase)),
            orders.Count(order => !string.IsNullOrWhiteSpace(order.Alert)),
            claims.Count(claim => !claim.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase)),
            inventory.Sum(item => item.AvailableQuantity),
            inventory.Count(item => item.AvailableQuantity <= item.ReorderLevel),
            alerts);
    }

    public OperationalSearchResult Search(string query)
    {
        var dealers = dbContext.DemoDealers.AsNoTracking().ToArray();
        var parts = dbContext.DemoParts.AsNoTracking().ToArray();
        var orders = dbContext.DemoPurchaseOrders.AsNoTracking().ToArray();
        var shipments = dbContext.DemoShipments.AsNoTracking().ToArray();
        var claims = dbContext.DemoClaims.AsNoTracking().ToArray();
        var inventory = dbContext.DemoInventory.AsNoTracking().ToArray();
        var lowStockQuery = query.Contains("low stock", StringComparison.OrdinalIgnoreCase)
            || query.Contains("low-stock", StringComparison.OrdinalIgnoreCase);
        var requestedOrderNumbers = OperationalSearchMatcher.ExtractPurchaseOrderIds(query);
        var requestedPartNumbers = OperationalSearchMatcher.ExtractPartNumbers(query);
        var isOrderScopedQuery = requestedOrderNumbers.Count > 0;
        var isPartScopedQuery = requestedPartNumbers.Count > 0;
        var isIdentifierScopedQuery = isOrderScopedQuery || isPartScopedQuery;

        var dealerIds = dealers.Where(dealer => OperationalSearchMatcher.Matches(query, dealer.DealerId, dealer.DealerName, dealer.City, dealer.ContactEmail))
            .Select(dealer => dealer.DealerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (isPartScopedQuery) dealerIds.Clear();
        var orderNumbers = orders.Where(order => isOrderScopedQuery ? requestedOrderNumbers.Contains(order.PoNo)
            : isPartScopedQuery ? requestedPartNumbers.Contains(order.PartNo)
            : OperationalSearchMatcher.Matches(query, order.PoNo, order.ShipmentId, order.PartNo) || dealerIds.Contains(order.DealerId))
            .Select(order => order.PoNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shipmentIds = shipments.Where(shipment => isIdentifierScopedQuery ? orderNumbers.Contains(shipment.PoNo)
            : OperationalSearchMatcher.Matches(query, shipment.ShipmentId, shipment.TrackingNo, shipment.PoNo) || dealerIds.Contains(shipment.DealerId) || orderNumbers.Contains(shipment.PoNo))
            .Select(shipment => shipment.ShipmentId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!isIdentifierScopedQuery)
            foreach (var order in orders.Where(order => shipmentIds.Contains(order.ShipmentId))) orderNumbers.Add(order.PoNo);
        foreach (var order in orders.Where(order => orderNumbers.Contains(order.PoNo))) dealerIds.Add(order.DealerId);
        var partNumbers = orders.Where(order => orderNumbers.Contains(order.PoNo)).Select(order => order.PartNo)
            .Concat(claims.Where(claim => isOrderScopedQuery ? orderNumbers.Contains(claim.PoNo)
                : isPartScopedQuery ? requestedPartNumbers.Contains(claim.PartNo)
                : OperationalSearchMatcher.Matches(query, claim.ClaimId, claim.PartNo, claim.PoNo) || dealerIds.Contains(claim.DealerId) || orderNumbers.Contains(claim.PoNo)).Select(claim => claim.PartNo))
            .Concat(parts.Where(part => isPartScopedQuery ? requestedPartNumbers.Contains(part.PartNo) : OperationalSearchMatcher.Matches(query, part.PartNo, part.PartName, part.SupplierName)).Select(part => part.PartNo))
            .Concat(inventory.Where(item => lowStockQuery && item.AvailableQuantity <= item.ReorderPoint).Select(item => item.PartNo))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        partNumbers.UnionWith(requestedPartNumbers);

        var records = new List<OperationalSearchRecord>();
        records.AddRange(dealers.Where(dealer => dealerIds.Contains(dealer.DealerId)).Select(dealer => new OperationalSearchRecord("Customer records", "Database: Customer Records", $"Customer {dealer.DealerName} ({dealer.DealerId}); city {dealer.City}, {dealer.Country}; region {dealer.Region}; tier {dealer.Tier}; status {dealer.Status}; contact {dealer.ContactEmail}; credit limit EUR {dealer.CreditLimitEur:N2}; onboarded {dealer.OnboardedDate:dd MMM yyyy}.")));
        records.AddRange(orders.Where(order => orderNumbers.Contains(order.PoNo)).Select(order => new OperationalSearchRecord("Orders", "Database: Orders & Deliveries", $"Order {order.PoNo}, line {order.PoLineNo}; dealer {dealers.FirstOrDefault(dealer => dealer.DealerId == order.DealerId)?.DealerName ?? order.DealerId}; part {order.PartNo}; quantity {order.OrderQuantity}; unit price {order.Currency} {order.UnitPriceEur:N2}; line total {order.Currency} {order.LineTotalEur:N2}; ordered {order.OrderDate:dd MMM yyyy}; requested delivery {order.RequestedDeliveryDate:dd MMM yyyy}; status {order.Status}; shipment {ValueOrNotFound(order.ShipmentId)}.")));
        records.AddRange(shipments.Where(shipment => shipmentIds.Contains(shipment.ShipmentId)).Select(shipment => new OperationalSearchRecord("Deliveries", "Database: Orders & Deliveries", $"Shipment {shipment.ShipmentId}; order {shipment.PoNo}; carrier {ValueOrNotFound(shipment.Carrier)}; tracking {ValueOrNotFound(shipment.TrackingNo)}; shipped {shipment.ShipDate:dd MMM yyyy}; estimated delivery {shipment.EstimatedDeliveryDate:dd MMM yyyy}; actual delivery {FormatDate(shipment.ActualDeliveryDate)}; quantity {shipment.Quantity}; status {shipment.Status}.")));
        records.AddRange(claims.Where(claim => isOrderScopedQuery ? orderNumbers.Contains(claim.PoNo)
            : isPartScopedQuery ? requestedPartNumbers.Contains(claim.PartNo)
            : dealerIds.Contains(claim.DealerId) || orderNumbers.Contains(claim.PoNo) || partNumbers.Contains(claim.PartNo) || OperationalSearchMatcher.Matches(query, claim.ClaimId)).Select(claim => new OperationalSearchRecord("Claims", "Database: Claims", $"Claim {claim.ClaimId}; type {claim.ClaimType}; order {claim.PoNo}; part {claim.PartNo}; quantity {claim.ClaimQuantity}; purchase date {claim.PurchaseDate:dd MMM yyyy}; claim date {claim.ClaimDate:dd MMM yyyy}; status {claim.Status}; reason {claim.Reason}; amount EUR {claim.ClaimAmountEur:N2}; resolution {ValueOrNotFound(claim.ResolutionCode)}.")));
        records.AddRange(parts.Where(part => partNumbers.Contains(part.PartNo)).Select(part => new OperationalSearchRecord("Parts", "Database: Parts & Inventory", $"Part {part.PartNo}; {part.PartName}; category {part.Category}; unit price {part.Currency} {part.UnitPriceEur:N2}; warranty {part.WarrantyMonths} months; supplier {part.SupplierName} ({part.SupplierId}); hazardous material {(part.HazmatFlag ? "yes" : "no")}; status {part.Status}; lead time {part.LeadTimeDays} days.")));
        records.AddRange(inventory.Where(item => partNumbers.Contains(item.PartNo) || lowStockQuery && item.AvailableQuantity <= item.ReorderPoint || OperationalSearchMatcher.Matches(query, item.InventoryId, item.PartNo, item.WarehouseLocation)).Select(item => new OperationalSearchRecord("Inventory", "Database: Parts & Inventory", $"Inventory {item.InventoryId}; part {item.PartNo}; warehouse {item.WarehouseLocation}; bin {item.BinLocation}; on hand {item.OnHandQuantity}; reserved {item.ReservedQuantity}; available {item.AvailableQuantity}; reorder point {item.ReorderPoint}; last count {item.LastCountDate:dd MMM yyyy}.")));
        return new OperationalSearchResult(records);
    }

    private static string ValueOrNotFound(string? value) => string.IsNullOrWhiteSpace(value) ? "not found" : value;
    private static string FormatDate(DateOnly? value) => value is null ? "not found" : value.Value.ToString("dd MMM yyyy");
}
