namespace AgenticRag.DataAccess.Configuration;

public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultContainerName { get; set; } = "documents";
}
