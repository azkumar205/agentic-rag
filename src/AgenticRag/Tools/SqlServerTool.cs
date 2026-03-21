using Microsoft.Data.SqlClient;
using System.Text;

namespace AgenticRag.Tools;

/// <summary>
/// Executes read-only SELECT queries against SQL Server.
/// Blocks all write/DDL operations for security.
/// </summary>
public sealed class SqlServerTool
{
    private readonly string _connectionString;

    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE",
        "TRUNCATE", "EXEC", "EXECUTE", "MERGE", "GRANT", "REVOKE",
        "xp_", "sp_"
    };

    private const int MaxRows = 100;

    public SqlServerTool(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>Execute a read-only SELECT query and return pipe-delimited results.</summary>
    public async Task<string> QueryAsync(string query)
    {
        var trimmed = query.Trim();

        // Must start with SELECT
        var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpper();
        if (firstWord != "SELECT")
            return "Error: Only SELECT queries are allowed.";

        // Check for blocked keywords
        var upper = query.ToUpper();
        foreach (var kw in BlockedKeywords)
        {
            if (upper.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return $"Error: '{kw}' operations are not permitted.";
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(trimmed, connection);
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync();

        var sb = new StringBuilder();

        // Column headers
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        sb.AppendLine(string.Join(" | ", columns));
        sb.AppendLine(new string('-', sb.Length));

        // Rows (max 100)
        int rowCount = 0;
        while (await reader.ReadAsync() && rowCount < MaxRows)
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "")
                .ToArray();
            sb.AppendLine(string.Join(" | ", values));
            rowCount++;
        }

        return sb.ToString();
    }
}
