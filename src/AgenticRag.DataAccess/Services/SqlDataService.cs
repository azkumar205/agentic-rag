using Microsoft.Data.SqlClient;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Executes read-only SQL queries against Azure SQL Server for the agentic RAG SQL tool.
/// </summary>
public class SqlDataService : ISqlDataService
{
    private readonly string _connectionString;
    private readonly ILogger<SqlDataService> _logger;

    public SqlDataService(IOptions<SqlOptions> options, ILogger<SqlDataService> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public async Task<IEnumerable<Dictionary<string, object?>>> QueryAsync(
        string sql,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing SQL query: {Sql}", sql);

        var results = new List<Dictionary<string, object?>>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 30;

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                command.Parameters.AddWithValue(key, value);
            }
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        _logger.LogInformation("SQL query returned {Count} rows", results.Count);
        return results;
    }

    public async Task<string> GetSchemaDescriptionAsync(CancellationToken cancellationToken = default)
    {
        const string schemaSql = """
            SELECT
                t.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.TABLES t
            JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION
            """;

        var rows = await QueryAsync(schemaSql, cancellationToken: cancellationToken);

        var lines = rows.Select(r =>
            $"{r["TABLE_NAME"]}.{r["COLUMN_NAME"]} ({r["DATA_TYPE"]}, nullable: {r["IS_NULLABLE"]})");

        return string.Join("\n", lines);
    }
}
