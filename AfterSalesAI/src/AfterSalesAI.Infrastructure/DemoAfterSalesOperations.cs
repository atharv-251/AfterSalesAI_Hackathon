using AfterSalesAI.Application;

namespace AfterSalesAI.Infrastructure;

public sealed class DemoAfterSalesOperations : IAfterSalesOperations
{
    private static readonly IReadOnlyCollection<DemoOrder> Orders =
    [
        new("45001234", "Nordstadt Autohaus", "Confirmed", "Delayed - transport exception", new DateOnly(2026, 9, 18), "5Q0-601-025", "Carrier exception is open; dealer follow-up is required."),
        new("45001235", "Rheinland Service", "In delivery", "On schedule", new DateOnly(2026, 9, 14), "04E-115-105", string.Empty),
        new("45001236", "Berlin Mobility", "Confirmed", "Awaiting allocation", new DateOnly(2026, 9, 23), "1K0-919-275", "Part allocation is pending.")
    ];
    private static readonly IReadOnlyCollection<DemoClaim> Claims =
    [new("CL-10021", "45001234", "Rejected", "Missing required photo evidence", new DateOnly(2026, 9, 9)), new("CL-10022", "45001235", "Open", "Under technical review", new DateOnly(2026, 9, 11))];
    private static readonly IReadOnlyCollection<DemoPartStock> Inventory =
    [new("5Q0-601-025", "Alloy wheel 17 inch", "Wolfsburg", 4, 8), new("04E-115-105", "Oil filter", "Kassel", 120, 25), new("1K0-919-275", "Parking sensor", "Brunswick", 0, 5)];

    public IReadOnlyCollection<DemoOrder> GetOrders() => Orders;
    public IReadOnlyCollection<DemoClaim> GetClaims() => Claims;
    public IReadOnlyCollection<DemoPartStock> GetInventory() => Inventory;
    public DemoDashboard GetDashboard() => new(3, 1, 1, 2, Inventory.Sum(item => item.AvailableQuantity), Inventory.Count(item => item.AvailableQuantity <= item.ReorderLevel), ["Order 45001234 has a delivery exception.", "Part 1K0-919-275 is unavailable.", "Claim CL-10021 was rejected."]);

    public OperationalSearchResult Search(string query)
    {
        var orders = Orders.Where(order => OperationalSearchMatcher.Matches(query, order.OrderId, order.Customer, order.PartNumber, order.TrackingNumber)).ToArray();
        var partNumbers = orders.Select(order => order.PartNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderIds = orders.Select(order => order.OrderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claims = Claims.Where(claim => OperationalSearchMatcher.Matches(query, claim.ClaimId, claim.OrderId) || orderIds.Contains(claim.OrderId)).ToArray();
        partNumbers.UnionWith(claims.Select(claim => Orders.FirstOrDefault(order => order.OrderId.Equals(claim.OrderId, StringComparison.OrdinalIgnoreCase))?.PartNumber)
            .Where(partNumber => partNumber is not null).Select(partNumber => partNumber!));
        var inventory = Inventory.Where(part => OperationalSearchMatcher.Matches(query, part.PartNumber, part.Description) || partNumbers.Contains(part.PartNumber)).ToArray();

        var records = new List<OperationalSearchRecord>();
        records.AddRange(orders.Select(order => new OperationalSearchRecord("Orders & deliveries", "API: Orders & Deliveries", $"Order {order.OrderId}; customer {order.Customer}; status {order.Status}; delivery {order.DeliveryStatus}; requested delivery {order.ExpectedDelivery:dd MMM yyyy}; part {order.PartNumber}; tracking {order.TrackingNumber ?? "not found"}.")));
        records.AddRange(claims.Select(claim => new OperationalSearchRecord("Claims", "API: Claims", $"Claim {claim.ClaimId}; order {claim.OrderId}; status {claim.Status}; reason {claim.Reason}; created {claim.CreatedOn:dd MMM yyyy}.")));
        records.AddRange(inventory.Select(part => new OperationalSearchRecord("Parts & inventory", "API: Parts & Inventory", $"Part {part.PartNumber}; {part.Description}; available quantity {part.AvailableQuantity}; warehouse {part.Plant}; reorder level {part.ReorderLevel}.")));
        return new OperationalSearchResult(records);
    }
}
