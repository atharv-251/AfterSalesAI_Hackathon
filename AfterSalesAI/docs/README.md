# AfterSalesAI

Reusable AI-powered after-sales orchestration platform.

## Current scaffold

- React + TypeScript frontend
- .NET 10 / ASP.NET Core backend
- Domain/application/infrastructure separation
- Tenant/application/tool/document domain model
- API and database integration source concepts
- Initial in-memory tool registry
- Docker Compose PostgreSQL + pgvector
- Health endpoint
- Starter assistant endpoint

## Intentional scope

The scaffold does **not** yet connect to the organization's live APIs, SQL Server systems, or LLMaaS credentials. Those are introduced in controlled implementation steps.

The assistant is designed to answer with:

- RAG-only knowledge
- Live API data
- Controlled database data
- Hybrid API/DB + RAG context

Business transactions such as claim submission or order placement are not the MVP focus.

## Local run

### Backend

```cmd
cd src\AfterSalesAI.Api
dotnet run --urls http://localhost:5000
```

Health check:

```text
http://localhost:5000/api/health
```

### SQL Server

The local platform database is `AfterSalesAI_Demo` on SQL Server LocalDB or SQL Server 2025 Express. SSMS is optional for administration and verification.

The platform stores tenant, application, integration-source, tool, knowledge-document, and knowledge-chunk records. External APIs and external application databases remain controlled integration boundaries and are not directly exposed to the LLM.

See the repository [README](../README.md) for migration, document ingestion, React startup, and VW Group LLMaaS configuration instructions.

### Knowledge and SOP sources

Knowledge ingestion and the UI library use the business PDFs in `API_Documents/Project Documents`, not the API structure Markdown in `Project api-docs`. The Knowledge & SOP page reads the active SQL index through `/api/knowledge/documents`; it does not bundle repository documents into the frontend.

After migrations, run the following from `AfterSalesAI/` to ingest without starting either server:

```powershell
dotnet run --project src/AfterSalesAI.Api -- --ingest-knowledge=true --tenant-id=00000000-0000-0000-0000-000000000001 --application-id=20000000-0000-0000-0000-000000000001
```

Extraction is local and requires text-based PDFs. Failed extraction leaves the existing index untouched. Successful ingestion deactivates obsolete API-reference knowledge records in the selected scope while preserving operational data and original files.

### Frontend

```cmd
cd frontend\after-sales-ai
npm install
npm run dev
```

The UI expects the backend at `http://localhost:5000` by default. Override with `VITE_API_BASE_URL` if needed.

## Next implementation steps

1. Implement application and integration-source onboarding APIs.
2. Implement API adapters.
3. Implement read-only database adapters.
4. Implement document ingestion and pgvector retrieval.
5. Integrate VW Group LLMaaS / Smart Router.
6. Implement tool-calling orchestrator.
7. Add tenant-scoped RAG and integration routing.
8. Add source-aware UI and demo flows.
9. Containerize application for EC2.
