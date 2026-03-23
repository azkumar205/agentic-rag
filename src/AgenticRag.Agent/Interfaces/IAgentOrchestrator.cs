using AgenticRag.Shared.Models;

namespace AgenticRag.Agent.Interfaces;

/// <summary>
/// Orchestrates multi-step reasoning and tool calling for agentic RAG.
/// </summary>
public interface IAgentOrchestrator
{
    Task<AgentResponse> RunAsync(string userQuery, string threadId, CancellationToken cancellationToken = default);
}
