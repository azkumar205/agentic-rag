using System.Text.Json;
using AgenticRag.Agent.Interfaces;
using AgenticRag.DataAccess.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Tools;

/// <summary>
/// Agent tool that translates natural language questions into SQL queries and executes them.
/// </summary>
public class SqlQueryTool : IAgentTool
{
    private readonly ISqlDataService _sqlDataService;
    private readonly ILogger<SqlQueryTool> _logger;

    public string Name => "sql_query";
    public string Description => "Execute a read-only SQL SELECT query against the SQL Server database. Input: valid SQL SELECT statement.";

    public SqlQueryTool(ISqlDataService sqlDataService, ILogger<SqlQueryTool> logger)
    {
        _sqlDataService = sqlDataService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        // Only allow SELECT statements for safety
        var trimmed = input.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[SqlQueryTool] Rejected non-SELECT statement: {Input}", input);
            return JsonSerializer.Serialize(new { error = "Only SELECT queries are permitted." });
        }

        _logger.LogInformation("[SqlQueryTool] Executing SQL: {Input}", input);

        var rows = await _sqlDataService.QueryAsync(input, cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
    }
}
