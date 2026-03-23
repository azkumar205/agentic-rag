namespace AgenticRag.Agent.Interfaces;

/// <summary>
/// Defines the contract for an agentic tool that can be invoked during reasoning.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}
