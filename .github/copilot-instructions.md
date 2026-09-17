# Copilot Instructions

## Project Guidelines
- For this hackathon's LLMaaS integration, prioritize low response latency and remain within a USD 15 budget; use cost-aware model routing and bounded outputs.
- For AfterSalesAI UI work, preserve real API/SQL behavior and data; use modular reusable accessible responsive components, navy/teal/cyan enterprise styling, functional navigation and filtering, 10-row default pagination, and a floating assistant wired to the existing backend. Avoid mock data and unnecessary dependencies. Ensure that the RAG and the Knowledge & SOP UI utilize the business PDFs in API_Documents/Project Documents, while the API_Documents/Project api-docs should not be used as the business knowledge/SOP corpus. Keep the UI library and RAG grounded in the same indexed documents.

## Document Summarization
- For AfterSalesAI document explanations, generate short, dealer-friendly summaries through LLMaaS from the requested business document, rather than pasting raw document excerpts or title-page metadata. 
- Keep document-summary formatting distinct from live operational status answers, cite only the selected grounding documents, and explicitly report generation failures instead of presenting copied text as a generated answer.
- For the After-Sales AI assistant, use the LLM as a grounded orchestration and response-generation layer with medium-length dealer-friendly answers by default; summarize large result sets, retain source citations, and avoid unsupported insights or recommendations.

## Coding Tasks
- For coding tasks in this workspace, proceed through implementation and validation without waiting for additional user approval once work has started.