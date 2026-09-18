# After-sales Tenant 2 database and controlled integration

## Deliverable and scope

Run `DatabaseScripts/02_CreateTenant2AfterSalesDb.sql` alone, in full, in SSMS. It creates the complete Tenant 2 business database, schema, constraints, indexes, views, procedures and related demo records. No other script, Excel workbook, SQLCMD mode or manual substitution is required. The former training installer is superseded, not a prerequisite. If training objects already exist, they are preserved but are not part of the new product or approved tools.

Implementation update: Tenant 2 EF mappings, HTTP endpoints, Tenant 1 wrappers, Core persistence, tenant-aware chat dispatch, explicit legacy knowledge transfer and frontend selection/management are now present. See [current setup and limitations](multi-tenant-setup.md) for the authoritative implementation status and executable request examples. The remaining sections retain the original design discussion; items described as planned must be checked against that status guide. Arbitrary configuration-driven onboarding and conversational follow-up context remain unfinished.

## Inspected Tenant 1

The application currently connects to `AfterSalesAI_Demo` on `(localdb)\MSSQLLocalDB` using `AfterSalesAIDbContext`. Domain entities are in `src/AfterSalesAI.Domain/DemoOperationalEntities.cs`; mappings are in `src/AfterSalesAI.Infrastructure/Configurations/DemoOperationalEntityConfigurations.cs`; queries are in `SqlServerAfterSalesOperations.cs`.

| Existing object | Primary key | Dealer relationship / usage |
|---|---|---|
| demo.Dealers | DealerId, nvarchar(50) | Authoritative Tenant 1 dealer identifier |
| demo.PurchaseOrders | PoNo + PoLineNo | DealerId; parts demand and orders |
| demo.Shipments | ShipmentId | DealerId and PoNo; deliveries |
| demo.Claims | ClaimId | DealerId, PoNo, PartNo; existing parts claims |
| demo.Parts | PartNo | Parts master; not a dealer correlation key |
| demo.Inventory | InventoryId | PartNo; warehouse stock |
| demo.Bom | BomId | AssemblyPartNo and ComponentPartNo |
| demo.Knowledge | DocumentId | Imported reference articles |

DealerId is already a unique primary key and has supporting indexes in related business tables. No Tenant 1 alteration or rollback script is necessary. Do not invent a replacement key. Dealer names are not unique: live inspection found Detroit Motors for both D001 and D003.

Rechecked matching records: D001 (5 order lines, 0 claims), D002 (7, 1), D003 (2, 1), D004 (8, 3), D005 (4, 3). These are inspection-time counts, not immutable expected values. Tenant 2-only keys T2ONLY-001 and T2ONLY-002 were absent in the inspected Tenant 1 dealer list.

## Three-database architecture

| Database | Context | Responsibility |
|---|---|---|
| MultiTenantAICoreDb | AICoreDbContext | Tenant registry, approved operation metadata, tenant prompts, new documents/chunks, sessions/messages |
| AfterSalesAI_Demo (existing, preserved) | Existing AfterSalesAIDbContext | Tenant 1 parts orders, shipments, inventory and claims |
| Tenant2DemoDb | Tenant2DbContext | Tenant 2 vehicle servicing, repair work and warranty decisions |

The current Tenant 1 context also maps the legacy AI registry/knowledge tables. Future Core implementation must transfer approved metadata and documents through scoped application reads/writes, preserving the originals until validated. Do not apply a new Core migration to Tenant 1. No automatic table dropping or cross-database INSERT/SELECT is part of this design.

Reuse the current Domain/Application/Infrastructure/API projects and React components. Place SQL-specific mappings and procedure execution in Infrastructure. Keep LLM credentials in server-side secrets and reuse the existing VW Group LLMaaS client. The new Tenant 2 DbContext must explicitly map only `aftersales` entities; do not apply all existing assembly configurations indiscriminately. Connections are fixed and validated at startup, not supplied by the browser or LLM.

AI Core metadata should use TenantId on every tenant-owned record, composite tenant/resource foreign keys, and active-operation validation. Store configuration references such as `ConnectionStrings:Tenant2`, never credential-bearing strings. Planned records include Tenant, DatabaseConfiguration, Document, DocumentChunk, ApiConfiguration, ApiOperation, DatabaseOperation, ChatSession and ChatMessage. Central business-record tables are prohibited. Tenant 1 and Tenant 2 prompts, documents and messages remain separate.

## Tenant 2 relational model

Tenant 2 is a vehicle workshop and warranty product, not a copy of the parts-order product.

| Table in aftersales schema | Primary key | Relationships / purpose | Seed rows |
|---|---|---|---:|
| DealerAccounts | DealerId | Independently managed service account; shared API correlation key | 8 |
| Vehicles | VehicleId | DealerId FK; unique VehicleReference; model and warranty dates | 10 |
| ServiceOperations | OperationCode | Diagnostic, repair, safety and maintenance operation master | 6 |
| RepairOrders | RepairOrderId | Unique RepairOrderNumber; composite VehicleId/DealerId FK prevents wrong-dealer vehicle assignments | 12 |
| RepairLines | RepairOrderId + LineNumber | RepairOrder FK, OperationCode FK; labour/material amounts, computed line total | 18 |
| WarrantyCases | WarrantyCaseId | Unique CaseNumber; RepairOrder FK; claim and approval amounts with decision constraints | 7 |
| RepairStatusEvents | RepairOrderId + EventSequence | RepairOrder FK; chronological public progress notes | 23 |

Local relationships: DealerAccounts -> Vehicles -> RepairOrders -> RepairLines, WarrantyCases and RepairStatusEvents; ServiceOperations -> RepairLines. All foreign keys stay inside Tenant2DemoDb. No TenantId column is needed on these business tables because the whole database is Tenant 2 only. DealerId is a business identifier, **not** TenantId and not authorization.

Money uses explicit decimal precision and EUR names. Dates use date; event/creation timestamps use datetime2(0) UTC. Views exclude internal identity keys where unnecessary. VehicleReference is a synthetic demo identifier, not a claim of a real VIN. No customer PII is seeded.

`vw_RepairOverview` returns repairs with costs aggregated before joining to prevent child-row multiplication. `vw_WarrantyOverview` returns warranty decisions. `vw_DealerWorkload` includes dealers with no repair work.

## Correlation and sample cases

| DealerId | Meaning |
|---|---|
| D001 | Tenant 1 orders + Tenant 2 overdue brake repair awaiting parts, completed maintenance, pending warranty review |
| D002 | Tenant 1 orders/claim + Tenant 2 battery repair with approved warranty and historic rejected warranty |
| D003 | Tenant 1 orders/claim + Tenant 2 diagnosis, cancellation and partially approved warranty |
| D004 | Exists in Tenant 1; deliberately not seeded in Tenant 2; test partial response |
| D005 | Tenant 1 active dealer; Tenant 2 inactive service account with historic work; preserve source-specific status |
| T2ONLY-001/002 | Tenant 2 independent workshops with active repair work; not a Tenant 1 integration match |
| T2ONLY-003 | Inactive Tenant 2 workshop with historic work |
| T2ONLY-004 | Active Tenant 2 account with no vehicles or repairs; distinguish empty account from unknown account |

DealerId correlation supports a **dealer-level overview**, not transaction matching. Do not imply a Tenant 1 order caused a particular repair delay or a Tenant 1 claim is the same as a Tenant 2 warranty case. Such attribution would require a separately verified transaction-level mapping. No fake PoNo, ClaimId or VIN linkage is seeded.

## Approved Tenant 2 database/API contracts (APIs planned)

| Proposed HTTP GET route | Implemented procedure | Result sets / DTO projection |
|---|---|---|
| /api/tenant2/dealers/{dealerId}/service-overview | aftersales.usp_GetDealerServiceOverview | Dealer header; repairs; repair lines |
| /api/tenant2/dealers/{dealerId}/repair-status | aftersales.usp_GetDealerRepairStatus | Dealer header; repair status/overdue flags; public status events |
| /api/tenant2/dealers/{dealerId}/warranty-summary | aftersales.usp_GetDealerWarrantySummary | Dealer summary/counts/amounts; warranty cases |

Every future request must include server-validated tenant context. The Tenant 2 API receives Tenant 2 context. A Tenant 1 request cannot directly select a Tenant 2 tool: only the fixed wrapper HTTP client may perform the approved outbound Tenant 2 read.

All procedures accept a parameterized DealerId and reject blank, non-uppercase-key characters or overlength input with error 51110. Use a wide input parameter to reject rather than silently truncate. Parameterize calls; never concatenate SQL. Empty header result means HTTP 404; existing account with empty children means HTTP 200. Invalid input maps to HTTP 400. Inactive accounts return their source status and historical records; do not present them as active.

Repair status optionally accepts AsOfDate for evaluating overdue flags against **current stored repair status**, not a historical snapshot. For stable seed demonstrations use 2026-09-18. Omitting the date uses today's UTC date. The three procedures expose no arbitrary SQL, table selection or database switching.

Proposed typed DTOs:
- DealerServiceOverviewDto: DealerId, DealerName, IsActive, Repairs[], Lines[]. Repair fields match the service overview procedure projection.
- DealerRepairStatusDto: DealerId, IsActive, Repairs[], Events[]. Preserve RepairOrderNumber on each child.
- DealerWarrantySummaryDto: DealerId, IsActive, TotalCases, PendingCases, ClaimedAmountEur, ApprovedAmountEur, Cases[].

Procedures return multiple result sets; implementation must consume them explicitly with a data reader and map approved DTOs, or use equivalent fixed EF LINQ projections. Do not claim a single EF FromSql entity maps all result sets. Preserve nullable ClosedDate/DecisionDate values and explicit decimal precision in EF models.

## Tenant 1 wrapper contracts (planned)

| Proposed HTTP GET route | Tenant 1 local source | Typed Tenant 2 HTTP call |
|---|---|---|
| /api/tenant1/wrapper/dealers/{dealerId}/aftersales-overview | demo.Dealers + dealer-filtered PurchaseOrders/Shipments | service-overview |
| /api/tenant1/wrapper/dealers/{dealerId}/repair-readiness | DealerId-filtered shipments and order statuses | repair-status |
| /api/tenant1/wrapper/dealers/{dealerId}/claims-warranty-summary | DealerId-filtered demo.Claims | warranty-summary |

Flow: React Tenant 1 selection -> validated Tenant 1 request context -> Tenant 1 approved wrapper tool -> Tenant 1 wrapper service -> existing Tenant 1 context -> extract/verify DealerId -> fixed typed HttpClient -> Tenant 2 API -> Tenant2DbContext -> approved DTO -> wrapper response -> tenant-specific LLM grounding -> Tenant 1 chat history.

Wrapper response contract: TenantId, DealerId, IntegrationStatus, Tenant1Data, nullable Tenant2Data, CombinedSummary. Keep source collections separate. Summaries must not add Tenant 1 claim amounts to Tenant 2 warranty amounts as if they were independent liabilities. Combined data may overlap and has different business semantics.

The wrapper service must not inject Tenant2DbContext, a Tenant 2 SQL connection or an unrestricted repository. Use a fixed server-configured base address, fixed relative paths, validated escaped keys and a typed HttpClient; never accept URLs from the LLM. Disable redirects or validate them against the configured destination. Do not permit the model to choose TenantId.

Errors: invalid key -> 400; Tenant 1 missing -> 404 without calling Tenant 2; Tenant 2 missing -> 200 with Tenant1Data and IntegrationStatus=NotFound; timeout/unavailable/invalid response -> controlled partial result with status Timeout/Unavailable/InvalidResponse. Verify response DealerId matches the requested key. Pass CancellationToken throughout, propagate caller cancellation, enforce a short configured timeout and log only structured status/correlation fields (no secrets or upstream bodies). Missing Tenant 2 records are not a claim that Tenant 1 records are missing.

## Orchestrator and frontend boundaries (planned)

Tenant 1 tools: tenant-owned document retrieval, approved local order/claim operations, and the three wrapper operations. Tenant 2 tools: Tenant 2 documents and the three Tenant 2 operations only. No Tenant 2 wrapper and no reverse HTTP client to Tenant 1.

Resolve the request TenantId against active Core registry data before querying any business service. Verify operation ownership and active state before execution. Frontend selection is context, not authentication; without login this demo isolates selected contexts but does not prevent a person from selecting a different tenant. Do not claim user-level access control.

React must require selection before chat, send TenantId on every scoped request, cancel in-flight requests and clear cached views on switches, and key history/caches by tenant/session. Existing hardcoded data.ts IDs and non-scoped operational endpoints still need application changes. Chat sessions must enforce tenant/session ownership and never share messages, prompts or retrieval results. Keep credentials and connection strings out of frontend DTOs and LLM context.

## Local setup and verification

1. In SSMS connect to `(localdb)\MSSQLLocalDB`, where Tenant 1 already exists. Do not create replacement Tenant 1 tables on localhost.
2. Open and execute **all** of `DatabaseScripts/02_CreateTenant2AfterSalesDb.sql`. It creates Tenant2DemoDb if absent. If the earlier training setup ran, it adds the aftersales schema without deleting training data.
3. Review row-count output and sample procedure result sets. Refresh Object Explorer -> Tenant2DemoDb -> Tables/Views/Programmability.
4. Re-execution preserves existing seed/user records and recreates the defined views/procedures. It is an idempotent installer for this schema, not a general migration engine for incompatible preexisting tables.
5. SQL Server 2019 syntax is used. The current localhost account previously lacked CREATE DATABASE permission; ask a DBA to execute the full file there if that is the intended deployment. LocalDB verification does not prove localhost permission or deployment.

No authentication system, Docker, packages or .NET runtime upgrade is introduced. SQL does not create HTTP APIs or wire up chatbot access.

## Validation checklist and example questions

Database checks: fresh creation; rerun unchanged counts; trusted FK/check constraints; zero cross-database dependencies/synonyms; matching dealer returns only its own children; D004 missing; T2ONLY-004 empty account; D005 inactive; invalid/oversize keys rejected; totals not multiplied by joins. Existing Tenant 1 rows must remain unchanged.

Future application tests: Tenant 1 local query does not call Tenant 2; combined request selects approved wrapper; missing Tenant 2 preserves Tenant 1; Tenant 2 cannot execute wrapper or Tenant 1 operations; cross-tenant document/session IDs are rejected; HTTP timeout, malformed body and mismatched DealerId return controlled partial responses.

Demo questions after API/orchestrator implementation:
- Tenant 1: Show D001 parts deliveries alongside its open workshop repairs.
- Tenant 1: Summarize D002 parts claims and vehicle warranty decisions separately.
- Tenant 1: Show D004 service overview even if the service system has no matching dealer.
- Tenant 2: Which D001 repairs are awaiting parts or overdue on 18 September 2026?
- Tenant 2: Explain the partially approved warranty for D003.
- Tenant 2: Show T2ONLY-001 repairs without querying the parts-order application.
