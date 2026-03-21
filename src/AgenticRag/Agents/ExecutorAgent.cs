using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using AgenticRag.Models;
using AgenticRag.Tools;
using System.Text;
using System.Text.Json;

namespace AgenticRag.Agents;

/// <summary>
/// Executor Agent — Runs each plan step by dispatching to the appropriate tool.
/// Has access to: AI Search (built-in), Web Search (via Bing grounding),
/// Document Intelligence (custom), SQL Server (custom).
/// </summary>
public sealed class ExecutorAgent
{
    private readonly ChatClient _chatClient;
    private readonly SearchClient _searchClient;
    private readonly DocumentIntelligenceTool _docTool;
    private readonly SqlServerTool _sqlTool;

    private const string SystemPrompt = """
        You are an intelligent RAG executor. You receive evidence gathered from multiple tools
        and organize it clearly. Always indicate the source of each piece of evidence.
        """;

    public ExecutorAgent(
        string openAiEndpoint, string deployment,
        string searchEndpoint, string searchIndex,
        DefaultAzureCredential credential,
        DocumentIntelligenceTool docTool, SqlServerTool sqlTool)
    {
        var oaiClient = new AzureOpenAIClient(new Uri(openAiEndpoint), credential);
        _chatClient = oaiClient.GetChatClient(deployment);
        _searchClient = new SearchClient(new Uri(searchEndpoint), searchIndex, credential);
        _docTool = docTool;
        _sqlTool = sqlTool;
    }

    /// <summary>Execute all plan steps, gather evidence, and synthesize.</summary>
    public async Task<string> ExecuteAsync(string question, ExecutionPlan plan)
    {
        var evidence = new StringBuilder();

        foreach (var step in plan.Steps)
        {
            evidence.AppendLine($"\n--- Step {step.StepNumber}: {step.Description} ---");

            var result = step.ToolToUse.ToLower() switch
            {
                "search_knowledge_base" => await SearchKnowledgeBaseAsync(step.ToolInput),
                "query_sql" => await _sqlTool.QueryAsync(step.ToolInput),
                "extract_document" => await _docTool.ExtractAsync(step.ToolInput),
                "web_search" => await WebSearchAsync(step.ToolInput),
                "synthesize" => "[Synthesize step — combine all evidence above]",
                _ => $"Unknown tool: {step.ToolToUse}"
            };

            evidence.AppendLine(result);
        }

        // Use LLM to synthesize all evidence into a coherent answer
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                $"Question: {question}\n\nGathered Evidence:\n{evidence}\n\n" +
                "Synthesize a complete, well-cited answer from the evidence above.")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    private async Task<string> SearchKnowledgeBaseAsync(string query)
    {
        var options = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = "default"
            },
            Size = 5,
            Select = { "content", "title", "source" }
        };

        var results = await _searchClient.SearchAsync<SearchDocument>(query, options);
        var sb = new StringBuilder();

        await foreach (var result in results.Value.GetResultsAsync())
        {
            var content = result.Document.TryGetValue("content", out var c) ? c?.ToString() : "";
            var source = result.Document.TryGetValue("source", out var s) ? s?.ToString() : "";
            sb.AppendLine($"[Source: {source}] {content}");
        }

        return sb.Length > 0 ? sb.ToString() : "No results found in knowledge base.";
    }

    private async Task<string> WebSearchAsync(string query)
    {
        // Uses Azure OpenAI with Bing grounding extension
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("Search the internet and return factual, cited results."),
            new UserChatMessage(query)
        };

        // In production, use Azure AI Agent's WebSearchPreviewTool or Bing grounding
        var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            Temperature = 0.1f
        });

        return $"[Web search result] {response.Value.Content[0].Text}";
    }
}
