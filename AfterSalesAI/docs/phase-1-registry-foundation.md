# Phase 1: PostgreSQL Registry Foundation

## Decision

AfterSalesAI uses PostgreSQL as its platform registry database. The `pgvector` extension is enabled in the initial migration so the same database can store retrieval embeddings in a later RAG phase.

## Registry boundary

The registry stores metadata, not external credentials or unrestricted access details:

- tenants
- applications
- integration sources
- tool definitions
- knowledge document metadata
- knowledge chunk metadata and content

Each integration source stores a configuration reference. Secrets such as API credentials, session cookies, and database passwords are supplied through environment variables or a future secret store.

## Tenant isolation

Applications, integration sources, tool definitions, knowledge documents, and knowledge chunks carry tenant and application identifiers. The PostgreSQL tool registry filters tools by tenant ID, application ID, active tool status, and active integration source status.

The initial schema uses foreign keys and restrictive deletes to protect the registry hierarchy. A later onboarding API must validate that a child record's tenant matches its referenced application or document.

## Local execution

Docker Compose provides PostgreSQL 17 with pgvector. A local `.env` file supplies `POSTGRES_PASSWORD`; it must not be committed. The API uses `ConnectionStrings__AfterSalesAI` and applies migrations at startup when the connection string is configured. Without it, the existing in-memory registry remains available for the scaffold API.

## Deferred work

This phase does not implement external API adapters, SQL Server access, document extraction, embeddings, vector retrieval, LLMaaS, or the AI orchestrator. These capabilities will use the registry contracts and tenant boundaries established here.
