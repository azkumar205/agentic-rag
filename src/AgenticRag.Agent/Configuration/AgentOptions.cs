namespace AgenticRag.Agent.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";
    public int MaxReasoningSteps { get; set; } = 5;
    public int CacheExpirationMinutes { get; set; } = 60;
    public int MaxChatHistoryMessages { get; set; } = 20;
    public bool EnableMemorization { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
    public bool EnableReasoningTrace { get; set; } = true;
}
