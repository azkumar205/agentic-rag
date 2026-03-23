namespace AgenticRag.DataAccess.Configuration;

public class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
