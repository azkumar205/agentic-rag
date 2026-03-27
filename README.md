# Agentic RAG - Quick Reference Guide

## What is This?

Agentic RAG (Retrieval-Augmented Generation) is an AI agent that answers questions by:
1. **Searching company documents** (PDFs, contracts, policies) via Azure AI Search
2. **Querying business databases** (billing, invoices, vendors) via Azure SQL
3. **Reasoning over results** with Azure OpenAI GPT-4o
4. **Self-correcting** via reflection scoring (ensuring answer quality ≥ 6/10)
5. **Caching responses** via Azure Redis for performance

---

## Quick Start

### 1. Deploy Infrastructure
```bash
cd "e:\Agentic RAG"

az deployment group create \
  --resource-group rg-agentic-rag \
  --template-file infra/main.bicep \
  --parameters suffix=agentic01 sqlAdminPassword=YourStr0ngP@ss1!
```

### 2. Populate Database
```bash
# After infrastructure deploys, run SQL schema
sqlcmd -S sql-agentic01.database.windows.net -d agenticragdb \
  -U sqladmin -P YourStr0ngP@ss1! -i sql/setup.sql
```

### 3. Start API in Debug Mode
```bash
# In VS Code: Press F5 or Debug > Start Debugging
# API listens on: http://localhost:5000
```

### 4. Test the Agent
```bash
curl -X POST http://localhost:5000/api/agent/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What are the contract terms with Contoso?",
    "sessionId": "test-001"
  }'
```

---

## Key Concepts

### Document Search
- **Index**: `agentic-rag-index` (Azure AI Search)
- **How It Works**: 
  1. PDFs uploaded to blob storage → chunked (2000 chars) → embedded (1536 dims)
  2. Chunks stored as searchable documents in index
  3. Query embedding + vector search finds relevant chunks
  4. Semantic reranking improves relevance

- **Adding Documents**: Upload a PDF to `stragagentic01/documents` container → indexer auto-chunks & indexes

### SQL Queries
- **Agent accesses 4 security views** (prevents direct table access):
  - `vw_BillingOverview` — current invoice status per vendor
  - `vw_ContractSummary` — contract metrics
  - `vw_InvoiceDetail` — line item details
  - `vw_VendorAnalysis` — vendor financial analysis

- **Tables** (internal, not exposed to agent):
  - `Vendors`, `Contracts`, `Invoices`, `BillingLineItems`

### Reflection Scoring
- **Purpose**: Ensures answer quality before returning to user
- **How**: LLM evaluates completeness, citations, accuracy (0-10 score)
- **Threshold**: Must be ≥ 6/10 to cache and return
- **Auto-Retry**: If score < 6, agent searches again and refines answer

### Caching
- **Via Redis**: Fast retrieval of answers to repeated questions
- **Semantic Similarity**: ~92% threshold (similar questions can use cached answers)
- **TTL**: 60 minutes per cached answer

---

## Architecture Overview

```
User Request
    ↓
API (ASP.NET 8, :5000)
    ↓
AgentOrchestrator
    ├→ Cache Check (Redis)
    │   └→ If hit: Return cached answer
    │
    ├→ Chat Completion (GPT-4o)
    │   ├→ SearchDocuments
    │   │   ↓
    │   │   Azure AI Search Index
    │   │   ├→ Vector search (content_vector)
    │   │   ├→ Semantic reranking
    │   │   └→ Returns chunks + citations
    │   │
    │   └→ QuerySql
    │       ↓
    │       Azure SQL Database
    │       ├→ Query vw_* views only
    │       ├→ Returns structured data
    │       └→ Returns [SQLSource] citation
    │
    ├→ Reflection Scoring
    │   ├→ Score ≥ 6? → Cache & Return
    │   └→ Score < 6? → Retry with refinement
    │
    └→ Return AgentResponse
        ├→ answer (complete text)
        ├→ toolsUsed (["SearchDocuments", "QuerySql"])
        ├→ textCitations ([DocSource 1], [SQLSource])
        ├→ reflectionScore (0-10)
        └→ reasoningSteps (trace of agent thinking)
```

---

## File Structure

```
Agentic RAG/
├── infra/
│   ├── main.bicep                 ← Infrastructure definition
│   ├── parameters.local.json      ← Deployment params
│   └── deploy.ps1                 ← Deploy script
│
├── src/
│   ├── AgenticRAG.Api/
│   │   ├── Program.cs             ← DI setup, EmbeddingClient
│   │   ├── appsettings.json       ← Config (endpoints, keys)
│   │   └── Controllers/
│   │       └── AgentController.cs
│   │
│   ├── AgenticRAG.Core/
│   │   ├── Agents/
│   │   │   ├── AgentOrchestrator.cs      ← Main agent logic
│   │   │   ├── ReflectionService.cs
│   │   │   └── ConversationMemoryService.cs
│   │   │
│   │   ├── Tools/
│   │   │   ├── DocumentSearchTool.cs     ← Search documents (fixed)
│   │   │   ├── SqlQueryTool.cs           ← Query SQL views
│   │   │   └── ImageCitationTool.cs
│   │   │
│   │   ├── Caching/
│   │   │   └── SemanticCacheService.cs   ← Redis cache
│   │   │
│   │   ├── Models/
│   │   │   ├── AgentRequest.cs
│   │   │   ├── AgentResponse.cs
│   │   │   └── ...
│   │   │
│   │   └── Configuration/
│   │       ├── AzureOpenAISettings.cs
│   │       ├── AzureAISearchSettings.cs
│   │       └── ...
│   │
│   └── AgenticRAG.Setup/
│       └── Program.cs             ← One-time setup utility
│
├── sql/
│   └── setup.sql                  ← Database schema + views
│
├── CHANGES.md                     ← Implementation summary
├── CLEANUP.md                     ← Temp files to remove
└── AgenticRAG.sln                 ← Solution file

```

---

## Azure Resources Created

| Resource | Name | Purpose |
|----------|------|---------|
| **Search Service** | search-agentic01 | Document indexing & vector search |
| **OpenAI** | openai-agentic01 | GPT-4o (chat) + text-embedding-3-large |
| **Storage Account** | stragagentic01 | Document blobs + images |
| **SQL Database** | sql-agentic01 / agenticragdb | Business data (vendors, contracts, invoices) |
| **Redis Cache** | redis-agentic01 | Semantic cache for answers |
| **App Service** | app-agentic01 | Host API |
| **Log Analytics** | log-agentic01 | Diagnostics & monitoring |
| **Key Vault** | kv-agentic01 | Store secrets |

---

## Configuration Reference

### appsettings.json
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://openai-agentic01.openai.azure.com/",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-large",
    "EmbeddingDimensions": 1536,
    "ApiKey": "[from env or Key Vault]"
  },
  "AzureAISearch": {
    "Endpoint": "https://search-agentic01.search.windows.net",
    "IndexName": "agentic-rag-index",
    "SemanticConfig": "agentic-rag-semantic"
  },
  "SqlServer": {
    "ConnectionString": "Server=sql-agentic01.database.windows.net;Database=agenticragdb;Authentication=Active Directory Default;...",
    "AllowedViews": ["vw_BillingOverview", "vw_ContractSummary", "vw_InvoiceDetail", "vw_VendorAnalysis"]
  },
  "Redis": {
    "ConnectionString": "redis-agentic01.redis.cache.windows.net:6380,..."
  },
  "Agent": {
    "ReflectionThreshold": 6,
    "MaxReflectionRetries": 2,
    "SemanticCacheThreshold": 0.92,
    "CacheTtlMinutes": 60
  }
}
```

---

## Debugging Tips

### Issue: API won't start (port 5000 in use)
```powershell
# Find process on port 5000
Get-NetTCPConnection -LocalPort 5000 | Select OwningProcess
# Kill it (if it's old): Stop-Process -Id <PID> -Force
```

### Issue: Document search returns 0 results
1. Check indexer status: `az rest GET indexes/agentic-rag-index/status`
2. Verify documents exist: `az list-blobs --account-name stragagentic01 --container-name documents`
3. Check searchable fields: `chat_id`, `content` must be searchable

### Issue: SQL queries fail with "login failed"
1. Verify connection string uses correct credentials
2. Check SQL firewall: Allow Azure services to access
3. Run `sql/setup.sql` if schema doesn't exist

### Issue: Low reflection scores (< 6/10)
1. Agent didn't use tools (check toolsUsed is populated)
2. Answer lacks citations (check for [DocSource] or [SQLSource])
3. Search returned irrelevant results (try broader query)

---

## Production Deployment

1. **Use Key Vault for secrets**: App reads from `KEYVAULT_ENDPOINTS` instead of appsettings
2. **Update Bicep parameters**: Change SKUs for capacity/cost trade-offs
3. **Enable monitoring**: Configure App Insights alerts on error rates
4. **Set up CI/CD**: Auto-deploy on main branch via Azure Pipelines
5. **Scale index**: Increase indexer batch size for large document uploads
6. **Archive logs**: Set retention policy on Log Analytics workspace

---

## Support & Documentation

- **Infrastructure**: See `infra/main.bicep`
- **API Docs**: Swagger at `http://localhost:5000/swagger` (in dev mode)
- **Changes Log**: See `CHANGES.md` for all implementation details
- **Data Model**: SQL schema in `sql/setup.sql`
