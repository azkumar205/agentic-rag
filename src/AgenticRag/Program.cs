using AgenticRag.Cache;
using AgenticRag.Services;
using AgenticRag.Tools;
using AgenticRag.Agents;
using AgenticRag.Workflow;
using AgenticRag.Models;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ─────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

// ─── Register Services via DI ──────────────────────
builder.Services.AddSingleton(new DefaultAzureCredential());

builder.Services.AddSingleton<DocumentIntelligenceTool>(sp =>
    new DocumentIntelligenceTool(
        builder.Configuration["DocumentIntelligence:Endpoint"]!,
        sp.GetRequiredService<DefaultAzureCredential>()));

builder.Services.AddSingleton<SqlServerTool>(sp =>
    new SqlServerTool(builder.Configuration["SqlServer:ConnectionString"]!));

builder.Services.AddSingleton<SemanticCache>(sp =>
    new SemanticCache(
        builder.Configuration["Redis:ConnectionString"]!,
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:EmbeddingDeployment"]!,
        int.Parse(builder.Configuration["AzureOpenAI:EmbeddingDimensions"] ?? "3072"),
        double.Parse(builder.Configuration["Redis:SimilarityThreshold"] ?? "0.92"),
        sp.GetRequiredService<DefaultAzureCredential>()));

builder.Services.AddSingleton<PlannerAgent>(sp =>
    new PlannerAgent(
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:ChatDeployment"]!,
        sp.GetRequiredService<DefaultAzureCredential>()));

builder.Services.AddSingleton<ExecutorAgent>(sp =>
    new ExecutorAgent(
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:ChatDeployment"]!,
        builder.Configuration["AzureAISearch:Endpoint"]!,
        builder.Configuration["AzureAISearch:IndexName"]!,
        sp.GetRequiredService<DefaultAzureCredential>(),
        sp.GetRequiredService<DocumentIntelligenceTool>(),
        sp.GetRequiredService<SqlServerTool>()));

builder.Services.AddSingleton<ReflectionAgent>(sp =>
    new ReflectionAgent(
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:ChatDeployment"]!,
        sp.GetRequiredService<DefaultAzureCredential>()));

builder.Services.AddSingleton<RagWorkflow>();

builder.Services.AddSingleton<IngestionService>(sp =>
    new IngestionService(
        builder.Configuration["AzureAISearch:Endpoint"]!,
        builder.Configuration["AzureAISearch:IndexName"]!,
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:EmbeddingDeployment"]!,
        int.Parse(builder.Configuration["AzureOpenAI:EmbeddingDimensions"] ?? "3072"),
        sp.GetRequiredService<DefaultAzureCredential>(),
        sp.GetRequiredService<DocumentIntelligenceTool>()));

var app = builder.Build();

// ─── API Endpoints ─────────────────────────────────

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Main RAG endpoint
app.MapPost("/api/chat", async (ChatRequest request, RagWorkflow workflow) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question is required." });

    var response = await workflow.AskAsync(request.Question, request.UserId ?? "default");
    return Results.Ok(response);
});

// Ingestion endpoint — full pipeline: Extract → Chunk → Embed → Index into Azure AI Search
app.MapPost("/api/ingest", async (HttpRequest http, IngestionService ingestionService) =>
{
    var form = await http.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest(new { error = "No file provided." });

    var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
    await using (var stream = File.Create(tempPath))
    {
        await file.CopyToAsync(stream);
    }

    try
    {
        // Ensure the AI Search index exists with the correct schema before ingesting
        await ingestionService.EnsureIndexExistsAsync();

        var result = await ingestionService.IngestFileAsync(tempPath);
        return Results.Ok(new
        {
            fileName = result.FileName,
            chunksCreated = result.ChunksCreated,
            charactersProcessed = result.CharactersProcessed,
            message = $"Successfully ingested {result.ChunksCreated} chunks from '{result.FileName}' into Azure AI Search."
        });
    }
    finally
    {
        File.Delete(tempPath);
    }
});

app.Run();
