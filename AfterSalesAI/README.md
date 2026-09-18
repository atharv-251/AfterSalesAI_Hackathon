# AfterSalesAI — Multi-tenant Demo

The demo preserves the existing Tenant 1 parts application and adds separate Tenant 2 vehicle service data, HTTP wrappers, AI Core configuration and tenant-scoped chat persistence.

**Start here:** [Multi-tenant setup, API routes, validation and remaining limitations](docs/multi-tenant-setup.md). Run database scripts `02_CreateTenant2AfterSalesDb.sql` and `03_CreateMultiTenantAICoreDb.sql`, then restart the API. Existing Tenant 1 business data must not be recreated.

## Implemented foundation

The API uses SQL Server with fixed contexts for AfterSalesAI_Demo, Tenant2DemoDb and MultiTenantAICoreDb. Original Tenant 1 knowledge rows remain preserved; runtime retrieval, new uploads and tenant-scoped sessions use Core. An explicit transfer command copies eligible legacy knowledge. See the setup guide for scope limitations rather than assuming every configuration field is dynamically executable.

See `docs/README.md` for local configuration and `docs/phase-1-registry-foundation.md` for persistence decisions.

## Architecture

React + TypeScript → ASP.NET Core/.NET 10 → tenant/operation validation → fixed API/database operations and keyword document retrieval → SQL Server / VW LLMaaS. Tenant 1 combined requests use HTTP wrappers; no cross-database business joins.

## Key design principles

- The AI works against business capabilities/tools, not vendor-specific APIs.
- New applications are onboarded through application/integration/tool/document registration.
- API and database connectivity are separate integration source types.
- Database access is intended to be controlled and read-only for the MVP.
- Documents are tenant- and application-scoped.
- The agent chooses API, DB, RAG, or a hybrid route based on the information required.
- Core transaction execution is intentionally out of scope for the MVP.

## Folders

- `src/` — .NET solution
- `frontend/after-sales-ai/` — React app
- `tests/` — unit and integration test projects
- `docker/` — future container assets
- `docs/` — implementation notes

See `docs/README.md` for setup instructions and the next implementation phases.

# AfterSalesAI

AfterSalesAI is a multi-tenant automotive after-sales orchestration layer. The local demo combines controlled operational data with tenant-scoped knowledge retrieval through one React conversational interface.

```text
React demo UI → ASP.NET Core API → orchestration → registered operational tools + SQL Server knowledge retrieval → VW Group LLMaaS (when configured)
```

## Stack

- React 19, TypeScript, Vite
- .NET 10, ASP.NET Core, EF Core 10
- SQL Server LocalDB/SQL Server 2025 Express, administered with SSMS
- No Docker, PostgreSQL, Npgsql, pgvector, or Supabase

## Local setup

1. Ensure SQL Server LocalDB or SQL Server 2025 Express is running. The checked-in local configuration targets `(localdb)\MSSQLLocalDB` and database `AfterSalesAI_Demo`.
2. From `AfterSalesAI/`, apply migrations:

```powershell
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
dotnet ef database update --project src/AfterSalesAI.Infrastructure --startup-project src/AfterSalesAI.Api
```

3. Ingest the business knowledge PDFs while both servers are stopped. From `AfterSalesAI/`:

```powershell
dotnet run --project src/AfterSalesAI.Api -- --ingest-knowledge=true --tenant-id=00000000-0000-0000-0000-000000000001 --application-id=20000000-0000-0000-0000-000000000001
```

This command reads `KnowledgeSources:RootPath`, updates only the selected tenant/application's knowledge index and exits without starting an HTTP listener. It does not apply migrations or import operational data. A nonzero exit code or nonempty `Failures` means ingestion did not complete.

4. Start the API:

```powershell
dotnet run --project src/AfterSalesAI.Api --urls http://localhost:5000
```

5. Start React:

```powershell
cd frontend/after-sales-ai
npm install
npm run dev
```

Open the Vite URL. The UI calls the local API and shows sources and tools used.

## Demo questions

- `Why is order 45001234 delayed and what should the dealer do next?`
- `Where is order 45001235?`
- `What is the standard operating procedure for a delayed shipment?`
- `What guidance does the policy document provide for claims?`

## Knowledge sources

- Business knowledge source: `API_Documents/Project Documents`, configured by `KnowledgeSources:RootPath` in API configuration (override with `KnowledgeSources__RootPath` on other machines).
- Seven PDFs: Business Process Document, Error Catalogues, FAQs, Knowledge Base, Policy Document, Standard Operating Procedure, and User Manual.
- PDF text is extracted locally using PdfPig, chunked with source/page metadata, and stored in SQL Server. No LLM calls are needed for ingestion. Scanned/image-only PDFs require separately approved OCR; unreadable or empty source sets leave the existing index unchanged.
- Successful ingestion replaces chunks and deactivates legacy `API_REFERENCE` records and removed PDF records only in the selected tenant/application. Original source files and operational tables are preserved. Re-running ingestion does not duplicate documents.
- `API_Documents/Project api-docs` contains API integration references, **not** business knowledge/SOP sources. These files are no longer bundled in the UI or used by knowledge retrieval.
- Knowledge & SOP loads `GET /api/knowledge/documents?tenantId=...&applicationId=...` and displays the same active SQL-indexed text used by RAG. The current retrieval mechanism is lexical chunk ranking, not embedding/vector search.

When the API is already running, `POST /api/knowledge/ingest?tenantId=...&applicationId=...` performs the same ingestion. Extraction failures return HTTP 422 without replacing the index. Refresh the Knowledge & SOP page after ingestion.

## Knowledge regression tests

Run `dotnet test AfterSalesAI.sln` from `AfterSalesAI/`. Knowledge integration tests use the seven repository PDFs and create/drop uniquely named `AfterSalesAI_KnowledgeTests_*` SQL databases, never `AfterSalesAI_Demo`. They default to LocalDB; set `AFTERSALESAI_TEST_SQL_CONNECTION` to a test SQL Server connection with create/drop database permission if needed. Test HTTP hosts use in-memory transport, not browser-facing server ports.

## LLMaaS configuration

The assistant calls VW Group LLMaaS through the documented CloudIDP client-credentials flow and `/v1/chat/completions` endpoint when all credentials are configured. It uses `smart-router`, sends only normalized approved context, and falls back to the deterministic source-grounded response if the provider is unavailable.

Initialize user secrets once, then store credentials only in user secrets or environment variables:

```powershell
dotnet user-secrets init --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj
dotnet user-secrets set "LlmAas:ClientId" "<CloudIDP client id>" --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj
dotnet user-secrets set "LlmAas:ClientSecret" "<CloudIDP client secret>" --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj
dotnet user-secrets set "LlmAas:VirtualKey" "<VW Group virtual key>" --project src/AfterSalesAI.Api/AfterSalesAI.Api.csproj
```

The base URL, documented CloudIDP token endpoint, and `smart-router` model have safe defaults in configuration. Never commit or expose credentials to React, database records, logs, or prompts.
