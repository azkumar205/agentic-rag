using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using AgenticRag.Models;
using System.Text.Json;

namespace AgenticRag.Agents;

/// <summary>
/// Reflection Agent — Evaluates gathered evidence for completeness and accuracy.
/// Assigns a confidence score and identifies gaps requiring re-planning.
/// </summary>
public sealed class ReflectionAgent
{
    private readonly ChatClient _chatClient;

    private const string SystemPrompt = """
        You are a critical reasoning agent. Given a user's question and gathered evidence,
        evaluate whether we have enough information for a complete, accurate answer.

        Analyze:
        1. Does the evidence directly answer the question?
        2. Are there contradictions between sources?
        3. Is any critical information missing?
        4. How confident are you (0.0 to 1.0)?

        Respond with ONLY a JSON object:
        {
          "isComplete": true/false,
          "confidenceScore": 0.0-1.0,
          "gapAnalysis": "what's missing — or null",
          "suggestedAction": "what to do next — or null",
          "finalAnswer": "synthesised answer if isComplete=true — null otherwise"
        }

        If confidence >= 0.8 → isComplete=true and provide finalAnswer.
        If confidence < 0.8 → isComplete=false and explain gaps.
        """;

    public ReflectionAgent(string endpoint, string deployment, DefaultAzureCredential credential)
    {
        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        _chatClient = client.GetChatClient(deployment);
    }

    /// <summary>Evaluate evidence and determine if the answer is complete.</summary>
    public async Task<ReflectionResult> EvaluateAsync(string question, string evidence)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage($"Question: {question}\n\nEvidence:\n{evidence}")
        };

        var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = 0.1f
        });

        var json = response.Value.Content[0].Text;
        var result = JsonSerializer.Deserialize<ReflectionResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? new ReflectionResult(false, 0, "Failed to parse reflection", null, null);
    }
}
