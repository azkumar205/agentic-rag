namespace AgenticRag.DataAccess.Configuration;

public class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = "rag-documents";
    public int VectorDimensions { get; set; } = 1536;
}
