using AfterSalesAI.Application;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class Tenant2DealerQueries(Tenant2DbContext db) : ITenant2DealerQueries
{
    public async Task<Tenant2DealerResponse?> GetAsync(Guid tenantId, string dealerId, string operation,
        DateOnly? evaluationDate = null, CancellationToken cancellationToken = default)
    {
        DemoTenants.Require(tenantId, DemoTenants.Tenant2);
        DemoTenants.ValidateDealerId(dealerId);
        DealerOperations.Validate(operation);
        var dealer = await db.Dealers.AsNoTracking().Where(x => x.DealerId == dealerId)
            .Select(x => new ServiceDealerDto(x.DealerId, x.DealerName, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
        if (dealer is null) return null;
        var date = evaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var repairs = new List<RepairDto>();
        var lines = new List<RepairLineDto>();
        var events = new List<RepairEventDto>();
        var warranties = new List<WarrantyDto>();
        WarrantySummaryDto? summary = null;
        if (operation != DealerOperations.Warranty)
        {
            repairs = await (from r in db.Repairs.AsNoTracking()
                join v in db.Vehicles.AsNoTracking() on new { r.VehicleId, r.DealerId } equals new { v.VehicleId, v.DealerId }
                where r.DealerId == dealerId
                orderby r.OpenedDate descending, r.RepairOrderNumber
                select new RepairDto(r.RepairOrderNumber, v.VehicleReference, v.ModelName, r.Complaint,
                    r.Status, r.Priority, r.OpenedDate, r.PromisedDate, r.ClosedDate,
                    db.Lines.Where(l => l.RepairOrderId == r.RepairOrderId && l.Status != "Cancelled").Sum(l => (decimal?)l.LineTotalEur) ?? 0m,
                    r.Status != "Completed" && r.Status != "Cancelled" && r.PromisedDate < date)).ToListAsync(cancellationToken);
        }
        if (operation == DealerOperations.Overview)
        {
            lines = await (from l in db.Lines.AsNoTracking()
                join r in db.Repairs.AsNoTracking() on l.RepairOrderId equals r.RepairOrderId
                join o in db.Operations.AsNoTracking() on l.OperationCode equals o.OperationCode
                where r.DealerId == dealerId orderby r.RepairOrderNumber, l.LineNumber
                select new RepairLineDto(r.RepairOrderNumber, l.LineNumber, l.OperationCode, o.Description,
                    l.LabourHours, l.LabourRateEur, l.MaterialsAmountEur, l.LineTotalEur, l.Status)).ToListAsync(cancellationToken);
        }
        if (operation == DealerOperations.Status)
        {
            events = await (from e in db.Events.AsNoTracking()
                join r in db.Repairs.AsNoTracking() on e.RepairOrderId equals r.RepairOrderId
                where r.DealerId == dealerId orderby r.RepairOrderNumber, e.EventSequence
                select new RepairEventDto(r.RepairOrderNumber, e.EventSequence, e.OccurredUtc, e.Status, e.PublicNote)).ToListAsync(cancellationToken);
        }
        if (operation == DealerOperations.Warranty)
        {
            warranties = await (from w in db.Warranties.AsNoTracking()
                join r in db.Repairs.AsNoTracking() on w.RepairOrderId equals r.RepairOrderId
                where r.DealerId == dealerId orderby w.SubmittedDate descending, w.CaseNumber
                select new WarrantyDto(r.RepairOrderNumber, w.CaseNumber, w.Status, w.Reason,
                    w.SubmittedDate, w.DecisionDate, w.ClaimedAmountEur, w.ApprovedAmountEur)).ToListAsync(cancellationToken);
            summary = new WarrantySummaryDto(warranties.Count, warranties.Count(x => x.Status is "Submitted" or "UnderReview"),
                warranties.Sum(x => x.ClaimedAmountEur), warranties.Sum(x => x.ApprovedAmountEur));
        }
        return new Tenant2DealerResponse(tenantId, dealer.DealerId, operation, dealer, date, repairs, lines, events, warranties, summary);
    }
}

public sealed class Tenant1DealerQueries(AfterSalesAIDbContext db) : ITenant1DealerQueries
{
    public async Task<Tenant1DealerData?> GetAsync(Guid tenantId, string dealerId, string operation, CancellationToken cancellationToken = default)
    {
        DemoTenants.Require(tenantId, DemoTenants.Tenant1);
        DemoTenants.ValidateDealerId(dealerId);
        DealerOperations.Validate(operation);
        var dealer = await db.DemoDealers.AsNoTracking().Where(x => x.DealerId == dealerId)
            .Select(x => new { x.DealerId, x.DealerName, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (dealer is null) return null;
        var orders = new List<DealerOrderDto>();
        var shipments = new List<DealerShipmentDto>();
        var claims = new List<DealerClaimDto>();
        if (operation != DealerOperations.Warranty)
        {
            orders = await db.DemoPurchaseOrders.AsNoTracking().Where(x => x.DealerId == dealerId)
                .OrderBy(x => x.PoNo).ThenBy(x => x.PoLineNo)
                .Select(x => new DealerOrderDto(x.PoNo, x.PoLineNo, x.PartNo, x.OrderQuantity, x.Status, x.RequestedDeliveryDate)).ToListAsync(cancellationToken);
            shipments = await db.DemoShipments.AsNoTracking().Where(x => x.DealerId == dealerId).OrderBy(x => x.ShipmentId)
                .Select(x => new DealerShipmentDto(x.ShipmentId, x.PoNo, x.Status, x.EstimatedDeliveryDate, x.ActualDeliveryDate)).ToListAsync(cancellationToken);
        }
        else
        {
            claims = await db.DemoClaims.AsNoTracking().Where(x => x.DealerId == dealerId).OrderBy(x => x.ClaimId)
                .Select(x => new DealerClaimDto(x.ClaimId, x.PoNo, x.PartNo, x.Status, x.Reason, x.ClaimAmountEur)).ToListAsync(cancellationToken);
        }
        return new Tenant1DealerData(dealer.DealerId, dealer.DealerName, dealer.Status, orders, shipments, claims);
    }
}
