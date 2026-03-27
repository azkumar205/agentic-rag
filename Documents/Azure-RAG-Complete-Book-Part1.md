# Azure RAG â€” Complete Production Guide (Concepts + .NET Implementation)

> **Audience**: Complete beginners (freshers) with zero Azure AI experience  
> **Technology**: .NET 8 / C# / ASP.NET Core Web API  
> **Goal**: Understand every concept AND build a production-ready RAG system end-to-end  
> **Design**: Two complete standalone paths â€” Custom Code OR Azure Managed â€” start from either one

---

## How This Book Works

This book has **two complete build paths**. Both produce a working production RAG system.

| Path | What You Build | Best For |
|------|---------------|----------|
| **Part 2: Custom Code** | Every service in C# â€” extraction, chunking, embedding, indexing | Learning deeply, full control, interview understanding |
| **Part 3: Managed Pipeline** | Azure handles ingestion; you code only the query side | Production speed, less code, automatic scheduling |

**Read Part 1 first** (foundations). Then pick **either** Part 2 or Part 3 â€” both are standalone and complete. No jumping between sections.

---

## Table of Contents

### Part 1: RAG Foundations

## 1. What is RAG and Why It Matters

### RAG = Retrieval-Augmented Generation

RAG (Retrieval-Augmented Generation) is a **design pattern** (not a product or tool) that makes AI (Artificial Intelligence) models answer questions using **your private data** instead of just their training data.

In simple terms: **Retrieve** relevant documents â†’ **Augment** the AI prompt with those documents â†’ **Generate** an accurate answer.

```
WITHOUT RAG:
  You: "What's in our 2025 vendor contract?"
  GPT-4o: "I don't have access to your internal documents."

WITH RAG:
  You: "What's in our 2025 vendor contract?"
  System:
    1. RETRIEVE â€” Searches YOUR documents for relevant sections
    2. AUGMENT  â€” Extracts key parts and adds them to the prompt
    3. GENERATE â€” Sends the context + question to GPT-4o (Generative Pre-trained Transformer 4o)
  Answer: "The 2025 vendor contract with Acme Corp includes a 90-day
  termination clause (Clause 14.2), annual review terms (Clause 8.1)..."
  [Source: vendor-contract-2025.pdf, Page 12-14]
```

### The Three Steps of RAG â€” Explained

| Step | Full Name | What Happens | Real-World Analogy |
|------|-----------|-------------|---------------------|
| **R** | **Retrieve** | Search your documents for the most relevant pieces based on the user's question | A librarian finding the right books from the library |
| **A** | **Augment** | Attach those retrieved pieces to the AI model's prompt as "context" | Handing the relevant books to a researcher before asking a question |
| **G** | **Generate** | The AI model (LLM â€” Large Language Model) reads the context and generates a grounded, accurate answer | The researcher writes a summary based only on the books you gave them |

### Why RAG Exists â€” The Four Problems It Solves

LLMs (Large Language Models) like GPT-4o (Generative Pre-trained Transformer 4o) are powerful but have fundamental limitations. RAG solves all four:

| # | Problem | Without RAG | With RAG | Why It Matters |
|---|---------|------------|----------|----------------|
| 1 | **Hallucination** | The LLM makes up facts that sound convincing but are wrong | Answers are **grounded** in your actual documents â€” the model only uses what you provide | In enterprise (legal, medical, finance), a wrong answer can be catastrophic |
| 2 | **Data Privacy** | You must upload private data to OpenAI's servers for the model to know about it | Data stays **inside your Azure tenant** â€” the LLM only sees the chunks you send per query | Compliance with GDPR (General Data Protection Regulation), HIPAA (Health Insurance Portability and Accountability Act), SOC 2 (System and Organization Controls 2) |
| 3 | **Stale Knowledge** | The model's training data has a cutoff date â€” it doesn't know about yesterday's contract | Uses documents you uploaded **yesterday** (or 5 minutes ago) | Business data changes daily â€” contracts, policies, processes |
| 4 | **No Citations** | The model cannot tell you WHERE information came from | Every answer includes **source document name, page number, and section** | Auditors and users need to verify answers against original documents |

> **ðŸŽ¯ Interview Point**: "RAG keeps data private inside our Azure tenant, grounds answers in actual documents to prevent hallucination, provides citations for auditability, and uses the latest uploaded documents instead of stale training data."

### What RAG Is NOT

| Misconception | Reality |
|---------------|---------|
| "RAG is an Azure product" | RAG is a **design pattern** â€” you implement it using services like Azure AI Search, Azure OpenAI, etc. |
| "RAG means the AI reads your entire database" | RAG retrieves only the **top 5-10 most relevant chunks** per query (not your entire dataset) |
| "RAG replaces fine-tuning" | RAG handles **knowledge** (facts, documents); fine-tuning changes **behavior** (tone, format). They serve different purposes. See Chapter 24 for comparison. |
| "RAG is only for chatbots" | RAG powers document Q&A, search engines, report generators, compliance tools, and any system that needs grounded AI answers |

---

## 2. Core Concepts

> Every term in this section is used throughout the book. Understand these first â€” everything else builds on them.

### 2.1 Embeddings â€” Converting Text to Numbers

**What**: An embedding is a list of numbers (called a **vector**) that captures the **meaning** of a piece of text. Texts with similar meanings produce vectors that are close together in this number space.

**Why we need them**: Computers cannot understand text directly. By converting text into numbers, we can mathematically compare two pieces of text and find how "similar" they are â€” even if they use completely different words.

```
"The contract shall be terminated"  â†’  [0.023, -0.156, 0.891, ...] (3072 numbers)
"This agreement will end"           â†’  [0.025, -0.152, 0.889, ...] (very close â€” similar meaning!)
"Chocolate cake recipe"             â†’  [-0.445, 0.332, 0.001, ...] (totally different â€” unrelated meaning)
```

**How it works in RAG**:
1. **During ingestion**: Every chunk of your document gets converted to an embedding vector and stored in the search index
2. **During query**: The user's question also gets converted to an embedding vector
3. **Search**: Azure AI Search finds chunks whose vectors are **closest** to the question's vector â†’ those are the most relevant chunks

**Embedding models used in this book**:

| Model | Dimensions | Max Input | Best For |
|-------|-----------|-----------|----------|
| `text-embedding-3-small` | 1,536 dimensions | 8,191 tokens | Custom path â€” good accuracy, lower cost |
| `text-embedding-3-large` | 3,072 dimensions | 8,191 tokens | Managed path â€” best accuracy, higher cost |

> **Dimensions** means how many numbers are in each vector. More dimensions = more detail captured about the text's meaning = better search accuracy, but more storage and slightly more cost.

> **ðŸŽ¯ Interview Point**: "Embeddings convert text into high-dimensional vectors that capture semantic meaning. We use text-embedding-3-small (1,536 dimensions) or text-embedding-3-large (3,072 dimensions) from Azure OpenAI. Similar texts produce vectors that are mathematically close, enabling semantic search."

### 2.2 Search Types â€” How We Find Relevant Documents

When a user asks a question, we need to find the most relevant chunks from potentially millions of indexed chunks. There are four levels of search, each building on the previous:

#### Level 1: Keyword Search (BM25 â€” Best Matching 25)

**How it works**: Matches **exact words** in the query against words in your documents. BM25 (Best Matching 25) is the industry-standard algorithm â€” it scores documents based on how often query terms appear, how rare those terms are, and how long the document is.

```
Query: "termination penalties"
Finds: "...termination penalties are defined in section 14.2..."  âœ… (exact words match)
Misses: "...cancellation fees include..."  âŒ (same meaning, different words)
```

**Strengths**: Fast, exact matches, great for names/codes/IDs.
**Weakness**: Cannot understand synonyms or rephrased questions.

#### Level 2: Vector Search (Semantic Search)

**How it works**: Converts both the query and documents into embedding vectors, then finds documents whose vectors are **closest** to the query vector. This catches **meaning**, not just words.

```
Query: "termination penalties"
Finds: "...cancellation fees include..."  âœ… (different words, same meaning!)
Misses: "The termination code is TRM-001"  âŒ (same word "termination" but different meaning)
```

**Strengths**: Understands synonyms, paraphrases, and intent.
**Weakness**: Can miss exact codes, IDs, or specific phrases.

**HNSW (Hierarchical Navigable Small World)**: This is the algorithm Azure AI Search uses internally to make vector search fast. Instead of comparing your query against **every** vector (which would be O(n) â€” checking all n items), HNSW builds a graph structure that finds the nearest vectors in O(log n) time. For 1 million documents, that's ~20 comparisons instead of 1,000,000.

#### Level 3: Hybrid Search (Keyword + Vector Combined)

**How it works**: Runs **both** keyword search and vector search in parallel, then combines their results using **RRF (Reciprocal Rank Fusion)**.

```
Query: "penalties for early termination"

  Keyword (BM25) results:        Vector results:
  1. "termination penalties..."   1. "cancellation fees..."
  2. "penalty clause section..."  2. "early exit charges..."
  3. "termination notice..."      3. "termination penalties..."

  RRF combines both result lists:
    â†’ Documents found by BOTH methods get the highest combined score
    â†’ Documents found by only one method still appear, but ranked lower
```

**RRF (Reciprocal Rank Fusion) Formula**:
```
Combined Score = Î£ (1 / (k + rank))    where k = 60 (constant), rank = position in each result list
```

For example, if a document is rank 1 in keyword and rank 3 in vector:
- Keyword contribution: 1/(60+1) = 0.0164
- Vector contribution: 1/(60+3) = 0.0159
- Combined RRF score: 0.0323

This document scores higher than one that was rank 1 in only one method.

**Why hybrid wins**: It catches both exact-word matches AND meaning-based matches. Very few relevant documents slip through.

#### Level 4: Hybrid + Semantic Ranking (Production Standard)

**How it works**: After hybrid search returns the top results, an AI model **re-reads each result** and re-ranks them based on true relevance to the question. This is not just word matching or vector distance â€” it's a deep language understanding model.

```
Hybrid returns 50 results
     â†“
Semantic Ranker re-reads each one
     â†“
Re-ordered by TRUE relevance (not just keyword/vector scores)
     â†“
Top 5 go to GPT-4o as context
```

**Adds ~200ms (milliseconds) latency** but significantly improves answer quality.

**Summary â€” Search Levels Build on Each Other:**

| Level | Method | What It Catches | What It Misses | Latency |
|-------|--------|----------------|----------------|---------|
| 1 | Keyword (BM25) | Exact words, names, codes | Synonyms, rephrased questions | ~50ms |
| 2 | Vector (Semantic) | Synonyms, meaning, intent | Exact codes, specific phrases | ~100ms |
| 3 | **Hybrid** (Keyword + Vector via RRF) | Nearly everything | Very few gaps | ~120ms |
| 4 | **Hybrid + Semantic Ranking** | True relevance | Almost nothing | ~300ms |

> **ðŸŽ¯ Production standard for this book**: **Hybrid Search + Semantic Ranking** (Level 4) â€” used in both Custom and Managed paths.

> **ðŸŽ¯ Interview Point**: "We use hybrid search which combines BM25 keyword search with vector search using RRF (Reciprocal Rank Fusion). Semantic ranking then re-ranks results using a deep language model. This catches both exact-term and meaning-based matches with the highest relevance accuracy."

### 2.3 Document Intelligence â€” Extracting Text from Documents

**Full name**: Azure AI Document Intelligence (formerly called Azure Form Recognizer)

**What it does**: Extracts text, tables, and structural information from any document format â€” PDFs (Portable Document Format), scanned images, Word documents, Excel spreadsheets, and more.

**Why it matters for RAG**: Before you can search a document, you need to extract its text. PDFs don't store text in a simple way â€” text is positioned at coordinates, tables are visual layouts, and scanned documents are just images. Document Intelligence handles all of this.

| Feature | What It Extracts | Example |
|---------|------------------|---------|
| **Text** | All text content, including scanned pages via OCR (Optical Character Recognition) | A scanned contract â†’ full text output |
| **Tables** | Rows, columns, headers â€” preserved as structured data | A pricing table in a PDF â†’ row-by-row data |
| **Structure** | Headings, sections, paragraphs with semantic roles (`title`, `sectionHeading`, `pageHeader`, `pageFooter`, `body`) | Knows that "Section 14: Termination" is a heading, not body text |
| **Key-Value Pairs** | Field labels and their values | "Invoice Number: INV-2025-001" â†’ key="Invoice Number", value="INV-2025-001" |

**Models available**:

| Model | Use Case | What It Does |
|-------|----------|-------------|
| `prebuilt-read` | Simple text extraction | Fast, text-only, no structure |
| **`prebuilt-layout`** | **Production RAG** â€” used in this book | Full structure: text + tables + headings + roles |
| `prebuilt-invoice` | Invoice processing | Pre-trained fields for invoices |
| `prebuilt-receipt` | Receipt processing | Pre-trained fields for receipts |

> **For RAG, always use `prebuilt-layout`** â€” it gives the richest structural information, which is essential for intelligent chunking (see Section 2.4).

> **ðŸŽ¯ Interview Point**: "We use Azure AI Document Intelligence with the prebuilt-layout model to extract text, tables, and document structure from PDFs. This gives us semantic roles like sectionHeading and body text, which enables layout-aware chunking."

### 2.4 Chunking â€” Splitting Documents into Searchable Pieces

**What**: Chunking is the process of splitting a long document into smaller pieces (called **chunks**) that can be individually indexed and searched.

**Why we chunk**: 
1. **LLM context limits** â€” GPT-4o has a 128K token context window, but sending an entire 200-page contract would be wasteful and expensive. We only send the 5 most relevant chunks (~2,500 tokens total).
2. **Search precision** â€” If you index an entire 200-page document as one item, searching for "termination clause" returns the entire document. With chunking, it returns just the 500-token chunk containing the termination clause.
3. **Relevance** â€” Smaller chunks mean more precise search results. The AI gets exactly the right context, not a haystack with a needle.

**Chunk size**: 500-1,000 tokens per chunk (approximately 375-750 words or 2,000-4,000 characters).

**Chunking Strategies:**

| Strategy | How It Works | Best For | Used In This Book? |
|----------|-------------|----------|-------------------|
| **Fixed-Size** | Split every N characters regardless of content | Simple text, logs | No â€” too crude |
| **Sentence-Based** | Split at sentence boundaries | Plain text, emails | No |
| **Layout-Aware** | Use document structure (headings, paragraphs, tables) to decide where to split | **Production RAG â€” PDFs, contracts, reports** | **Yes (Custom Path)** |
| **Semantic** | AI-driven: split where the topic changes | High-value documents | No â€” too expensive at scale |
| **Page-Based** | Split by page boundaries | The Managed Path indexer uses this internally | **Yes (Managed Path)** |

**Critical Chunking Rules:**

| Rule | Why |
|------|-----|
| **Never split a table across two chunks** | A table row in one chunk and its header in another is useless |
| **Exclude headers and footers** (`pageHeader`, `pageFooter`) | "Page 5 of 20" and "Confidential" add noise, not value |
| **Use 10-20% overlap between chunks** | If a key fact spans a chunk boundary, overlap ensures it appears fully in at least one chunk |
| **Preserve metadata** (filename, page number, section heading) | Needed for citations: "Source: contract.pdf, Page 12, Section 14" |

**Example â€” How a 30-page contract gets chunked:**
```
30-page contract (PDF)
     â†“
Document Intelligence extracts text + structure
     â†“
42 chunks created:
  Chunk 1:  "1. DEFINITIONS â€” In this agreement..." (800 tokens)
  Chunk 2:  "2. SCOPE OF SERVICES â€” The vendor shall..." (750 tokens)
  ...
  Chunk 14: "14. TERMINATION â€” Either party may terminate..." (600 tokens)
  ...
  Chunk 42: "APPENDIX C â€” Pricing Schedule [TABLE]..." (900 tokens)
     â†“
Each chunk gets: text + embedding vector + metadata (title, page, section)
     â†“
All 42 chunks indexed in Azure AI Search
```

> **ðŸŽ¯ Interview Point**: "We use layout-aware chunking in the custom path and page-based chunking in the managed path. Chunk size is 500-1,000 tokens with 10-20% overlap. We never split tables across chunks, and we exclude headers/footers to reduce noise."

### 2.5 Tokens â€” What AI Models Actually Process

**What**: A token is the smallest unit that an AI model reads and generates. It is NOT the same as a word or a character â€” it's somewhere in between.

**Approximate conversions:**
- 1 token â‰ˆ 4 characters â‰ˆ 0.75 words
- 100 tokens â‰ˆ 75 words
- 1,000 tokens â‰ˆ 750 words â‰ˆ about 1.5 pages of text

**Why tokens matter**: Every API (Application Programming Interface) call to Azure OpenAI is billed per token. Both the **input** (your prompt + context) and the **output** (the model's answer) count.

**Models and token limits used in this book:**

| Model | Type | Context Window | Cost per 1M Input Tokens | Cost per 1M Output Tokens |
|-------|------|----------------|--------------------------|---------------------------|
| GPT-4o (Generative Pre-trained Transformer 4o) | Chat / Answer generation | 128,000 tokens | $2.50 | $10.00 |
| GPT-4o-mini | Chat / Answer generation (cheaper) | 128,000 tokens | $0.15 | $0.60 |
| text-embedding-3-small | Embedding generation | 8,191 tokens | $0.02 | N/A (embeddings only) |
| text-embedding-3-large | Embedding generation | 8,191 tokens | $0.13 | N/A (embeddings only) |

**Typical RAG query token breakdown:**

```
System prompt:            ~200 tokens   (instructions to the AI)
User question:            ~30 tokens    ("What are the termination penalties?")
Context (5 chunks):       ~2,500 tokens (the 5 most relevant chunks)
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Total INPUT:              ~2,730 tokens
AI answer OUTPUT:         ~500 tokens
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Total per query:          ~3,230 tokens

Cost per query (GPT-4o):
  Input:  2,730 / 1,000,000 Ã— $2.50  = $0.0068
  Output:   500 / 1,000,000 Ã— $10.00 = $0.0050
  â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  Total:  ~$0.012 per query  (~â‚¹1 per query)
```

> **ðŸŽ¯ Interview Point**: "A typical RAG query costs about $0.012 with GPT-4o. The input includes ~200 tokens for the system prompt, ~30 for the question, and ~2,500 for the top 5 retrieved chunks. The output is ~500 tokens for the answer."

### 2.6 Azure Services Used in This Book â€” Complete Map

> This section maps every Azure service used in this book, what it does, and which path uses it.

| Azure Service | Full Name | What It Does in RAG | Custom Path | Managed Path |
|---------------|-----------|---------------------|-------------|--------------|
| **Azure AI Search** | Azure AI Search (formerly Azure Cognitive Search) | Stores and searches document chunks using keyword, vector, and hybrid search | âœ… | âœ… |
| **Azure OpenAI** | Azure OpenAI Service | Hosts GPT-4o (chat/answers) and embedding models (text-to-vector) | âœ… | âœ… |
| **Azure Blob Storage** | Azure Blob (Binary Large Object) Storage | Stores uploaded PDF documents | âœ… | âœ… |
| **Azure AI Document Intelligence** | Azure AI Document Intelligence (formerly Form Recognizer) | Extracts text, tables, and structure from PDFs | âœ… | âŒ (indexer does it) |
| **Azure App Service** | Azure App Service | Hosts the .NET Web API (Application Programming Interface) | âœ… | âœ… |
| **Azure Key Vault** | Azure Key Vault | Stores secrets, connection strings, API keys securely | âœ… | âœ… |
| **Azure Monitor / Application Insights** | Azure Monitor with Application Insights | Logging, metrics, distributed tracing, alerting | âœ… | âœ… |
| **Microsoft Entra ID** | Microsoft Entra ID (formerly Azure Active Directory / Azure AD) | Authentication and Managed Identity (no passwords in code) | âœ… | âœ… |

### 2.7 Key Terminology Quick Reference

| Term | Full Form | Simple Meaning |
|------|-----------|---------------|
| **RAG** | Retrieval-Augmented Generation | Design pattern: retrieve docs â†’ augment prompt â†’ generate answer |
| **LLM** | Large Language Model | AI model trained on massive text data (e.g., GPT-4o) |
| **GPT** | Generative Pre-trained Transformer | Type of LLM by OpenAI â€” generates human-like text |
| **API** | Application Programming Interface | A way for programs to communicate (your app calls Azure OpenAI's API) |
| **REST** | Representational State Transfer | An API style using HTTP methods (GET, POST, PUT, DELETE) |
| **SDK** | Software Development Kit | Library/package that simplifies API calls (e.g., `Azure.Search.Documents` NuGet package) |
| **NuGet** | NuGet Package Manager | .NET's package manager (like npm for JavaScript) |
| **BM25** | Best Matching 25 | Industry-standard keyword search algorithm |
| **RRF** | Reciprocal Rank Fusion | Algorithm that combines results from multiple search methods |
| **HNSW** | Hierarchical Navigable Small World | Graph algorithm for fast vector search â€” O(log n) instead of O(n) |
| **OCR** | Optical Character Recognition | Converting scanned images/photos of text into actual text |
| **PDF** | Portable Document Format | Common document format â€” not plain text, needs extraction |
| **RBAC** | Role-Based Access Control | Granting permissions based on roles (e.g., "Search Index Data Reader") |
| **HTTPS** | Hypertext Transfer Protocol Secure | Encrypted communication between your app and Azure services |
| **CI/CD** | Continuous Integration / Continuous Deployment | Automated build, test, and deploy pipelines (e.g., GitHub Actions) |
| **IaC** | Infrastructure as Code | Defining cloud resources in code files instead of clicking in portal |
| **GDPR** | General Data Protection Regulation | EU data privacy law |
| **SOC 2** | System and Organization Controls 2 | Security compliance standard for service organizations |

---

## 3. End-to-End Architecture

### 3.1 System Overview

This is the complete architecture of the RAG system you will build. Every box is an Azure service. The arrows show data flow.

```
                          USERS (Browser / Mobile / API Client)
                                        â”‚
                                   HTTPS request
                                        â”‚
                                        â–¼
                        â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
                        â”‚  .NET 8 Web API               â”‚
                        â”‚  (Hosted on Azure App Service) â”‚
                        â”‚                                â”‚
                        â”‚  1. Receives user question      â”‚
                        â”‚  2. Calls Azure AI Search       â”‚
                        â”‚  3. Sends context to GPT-4o     â”‚
                        â”‚  4. Returns answer + citations   â”‚
                        â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                                   â”‚
               â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
               â”‚           â”‚               â”‚          â”‚            â”‚
               â–¼           â–¼               â–¼          â–¼            â–¼
         Blob Storage  Doc Intel      Azure OpenAI  AI Search  Key Vault
         (store PDFs)  (extract text) (GPT + Embed) (index &   (secrets &
                                                     search)    config)
```

**How the pieces connect:**

| Service | Role | Analogy |
|---------|------|---------|
| **Blob Storage** | Stores the original PDF files | The filing cabinet |
| **Document Intelligence** | Extracts text/tables from PDFs (Custom Path only) | The person who reads and transcribes documents |
| **Azure OpenAI** | Generates embeddings AND final answers | The brain â€” both for indexing and answering |
| **Azure AI Search** | Stores chunks + vectors, runs search queries | The searchable library catalog |
| **Key Vault** | Stores secrets securely (accessed via Managed Identity) | The locked safe |
| **App Service** | Hosts the .NET Web API that users call | The front desk |

### 3.2 Two Pipelines â€” Ingestion and Query

Every RAG system has exactly **two pipelines**. They are separate â€” ingestion runs in the background; query runs on every user request.

**Pipeline 1: Ingestion (Document Processing) â€” Runs Once Per Document**

```
Upload PDF to Blob Storage
        â”‚
        â–¼
Document Intelligence extracts text, tables, and structure
  (Custom Path: your .NET code calls Doc Intel API)
  (Managed Path: the indexer's built-in document cracking does this)
        â”‚
        â–¼
Chunking splits the text into 500-1,000 token pieces
  (Custom Path: your .NET code does layout-aware chunking)
  (Managed Path: the SplitSkill does page-based chunking)
        â”‚
        â–¼
Embedding converts each chunk into a vector (list of numbers)
  (Custom Path: your .NET code calls Azure OpenAI embedding API)
  (Managed Path: the AzureOpenAIEmbeddingSkill does this)
        â”‚
        â–¼
Azure AI Search stores each chunk as a document:
  { chunk_id, text, embedding_vector, title, page_number, category }
```

**Pipeline 2: Query (User Asks a Question) â€” Runs On Every Request**

```
User asks: "What are the termination penalties?"
        â”‚
        â–¼
.NET Web API receives the HTTP request
        â”‚
        â–¼
Hybrid Search (keyword BM25 + vector) finds relevant chunks
        â”‚
        â–¼
Semantic Ranking re-orders results by true relevance
        â”‚
        â–¼
Top 5 chunks selected and formatted as "context"
        â”‚
        â–¼
GPT-4o receives: system prompt + context (5 chunks) + user question
        â”‚
        â–¼
GPT-4o generates answer using ONLY the provided context
        â”‚
        â–¼
Response returned: { answer, citations: [{source, page, section}], token_usage }
```

### 3.3 Custom Path vs Managed Path â€” Overview

| Aspect | Custom Code Path (Part 2) | Managed Path (Part 3) |
|--------|--------------------------|----------------------|
| **Ingestion** | You write all C# code: extract, chunk, embed, index | Azure AI Search indexer + skillset handles everything |
| **Query** | You write the query + chat code | You write the query + chat code (same) |
| **Control** | Full control over every step | Less control, but less code to maintain |
| **Code lines** | ~1,500 lines of C# | ~500 lines of C# (query only) |
| **Best for** | Learning deeply, custom logic, interviews | Production speed, automatic scheduling |
| **Scheduling** | You trigger ingestion (CI/CD or manual) | Indexer runs on a schedule (e.g., every 5 minutes) |

> Both paths produce the same end result: a searchable index of document chunks that your .NET API queries to generate AI answers.

### 3.4 Real Numbers â€” What to Expect

| Metric | Ingestion (30-page PDF) | Query (Single Question) |
|--------|------------------------|------------------------|
| **Time** | ~20 seconds | ~2-4 seconds |
| **Cost** | ~$0.05 (embedding all chunks) | ~$0.012 (search + GPT-4o answer) |
| **Output** | ~42 indexed chunks in AI Search | 1 answer with citations |
| **API calls** | 1 Doc Intel + 42 embedding + 42 index | 1 search + 1 GPT-4o |

**At scale â€” 10,000 documents:**

| Metric | Value |
|--------|-------|
| Total chunks | ~400,000 |
| Index size | ~2 GB (Basic tier handles up to 15 GB) |
| Query latency (P95) | ~3 seconds |
| Monthly cost (1,000 queries/day) | ~$500-700/month (see Chapter 18 for full breakdown) |

> **ðŸŽ¯ Interview Point**: "Our RAG system processes a 30-page PDF in about 20 seconds, creating roughly 42 chunks. Each query takes 2-4 seconds and costs about $0.012. The system scales to 400K+ chunks on a Basic tier search service."

---


# Part 2: Complete Custom Code Path (Standalone)

> **Start here if**: You want to understand every piece of RAG, write all services yourself, and learn what happens at each step.  
> **What you build**: Full ingestion pipeline + query pipeline in C#  
> **Duration**: ~4-5 hours to complete all steps  
> **Prerequisite**: Part 1 (concepts only)

---

## 4. Azure Setup â€” Custom Path

### 4.1 What You Need to Create

| # | Resource | Purpose | SKU |
|---|----------|---------|-----|
| 1 | Resource Group | Container for all resources | â€” |
| 2 | Azure AI Search | Index and search your documents | Basic ($74/mo) |
| 3 | Azure OpenAI | GPT-4o + Embeddings | Standard S0 |
| 4 | Azure Storage (Blob) | Store uploaded documents | Standard LRS |
| 5 | Document Intelligence | Extract text from PDFs | Standard S0 |
| 6 | Key Vault | Store API keys securely | Standard |

### 4.2 Step-by-Step Portal Setup

#### Step 1: Create Resource Group

```
Portal > Resource Groups > Create
  Subscription:    [Your subscription]
  Resource Group:  rg-rag-custom-dev
  Region:          Central India
  > Review + Create > Create
```

#### Step 2: Create Azure AI Search

```
Portal > "Azure AI Search" > Create
  Resource Group:  rg-rag-custom-dev
  Service name:    search-rag-custom-[unique]
  Location:        Central India
  Pricing tier:    Basic
  > Review + Create > Create

After creation:
  Go to resource > Settings > Keys
  Copy: Admin Key, Query Key, Endpoint URL
```

> **Why Basic, not Free?** Free tier: 1 index, 50 MB, no semantic ranker. Basic: 15 indexes, 2 GB, semantic ranker enabled. Minimum for a real project.

#### Step 3: Create Azure OpenAI

```
Portal > "Azure OpenAI" > Create
  Resource Group:  rg-rag-custom-dev
  Region:          Central India
  Name:            openai-rag-custom-[unique]
  Pricing:         Standard S0
  > Review + Create > Create

After creation:
  Go to resource > Model Deployments > Deploy Model:
    Model 1: gpt-4o          Deployment name: gpt-4o
    Model 2: text-embedding-3-small  Deployment name: text-embedding-3-small
  
  Go to Keys and Endpoint:
    Copy: Key 1, Endpoint
```

#### Step 4: Create Storage Account

```
Portal > "Storage accounts" > Create
  Resource Group:  rg-rag-custom-dev
  Name:            stragcustom[unique]
  Region:          Central India
  Performance:     Standard
  Redundancy:      LRS
  > Review + Create > Create

After creation:
  Go to resource > Data storage > Containers > + Container
    Name: documents
    Access: Private
  
  Go to Security + networking > Access keys
    Copy: Connection string
```

#### Step 5: Create Document Intelligence

```
Portal > "Document Intelligence" > Create
  Resource Group:  rg-rag-custom-dev
  Region:          Central India
  Name:            docint-rag-custom-[unique]
  Pricing:         Standard S0
  > Review + Create > Create

After creation:
  Go to Keys and Endpoint:
    Copy: Key 1, Endpoint
```

#### Step 6: Create Key Vault

```
Portal > "Key Vault" > Create
  Resource Group:  rg-rag-custom-dev
  Name:            kv-rag-custom-[unique]
  Region:          Central India
  Pricing:         Standard
  > Review + Create > Create

After creation:
  Go to Secrets > + Generate/Import:
    Add each secret:
      SearchApiKey          = [your search admin key]
      SearchEndpoint        = [your search endpoint]
      OpenAIApiKey          = [your openai key]
      OpenAIEndpoint        = [your openai endpoint]
      StorageConnection     = [your storage connection string]
      DocIntelligenceKey    = [your doc intelligence key]
      DocIntelligenceEndpoint = [your doc intelligence endpoint]
```

### 4.3 Collect Your Configuration

Save these â€” you need them in the next chapter:

```
Search Endpoint:           https://search-rag-custom-xxx.search.windows.net
Search Admin Key:          xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
OpenAI Endpoint:           https://openai-rag-custom-xxx.openai.azure.com/
OpenAI Key:                xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Storage Connection:        DefaultEndpointsProtocol=https;AccountName=...
Doc Intelligence Endpoint: https://docint-rag-custom-xxx.cognitiveservices.azure.com/
Doc Intelligence Key:      xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## 5. .NET Project Setup

### 5.1 Create the Project

```bash
dotnet new webapi -n AzureRAG.Custom -o AzureRAG.Custom
cd AzureRAG.Custom
```

### 5.2 Install NuGet Packages

```bash
dotnet add package Azure.AI.OpenAI --version 2.1.0
dotnet add package Azure.AI.FormRecognizer --version 4.1.0
dotnet add package Azure.Search.Documents --version 11.6.0
dotnet add package Azure.Storage.Blobs --version 12.19.1
dotnet add package Azure.Identity --version 1.13.1
dotnet add package Azure.Security.KeyVault.Secrets --version 4.7.0
dotnet add package Microsoft.Extensions.Http.Resilience --version 8.0.0
dotnet add package Microsoft.Extensions.Caching.Memory --version 8.0.0
```

### 5.3 Project Structure

```
AzureRAG.Custom/
  Controllers/
    DocumentsController.cs     â† Upload + ingest documents
    QueryController.cs         â† Ask questions
    HealthController.cs        â† Health check endpoint
  Services/
    ExtractionService.cs       â† Document Intelligence
    ChunkingService.cs         â† Smart chunking
    EmbeddingService.cs        â† Azure OpenAI embeddings
    SearchIndexService.cs      â† Create/manage search index
    SearchService.cs           â† Hybrid search + ranking
    RagService.cs              â† Orchestrate search â†’ GPT
    BlobStorageService.cs      â† Upload/download files
  Models/
    DocumentChunk.cs           â† Core data model
    SearchRequest.cs           â† Query request
    SearchResponse.cs          â† Query response with citations
  Program.cs                   â† DI registration
  appsettings.json             â† Config (non-secret)
  appsettings.Development.json â† Local dev overrides
```

### 5.4 Configuration

**appsettings.json** â€” safe, non-secret values:

```json
{
  "AzureSearch": {
    "Endpoint": "https://search-rag-custom-xxx.search.windows.net",
    "IndexName": "rag-index",
    "SemanticConfigName": "rag-semantic-config"
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-rag-custom-xxx.openai.azure.com/",
    "ChatDeployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-small",
    "EmbeddingDimensions": 1536
  },
  "DocumentIntelligence": {
    "Endpoint": "https://docint-rag-custom-xxx.cognitiveservices.azure.com/"
  },
  "ChunkingOptions": {
    "MaxTokensPerChunk": 512,
    "OverlapTokens": 50,
    "ExcludeRoles": [ "pageHeader", "pageFooter", "pageNumber" ]
  }
}
```

**appsettings.Development.json** â€” API keys for local dev only:

```json
{
  "AzureSearch": {
    "ApiKey": "your-search-admin-key"
  },
  "AzureOpenAI": {
    "ApiKey": "your-openai-key"
  },
  "DocumentIntelligence": {
    "ApiKey": "your-doc-intelligence-key"
  },
  "BlobStorage": {
    "ConnectionString": "your-storage-connection-string"
  }
}
```

> **Security**: `appsettings.Development.json` is in `.gitignore`. Production uses Key Vault via Managed Identity (Chapter 10).

### 5.5 Core Models

**DocumentChunk.cs**:

```csharp
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

public class DocumentChunk
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string ChunkId { get; set; } = string.Empty;

    [SearchableField(AnalyzerName = "en.microsoft")]
    public string Content { get; set; } = string.Empty;

    [SearchableField(AnalyzerName = "en.microsoft")]
    public string Title { get; set; } = string.Empty;

    [VectorSearchField(
        VectorSearchDimensions = 1536,
        VectorSearchProfileName = "vector-profile")]
    public ReadOnlyMemory<float>? ContentVector { get; set; }

    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string Category { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string SourceDocument { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public int PageNumber { get; set; }

    [SimpleField(IsFilterable = true)]
    public int ChunkIndex { get; set; }

    [SimpleField(IsFilterable = true)]
    public DateTimeOffset UploadedAt { get; set; }

    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string DocumentType { get; set; } = string.Empty;
}
```

**SearchRequest.cs** / **SearchResponse.cs**:

```csharp
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int TopResults { get; set; } = 5;
    public bool UseSemanticRanking { get; set; } = true;
}

public class SearchResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public TokenUsage Tokens { get; set; } = new();
}

public class Citation
{
    public string Content { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public double RelevanceScore { get; set; }
}

public class TokenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
}
```

### 5.6 Program.cs â€” Service Registration

```csharp
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Azure AI Search
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("AzureSearch");
    var endpoint = new Uri(config["Endpoint"]!);

    // Use API key in development, Managed Identity in production
    if (!string.IsNullOrEmpty(config["ApiKey"]))
        return new SearchClient(endpoint, config["IndexName"]!, new AzureKeyCredential(config["ApiKey"]!));

    return new SearchClient(endpoint, config["IndexName"]!, new DefaultAzureCredential());
});

builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("AzureSearch");
    var endpoint = new Uri(config["Endpoint"]!);

    if (!string.IsNullOrEmpty(config["ApiKey"]))
        return new SearchIndexClient(endpoint, new AzureKeyCredential(config["ApiKey"]!));

    return new SearchIndexClient(endpoint, new DefaultAzureCredential());
});

// Azure OpenAI
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("AzureOpenAI");
    var endpoint = new Uri(config["Endpoint"]!);

    if (!string.IsNullOrEmpty(config["ApiKey"]))
        return new AzureOpenAIClient(endpoint, new AzureKeyCredential(config["ApiKey"]!));

    return new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
});

// Blob Storage
builder.Services.AddSingleton(sp =>
{
    var connStr = builder.Configuration["BlobStorage:ConnectionString"];
    if (!string.IsNullOrEmpty(connStr))
        return new BlobServiceClient(connStr);

    // Use Managed Identity in production
    var endpoint = new Uri(builder.Configuration["BlobStorage:Endpoint"]!);
    return new BlobServiceClient(endpoint, new DefaultAzureCredential());
});

// Application services
builder.Services.AddSingleton<ExtractionService>();
builder.Services.AddSingleton<ChunkingService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<SearchIndexService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<BlobStorageService>();

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
```

---

## 6. Build the Ingestion Pipeline

> This is **the heart of the Custom Code path**. You write every service that processes documents from upload to indexed search chunks.

### 6.1 Flow Overview

```
PDF Upload â†’ Blob Storage â†’ Document Intelligence â†’ Chunking â†’ Embedding â†’ Search Index
```

### 6.2 BlobStorageService â€” Store Documents

```csharp
public class BlobStorageService
{
    private readonly BlobServiceClient _blobClient;
    private const string ContainerName = "documents";

    public BlobStorageService(BlobServiceClient blobClient)
    {
        _blobClient = blobClient;
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        var container = _blobClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync();

        var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{fileName}";
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders
        {
            ContentType = contentType
        });

        return blob.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string blobUri)
    {
        var blob = new Azure.Storage.Blobs.BlobClient(new Uri(blobUri));
        var download = await blob.DownloadStreamingAsync();
        return download.Value.Content;
    }
}
```

### 6.3 ExtractionService â€” Extract Text from PDFs

This is where Document Intelligence turns PDFs into structured text.

```csharp
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

public class ExtractionService
{
    private readonly DocumentAnalysisClient _client;

    public ExtractionService(IConfiguration config)
    {
        var endpoint = new Uri(config["DocumentIntelligence:Endpoint"]!);
        var key = config["DocumentIntelligence:ApiKey"];

        _client = !string.IsNullOrEmpty(key)
            ? new DocumentAnalysisClient(endpoint, new AzureKeyCredential(key))
            : new DocumentAnalysisClient(endpoint, new Azure.Identity.DefaultAzureCredential());
    }

    public async Task<ExtractionResult> ExtractAsync(Stream document, string fileName)
    {
        // Use prebuilt-layout for richest structure
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            document);

        var result = operation.Value;
        var pages = new List<PageContent>();

        foreach (var page in result.Pages)
        {
            var paragraphs = result.Paragraphs
                .Where(p => p.BoundingRegions.Any(r => r.PageNumber == page.PageNumber))
                .Select(p => new ParagraphContent
                {
                    Text = p.Content,
                    Role = p.Role ?? "body",
                    PageNumber = page.PageNumber
                })
                .ToList();

            var tables = result.Tables
                .Where(t => t.BoundingRegions.Any(r => r.PageNumber == page.PageNumber))
                .Select(ConvertTable)
                .ToList();

            pages.Add(new PageContent
            {
                PageNumber = page.PageNumber,
                Paragraphs = paragraphs,
                Tables = tables
            });
        }

        return new ExtractionResult
        {
            FileName = fileName,
            Pages = pages,
            TotalPages = result.Pages.Count
        };
    }

    private TableContent ConvertTable(DocumentTable table)
    {
        var rows = new List<List<string>>();
        var maxRow = table.Cells.Max(c => c.RowIndex);
        var maxCol = table.Cells.Max(c => c.ColumnIndex);

        for (int r = 0; r <= maxRow; r++)
        {
            var row = new List<string>();
            for (int c = 0; c <= maxCol; c++)
            {
                var cell = table.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == c);
                row.Add(cell?.Content ?? "");
            }
            rows.Add(row);
        }

        return new TableContent
        {
            Rows = rows,
            AsMarkdown = ConvertTableToMarkdown(rows)
        };
    }

    private string ConvertTableToMarkdown(List<List<string>> rows)
    {
        if (rows.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        // Header
        sb.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
        sb.AppendLine("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
        // Data rows
        for (int i = 1; i < rows.Count; i++)
            sb.AppendLine("| " + string.Join(" | ", rows[i]) + " |");
        return sb.ToString();
    }
}

// Supporting models
public class ExtractionResult
{
    public string FileName { get; set; } = "";
    public List<PageContent> Pages { get; set; } = new();
    public int TotalPages { get; set; }
}

public class PageContent
{
    public int PageNumber { get; set; }
    public List<ParagraphContent> Paragraphs { get; set; } = new();
    public List<TableContent> Tables { get; set; } = new();
}

public class ParagraphContent
{
    public string Text { get; set; } = "";
    public string Role { get; set; } = "body"; // title, sectionHeading, body, footnote
    public int PageNumber { get; set; }
}

public class TableContent
{
    public List<List<string>> Rows { get; set; } = new();
    public string AsMarkdown { get; set; } = "";
}
```

### 6.4 ChunkingService â€” Smart Layout-Aware Chunking

```csharp
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

public class ChunkingOptions
{
    public int MaxTokensPerChunk { get; set; } = 512;
    public int OverlapTokens { get; set; } = 50;
    public string[] ExcludeRoles { get; set; } = { "pageHeader", "pageFooter", "pageNumber" };
}

public class ChunkingService
{
    private readonly ChunkingOptions _options;

    public ChunkingService(IConfiguration config)
    {
        _options = config.GetSection("ChunkingOptions").Get<ChunkingOptions>()
                   ?? new ChunkingOptions();
    }

    public List<ChunkResult> ChunkDocument(ExtractionResult document)
    {
        var chunks = new List<ChunkResult>();
        var currentContent = new System.Text.StringBuilder();
        var currentTokens = 0;
        var currentPage = 1;
        var chunkIndex = 0;

        foreach (var page in document.Pages)
        {
            // Add tables as standalone chunks (never split a table)
            foreach (var table in page.Tables)
            {
                var tableTokens = EstimateTokens(table.AsMarkdown);
                if (tableTokens > 0)
                {
                    // If current chunk has content, flush it first
                    if (currentContent.Length > 0)
                    {
                        chunks.Add(CreateChunk(currentContent.ToString(), document.FileName,
                            currentPage, chunkIndex++));
                        currentContent.Clear();
                        currentTokens = 0;
                    }

                    chunks.Add(CreateChunk(table.AsMarkdown, document.FileName,
                        page.PageNumber, chunkIndex++));
                }
            }

            // Process paragraphs
            foreach (var para in page.Paragraphs)
            {
                // Skip noise
                if (_options.ExcludeRoles.Contains(para.Role))
                    continue;

                var paraTokens = EstimateTokens(para.Text);

                // Would this paragraph push us over the limit?
                if (currentTokens + paraTokens > _options.MaxTokensPerChunk && currentContent.Length > 0)
                {
                    // Save current chunk
                    chunks.Add(CreateChunk(currentContent.ToString(), document.FileName,
                        currentPage, chunkIndex++));

                    // Start new chunk with overlap
                    var overlap = GetOverlapText(currentContent.ToString());
                    currentContent.Clear();
                    currentContent.Append(overlap);
                    currentTokens = EstimateTokens(overlap);
                    currentPage = page.PageNumber;
                }

                // Add heading prefix for context
                if (para.Role == "title" || para.Role == "sectionHeading")
                    currentContent.AppendLine($"\n## {para.Text}");
                else
                    currentContent.AppendLine(para.Text);

                currentTokens += paraTokens;
                currentPage = page.PageNumber;
            }
        }

        // Flush remaining
        if (currentContent.Length > 0)
        {
            chunks.Add(CreateChunk(currentContent.ToString(), document.FileName,
                currentPage, chunkIndex));
        }

        return chunks;
    }

    private ChunkResult CreateChunk(string content, string fileName, int page, int index)
    {
        return new ChunkResult
        {
            Content = content.Trim(),
            SourceDocument = fileName,
            PageNumber = page,
            ChunkIndex = index,
            TokenCount = EstimateTokens(content)
        };
    }

    private string GetOverlapText(string text)
    {
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
        var overlap = new System.Text.StringBuilder();
        var tokens = 0;

        for (int i = sentences.Length - 1; i >= 0 && tokens < _options.OverlapTokens; i--)
        {
            overlap.Insert(0, sentences[i] + " ");
            tokens += EstimateTokens(sentences[i]);
        }

        return overlap.ToString();
    }

    private int EstimateTokens(string text) => (int)(text.Length / 4.0);
}

public class ChunkResult
{
    public string Content { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }
}
```

### 6.5 EmbeddingService â€” Convert Text to Vectors

```csharp
using Azure.AI.OpenAI;
using OpenAI.Embeddings;

public class EmbeddingService
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;
    private readonly int _dimensions;

    public EmbeddingService(AzureOpenAIClient client, IConfiguration config)
    {
        _client = client;
        _deploymentName = config["AzureOpenAI:EmbeddingDeployment"]!;
        _dimensions = int.Parse(config["AzureOpenAI:EmbeddingDimensions"] ?? "1536");
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
    {
        var embeddingClient = _client.GetEmbeddingClient(_deploymentName);
        var options = new EmbeddingGenerationOptions { Dimensions = _dimensions };
        var result = await embeddingClient.GenerateEmbeddingAsync(text, options);
        return result.Value.ToFloats();
    }

    public async Task<List<ReadOnlyMemory<float>>> GetEmbeddingsBatchAsync(List<string> texts)
    {
        // Process in batches of 16 (API limit)
        var allEmbeddings = new List<ReadOnlyMemory<float>>();
        var embeddingClient = _client.GetEmbeddingClient(_deploymentName);
        var options = new EmbeddingGenerationOptions { Dimensions = _dimensions };

        foreach (var batch in texts.Chunk(16))
        {
            var result = await embeddingClient.GenerateEmbeddingsAsync(batch, options);
            allEmbeddings.AddRange(result.Value.Select(e => e.ToFloats()));
        }

        return allEmbeddings;
    }
}
```

### 6.6 SearchIndexService â€” Create the Index

```csharp
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

public class SearchIndexService
{
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;

    public SearchIndexService(SearchIndexClient indexClient, IConfiguration config)
    {
        _indexClient = indexClient;
        _indexName = config["AzureSearch:IndexName"]!;
    }

    public async Task CreateOrUpdateIndexAsync()
    {
        var index = new SearchIndex(_indexName)
        {
            Fields = new FieldBuilder().Build(typeof(DocumentChunk)),
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("hnsw-config")
                {
                    Parameters = new HnswParameters
                    {
                        Metric = VectorSearchAlgorithmMetric.Cosine,
                        M = 4,
                        EfConstruction = 400,
                        EfSearch = 500
                    }
                }},
                Profiles = { new VectorSearchProfile("vector-profile", "hnsw-config") }
            },
            SemanticSearch = new SemanticSearch
            {
                Configurations =
                {
                    new SemanticConfiguration("rag-semantic-config",
                        new SemanticPrioritizedFields
                        {
                            ContentFields = { new SemanticField("Content") },
                            TitleField = new SemanticField("Title"),
                            KeywordsFields = { new SemanticField("Category") }
                        })
                }
            }
        };

        await _indexClient.CreateOrUpdateIndexAsync(index);
    }

    public async Task IndexChunksAsync(SearchClient searchClient, List<DocumentChunk> chunks)
    {
        // Upload in batches of 100
        foreach (var batch in chunks.Chunk(100))
        {
            var actions = batch.Select(c =>
                Azure.Search.Documents.Models.IndexDocumentsAction.Upload(c));

            await searchClient.IndexDocumentsAsync(
                Azure.Search.Documents.Models.IndexDocumentsBatch.Create(actions.ToArray()));
        }
    }
}
```

### 6.7 DocumentsController â€” Full Ingestion Endpoint

This ties all services together into a single upload-and-index endpoint:

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly BlobStorageService _blobService;
    private readonly ExtractionService _extractionService;
    private readonly ChunkingService _chunkingService;
    private readonly EmbeddingService _embeddingService;
    private readonly SearchIndexService _indexService;
    private readonly SearchClient _searchClient;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        BlobStorageService blobService,
        ExtractionService extractionService,
        ChunkingService chunkingService,
        EmbeddingService embeddingService,
        SearchIndexService indexService,
        SearchClient searchClient,
        ILogger<DocumentsController> logger)
    {
        _blobService = blobService;
        _extractionService = extractionService;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _indexService = indexService;
        _searchClient = searchClient;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? category)
    {
        if (file.Length == 0)
            return BadRequest("File is empty");

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png",
                                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest("Unsupported file type. Use PDF, JPEG, PNG, or DOCX.");

        _logger.LogInformation("Processing {FileName} ({Size} bytes)", file.FileName, file.Length);

        // Step 1: Upload to Blob Storage
        using var stream = file.OpenReadStream();
        var blobUri = await _blobService.UploadAsync(stream, file.FileName, file.ContentType);

        // Step 2: Extract text and structure
        stream.Position = 0;
        var extraction = await _extractionService.ExtractAsync(stream, file.FileName);
        _logger.LogInformation("Extracted {Pages} pages", extraction.TotalPages);

        // Step 3: Chunk the document
        var chunkResults = _chunkingService.ChunkDocument(extraction);
        _logger.LogInformation("Created {Chunks} chunks", chunkResults.Count);

        // Step 4: Generate embeddings
        var texts = chunkResults.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GetEmbeddingsBatchAsync(texts);

        // Step 5: Create index (idempotent) and upload
        await _indexService.CreateOrUpdateIndexAsync();

        var chunks = chunkResults.Select((c, i) => new DocumentChunk
        {
            ChunkId = $"{Path.GetFileNameWithoutExtension(file.FileName)}-{c.ChunkIndex}",
            Content = c.Content,
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            ContentVector = embeddings[i],
            Category = category ?? "General",
            SourceDocument = file.FileName,
            PageNumber = c.PageNumber,
            ChunkIndex = c.ChunkIndex,
            UploadedAt = DateTimeOffset.UtcNow,
            DocumentType = Path.GetExtension(file.FileName).TrimStart('.')
        }).ToList();

        await _indexService.IndexChunksAsync(_searchClient, chunks);

        return Ok(new
        {
            fileName = file.FileName,
            totalPages = extraction.TotalPages,
            totalChunks = chunks.Count,
            blobUri
        });
    }
}
```

### 6.8 Test the Ingestion Pipeline

```bash
# Run the API
dotnet run

# In another terminal, upload a document
curl -X POST "https://localhost:5001/api/documents/upload?category=Legal" \
  -F "file=@contract.pdf"

# Expected response:
# { "fileName": "contract.pdf", "totalPages": 30, "totalChunks": 42, "blobUri": "..." }
```

> **What just happened**: Your PDF was uploaded to Blob Storage â†’ Document Intelligence extracted all text, tables, and structure â†’ Chunking split it into ~42 pieces â†’ Each piece got a 1536-dimension embedding â†’ All pieces were indexed in Azure AI Search.

---

## 7. Build the Query Pipeline

### 7.1 Flow Overview

```
User Question â†’ Embed â†’ Hybrid Search â†’ Semantic Rank â†’ Top 5 Chunks â†’ GPT-4o â†’ Answer with Citations
```

### 7.2 SearchService â€” Hybrid Search with Semantic Ranking

```csharp
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

public class SearchService
{
    private readonly SearchClient _searchClient;
    private readonly EmbeddingService _embeddingService;
    private readonly string _semanticConfig;

    public SearchService(SearchClient searchClient, EmbeddingService embeddingService, IConfiguration config)
    {
        _searchClient = searchClient;
        _embeddingService = embeddingService;
        _semanticConfig = config["AzureSearch:SemanticConfigName"]!;
    }

    public async Task<List<SearchResult>> HybridSearchAsync(SearchRequest request)
    {
        // Step 1: Embed the question
        var queryVector = await _embeddingService.GetEmbeddingAsync(request.Query);

        // Step 2: Build hybrid search options
        var options = new SearchOptions
        {
            Size = request.TopResults,
            Select = { "ChunkId", "Content", "Title", "SourceDocument",
                       "PageNumber", "Category", "ChunkIndex" },
            QueryType = request.UseSemanticRanking ? SearchQueryType.Semantic : SearchQueryType.Simple,
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = request.TopResults * 2,
                        Fields = { "ContentVector" }
                    }
                }
            }
        };

        if (request.UseSemanticRanking)
        {
            options.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _semanticConfig,
                QueryCaption = QueryCaptionType.Extractive,
                QueryAnswer = QueryAnswerType.Extractive
            };
        }

        // Filter by category if provided
        if (!string.IsNullOrEmpty(request.Category))
        {
            options.Filter = $"Category eq '{request.Category}'";
        }

        // Step 3: Execute search
        var response = await _searchClient.SearchAsync<DocumentChunk>(request.Query, options);

        var results = new List<SearchResult>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(new SearchResult
            {
                Content = result.Document.Content,
                SourceDocument = result.Document.SourceDocument,
                PageNumber = result.Document.PageNumber,
                Score = result.Score ?? 0,
                SemanticScore = result.SemanticSearch?.RerankerScore ?? 0
            });
        }

        return results;
    }
}

public class SearchResult
{
    public string Content { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public int PageNumber { get; set; }
    public double Score { get; set; }
    public double SemanticScore { get; set; }
}
```

### 7.3 RagService â€” Orchestrate Search to Answer

```csharp
using Azure.AI.OpenAI;
using OpenAI.Chat;

public class RagService
{
    private readonly SearchService _searchService;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _chatDeployment;

    public RagService(SearchService searchService, AzureOpenAIClient openAIClient, IConfiguration config)
    {
        _searchService = searchService;
        _openAIClient = openAIClient;
        _chatDeployment = config["AzureOpenAI:ChatDeployment"]!;
    }

    public async Task<SearchResponse> AskAsync(SearchRequest request)
    {
        // Step 1: Search for relevant chunks
        var searchResults = await _searchService.HybridSearchAsync(request);

        if (searchResults.Count == 0)
        {
            return new SearchResponse
            {
                Answer = "I couldn't find relevant information in the documents to answer your question.",
                Citations = new List<Citation>(),
                ConfidenceScore = 0
            };
        }

        // Step 2: Build prompt with context
        var context = string.Join("\n\n---\n\n",
            searchResults.Select((r, i) =>
                $"[Source {i + 1}: {r.SourceDocument}, Page {r.PageNumber}]\n{r.Content}"));

        var systemPrompt = @"You are a precise document assistant. Answer ONLY from the provided context.

RULES:
1. If the context doesn't contain the answer, say 'The provided documents don't contain this information.'
2. Always cite sources as [Source N] at the end of relevant sentences.
3. Preserve exact numbers, dates, and legal terms from the source â€” never paraphrase these.
4. Use bullet points for lists of 3+ items.
5. Keep answers focused and under 300 words unless the question requires detail.";

        var userPrompt = $@"CONTEXT:
{context}

QUESTION: {request.Query}

Answer using only the context above. Cite every fact with [Source N].";

        // Step 3: Call GPT-4o
        var chatClient = _openAIClient.GetChatClient(_chatDeployment);
        var completion = await chatClient.CompleteChatAsync(new[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        });

        var usage = completion.Value.Usage;

        return new SearchResponse
        {
            Answer = completion.Value.Content[0].Text,
            Citations = searchResults.Select(r => new Citation
            {
                Content = r.Content.Length > 200 ? r.Content[..200] + "..." : r.Content,
                SourceDocument = r.SourceDocument,
                PageNumber = r.PageNumber,
                RelevanceScore = r.SemanticScore > 0 ? r.SemanticScore : r.Score
            }).ToList(),
            ConfidenceScore = searchResults.Average(r =>
                r.SemanticScore > 0 ? r.SemanticScore : r.Score),
            Tokens = new TokenUsage
            {
                PromptTokens = usage.InputTokenCount,
                CompletionTokens = usage.OutputTokenCount,
                TotalTokens = usage.TotalTokenCount,
                EstimatedCost = (usage.InputTokenCount * 2.50m / 1_000_000)
                              + (usage.OutputTokenCount * 10.00m / 1_000_000)
            }
        };
    }
}
```

### 7.4 QueryController â€” API Endpoint

```csharp
[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly RagService _ragService;

    public QueryController(RagService ragService)
    {
        _ragService = ragService;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query cannot be empty");

        var response = await _ragService.AskAsync(request);
        return Ok(response);
    }
}
```

### 7.5 HealthController â€” Health Check

```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        version = "1.0.0"
    });
}
```

### 7.6 Test the Query Pipeline

```bash
curl -X POST "https://localhost:5001/api/query" \
  -H "Content-Type: application/json" \
  -d '{"query": "What are the termination penalties?", "topResults": 5}'

# Response with citations:
# {
#   "answer": "According to the vendor contract, termination penalties include:
#              - Early termination fee of 25% of remaining contract value [Source 1]
#              - 90-day notice period required [Source 2]...",
#   "citations": [...],
#   "tokens": { "totalTokens": 2341, "estimatedCost": 0.013 }
# }
```

---

## 8. Legal Document Support

### 8.1 What Makes Legal Documents Special

Legal documents use multi-level numbering (Section 1.2.3(a)(i)) and precise terminology. Standard chunking would lose this structure.

### 8.2 Enhanced Legal Chunking

Add this to `ChunkingService`:

```csharp
public List<ChunkResult> ChunkLegalDocument(ExtractionResult document)
{
    var chunks = new List<ChunkResult>();
    var currentSection = new System.Text.StringBuilder();
    var currentHeading = "";
    var currentPage = 1;
    var chunkIndex = 0;

    foreach (var page in document.Pages)
    {
        foreach (var para in page.Paragraphs)
        {
            if (_options.ExcludeRoles.Contains(para.Role))
                continue;

            // Detect legal section headings
            bool isHeading = para.Role == "sectionHeading" || para.Role == "title"
                || System.Text.RegularExpressions.Regex.IsMatch(
                    para.Text, @"^\d+(\.\d+)*\s+[A-Z]");

            if (isHeading && currentSection.Length > 0)
            {
                // Flush current section
                chunks.Add(new ChunkResult
                {
                    Content = $"## {currentHeading}\n\n{currentSection.ToString().Trim()}",
                    SourceDocument = document.FileName,
                    PageNumber = currentPage,
                    ChunkIndex = chunkIndex++,
                    TokenCount = EstimateTokens(currentSection.ToString())
                });
                currentSection.Clear();
            }

            if (isHeading)
                currentHeading = para.Text;
            else
                currentSection.AppendLine(para.Text);

            currentPage = page.PageNumber;
        }
    }

    // Flush last section
    if (currentSection.Length > 0)
    {
        chunks.Add(new ChunkResult
        {
            Content = $"## {currentHeading}\n\n{currentSection.ToString().Trim()}",
            SourceDocument = document.FileName,
            PageNumber = currentPage,
            ChunkIndex = chunkIndex,
            TokenCount = EstimateTokens(currentSection.ToString())
        });
    }

    // Handle over-sized chunks: split any chunk > MaxTokensPerChunk
    return chunks.SelectMany(SplitIfTooLarge).ToList();
}

private IEnumerable<ChunkResult> SplitIfTooLarge(ChunkResult chunk)
{
    if (chunk.TokenCount <= _options.MaxTokensPerChunk)
    {
        yield return chunk;
        yield break;
    }

    var subChunks = ChunkByTokenLimit(chunk.Content, _options.MaxTokensPerChunk);
    int subIndex = 0;
    foreach (var sub in subChunks)
    {
        yield return new ChunkResult
        {
            Content = sub,
            SourceDocument = chunk.SourceDocument,
            PageNumber = chunk.PageNumber,
            ChunkIndex = chunk.ChunkIndex * 100 + subIndex++,
            TokenCount = EstimateTokens(sub)
        };
    }
}

private List<string> ChunkByTokenLimit(string text, int maxTokens)
{
    var result = new List<string>();
    var sentences = System.Text.RegularExpressions.Regex.Split(text, @"(?<=[.!?;])\s+");
    var current = new System.Text.StringBuilder();
    int tokens = 0;

    foreach (var sentence in sentences)
    {
        int sentenceTokens = EstimateTokens(sentence);
        if (tokens + sentenceTokens > maxTokens && current.Length > 0)
        {
            result.Add(current.ToString().Trim());
            current.Clear();
            tokens = 0;
        }
        current.AppendLine(sentence);
        tokens += sentenceTokens;
    }
    if (current.Length > 0)
        result.Add(current.ToString().Trim());

    return result;
}
```

### 8.3 Legal-Specific Prompt

Update `RagService` for legal queries:

```csharp
private string GetLegalSystemPrompt()
{
    return @"You are a legal document analyst. Answer ONLY from the provided context.

RULES:
1. Preserve exact clause numbers (e.g., Section 14.2(a)) â€” NEVER renumber.
2. Quote critical legal language verbatim using quotation marks.
3. Distinguish between defined terms (capitalized) and general language.
4. When multiple clauses are relevant, list them in document order.
5. Always include the source reference [Source N] for each cited clause.
6. Flag any contradictions between clauses if detected.
7. If the answer requires legal interpretation beyond the text, state that clearly.";
}
```

---

## 9. Deploy and CI/CD â€” Custom Path

### 9.1 Prepare for Azure App Service

**Add Dockerfile**:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AzureRAG.Custom.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AzureRAG.Custom.dll"]
```

### 9.2 Deploy to Azure App Service

```bash
# Login
az login

# Create App Service Plan
az appservice plan create \
  --name plan-rag-custom \
  --resource-group rg-rag-custom-dev \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name webapp-rag-custom-[unique] \
  --resource-group rg-rag-custom-dev \
  --plan plan-rag-custom \
  --runtime "DOTNETCORE:8.0"

# Configure app settings (from Key Vault in production â€” see Ch 10)
az webapp config appsettings set \
  --name webapp-rag-custom-[unique] \
  --resource-group rg-rag-custom-dev \
  --settings \
    AzureSearch__Endpoint="https://search-rag-custom-xxx.search.windows.net" \
    AzureSearch__IndexName="rag-index" \
    AzureOpenAI__Endpoint="https://openai-rag-custom-xxx.openai.azure.com/" \
    AzureOpenAI__ChatDeployment="gpt-4o" \
    AzureOpenAI__EmbeddingDeployment="text-embedding-3-small"

# Deploy
az webapp deploy \
  --name webapp-rag-custom-[unique] \
  --resource-group rg-rag-custom-dev \
  --src-path ./publish.zip \
  --type zip
```

### 9.3 Infrastructure Setup (Azure CLI)

Create all Azure resources with Azure CLI commands. Run these once to set up the environment:

```bash
# Variables â€” change these for your environment
RG="rg-rag-custom-dev"
LOCATION="centralindia"
SUFFIX="dev01"   # Unique suffix for resource names

# 1. Create Resource Group
az group create --name $RG --location $LOCATION

# 2. Azure AI Search (Basic SKU for development)
az search service create \
  --name "search-rag-$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --sku basic \
  --replica-count 1 \
  --partition-count 1

# 3. Azure OpenAI
az cognitiveservices account create \
  --name "openai-rag-$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --kind OpenAI \
  --sku S0

# 4. Deploy GPT-4o model
az cognitiveservices account deployment create \
  --name "openai-rag-$SUFFIX" \
  --resource-group $RG \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-08-06" \
  --model-format OpenAI \
  --sku-name Standard \
  --sku-capacity 30

# 5. Deploy Embedding model
az cognitiveservices account deployment create \
  --name "openai-rag-$SUFFIX" \
  --resource-group $RG \
  --deployment-name text-embedding-3-small \
  --model-name text-embedding-3-small \
  --model-version "1" \
  --model-format OpenAI \
  --sku-name Standard \
  --sku-capacity 120

# 6. Storage Account (no public blob access, TLS 1.2)
az storage account create \
  --name "strag$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --allow-blob-public-access false \
  --min-tls-version TLS1_2

# 7. Document Intelligence (Form Recognizer)
az cognitiveservices account create \
  --name "docint-rag-$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --kind FormRecognizer \
  --sku S0

# 8. Key Vault (RBAC authorization, soft-delete enabled)
az keyvault create \
  --name "kv-rag-$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --enable-rbac-authorization true \
  --enable-soft-delete true

# 9. App Service Plan (Linux, B1 for dev)
az appservice plan create \
  --name "plan-rag-$SUFFIX" \
  --resource-group $RG \
  --location $LOCATION \
  --sku B1 \
  --is-linux

# 10. Web App (.NET 8, System-Assigned Managed Identity)
az webapp create \
  --name "webapp-rag-$SUFFIX" \
  --resource-group $RG \
  --plan "plan-rag-$SUFFIX" \
  --runtime "DOTNETCORE:8.0"

az webapp identity assign \
  --name "webapp-rag-$SUFFIX" \
  --resource-group $RG

# 11. Configure App Settings (endpoints only â€” no secrets)
az webapp config appsettings set \
  --name "webapp-rag-$SUFFIX" \
  --resource-group $RG \
  --settings \
    AzureSearch__Endpoint="https://search-rag-$SUFFIX.search.windows.net" \
    AzureSearch__IndexName="rag-index" \
    AzureOpenAI__Endpoint="https://openai-rag-$SUFFIX.openai.azure.com" \
    AzureOpenAI__ChatDeployment="gpt-4o" \
    AzureOpenAI__EmbeddingDeployment="text-embedding-3-small"
```

**What this creates:**

| # | Resource | Name | Purpose |
|---|----------|------|---------|
| 1 | Resource Group | rg-rag-custom-dev | Container for all resources |
| 2 | Azure AI Search | search-rag-dev01 | Full-text + vector search |
| 3 | Azure OpenAI | openai-rag-dev01 | LLM (Large Language Model) host |
| 4 | GPT-4o deployment | gpt-4o | Chat completion model |
| 5 | Embedding deployment | text-embedding-3-small | Vector embedding model |
| 6 | Storage Account | stragdev01 | Document storage (Blob) |
| 7 | Document Intelligence | docint-rag-dev01 | PDF/image text extraction (OCR) |
| 8 | Key Vault | kv-rag-dev01 | Secret management (RBAC-based) |
| 9 | App Service Plan | plan-rag-dev01 | Compute plan (Linux) |
| 10 | Web App | webapp-rag-dev01 | Hosts the .NET query API |

> **ðŸŽ¯ Interview Point**: "We create all Azure resources using Azure CLI scripts checked into Git. App Settings contain only endpoints â€” no API keys or connection strings. Authentication uses Managed Identity with RBAC roles, which is the zero-trust approach."

### 9.4 CI/CD with GitHub Actions

**.github/workflows/deploy-custom.yml**:

```yaml
name: Deploy Custom RAG App

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

env:
  AZURE_WEBAPP_NAME: webapp-rag-custom-dev01
  RESOURCE_GROUP: rg-rag-custom-dev
  DOTNET_VERSION: '8.0.x'

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Test
      run: dotnet test --no-build --configuration Release

    - name: Publish
      run: dotnet publish -c Release -o ./publish

    - name: Upload artifact
      uses: actions/upload-artifact@v4
      with:
        name: custom-rag-app
        path: ./publish

  deploy-app:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production

    steps:
    - name: Download artifact
      uses: actions/download-artifact@v4
      with:
        name: custom-rag-app
        path: ./publish

    - name: Login to Azure
      uses: azure/login@v2
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v3
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        package: ./publish

    - name: Health check
      run: |
        sleep 30
        STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
          https://${{ env.AZURE_WEBAPP_NAME }}.azurewebsites.net/api/health)
        if [ "$STATUS" != "200" ]; then
          echo "Health check failed with status $STATUS"
          exit 1
        fi
```

### 9.5 Deploy Checklist

| Step | Command / Action | Verify |
|------|-----------------|--------|
| 1. Create resources | Run Azure CLI script from section 9.3 | Resources visible in Azure Portal |
| 2. Assign RBAC roles | `az role assignment create` (see Chapter 10) | Managed Identity has access |
| 3. Deploy app | Push to `main` branch â†’ GitHub Action triggers | Action completes green |
| 4. Verify health | `curl https://webapp-rag-dev01.azurewebsites.net/api/health` | Returns `healthy` |
| 5. Upload test doc | POST to `/api/documents/upload` | Returns chunk count |
| 6. Test query | POST to `/api/query` | Returns answer with citations |

---

## 10. Security and Monitoring â€” Custom Path

### 10.1 Managed Identity (Eliminate API Keys)

**Step 1**: Enable System-Assigned Managed Identity on the Web App:

```bash
az webapp identity assign \
  --name webapp-rag-custom-xxx \
  --resource-group rg-rag-custom-dev
```

**Step 2**: Grant the Web App access to each service:

```bash
# Get the Web App's identity
PRINCIPAL_ID=$(az webapp identity show --name webapp-rag-custom-xxx \
  --resource-group rg-rag-custom-dev --query principalId -o tsv)

# Azure AI Search â€” Search Index Data Contributor
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Search Index Data Contributor" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.Search/searchServices/search-rag-custom-xxx

# Azure OpenAI â€” Cognitive Services OpenAI User
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cognitive Services OpenAI User" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.CognitiveServices/accounts/openai-rag-custom-xxx

# Blob Storage â€” Storage Blob Data Contributor
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.Storage/storageAccounts/stragcustomxxx

# Key Vault â€” Key Vault Secrets User
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.KeyVault/vaults/kv-rag-custom-xxx
```

**Step 3**: Remove API keys from app settings. The code already uses `DefaultAzureCredential` as fallback (see Chapter 5 Program.cs).

> **Interview tip**: "Managed Identity means no credentials stored anywhere â€” not in code, not in config, not in Key Vault. Azure handles authentication via tokens automatically."

### 10.2 Key Vault Integration

For scenarios where you still need to store secrets (3rd-party API keys, connection strings):

```csharp
// In Program.cs, add Key Vault as a configuration source
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}
```

Secrets stored in Key Vault automatically map to configuration keys:
- `SearchApiKey` in Key Vault â†’ `builder.Configuration["SearchApiKey"]`

### 10.3 Private Endpoints

For production environments where services must not be accessible from the public internet:

```bash
# Make AI Search private
az network private-endpoint create \
  --name pe-search \
  --resource-group rg-rag-custom-dev \
  --vnet-name vnet-rag \
  --subnet subnet-private-endpoints \
  --private-connection-resource-id /subscriptions/{sub}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.Search/searchServices/search-rag-custom-xxx \
  --group-ids searchService \
  --connection-name plsc-search

# Make OpenAI private
az network private-endpoint create \
  --name pe-openai \
  --resource-group rg-rag-custom-dev \
  --vnet-name vnet-rag \
  --subnet subnet-private-endpoints \
  --private-connection-resource-id /subscriptions/{sub}/resourceGroups/rg-rag-custom-dev/providers/Microsoft.CognitiveServices/accounts/openai-rag-custom-xxx \
  --group-ids account \
  --connection-name plsc-openai
```

### 10.4 Application Insights Monitoring

```bash
# Create Application Insights
az monitor app-insights component create \
  --app appinsights-rag-custom \
  --location centralindia \
  --resource-group rg-rag-custom-dev

# Link to web app
az webapp config appsettings set \
  --name webapp-rag-custom-xxx \
  --resource-group rg-rag-custom-dev \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=xxx..."
```

**Add to Program.cs**:

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

**Custom telemetry for RAG operations**:

```csharp
using Microsoft.ApplicationInsights;

public class RagService
{
    private readonly TelemetryClient _telemetry;

    // In AskAsync method, add:
    private async Task<SearchResponse> AskAsyncWithTelemetry(SearchRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await AskAsync(request);

        _telemetry.TrackEvent("RAGQuery", new Dictionary<string, string>
        {
            ["Query"] = request.Query,
            ["CitationCount"] = response.Citations.Count.ToString(),
            ["Category"] = request.Category ?? "All"
        }, new Dictionary<string, double>
        {
            ["DurationMs"] = stopwatch.ElapsedMilliseconds,
            ["TokenCount"] = response.Tokens.TotalTokens,
            ["EstimatedCost"] = (double)response.Tokens.EstimatedCost,
            ["ConfidenceScore"] = response.ConfidenceScore
        });

        return response;
    }
}
```

### 10.5 Security Checklist â€” Custom Path

| Security Layer | Implementation | Status |
|---------------|---------------|--------|
| Authentication | Managed Identity (no API keys in prod) | Required |
| Secrets | Key Vault with RBAC | Required |
| Network | Private Endpoints + VNet | Recommended |
| Transport | HTTPS only (TLS 1.2+) | Required |
| Monitoring | Application Insights | Required |
| Access | RBAC least-privilege roles | Required |
| Data | Encryption at rest (service-managed) | Automatic |

---



# Part 3: Complete Managed Path (Standalone, Production-Ready)

> **Start here if you want Azure to handle document cracking, chunking, embedding, and indexing automatically.** You write zero ingestion code â€” Azure AI Search skillsets and indexers do everything. You only write the query-side .NET app.

---

## 11. Azure Setup â€” Managed Path

> **This chapter is completely standalone.** Even if you already created resources in Part 2, follow every step here â€” managed path needs different configurations.

### 11.1 What You Will Create

| Resource | Purpose | SKU |
|----------|---------|-----|
| **Resource Group** | Container for all resources | â€” |
| **Azure AI Search** | Search index + built-in skillsets + indexer | **Basic** (learning default, lower cost) |
| **Azure OpenAI** | Chat (**gpt-4o-mini**) + Embeddings (text-embedding-3-large) | Standard |
| **Azure Blob Storage** | Document storage (indexer data source) | Standard LRS |
| **Azure Document Intelligence** | PDF/image extraction (called by skillset) | **F0** (free tier for learning) |
| **Key Vault** | Store endpoints and connection strings | Standard |
| **Application Insights + Log Analytics** | Monitoring and logging | â€” |
| **App Service Plan + Web App** | Query API hosting | **B1** + .NET 8 |
| **RBAC role assignments** | Least-privilege access between services | â€” |

> **âœ… Update (current implementation)**: For this book's managed pipeline, **Azure AI Search Basic works** with skillsets/indexers and is used as the learning default.

### 11.2 Deploy Infrastructure (Bicep-first, current path)

```powershell
# Step 1: Create resource group
$RESOURCE_GROUP = "rg-rag-managed"
az group create --name $RESOURCE_GROUP --location centralindia

# Step 2: Preview changes
az deployment group what-if `
  --resource-group $RESOURCE_GROUP `
  --template-file infra/main.bicep `
  --parameters location=centralindia suffix=managed

# Step 3: Deploy all managed-path resources
az deployment group create `
  --resource-group $RESOURCE_GROUP `
  --template-file infra/main.bicep `
  --parameters location=centralindia suffix=managed

# Step 4: Show outputs (endpoints, IDs)
az deployment group show `
  --resource-group $RESOURCE_GROUP `
  --name main `
  --query properties.outputs
```

Optional low-cost tuning parameters already supported in `infra/main.bicep`:

- `embeddingCapacity=10` (default)
- `gptCapacity=1` (default)
- `maxContextChunks=3` (default)
- `maxChunkChars=500` (default)
- `maxOutputTokens=200` (default)

### 11.3 Managed Identity + RBAC (what is automatic vs manual)

`infra/main.bicep` already configures:

- System-assigned MI on Search and Web App
- Service-to-service RBAC assignments
- Search service auth mode = `aadOrApiKey`

For **local development** (DefaultAzureCredential from Azure CLI), grant your signed-in user:

```powershell
$USER_ID = az ad signed-in-user show --query id --output tsv
$SEARCH_ID = az search service show --name search-rag-managed --resource-group rg-rag-managed --query id --output tsv
$STORAGE_ID = az storage account show --name stragmanaged --resource-group rg-rag-managed --query id --output tsv

az role assignment create --assignee $USER_ID --role "Search Index Data Reader" --scope $SEARCH_ID
az role assignment create --assignee $USER_ID --role "Search Service Contributor" --scope $SEARCH_ID
az role assignment create --assignee $USER_ID --role "Storage Blob Data Contributor" --scope $STORAGE_ID

# Ensure AAD tokens are accepted by Search data-plane
az search service update `
  --name search-rag-managed `
  --resource-group rg-rag-managed `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http401WithBearerChallenge
```

### 11.4 Verify All Resources

```powershell
# Verify everything is created
az resource list \
  --resource-group rg-rag-managed \
  --output table \
  --query "[].{Name:name, Type:type, Location:location}"
```

Expected output â€” 10+ resources (plus one auto-created Smart Detection action group):
```
Name                      Type                                          Location
------------------------  --------------------------------------------  --------
search-rag-managed        Microsoft.Search/searchServices               centralindia
openai-rag-managed        Microsoft.CognitiveServices/accounts          eastus2
stragmanaged              Microsoft.Storage/storageAccounts              centralindia
docintel-rag-managed      Microsoft.CognitiveServices/accounts          centralindia
kv-rag-managed            Microsoft.KeyVault/vaults                     centralindia
appinsights-rag-managed   Microsoft.Insights/components                 centralindia
log-rag-managed           Microsoft.OperationalInsights/workspaces      centralindia
plan-rag-managed          Microsoft.Web/serverfarms                     centralindia
app-rag-managed           Microsoft.Web/sites                           centralindia
```

> **ðŸŽ¯ Interview Point**: "In the managed path, Azure AI Search needs its own Managed Identity with RBAC roles â€” it must authenticate to Blob Storage, OpenAI, and Document Intelligence without API keys."

---

## 12. Create the Managed Pipeline

> **This is the core of the managed path.** You create the search index, skillset, data source, and indexer using a **.NET console app** (`ManagedRagSetup`). The project is in your folder â€” just run `dotnet run`.

### 12.1 How the Managed Pipeline Works

```
Upload PDF to Blob Storage
        â”‚
        â–¼
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚            Azure AI Search Indexer                â”‚
â”‚  (Runs automatically on schedule or on-demand)   â”‚
â”‚                                                   â”‚
â”‚  Step 1: DATA SOURCE                              â”‚
â”‚    â””â”€â”€ Connects to Blob Storage container          â”‚
â”‚    â””â”€â”€ Detects new/changed/deleted blobs           â”‚
â”‚                                                   â”‚
â”‚  Step 2: SKILLSET (AI Enrichment Pipeline)        â”‚
â”‚    â”œâ”€â”€ #1 Document Cracking                       â”‚
â”‚    â”‚   â””â”€â”€ Built-in: extracts text from PDFs       â”‚
â”‚    â”œâ”€â”€ #2 Text Split Skill                         â”‚
â”‚    â”‚   â””â”€â”€ Chunks text (configurable page size)    â”‚
â”‚    â”œâ”€â”€ #3 Azure OpenAI Embedding Skill             â”‚
â”‚    â”‚   â””â”€â”€ Generates vectors for each chunk        â”‚
â”‚    â””â”€â”€ Knowledge Store (optional)                  â”‚
â”‚        â””â”€â”€ Saves enriched data to storage          â”‚
â”‚                                                   â”‚
â”‚  Step 3: INDEX                                    â”‚
â”‚    â””â”€â”€ Stores chunks + vectors + metadata          â”‚
â”‚                                                   â”‚
â”‚  Step 4: INDEXER                                  â”‚
â”‚    â””â”€â”€ Maps skillset outputs â†’ index fields        â”‚
â”‚    â””â”€â”€ Runs on schedule (e.g., every 5 minutes)    â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
        â”‚
        â–¼
   Ready for queries
```

### 12.2 Real-World Approach: Why .NET SDK, Not Manual Commands

In production, **nobody creates search indexes by running curl commands or clicking the Azure Portal**. Here's what real teams do:

| Approach | When Used | Who Uses It |
|----------|-----------|-------------|
| **Azure SDK (.NET/Python)** | Production setup scripts in CI/CD | .NET teams |
| **Bicep / Terraform (IaC)** | Production â€” infrastructure-first teams | DevOps/Platform teams |
| **Azure Portal wizard** | Initial prototyping only | Proof-of-concept phase |
| **REST API (curl)** | Never in production | Only for learning/debugging |

**Why .NET SDK wins for .NET teams:**
- **Same language** â€” your team already knows C#, no need to learn Bicep/HCL
- **Version-controlled** â€” the setup project is in Git, reviewable in PRs (Pull Requests)
- **Repeatable** â€” `dotnet run` creates the same pipeline in dev, staging, production
- **Idempotent** â€” `CreateOrUpdate` methods ensure no duplicates, safe to run repeatedly
- **Typed** â€” compile-time safety, IntelliSense, no YAML/JSON typos

> **ðŸŽ¯ Interview Point**: "We use a .NET console app with the Azure.Search.Documents SDK to create the search index, data source, skillset, and indexer. It uses `CreateOrUpdate` methods which are idempotent â€” safe to run in CI/CD repeatedly. The same code deploys to dev, staging, and production with different appsettings."

### 12.3 The ManagedRagSetup Project â€” Already in Your Folder

The project is ready to run at `ManagedRagSetup/`. Here's the structure:

```
ManagedRagSetup/
  ManagedRagSetup.csproj      â† Project file (Azure.Search.Documents + Azure.Identity)
  Program.cs                   â† Creates index â†’ data source â†’ skillset â†’ indexer
  appsettings.json             â† Dev environment config
  appsettings.prod.json        â† Production environment config
```

**NuGet packages used:**

| Package | Version | Purpose |
|---------|---------|---------|
| `Azure.Search.Documents` | 11.6.0 | `SearchIndexClient` (manage indexes) + `SearchIndexerClient` (manage data sources, skillsets, indexers) |
| `Azure.Identity` | 1.13.2 | `DefaultAzureCredential` â€” Managed Identity in cloud, Azure CLI locally |
| `Microsoft.Extensions.Configuration` | 8.0.0 | Load appsettings.json |
| `Microsoft.Extensions.Configuration.Json` | 8.0.1 | JSON config provider |

### 12.4 Configuration â€” appsettings.json

**ManagedRagSetup/appsettings.json** (dev):

```json
{
  "AzureAISearch": {
    "Endpoint": "https://search-rag-managed.search.windows.net",
    "IndexName": "rag-index"
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-rag-managed.openai.azure.com",
    "EmbeddingDeployment": "text-embedding-3-large"
  },
  "BlobStorage": {
    "ConnectionString": "ResourceId=/subscriptions/{sub-id}/resourceGroups/rg-rag-managed/providers/Microsoft.Storage/storageAccounts/stragmanaged",
    "ContainerName": "documents"
  },
  "Pipeline": {
    "ChunkSize": 2000,
    "ChunkOverlap": 500,
    "IndexerScheduleMinutes": 5
  }
}
```

**ManagedRagSetup/appsettings.prod.json** (production overrides):

```json
{
  "AzureAISearch": {
    "Endpoint": "https://search-rag-prod.search.windows.net"
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-rag-prod.openai.azure.com"
  },
  "BlobStorage": {
    "ConnectionString": "ResourceId=/subscriptions/{sub-id}/resourceGroups/rg-rag-prod/providers/Microsoft.Storage/storageAccounts/stragprod"
  },
  "Pipeline": {
    "IndexerScheduleMinutes": 60
  }
}
```

> **Note**: Replace `{sub-id}` with your actual Azure subscription ID. The `ResourceId=` connection string format uses Managed Identity â€” no keys or passwords.

### 12.5 Program.cs â€” What It Does (Step by Step)

Open `ManagedRagSetup/Program.cs`. The code runs four steps in sequence. Here's what each step does and the key SDK classes:

#### Step 1: Create the Search Index

Uses `SearchIndexClient.CreateOrUpdateIndex()` to create the index with:

| Field | Type | Purpose |
|-------|------|---------|
| `chunk_id` | `Edm.String` (Key) | Unique ID for each chunk |
| `parent_id` | `Edm.String` | Links chunk back to original document |
| `title` | `Edm.String` (Searchable, Filterable) | Document filename |
| `chunk` | `Edm.String` (Searchable) | The actual text content â€” keyword search target |
| `text_vector` | `Collection(Edm.Single)` â€” 3072 dimensions | Embedding vector â€” vector search target |
| `category` | `Edm.String` (Filterable, Facetable) | Category label for filtering |

**Vector search config:**
- **Algorithm**: HNSW (Hierarchical Navigable Small World) â€” `m=4`, `efConstruction=400`, `efSearch=500`, cosine metric
- **Vectorizer**: Azure OpenAI â€” the index auto-generates embeddings at query time using text-embedding-3-large
- **Semantic config**: `semantic-config` with `chunk` as content field and `title` as title field

```csharp
// Key SDK classes used:
var index = new SearchIndex(indexName)
{
    Fields = { ... },
    VectorSearch = new VectorSearch { Algorithms = { ... }, Profiles = { ... }, Vectorizers = { ... } },
    SemanticSearch = new SemanticSearch { Configurations = { ... } }
};
indexClient.CreateOrUpdateIndex(index);
```

#### Step 2: Create the Data Source

Uses `SearchIndexerClient.CreateOrUpdateDataSourceConnection()` to connect to Blob Storage:

```csharp
var dataSource = new SearchIndexerDataSourceConnection(
    name: "rag-blob-datasource",
    type: SearchIndexerDataSourceType.AzureBlob,
    connectionString: blobConnectionString,     // ResourceId=... (Managed Identity)
    container: new SearchIndexerDataContainer(containerName));
indexerClient.CreateOrUpdateDataSourceConnection(dataSource);
```

> **Managed Identity connection**: The `ResourceId=` format means no storage account keys in config. The search service's Managed Identity accesses Blob Storage via RBAC (Role-Based Access Control).

#### Step 3: Create the Skillset

Uses `SearchIndexerClient.CreateOrUpdateSkillset()` with two skills:

| Skill | SDK Class | What It Does |
|-------|-----------|-------------|
| **Text Split** | `SplitSkill` | Splits document text into chunks (2000 chars, 500 overlap) |
| **Embedding** | `AzureOpenAIEmbeddingSkill` | Generates 3072-dimension vectors for each chunk |

**Index Projections** map the one-to-many relationship: one document â†’ many chunks. Each chunk becomes its own search document with `parent_id` linking back to the source.

```csharp
var skillset = new SearchIndexerSkillset("rag-skillset", new List<SearchIndexerSkill>
{
    new SplitSkill(...) { TextSplitMode = TextSplitMode.Pages, MaximumPageLength = 2000 },
    new AzureOpenAIEmbeddingSkill(...) { DeploymentName = "text-embedding-3-large" }
})
{
    IndexProjections = new SearchIndexerIndexProjection(...)
};
```

#### Step 4: Create the Indexer

Uses `SearchIndexerClient.CreateOrUpdateIndexer()` â€” ties data source, index, and skillset together:

```csharp
var indexer = new SearchIndexer("rag-indexer", "rag-blob-datasource", indexName)
{
    SkillsetName = "rag-skillset",
    Schedule = new IndexingSchedule(TimeSpan.FromMinutes(5)),   // auto-runs every 5 min
  Parameters = new IndexingParameters
  {
    IndexingParametersConfiguration = new IndexingParametersConfiguration
    {
      DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
      ParsingMode = BlobIndexerParsingMode.Default,
      ImageAction = BlobIndexerImageAction.None    // lower cost for learning mode
    }
  }
};
indexerClient.CreateOrUpdateIndexer(indexer);
```

### 12.6 Run It

```bash
# From the project folder
cd ManagedRagSetup

# Run for dev (default)
dotnet run

# Run for production
dotnet run -- --environment prod
```

**Expected output:**

```
â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  ManagedRagSetup â€” Environment: dev
  Search: https://search-rag-managed.search.windows.net
  Index:  rag-index
â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

[1/4] Creating search index...
  âœ“ Index 'rag-index' created/updated

[2/4] Creating data source...
  âœ“ Data source 'rag-blob-datasource' â†’ container 'documents'

[3/4] Creating skillset...
  âœ“ Skillset 'rag-skillset' (SplitSkill + EmbeddingSkill)

[4/4] Creating indexer...
  âœ“ Indexer 'rag-indexer' (schedule: every 5 min)

â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  âœ… Managed pipeline setup complete!
  Upload PDFs to Blob Storage â†’ indexer processes them automatically.
â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
```

**What this single command does:**
1. Creates the search index with all fields, vector search (HNSW), and semantic config
2. Creates the data source pointing to Blob Storage (via Managed Identity)
3. Creates the skillset with text splitting + embedding generation
4. Creates the indexer with scheduling and field mappings
5. All idempotent â€” safe to run repeatedly (uses `CreateOrUpdate`)

### 12.7 Upload Documents and Verify

```powershell
# Upload a test document
az storage blob upload `
  --account-name stragmanaged `
  --container-name documents `
  --file .\sample-contract.pdf `
  --name sample-contract.pdf `
  --auth-mode login

# Trigger indexer immediately via REST (works across CLI versions)
$TOKEN = az account get-access-token --resource "https://search.azure.com" --query accessToken --output tsv
Invoke-RestMethod -Uri "https://search-rag-managed.search.windows.net/indexers/rag-indexer/run?api-version=2024-07-01" `
  -Method POST `
  -Headers @{ Authorization = "Bearer $TOKEN" }

# Check indexer status
$STATUS = Invoke-RestMethod -Uri "https://search-rag-managed.search.windows.net/indexers/rag-indexer/status?api-version=2024-07-01" `
  -Headers @{ Authorization = "Bearer $TOKEN" }
$STATUS.lastResult
```

**Expected Output:**
```
Status:           success
Items Processed:  1
Items Failed:     0
```

### 12.8 Managed Pipeline Settings Reference

All settings are in `appsettings.json` â€” change them and re-run `dotnet run`:

| Setting | Config Key | Options | Default |
|---------|-----------|---------|---------|
| **Chunk Size** | `Pipeline:ChunkSize` | 500â€“10000 chars | 2000 |
| **Chunk Overlap** | `Pipeline:ChunkOverlap` | 0â€“overlap | 500 (25% of chunk size) |
| **Embedding Model** | `AzureOpenAI:EmbeddingDeployment` | text-embedding-3-small/large | text-embedding-3-large |
| **Indexer Schedule** | `Pipeline:IndexerScheduleMinutes` | 5â€“1440 (minutes) | 5 (dev), 60 (prod) |
| **Vector Dimensions** | In `Program.cs` â†’ `VectorSearchDimensions` | 1536 or 3072 | 3072 |

> **Changing settings**: Edit `appsettings.json` â†’ commit to Git â†’ CI/CD runs `dotnet run` â†’ pipeline updated. Full audit trail.

### 12.9 Under the Hood: What the SDK Generates (REST API Reference)

> **This section is for interview knowledge only.** You never call REST directly â€” the SDK makes these calls. But interviewers may ask "what does the REST API look like?"

When the SDK calls `CreateOrUpdateIndex`, `CreateOrUpdateDataSourceConnection`, etc., it makes these REST API calls internally:

**Index** â€” `PUT /indexes/rag-index?api-version=2024-07-01`:
```json
{
  "fields": [
    { "name": "chunk_id", "type": "Edm.String", "key": true },
    { "name": "chunk", "type": "Edm.String", "searchable": true },
    { "name": "text_vector", "type": "Collection(Edm.Single)", "dimensions": 3072 }
  ],
  "vectorSearch": { "algorithms": [{ "name": "hnsw-algo", "kind": "hnsw" }] },
  "semantic": { "configurations": [{ "name": "semantic-config" }] }
}
```

**Data Source** â€” `PUT /datasources/rag-blob-datasource`:
```json
{
  "type": "azureblob",
  "credentials": { "connectionString": "ResourceId=/subscriptions/.../storageAccounts/stragmanaged" },
  "container": { "name": "documents" }
}
```

**Skillset** â€” `PUT /skillsets/rag-skillset`:
```json
{
  "skills": [
    { "@odata.type": "#Microsoft.Skills.Text.SplitSkill", "maximumPageLength": 2000, "pageOverlapLength": 500 },
    { "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill", "modelName": "text-embedding-3-large" }
  ],
  "indexProjections": { "projectionMode": "generatedKeyAsId" }
}
```

**Indexer** â€” `PUT /indexers/rag-indexer`:
```json
{
  "dataSourceName": "rag-blob-datasource",
  "targetIndexName": "rag-index",
  "skillsetName": "rag-skillset",
  "schedule": { "interval": "PT5M" }
}
```

> **ðŸŽ¯ Interview Point**: "The REST API uses PUT (not POST) because these are named resources with create-or-update semantics â€” idempotent by design. The .NET SDK's `CreateOrUpdate` methods wrap these PUT calls. In production, we run the setup project via CI/CD â€” nobody calls REST manually."
---

## 13. Build the Query-Side .NET App

> **In the managed path, you only write query-side code.** The ingestion is fully handled by the indexer/skillset (set up in Chapter 12). Your .NET app sends search queries and generates AI answers.

### 13.1 The ManagedRagApp Project â€” Already in Your Folder

The project is ready to run at `ManagedRagApp/`. Here's the structure:

```
ManagedRagApp/
  ManagedRagApp.csproj                 â† Project file (Web API)
  Program.cs                            â† Wire up DI (Dependency Injection), Azure clients
  appsettings.json                      â† Endpoints and deployment names
  Dockerfile                            â† For container deployment
  Controllers/
    ChatController.cs                   â† POST /api/chat + GET /api/chat/health
  Services/
    SearchService.cs                    â† Hybrid search (keyword + vector + semantic ranking)
    ChatService.cs                      â† gpt-4o-mini answer generation + 429 handling + low-cost limits
  Models/
    AzureSettings.cs                    â† Configuration binding classes
    ChatModels.cs                       â† Request/response DTOs (Data Transfer Objects)
```

**NuGet packages used:**

| Package | Version | Purpose |
|---------|---------|---------|
| `Azure.Search.Documents` | 11.6.0 | `SearchClient` for querying the index |
| `Azure.AI.OpenAI` | 2.1.0 | `AzureOpenAIClient` for chat completions (gpt-4o-mini default) |
| `Azure.Identity` | 1.13.2 | `DefaultAzureCredential` â€” no passwords, Managed Identity |
| `Azure.Monitor.OpenTelemetry.AspNetCore` | 1.3.0 | Application Insights telemetry |
| `Microsoft.Extensions.Azure` | 1.8.0 | Azure client DI integration |

### 13.2 Configuration â€” appsettings.json

```json
{
  "AzureAISearch": {
    "Endpoint": "https://search-rag-managed.search.windows.net",
    "IndexName": "rag-index",
    "SemanticConfigName": "semantic-config"
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-rag-managed.openai.azure.com",
    "ChatDeployment": "gpt-4o-mini",
    "EmbeddingDeployment": "text-embedding-3-large"
  },
  "RagLimits": {
    "MaxContextChunks": 3,
    "MaxChunkChars": 500,
    "MaxOutputTokens": 200
  }
}
```

### 13.3 How the Code Works â€” File by File

#### Program.cs â€” Dependency Injection Setup

Registers Azure SDK clients and application services:

```csharp
var credential = new DefaultAzureCredential();

// Register Azure clients â€” singleton, reused across all requests
builder.Services.AddSingleton(new SearchClient(
    new Uri(searchSettings.Endpoint), searchSettings.IndexName, credential));
builder.Services.AddSingleton(new AzureOpenAIClient(
    new Uri(openAISettings.Endpoint), credential));

// Register services
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<ChatService>();

// Add Application Insights monitoring
builder.Services.AddOpenTelemetry().UseAzureMonitor();
```

#### SearchService.cs â€” Hybrid Search (The Key File)

This is the most important file. It runs **hybrid search** (keyword + vector + semantic ranking) in a single call:

```csharp
public async Task<List<SearchChunk>> HybridSearchAsync(string query, string? category = null, int topK = 3)
{
    var options = new SearchOptions
    {
        Size = topK,
        QueryType = SearchQueryType.Semantic,                    // Enable semantic ranking
        SemanticSearch = new SemanticSearchOptions
        {
            SemanticConfigurationName = _settings.SemanticConfigName,
            QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
        },
        VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizableTextQuery(query)                 // â† Index vectorizer generates embedding
                {
                    KNearestNeighborsCount = topK,
                    Fields = { "text_vector" }
                }
            }
        },
        Select = { "chunk_id", "chunk", "title", "category" }
    };

    var results = await _searchClient.SearchAsync<SearchDocument>(query, options);
    // ... map results to SearchChunk list
}
```

> **Key difference from Custom Path**: Notice we use `VectorizableTextQuery` instead of generating embeddings ourselves. The index's vectorizer (configured in Chapter 12's setup project) handles embedding at query time too. This means **zero embedding code** in the query app.

#### ChatService.cs â€” gpt-4o-mini Answer Generation (Low-Cost + 429-Safe)

Sends the question + retrieved chunks to gpt-4o-mini and returns a grounded answer:

```csharp
public async Task<ChatResponse> GenerateAnswerAsync(string question, List<SearchChunk> chunks)
{
    var chatClient = _openAIClient.GetChatClient(_settings.ChatDeployment);

  // Low-cost context shaping: cap chunks + trim content
  var selected = chunks.Take(3).ToList();
  var context = string.Join("\n\n", selected.Select((c, i) =>
    $"[Source {i + 1}: {c.Title}]\n{c.Content[..Math.Min(c.Content.Length, 500)]}"));

    var systemPrompt = @"You are an AI assistant that answers questions based ONLY on the provided context.
    Rules:
    1. Answer ONLY from the provided context.
    2. Cite sources using [Source N] format after each claim.
    3. Never make up information not in the context.";

    // Max output tokens kept small and 429 retry logic applied in service
    // ... send to gpt-4o-mini and return ChatResponse with Answer + Citations + TokenUsage
}
```

#### ChatController.cs â€” API Endpoint

Two endpoints:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/chat` | POST | Accepts `{ question, category?, topK? }` â†’ returns answer with citations |
| `/api/chat/health` | GET | Health check for load balancers and CI/CD |

**Request flow**: `ChatController` â†’ `SearchService.HybridSearchAsync()` â†’ `ChatService.GenerateAnswerAsync()` â†’ response

### 13.4 Run and Test Locally

```bash
# From the project folder
cd ManagedRagApp

# Restore packages and run
dotnet run
```

```bash
# Test the health endpoint
curl https://localhost:5001/api/chat/health

# Test the chat endpoint
curl -X POST https://localhost:5001/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"question\": \"What are the termination clauses?\", \"category\": \"contracts\"}"
```

**Example response:**

```json
{
  "answer": "The termination clauses state that either party may terminate with 90 days written notice [Source 1]. Early termination incurs a fee of 25% of remaining contract value [Source 2].",
  "citations": [
    { "index": 1, "title": "vendor-contract-2025.pdf", "chunkId": "abc123", "relevanceScore": 3.42 },
    { "index": 2, "title": "vendor-contract-2025.pdf", "chunkId": "def456", "relevanceScore": 3.18 }
  ],
  "tokenUsage": { "promptTokens": 2730, "completionTokens": 85, "totalTokens": 2815 }
}
```

### 13.5 What You Write vs What Azure Handles

| Component | Custom Path (Part 2) | Managed Path (This Chapter) |
|-----------|---------------------|----------------------------|
| Document extraction | Your C# code calls Document Intelligence | Indexer's built-in document cracking |
| Chunking | Your C# code splits text | SplitSkill in skillset |
| Embedding generation | Your C# code calls Azure OpenAI | AzureOpenAIEmbeddingSkill |
| Indexing documents | Your C# code calls SearchClient.IndexDocumentsAsync | Indexer does it automatically |
| **Search + Answer** | **Your C# code** | **Your C# code (same)** |

> **ðŸŽ¯ Interview Point**: "In the managed path, my .NET app has zero ingestion code. I only write SearchService (hybrid search with VectorizableTextQuery â€” the index vectorizer handles embeddings) and ChatService (gpt-4o-mini with retrieved context, low-cost limits, and retry on rate limits). The ManagedRagSetup project creates the pipeline, and Azure AI Search handles all document processing through its indexer."
---

## 14. Deploy and CI/CD â€” Managed Path

### 14.1 Dockerfile

Already in `ManagedRagApp/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ManagedRagApp.dll"]
```

### 14.2 Deploy to Azure App Service

> **Current repo note**: `infra/main.bicep` already creates `plan-rag-managed` and `app-rag-managed`. Use the commands below only if you want to recreate or adjust manually.

```bash
# Variables
APP_NAME="app-rag-managed"
PLAN_NAME="plan-rag-managed"
RESOURCE_GROUP="rg-rag-managed"
SEARCH_NAME="search-rag-managed"
OPENAI_NAME="openai-rag-managed"
APPINSIGHTS_NAME="appinsights-rag-managed"

# Login
az login

# Create App Service Plan (Linux, B1 low-cost default for learning)
az appservice plan create \
  --name $PLAN_NAME \
  --resource-group $RESOURCE_GROUP \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $PLAN_NAME \
  --runtime "DOTNETCORE:8.0"

# Enable Managed Identity
az webapp identity assign \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Get Web App principal ID
APP_PRINCIPAL=$(az webapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

# Grant RBAC roles
SEARCH_ID=$(az search service show --name $SEARCH_NAME --resource-group $RESOURCE_GROUP --query id -o tsv)
OPENAI_ID=$(az cognitiveservices account show --name $OPENAI_NAME --resource-group $RESOURCE_GROUP --query id -o tsv)
APPINSIGHTS_CONN=$(az monitor app-insights component show \
  --app $APPINSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

az role assignment create --assignee $APP_PRINCIPAL --role "Search Index Data Reader" --scope $SEARCH_ID
az role assignment create --assignee $APP_PRINCIPAL --role "Cognitive Services OpenAI User" --scope $OPENAI_ID

# Configure app settings (no secrets â€” just endpoints)
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    AzureAISearch__Endpoint="https://${SEARCH_NAME}.search.windows.net" \
    AzureAISearch__IndexName="rag-index" \
    AzureAISearch__SemanticConfigName="semantic-config" \
    AzureOpenAI__Endpoint="https://${OPENAI_NAME}.openai.azure.com" \
    AzureOpenAI__ChatDeployment="gpt-4o-mini" \
    AzureOpenAI__EmbeddingDeployment="text-embedding-3-large" \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_CONN" \
    RagLimits__MaxContextChunks="3" \
    RagLimits__MaxChunkChars="500" \
    RagLimits__MaxOutputTokens="200"

# Deploy the app
cd ManagedRagApp
dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip . && cd ..
az webapp deploy --name $APP_NAME --resource-group $RESOURCE_GROUP --src-path deploy.zip --type zip
```

### 14.3 CI/CD Pipeline â€” GitHub Actions (Setup + App)

This is the **real-world production pipeline**. It runs the ManagedRagSetup project (to create/update the search pipeline) AND deploys the ManagedRagApp query API in a single workflow:

```yaml
# .github/workflows/deploy-managed.yml
name: Deploy Managed RAG â€” Pipeline Setup + App

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  id-token: write
  contents: read

env:
  AZURE_WEBAPP_NAME: app-rag-managed
  RESOURCE_GROUP: rg-rag-managed
  DOTNET_VERSION: '8.0.x'

jobs:

  # Job 1: Build and Test the .NET Query App
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore and Build
        run: |
          dotnet restore ManagedRagApp/ManagedRagApp.csproj
          dotnet build ManagedRagApp --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Publish
        run: dotnet publish ManagedRagApp -c Release -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: managed-rag-app
          path: ./publish

  # Job 2: Run ManagedRagSetup (create/update index, data source, skillset, indexer)
  setup-search-pipeline:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Run ManagedRagSetup (production)
        run: dotnet run --project ManagedRagSetup -- --environment prod

      - name: Verify indexer is running
        run: |
          TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)
          curl -s "https://search-rag-managed.search.windows.net/indexers/rag-indexer/status?api-version=2024-07-01" \
            -H "Authorization: Bearer $TOKEN" \
            | jq -r '.lastResult.status'

  # Job 3: Deploy App (depends on both build + pipeline setup)
  deploy-app:
    needs: [build-and-test, setup-search-pipeline]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production

    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: managed-rag-app
          path: ./publish

      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}
          package: ./publish

      - name: Health check
        run: |
          sleep 30
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            https://${{ env.AZURE_WEBAPP_NAME }}.azurewebsites.net/api/chat/health)
          if [ "$STATUS" != "200" ]; then
            echo "Health check failed with status $STATUS"
            exit 1
          fi
```

**Pipeline Flow:**

```
Push to main
     â”‚
     â”œâ”€â”€ Job 1: Build + Test (ManagedRagApp â€” query API)
     â”‚
     â”œâ”€â”€ Job 2: Setup Pipeline (ManagedRagSetup â€” dotnet run --environment prod)
     â”‚          Creates/updates: index â†’ data source â†’ skillset â†’ indexer
     â”‚
     â””â”€â”€ Job 3: Deploy App (waits for Jobs 1 + 2)
              â””â”€â”€ Health check
```

> **Key**: Pipeline setup and app deploy together. If someone changes the chunk size in `appsettings.prod.json`, the CI/CD re-runs `dotnet run` and updates the pipeline in the same workflow. Single source of truth â€” all .NET, all in Git.

### 14.4 Ingestion Automation â€” Document Upload Pipeline

```yaml
# .github/workflows/ingest-documents.yml
name: Upload Documents to Managed Pipeline

on:
  push:
    paths:
      - 'documents/**'

permissions:
  id-token: write
  contents: read

jobs:
  upload:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 2

      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Upload changed documents to Blob Storage
        run: |
          CHANGED=$(git diff --name-only HEAD~1 HEAD -- documents/)
          for FILE in $CHANGED; do
            echo "Uploading $FILE..."
            az storage blob upload \
              --account-name stragmanaged \
              --container-name documents \
              --file "$FILE" \
              --name "$(basename $FILE)" \
              --overwrite \
              --auth-mode login
          done

      - name: Trigger indexer run
        run: |
          TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)
          curl -s -X POST "https://search-rag-managed.search.windows.net/indexers/rag-indexer/run?api-version=2024-07-01" \
            -H "Authorization: Bearer $TOKEN"
```

> **ðŸŽ¯ Interview Point**: "We have two CI/CD pipelines: (1) the main pipeline runs our .NET setup project (`dotnet run --environment prod`) to create/update the search pipeline, then deploys the query API, and (2) a document pipeline uploads new files to Blob Storage and triggers the indexer. Infrastructure is provisioned by Bicep, and the app + pipeline behavior is automated through .NET and GitHub Actions."
---

## 15. Security and Monitoring â€” Managed Path

### 15.1 Production Security Checklist

```bash
# 1. Enable Managed Identity (already done in Step 14.2)

# 2. Configure AI Search data-plane auth (recommended: AAD + API key compatibility)
az search service update \
  --name $SEARCH_NAME \
  --resource-group $RESOURCE_GROUP \
  --auth-options aadOrApiKey \
  --aad-auth-failure-mode http401WithBearerChallenge

# 3. Create Private Endpoints (production requirement)
# Private endpoint for AI Search
az network private-endpoint create \
  --name pe-search-managed \
  --resource-group $RESOURCE_GROUP \
  --vnet-name vnet-rag-managed \
  --subnet subnet-private \
  --private-connection-resource-id $SEARCH_ID \
  --group-id searchService \
  --connection-name connection-search

# Private endpoint for OpenAI
az network private-endpoint create \
  --name pe-openai-managed \
  --resource-group $RESOURCE_GROUP \
  --vnet-name vnet-rag-managed \
  --subnet subnet-private \
  --private-connection-resource-id $OPENAI_ID \
  --group-id account \
  --connection-name connection-openai

# Private endpoint for Storage
az network private-endpoint create \
  --name pe-storage-managed \
  --resource-group $RESOURCE_GROUP \
  --vnet-name vnet-rag-managed \
  --subnet subnet-private \
  --private-connection-resource-id $STORAGE_ID \
  --group-id blob \
  --connection-name connection-storage

# 4. Enable VNet integration for App Service
az webapp vnet-integration add \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --vnet vnet-rag-managed \
  --subnet subnet-app
```

### 15.2 Monitoring Setup

```bash
# Link Application Insights to web app
APPINSIGHTS_KEY=$(az monitor app-insights component show \
  --app appinsights-rag-managed \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_KEY"

# Monitor indexer health (run periodically)
TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)
curl -s "${SEARCH_ENDPOINT}/indexers/rag-indexer/status?api-version=2024-07-01" \
  -H "Authorization: Bearer $TOKEN" \
  | python3 -c "
import json, sys
data = json.load(sys.stdin)
last = data.get('lastResult', {})
print(f'Status: {last.get(\"status\", \"unknown\")}')
print(f'Items Processed: {last.get(\"itemsProcessed\", 0)}')
print(f'Items Failed: {last.get(\"itemsFailed\", 0)}')
"
```

### 15.3 Indexer Monitoring Alerts

Set up alerts for indexer failures:

```bash
# Create alert rule for indexer failures
az monitor metrics alert create \
  --name "indexer-failure-alert" \
  --resource-group $RESOURCE_GROUP \
  --scopes $SEARCH_ID \
  --condition "total DocumentsProcessedCount < 1" \
  --window-size 1h \
  --evaluation-frequency 15m \
  --description "Alert if indexer stops processing documents"
```

### 15.4 Security Architecture â€” Managed Path

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚  Virtual Network                                                 â”‚
â”‚                                                                   â”‚
â”‚  â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”     Private      â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”         â”‚
â”‚  â”‚  App Service  â”‚â”€â”€â”€â”€Endpointâ”€â”€â”€â”€â–¶â”‚  Azure AI Search  â”‚         â”‚
â”‚  â”‚ (Managed ID)  â”‚                  â”‚   (Managed ID)    â”‚         â”‚
â”‚  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜                  â””â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜         â”‚
â”‚         â”‚                                    â”‚                    â”‚
â”‚     Private EP                           Private EP               â”‚
â”‚         â”‚                          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”         â”‚
â”‚         â–¼                          â–¼         â–¼         â–¼         â”‚
â”‚  â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”  â”‚
â”‚  â”‚  Key Vault  â”‚          â”‚  OpenAI    â”‚ â”‚ Blob     â”‚ â”‚Doc     â”‚  â”‚
â”‚  â”‚   (RBAC)    â”‚          â”‚  (RBAC)    â”‚ â”‚ Storage  â”‚ â”‚Intel   â”‚  â”‚
â”‚  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜          â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”€â”€â”˜  â”‚
â”‚                                                                   â”‚
â”‚  All traffic stays within VNet â€” no public internet exposure      â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### 15.5 Security Checklist â€” Managed Path

| Security Layer | Implementation | Status |
|---------------|---------------|--------|
| App Authentication | Managed Identity (DefaultAzureCredential) | Required |
| Search Authentication | Managed Identity (Search â†’ OpenAI, Storage, DocIntel) | Required |
| Secrets | Key Vault with RBAC (no API keys in app settings) | Required |
| Network | Private Endpoints + VNet Integration | Required for production |
| Transport | HTTPS only (TLS 1.2+) | Required |
| Monitoring | Application Insights + Indexer status alerts | Required |
| Access | RBAC least-privilege roles per service | Required |
| Data | Encryption at rest (service-managed) | Automatic |
| Indexer | Disable public access after private endpoints configured | Recommended |

> **ðŸŽ¯ Interview Point**: "The managed path has two identity layers â€” the App Service's Managed Identity for query operations, and the Search service's Managed Identity for indexer operations. Both use RBAC, no API keys."

---


# Part 4: Custom vs Managed â€” Clear Comparison

---

## 16. Complete Comparison

### 16.1 Side-by-Side Overview

| Aspect | Custom Code Path (Part 2) | Managed Path (Part 3) |
|--------|--------------------------|----------------------|
| **Ingestion Code** | You write all code: extraction, chunking, embedding, indexing | Zero code â€” skillset + indexer handles everything |
| **Query Code** | You write embedding generation + search + chat | You write search + chat (vectorizer handles embedding) |
| **Document Intelligence** | Your code calls DI API, processes response | Skillset calls DI automatically |
| **Chunking** | Your code splits text (full control over strategy) | SplitSkill does it (configurable page size + overlap) |
| **Embedding** | Your code calls OpenAI embedding API | EmbeddingSkill does it automatically |
| **New Document Handling** | Upload triggers your code (API endpoint or Azure Function) | Upload to blob â†’ indexer detects and processes automatically |
| **Deleted Document Handling** | Your code must detect and remove from index | Indexer handles deletion tracking automatically |
| **Schedule** | You build scheduling (cron, timer trigger) | Indexer has built-in schedule (`PT5M`, `PT1H`) |
| **Azure AI Search SKU** | Free or Basic works | Basic works for learning; scale to Standard (S1) for higher throughput |
| **Cost** | Usually lower at very small scale; more app-side ingestion effort | Similar at learning scale (Basic + B1), often higher at larger scale due Search tier |
| **Debugging** | Standard .NET debugging â€” breakpoints, logs, step-through | Azure portal logs + indexer status API |
| **Flexibility** | Unlimited â€” any chunking strategy, any preprocessing | Limited to available skills and configurations |
| **CI/CD** | Single pipeline: build + test + deploy (app + ingestion) | Two pipelines: app deploy + document upload |
| **Monitoring** | Application Insights on your code | Application Insights on app + indexer monitoring |
| **Team Size** | Needs .NET developer who understands the full pipeline | Can be managed by someone comfortable with Azure portal/CLI |

### 16.2 When to Use Each

**Use Custom Code Path When:**
- You need custom chunking strategies (sentence-based, semantic, domain-specific)
- You want full debugging control (breakpoints in extraction, chunking, embedding)
- Your documents require preprocessing (PII removal, format conversion, metadata enrichment)
- You want to minimize Azure AI Search cost (can use Free/Basic tier)
- You have .NET developers who want full ownership of the pipeline

**Use Managed Path When:**
- You want the fastest path to production
- Standard chunking (fixed-size pages with overlap) is sufficient
- Your team prefers configuration over code
- You want automatic handling of new/modified/deleted documents
- You want built-in scheduling without writing timer triggers
- You are comfortable starting on Basic Search and scaling to S1 when throughput requires it

### 16.3 Cost Comparison (Monthly Estimate)

| Component | Custom Path | Managed Path | Notes |
|-----------|------------|-------------|-------|
| Azure AI Search | $0 (Free) or ~$75 (Basic) | ~$75 (Basic learning) or ~$250 (S1 production) | Managed can run on Basic for learning |
| Azure OpenAI (Embeddings) | ~$2-10 | ~$2-10 | Similar cost |
| Azure OpenAI (Chat) | ~$1-8 | ~$1-8 | Similar with `gpt-4o-mini` + low token limits |
| Document Intelligence | ~$1-5 | ~$1-5 | Similar cost |
| App Service | ~$13 (B1) to ~$55 (P1V3) | ~$13 (B1) to ~$55 (P1V3) | Start with B1 for learning |
| Blob Storage | ~$1-2 | ~$1-2 | Similar cost |
| **Total (Small Scale)** | **~$15-95/month** | **~$90-140/month** | Custom can be cheapest with Free Search + local ingestion |
| **Total (Production)** | **~$250-500/month** | **~$300-550/month** | Difference is driven mainly by Search tier and traffic |

> **Key Insight**: The biggest cost driver is Azure AI Search tier selection. For learning, Basic + B1 keeps managed costs reasonable; at higher throughput, S1 can increase spend but reduces operational complexity.

### 16.4 Code Comparison â€” What You Write vs What Azure Does

**Ingestion â€” Custom Path (you write ~500 lines):**
```
Your Code:
  DocIntelligenceService.cs  â†’ Call DI API, extract text + tables
  ChunkingService.cs         â†’ Split text into chunks with overlap
  EmbeddingService.cs        â†’ Call OpenAI for vectors
  SearchIndexService.cs      â†’ Upload chunks to search index
  IngestionController.cs     â†’ API endpoint to trigger pipeline
```

**Ingestion â€” Managed Path (you write 0 lines, configure JSON):**
```
Azure Configuration:
  Data Source    â†’ Points to Blob Storage container
  Skillset       â†’ SplitSkill (chunking) + EmbeddingSkill (vectors)
  Index          â†’ Field definitions + vector search profile
  Indexer        â†’ Connects data source â†’ skillset â†’ index, runs on schedule
```

**Query â€” Custom Path (~300 lines):**
```
Your Code:
  EmbeddingService.cs    â†’ Generate query embedding (you call OpenAI)
  SearchService.cs       â†’ Build search request with embedding vector
  ChatService.cs         â†’ Send context to gpt-4o-mini (or your chosen chat model)
  ChatController.cs      â†’ API endpoint
```

**Query â€” Managed Path (~250 lines):**
```
Your Code:
  SearchService.cs     â†’ Build search request with VectorizableTextQuery (Azure handles embedding)
  ChatService.cs       â†’ Send context to gpt-4o-mini (identical to custom)
  ChatController.cs    â†’ API endpoint (identical to custom)
```

### 16.5 Decision Flowchart

```
Do you need custom chunking strategies?
  â”œâ”€â”€ YES â†’ Custom Code Path
  â””â”€â”€ NO
      â”‚
      Is fixed-size chunking (with overlap) acceptable?
      â”œâ”€â”€ NO â†’ Custom Code Path
      â””â”€â”€ YES
          â”‚
      Do you want zero ingestion code + built-in indexer scheduling?
      â”œâ”€â”€ YES â†’ Managed Path âœ… (start with Basic Search, scale to S1 if needed)
      â””â”€â”€ NO â†’ Custom Code Path (you prefer full control)
```

> **ðŸŽ¯ Interview Answer**: "I've built both custom and managed RAG pipelines. Custom gives full control â€” I write extraction, chunking, embedding, and indexing code. Managed uses Azure AI Search skillsets and indexers â€” zero ingestion code, just configuration. I choose based on chunking needs, budget, and team preference: Basic Search is enough for learning, and I scale to S1 only when throughput requires it."

---

## 17. Migration Guide: Custom to Managed

### 17.1 Why Migrate?

You built the custom path first (learning, control, debugging). Now for production, you want:
- Automatic document processing (no ingestion code to maintain)
- Built-in scheduling and change detection
- Simpler operations (fewer moving parts)

### 17.2 What Changes During Migration

| Component | Before (Custom) | After (Managed) | Action |
|-----------|-----------------|-----------------|--------|
| Document extraction | `DocIntelligenceService.cs` | Skillset built-in cracking | Remove service class |
| Chunking | `ChunkingService.cs` | SplitSkill in skillset | Remove service class |
| Embedding generation | `EmbeddingService.cs` | EmbeddingSkill in skillset | Remove service class (ingestion only) |
| Index management | `SearchIndexService.cs` | Indexer manages automatically | Remove upload code |
| Ingestion trigger | `IngestionController.cs` | Blob â†’ indexer (automatic) | Remove controller |
| Query embedding | `EmbeddingService.cs` | VectorizableTextQuery | Switch query approach |
| Search service | `SearchService.cs` | Update to use VectorizableTextQuery | Modify service |
| Chat service | `ChatService.cs` | No change | Keep as-is |
| Search index | Your code creates it | You define it with vectorizer | Recreate index |
| CI/CD | Single pipeline | Two pipelines (app + docs) | Update workflows |

### 17.3 Step-by-Step Migration

**Step 1: Ensure Azure AI Search tier supports managed pipeline features**
```bash
# If you're on Free tier, create a new Basic (learning) or Standard (higher scale) service
az search service create \
  --name search-rag-managed \
  --resource-group $RESOURCE_GROUP \
  --sku basic \
  --location centralindia
```

**Step 2: Create Data Source, Skillset, Index, and Indexer**
Follow Chapter 12 steps exactly â€” create all four components.

**Step 3: Upload existing documents to blob container**
```bash
# Upload all your existing documents
az storage blob upload-batch \
  --account-name $STORAGE_NAME \
  --destination documents \
  --source ./your-documents-folder \
  --auth-mode login
```

**Step 4: Run the indexer and verify**
```bash
TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)

# Trigger indexer
curl -X POST "${SEARCH_ENDPOINT}/indexers/rag-indexer/run?api-version=2024-07-01" \
  -H "Authorization: Bearer $TOKEN"

# Check count matches your custom path
curl "${SEARCH_ENDPOINT}/indexes/rag-index/docs/\$count?api-version=2024-07-01" \
  -H "Authorization: Bearer $TOKEN"
```

**Step 5: Update your .NET query code**
```csharp
// BEFORE (Custom Path) â€” you generate the embedding yourself
var embedding = await _embeddingService.GenerateEmbeddingAsync(query);
var vectorQuery = new VectorizedQuery(embedding)
{
    KNearestNeighborsCount = topK,
    Fields = { "contentVector" }
};

// AFTER (Managed Path) â€” Azure generates the embedding via vectorizer
var vectorQuery = new VectorizableTextQuery(query)
{
    KNearestNeighborsCount = topK,
    Fields = { "text_vector" }
};
```

**Step 6: Remove ingestion code**
Delete these files (no longer needed):
- `DocIntelligenceService.cs`
- `ChunkingService.cs`
- `EmbeddingService.cs` (ingestion methods only â€” keep if shared with query)
- `SearchIndexService.cs` (upload methods)
- `IngestionController.cs`

**Step 7: Update CI/CD**
Replace single pipeline with two pipelines (Chapter 14.3 and 14.4).

### 17.4 Migration Verification Checklist

| Check | How to Verify | Expected |
|-------|---------------|----------|
| Documents indexed | Check index document count | Same count as custom path |
| Search quality | Run same test queries | Similar or better results |
| Chat answers | Ask same questions | Same quality answers with citations |
| Ingestion automation | Upload new doc to blob | Appears in index within 5 minutes |
| Deletion handling | Delete blob | Removed from index on next run |
| CI/CD | Push code change | App deploys, health check passes |
| Monitoring | Check App Insights | Requests logged, no errors |

> **ðŸŽ¯ Interview Answer**: "I migrated from custom to managed by creating a skillset with SplitSkill and EmbeddingSkill, switching from VectorizedQuery to VectorizableTextQuery, and removing all ingestion code. The key platform change was moving to a Search tier that supports managed features (Basic for learning, S1 when higher throughput is needed). I verified by comparing search results and document counts between both paths."

---


