namespace AgenticRag.DataAccess.Configuration;

public class SqlOptions
{
    public const string SectionName = "SqlDatabase";
    public string ConnectionString { get; set; } = string.Empty;
}
