# AfterSalesAI — Hackathon Project Scaffold

This repository is the initial implementation scaffold for the AI After-Sales Intelligence & Orchestration Assistant.

## Implemented foundation

The Phase 1 registry foundation persists tenant, application, integration source, tool, knowledge-document, and knowledge-chunk metadata in PostgreSQL. The API uses the PostgreSQL registry when `ConnectionStrings__AfterSalesAI` is configured; otherwise it preserves the in-memory scaffold registry for local UI/API runs without a database.

See `docs/README.md` for local configuration and `docs/phase-1-registry-foundation.md` for persistence decisions.

## Architecture

React + TypeScript → ASP.NET Core/.NET 10 → AI Orchestrator → Tool Registry → API / Database adapters + RAG → PostgreSQL/pgvector → LLMaaS

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
