# Multi-tenant demo: implemented setup and limitations

## Start locally

1. Preserve the existing `AfterSalesAI_Demo` database. No Tenant 1 schema alteration is required.
2. On `(localdb)\MSSQLLocalDB`, execute `DatabaseScripts/02_CreateTenant2AfterSalesDb.sql`, then `DatabaseScripts/03_CreateMultiTenantAICoreDb.sql`, then `DatabaseScripts/04_AddDemoUsersToAICore.sql`. The former training script is not required.
   To copy existing active Tenant 1 knowledge without changing originals, run `dotnet run --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj -- --copy-legacy-knowledge=true`. The inspected database returned zero eligible active documents; new Core guides are seeded and uploads are available. Transfer uses separate scoped application reads/writes, not cross-database SQL.
3. Stop the old Visual Studio debugging session, rebuild, and restart the API. Dependency-injection changes require restart, not just hot reload.
4. From the solution directory, run `dotnet run --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj --urls http://127.0.0.1:5088`.
5. From `frontend/after-sales-ai`, run `npm run dev`. Sign in; the account's tenant determines the accessible workspace.
6. For another SQL instance, configure the three server-side connection strings through user secrets/environment variables. Never put credentials in the React app. The localhost SQL Server 2019 account previously lacked database creation permission.

Configuration keys: `ConnectionStrings:AfterSalesAI`, `ConnectionStrings:Tenant2`, `ConnectionStrings:AICore`, `Tenant2Api:BaseUrl`, `Tenant1WrapperApi:BaseUrl`. Tenant 2 must target Tenant2DemoDb; Core must target MultiTenantAICoreDb. The two HTTP base URLs default to http://127.0.0.1:5088/. If using another port, set both. A single process can host the demo routes; wrappers still use actual HTTP, not direct Tenant 2 SQL access.

Existing VW LLMaaS credentials stay in API user secrets or environment variables. No LLM call is required to test the dealer APIs. Without configured credentials the new dealer chat branch returns its grounded DTO as a deterministic fallback, not a polished narrative. Live provider validity was not tested in the smoke runs.

## Implemented

- Three fixed DbContexts: AfterSalesAIDbContext, Tenant2DbContext and AICoreDbContext.
- Core tenant registry, database references, operation metadata/activation, tenant prompts, documents/chunks and chat sessions/messages.
- Seven Tenant 2 entities mapped to installed aftersales tables; read-only dealer-filtered LINQ projections.
- Three Tenant 2 GET APIs and three Tenant 1 HTTP wrapper APIs.
- Fixed typed client, redirects disabled, bounded response buffering, 10-second upstream timeout, safe partial statuses and cancellation.
- Tenant 1 wrapper response has separate Tenant1Data/Tenant2Data and a dealer-correlation warning. No cross-database joins.
- Tenant-aware chat dispatch with Core active-tenant/operation checks. Tenant 1 combined requests call its wrapper over HTTP. Tenant 2 dispatch cannot select Tenant 1 operations.
- Core chat sessions/messages persisted and checked by TenantId + SessionId. Responses include SessionId; the browser sends it on subsequent messages.
- Tenant selection, separately mounted Tenant 1/Tenant 2 workspaces, dealer API test panels, prompt editing, scoped operation enable/disable, document upload/view/deactivate, and saved-history viewing.
- PDF/DOCX/TXT/Markdown upload, 5 MB file limit, bounded extracted text, lexical chunk retrieval, no OCR.
- Legacy Tenant 1 operational requests now require `?tenantId=00000000-0000-0000-0000-000000000001`; wrong tenant returns 403, missing tenant returns 400.
- Error responses hide connection details and stack traces.

Tenant IDs: Tenant 1 ends in 0001; Tenant 2 ends in 0002. Application IDs are respectively 20000000-0000-0000-0000-000000000001 and 20000000-0000-0000-0000-000000000002.

## Login and authorization

The API now uses local HTTP-only cookie authentication. Every non-health route requires a signed-in user, and API middleware compares every tenantId request value with the server-side `tenant_id` claim. A Tenant 1 session cannot call Tenant 2 routes, documents, operations, wrappers, chats, or history; the equivalent Tenant 2-to-Tenant 1 requests are also rejected with 403. The client cannot select another tenant after login.

Demo credentials are seeded at API startup only when absent:

| Account | Password | Tenant |
|---|---|---|
| tenant1.demo | Tenant1Demo! | Tenant 1 — parts and orders |
| tenant2.demo | Tenant2Demo! | Tenant 2 — vehicle service and warranty |

Passwords are salted PBKDF2-SHA256 hashes with 100,000 iterations in `CoreDemoUsers`; plaintext credentials are not in the SQL script or API responses. These credentials are deliberately visible in the local demo login page and must be replaced/removed before any non-demo deployment. The application now intentionally adds login/authentication, superseding the prior no-authentication demo limitation.

## Routes

Tenant 2: GET `/api/tenant2/dealers/{dealerId}/service-overview`, `/repair-status`, `/warranty-summary`, each with Tenant 2 tenantId query parameter. Repair status accepts optional evaluationDate. Returns 404 for unknown dealer.

Tenant 1: GET `/api/tenant1/wrapper/dealers/{dealerId}/aftersales-overview`, `/repair-readiness`, `/claims-warranty-summary`, each with Tenant 1 tenantId. D001/D002/D003 match; D004 preserves local data with NotFound upstream status; D005 is inactive only in the Tenant 2 service source.

Core: GET `/api/core/tenants`; GET/PUT `/api/core/tenant`; GET `/api/core/operations`; PUT `/api/core/operations/{operationId}`; GET/POST `/api/core/documents`; GET/DELETE `/api/core/documents/{documentId}`; GET `/api/core/sessions`; GET `/api/core/sessions/{sessionId}/messages`. Except the public tenant selection list, all require tenantId. Upload uses multipart form field `file`. Operation update accepts IsActive only. Tenant update accepts SystemPrompt only.

Runnable request examples are in `docs/multi-tenant-api.http`.

## Demo prompts

Tenant 1: Show combined repairs for D001. Show claims and warranty for D002. Show repair readiness for D004. Existing specific parts/order questions still use the preserved local assistant.

Tenant 2: Show repair status for D001. Show warranty for D003. Show repairs for T2ONLY-001. Explain warranty workflow.

DealerId is a dealer-level correlation key, not a transaction link. Never claim an individual shipment caused a repair delay or sum parts claims and warranty amounts as independent liabilities.

## Validation performed

- API compiled successfully to a separate output directory while Visual Studio debugging blocked its build command.
- Final backend build and React TypeScript/Vite production build passed, including management screens.
- All 73 unit tests passed, including 21 new dealer/wrapper tests and four Core isolation tests.
- Nine existing knowledge/API integration tests passed. One pre-existing CreditLimitEur decimal precision warning remains.
- Temporary API smoke tests exercised actual SQL model projection, all three HTTP wrappers, D004 partial response, wrong-tenant 403s, tenant-specific chat dispatch, Core registry, saved messages, and cross-tenant history/document 404s. Temporary process was stopped afterward.
- Smoke chat requests created a small number of demonstration sessions in Core; no tenant business rows were modified.

## Important remaining limitations against the full original brief

This is not a claim that all 26 deliverables are complete to their fullest scope.

- Tenant onboarding is limited to the two fixed demo tenants. UI edits prompts but does not create arbitrary new tenants/connections.
- The original Tenant 1 registry and PDF knowledge tables remain preserved in AfterSalesAI_Demo. Runtime document retrieval and Knowledge & SOP now read Core; an explicit idempotent transfer command copies eligible original documents without deleting them. It does not continually synchronize modified legacy documents. The old HTTP ingestion route returns 410; use Core uploads.
- CoreOperation combines operation metadata into one table rather than separate API configuration, API operation and database-operation tables. Metadata describes fixed compiled operations; changes to arbitrary URLs, SQL, headers, response mappings and timeouts are not dynamically executed. Only activation is editable. This prevents unrestricted SQL/SSRF but is narrower than a complete configuration designer.
- Tenant 2 reads use fixed parameterized EF queries equivalent to approved projections, not the installed stored procedures. Procedure names are metadata, not an unrestricted executor.
- The chat intent router is deterministic keyword routing with one dealer key per question, not general LLM function calling. Up to ten previous tenant/session-owned messages, bounded to 12,000 characters, are supplied as LLM conversation context. Tool selection still requires DealerId in each operational question and never comes from history or model-generated URLs/SQL.
- Document-first routing is selected for process/policy/document/workflow questions; the preserved Tenant 1 assistant also uses the scoped Core retriever/library for fallback knowledge.
- Authorization is tenant-scoped only; it has no roles or per-user administrative permission model. Each demo account can manage its own prompt, documents, operations, and history. Do not expose these demo credentials publicly.
- HTTP smoke checks are not yet all retained as automated end-to-end Test Explorer scenarios. TXT upload/read/deactivation passed live HTTP validation; PDF/DOCX/Markdown upload round trips and browser interaction testing still need validation.
- The existing knowledge/workbook paths in appsettings refer to a different workstation; configure valid paths before legacy ingestion/import. Normal existing SQL reads do not require those files.
- Do not run EF EnsureCreated/Migrate for Core or Tenant 2; their supplied SQL scripts own schema creation. Existing Tenant 1 startup migration behavior remains unchanged.

## Troubleshooting

503 from new routes: check Core script execution, all three connections, database permissions, and API restart. Partial Unavailable/Timeout: verify Tenant2Api:BaseUrl and actual port. Chat UNAVAILABLE: also verify Tenant1WrapperApi:BaseUrl. 403: wrong/inactive tenant or disabled operation. Document upload 400/422: check supported type, size, readable text and no PDF encryption. Blank legacy knowledge: correct KnowledgeSources:RootPath and perform tenant-scoped ingestion. Empty service account T2ONLY-004 is expected; D004 is intentionally absent in Tenant 2.
Runtime knowledge now comes from Core. If no documents appear, upload files in the Core management panel or run the explicit legacy transfer command; the old HTTP ingestion endpoint no longer writes to the Tenant 1 store.
