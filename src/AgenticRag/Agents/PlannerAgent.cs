using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using AgenticRag.Models;
using System.Text.Json;

namespace AgenticRag.Agents;

/// <summary>
/// Planner Agent — Decomposes user queries into ordered execution steps.
/// Each step specifies which tool to call and with what input.
/// </summary>
public sealed class PlannerAgent
{
    private readonly ChatClient _chatClient;

    private const string SystemPrompt = """
        You are a planning agent. Given a user question, decompose it into ordered steps.
        
        Available tools:
        - "search_knowledge_base": Search internal documents via Azure AI Search (hybrid)
        - "query_sql": Query structured data in SQL Server (generate SELECT only)
        - "extract_document": Extract text from a PDF or image file path
        - "web_search": Search the internet for real-time or external information
        
        Rules:
        1. Prefer internal sources before web_search
        2. For simple questions, 1-2 steps is enough
        3. Always end with a step using toolToUse: "synthesize"
        
        Respond ONLY with JSON:
        { "steps": [{ "stepNumber": 1, "description": "...", "toolToUse": "...", "toolInput": "..." }] }
        """;

    public PlannerAgent(string endpoint, string deployment, DefaultAzureCredential credential)
    {
        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        _chatClient = client.GetChatClient(deployment);
    }

    /// <summary>Create an execution plan for the question.</summary>
    public async Task<ExecutionPlan> CreatePlanAsync(string question, string? gapInfo = null)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                gapInfo is null
                    ? $"Question: {question}"
                    : $"Question: {question}\nGap: {gapInfo}")
        };

        var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0.2f
        });

        var json = response.Value.Content[0].Text;
        var plan = JsonSerializer.Deserialize<ExecutionPlan>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return plan ?? new ExecutionPlan(new List<PlanStep>());
    }
}
