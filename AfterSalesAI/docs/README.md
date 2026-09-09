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

### PostgreSQL + pgvector

From repository root:

```cmd
copy .env.example .env
docker compose up -d postgres
```

Set a non-production `POSTGRES_PASSWORD` in the local `.env` file. Before running the API with the PostgreSQL registry, set its connection string in the current PowerShell session:

```powershell
$env:ConnectionStrings__AfterSalesAI = "Host=localhost;Port=5432;Database=aftersalesai;Username=aftersales;Password=$env:POSTGRES_PASSWORD"
```

The API applies the checked-in initial registry migration at startup. When no connection string is configured, it remains runnable and uses the original in-memory tool registry.

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
