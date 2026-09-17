# AfterSalesAI Demo Implementation Report

## Database

- Provider: `Microsoft.EntityFrameworkCore.SqlServer` 10.0.0.
- Local database: `AfterSalesAI_Demo` on `(localdb)\MSSQLLocalDB` using integrated authentication.
- EF migrations: `InitialSqlServerRegistry` and `KnowledgeIngestionMetadata`.
- Original scaffold data: 1 tenant, 1 application, 2 integration sources, 3 tool definitions, 19 API-reference Markdown documents, and 83 chunks. That knowledge source is superseded by the business PDF pipeline below.

## Removed stack

- Removed Npgsql and Pgvector packages, PostgreSQL provider calls, PostgreSQL migration artifacts, Docker Compose, PostgreSQL environment configuration, and Docker/PostgreSQL setup documentation.
- No active Npgsql, Pgvector, Supabase, or PostgreSQL provider package remains.

## Demo capabilities

- ASP.NET endpoints: dashboard, orders, order by ID, claims, inventory, assistant chat, indexed knowledge library, and controlled PDF ingestion.
- SQL Server-backed, tenant/application-scoped PDF ingestion and lexical retrieval.
- Controlled operational demo adapter covers order 45001234 delivery exception, rejected claim, in-stock, low-stock, and unavailable part scenarios.
- Assistant returns grounded answers with sources and registered tool names.
- React dashboard consumes only backend endpoints and displays data, alerts, assistant answer, sources, and tools.

## Knowledge source

`C:\Users\D04TJBH\source\Workspaces\Workspace\AfterSalesAI_Hackathon\API_Documents\Project Documents`

The seven business PDFs are extracted locally with PdfPig. Their categorized, page-tagged text is stored in SQL Server and shared by RAG and the Knowledge & SOP page. The original PDFs are not modified. Image-only PDFs require approved OCR before ingestion.

The old `Project api-docs` Markdown describes API structures rather than business knowledge. Successful PDF ingestion deactivates those legacy `API_REFERENCE` records within the selected tenant/application; retrieval and library reads also exclude API references. SQL changes are transactional, and extraction failures preserve the existing index. An offline ingestion command is documented in the project README and exits without starting the backend.

## LLMaaS

Non-secret VW Group LLMaaS configuration is ready for `https://llmapi.ai.vwgroup.com` and `smart-router`. CloudIDP client ID/secret and the virtual key must be provided through user secrets or environment variables. No credentials were stored in source, React, SQL Server, or logs. The current assistant uses deterministic source-grounded fallback output until a server-side LLMaaS client is enabled and validated with supplied keys.

## Original scaffold validation (historical, before source correction)

- EF migrations applied successfully to `AfterSalesAI_Demo`.
- Knowledge ingestion: 19 documents, 83 chunks, 0 failures.
- Hybrid question verified with Order API, Delivery API, and Markdown sources.
- `dotnet build AfterSalesAI.sln`: passed.
- `dotnet test AfterSalesAI.sln`: 4 passed, 0 failed.
- `npm run build`: passed.

## Security remediation

`System.Security.Cryptography.Xml` was updated from the vulnerable stable 10.0.1 version to `11.0.0-rc.1.26425.128`, the newer version available from the configured package source. The .NET 10 solution restored, built, and passed all tests with this version and no NU1903 warning was reported.

## Manual SSMS steps

Connect SSMS to `(localdb)\MSSQLLocalDB` with Windows Authentication, refresh Databases, then expand `AfterSalesAI_Demo` to inspect the EF migration history and registry/knowledge tables.
