# Copilot Instructions

## Project Guidelines
- For this AfterSalesAI hackathon workspace, Docker must not be used for the local PostgreSQL setup; use a non-Docker alternative.
- For the AfterSalesAI persistence migration, use a new clean local SQL Server database for the migration and demo rather than modifying or deleting the existing populated AfterSalesAI database. Use SQL Server 2025 Express with SSMS and integrated/local configurable authentication. Remove active Docker, PostgreSQL, Supabase, Npgsql, and pgvector dependencies; keep SQL Server-specific behavior confined to Infrastructure; do not proceed to later feature phases after verification.
- Review existing uncommitted changes and incorporate any changes that are relevant to the SQL Server migration; preserve unrelated work.
- Prioritize resolving the remaining System.Security.Cryptography.Xml high-severity vulnerability rather than leaving it documented as an accepted warning.

## API Usage
- Prepare server-side VW Group LLMaaS configuration now so CloudIDP client ID/secret and virtual key can later be added through user secrets or environment variables and tested without code changes.
- Use VW Group LLMaaS only via configurable server-side calls: obtain CloudIDP bearer tokens, supply the virtual key in X-LLM-API-CLIENT-ID, and use the OpenAI-compatible chat-completions endpoint with model smart-router.
- Keep all credentials out of source control, the React client, logs, database records, and prompts.