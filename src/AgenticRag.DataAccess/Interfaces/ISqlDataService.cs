namespace AgenticRag.DataAccess.Interfaces;

/// <summary>
/// Interface for SQL Server data access.
/// </summary>
public interface ISqlDataService
{
    Task<IEnumerable<Dictionary<string, object?>>> QueryAsync(string sql, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<string> GetSchemaDescriptionAsync(CancellationToken cancellationToken = default);
}
