# Multimodal Agentic RAG — Azure + .NET

A complete, production-ready implementation of **Classic RAG (Phase 1)** and **Agentic RAG (Phase 2)** using Azure services and .NET 9. Supports multimodal document ingestion (text, images, tables, OCR), weekly incremental ingestion, hybrid search with downloadable citations, and a multi-step reasoning agent that queries documents, SQL databases, and the web.

## Solution Structure

```
AgenticRag.slnx
src/
├── AgenticRag.Shared/         # Shared models & enums
├── AgenticRag.DataAccess/     # Azure service integrations
├── AgenticRag.Agent/          # Agentic orchestration & tools
└── AgenticRag.Api/            # ASP.NET Core Web API
infra/
├── main.bicep                 # Azure resource provisioning entry point
└── modules/                  # storage, search, openai, sql, appservice, documentintelligence
docs/
└── index.html                 # Full HTML documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| Application | .NET 9 / C# (ASP.NET Core Web API) |
| Vector Search | Azure AI Search (semantic + hybrid) |
| Embeddings + Chat | Azure OpenAI (text-embedding-ada-002 + GPT-4o) |
| Document Extraction | Azure Document Intelligence (OCR, tables, images) |
| Document Storage | Azure Blob Storage |
| Structured Data | Azure SQL Server |
| Web Search | Azure Bing Search API |
| Infrastructure | Azure Bicep |

## Phase 1 — Classic RAG Features

- ✅ Weekly incremental ingestion (PDFs, Word, images, scanned docs)
- ✅ Multimodal extraction: text, tables, images, OCR via Azure Document Intelligence
- ✅ Sliding-window chunking (500 words, 50 overlap)
- ✅ Batch embedding generation (Azure OpenAI text-embedding-ada-002)
- ✅ Semantic + hybrid search (Azure AI Search with vector + BM25 + RRF)
- ✅ Downloadable citation bundles (JSON)
- ✅ REST API for RAG queries with citation metadata

## Phase 2 — Agentic RAG Features

- ✅ Multi-step reasoning (ReAct pattern, configurable step limit)
- ✅ Tool calling: document search, SQL SELECT queries, web search
- ✅ Multi-source result synthesis
- ✅ Downloadable multi-source citation bundles
- ✅ Response caching (in-memory, configurable TTL)
- ✅ User memorization (preferences + important facts)
- ✅ Chat threads with history, context carry-over, reset options
- ✅ Unique thread IDs

## Quick Start

### 1. Deploy Azure Infrastructure

```bash
az group create --name rg-agentic-rag-dev --location eastus2
az deployment group create \
  --resource-group rg-agentic-rag-dev \
  --template-file infra/main.bicep \
  --parameters environmentName=dev sqlAdminPassword='YourSecureP@ssw0rd!'
```

### 2. Configure Connection Strings

Update `src/AgenticRag.Api/appsettings.json` with outputs from the Bicep deployment, or use environment variables / user secrets:

```bash
cd src/AgenticRag.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
dotnet user-secrets set "AzureSearch:ApiKey" "your-key"
# ... etc
```

### 3. Run the API

```bash
cd src/AgenticRag.Api
dotnet run
```

### 4. Trigger Ingestion

```bash
curl -X POST http://localhost:5000/api/ingestion/run \
  -H "Content-Type: application/json" \
  -d '{"containerName": "documents"}'
```

### 5. Query (Classic RAG)

```bash
curl -X POST http://localhost:5000/api/rag/query \
  -H "Content-Type: application/json" \
  -d '{"query": "What are the main risks?", "searchType": "Hybrid"}'
```

### 6. Agentic Chat

```bash
# Create thread
THREAD=$(curl -s -X POST http://localhost:5000/api/chat/threads | jq -r '.id')

# Send message
curl -X POST "http://localhost:5000/api/chat/threads/$THREAD/messages" \
  -H "Content-Type: application/json" \
  -d '{"content": "Compare Q3 revenue from documents with industry web data"}'
```

## API Reference

| Method | Endpoint | Description |
|---|---|---|
| POST | /api/rag/query | Classic RAG: semantic/hybrid search |
| POST | /api/rag/citations/download | Download citation bundle |
| POST | /api/ingestion/run | Trigger incremental ingestion |
| GET | /api/ingestion/jobs/{id} | Check ingestion job status |
| POST | /api/agent/query | Agentic one-shot query |
| POST | /api/agent/citations/download | Download agent citations |
| POST | /api/chat/threads | Create chat thread |
| GET | /api/chat/threads/{id} | Get thread with history |
| POST | /api/chat/threads/{id}/messages | Send message to thread |
| POST | /api/chat/threads/{id}/reset | Reset thread history |
| GET | /api/memory/{userId} | Get user memory |
| POST | /api/memory/facts | Add important fact |
| POST | /api/memory/preferences | Set user preference |

## Configuration

| Key | Description | Default |
|---|---|---|
| Agent:MaxReasoningSteps | Max LLM reasoning iterations | 5 |
| Agent:CacheExpirationMinutes | Cache TTL | 60 |
| Agent:MaxChatHistoryMessages | Messages kept in thread context | 20 |
| Agent:EnableCaching | Toggle response caching | true |
| Agent:EnableMemorization | Toggle user memory | true |
| Agent:EnableReasoningTrace | Include reasoning trace in response | true |

## Documentation

Full HTML documentation including beginner → advanced concepts, architecture diagrams, flow diagrams, and interview preparation is available at [`docs/index.html`](docs/index.html).
