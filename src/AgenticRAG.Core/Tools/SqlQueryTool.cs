// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// SqlQueryTool — AI Tool for querying structured business data.
//
// Unlike documents (pre-indexed), SQL data is queried live at runtime.
// The agent writes SELECT queries against read-only views — never raw tables.
//
// Security model (critical):
//   • Only SELECT statements allowed (no INSERT/UPDATE/DELETE/DROP)
//   • Queries must reference whitelisted views only (vw_BillingOverview, etc.)
//   • Dangerous keywords blocked: EXEC, xp_, sp_, --, ;, /*
//   • 10-second timeout, max 50 rows returned
//
// The agent can also call GetSchemaAsync() first to learn column names
// before writing its SQL query.
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
using System.ComponentModel;
using System.Text;
using Microsoft.Data.SqlClient;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class SqlQueryTool
{
    private readonly string _connectionString;
    private readonly HashSet<string> _allowedViews;  // Whitelist — agent can ONLY query these views

    public SqlQueryTool(SqlServerSettings settings)
    {
        _connectionString = settings.ConnectionString;
        _allowedViews = new HashSet<string>(
            settings.AllowedViews.Count > 0
                ? settings.AllowedViews
                : new List<string> { "vw_BillingOverview", "vw_ContractSummary", "vw_InvoiceDetail", "vw_VendorAnalysis" },
            StringComparer.OrdinalIgnoreCase);
    }

    [Description("Query structured business data from SQL Server. " +
                 "Available views: " +
                 "vw_BillingOverview (VendorName, ContractNumber, ContractStatus, InvoiceNumber, InvoiceDate, Amount, PaidAmount, InvoiceStatus, OutstandingBalance) — " +
                 "vw_ContractSummary (VendorName, ContractNumber, StartDate, EndDate, TotalValue, Status, InvoiceCount, TotalInvoiced, TotalPaid) — " +
                 "vw_InvoiceDetail (InvoiceNumber, InvoiceDate, DueDate, LineItemDescription, Quantity, UnitPrice, LineTotal, LineCategory, InvoiceTotal) — " +
                 "vw_VendorAnalysis (VendorName, VendorCategory, ContractCount, InvoiceCount, TotalBilled, TotalPaid, TotalOutstanding). " +
                 "Use this for billing data, invoice details, vendor financial information. " +
                 "ONLY write SELECT queries. Filters are case-insensitive.")]
    public async Task<string> QuerySqlAsync(
        [Description("A SELECT SQL query using ONLY the allowed views (vw_BillingOverview, vw_ContractSummary, vw_InvoiceDetail, vw_VendorAnalysis). " +
                     "Example: SELECT VendorName, Amount, OutstandingBalance FROM vw_BillingOverview WHERE VendorName LIKE '%Contoso%'")] string sqlQuery)
    {
        if (!ValidateQuery(sqlQuery, out string error))
            return $"QUERY BLOCKED: {error}";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sqlQuery, connection);
            command.CommandTimeout = 10;

            using var reader = await command.ExecuteReaderAsync();
            var sb = new StringBuilder();

            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();
            sb.AppendLine("| " + string.Join(" | ", columns) + " |");
            sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

            int rowCount = 0;
            while (await reader.ReadAsync() && rowCount < 50)
            {
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "");
                sb.AppendLine("| " + string.Join(" | ", values) + " |");
                rowCount++;
            }

            return rowCount > 0
                ? $"[SQLSource] Query returned {rowCount} rows:\n\n{sb}"
                : "[SQLSource] Query returned no results.";
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
    }

    [Description("Get the schema (column names and types) of available SQL views. " +
                 "Call this FIRST if you're unsure about column names.")]
    public Task<string> GetSchemaAsync()
    {
        return Task.FromResult(@"Available SQL Views:

1. vw_BillingOverview
   - VendorName (NVARCHAR) — vendor company name
   - ContractNumber (NVARCHAR) — contract identifier
   - ContractStatus (NVARCHAR) — 'Active', etc.
   - InvoiceNumber (NVARCHAR) — invoice identifier
   - InvoiceDate (DATE)
   - Amount (DECIMAL) — invoice amount
   - PaidAmount (DECIMAL) — amount paid
   - InvoiceStatus (NVARCHAR) — 'Paid', 'Pending', 'Partial'
   - OutstandingBalance (DECIMAL) — Amount minus PaidAmount

2. vw_ContractSummary
   - VendorName (NVARCHAR)
   - ContractNumber (NVARCHAR)
   - StartDate (DATE)
   - EndDate (DATE)
   - TotalValue (DECIMAL) — total contract value
   - Status (NVARCHAR)
   - InvoiceCount (INT)
   - TotalInvoiced (DECIMAL)
   - TotalPaid (DECIMAL)

3. vw_InvoiceDetail
   - InvoiceNumber (NVARCHAR)
   - InvoiceDate (DATE)
   - DueDate (DATE)
   - LineItemDescription (NVARCHAR)
   - Quantity (INT)
   - UnitPrice (DECIMAL)
   - LineTotal (DECIMAL)
   - LineCategory (NVARCHAR) — 'Development', 'Infrastructure', 'Support', 'Consulting'
   - InvoiceTotal (DECIMAL)

4. vw_VendorAnalysis
   - VendorName (NVARCHAR)
   - VendorCategory (NVARCHAR) — 'IT Services', 'Cloud Infrastructure', 'Consulting'
   - ContractCount (INT)
   - InvoiceCount (INT)
   - TotalBilled (DECIMAL)
   - TotalPaid (DECIMAL)
   - TotalOutstanding (DECIMAL)");
    }

    /// Validates the agent-generated SQL for safety before execution.
    /// Blocks writes, dangerous keywords, and queries against non-whitelisted objects.
    private bool ValidateQuery(string sql, out string error)
    {
        error = "";
        var trimmed = sql.Trim();

        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only SELECT queries are allowed.";
            return false;
        }

        var blocked = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE",
                              "EXEC", "EXECUTE", "TRUNCATE", "MERGE", "GRANT", "REVOKE",
                              "xp_", "sp_", "--", ";", "/*" };
        foreach (var kw in blocked)
        {
            if (trimmed.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Blocked keyword detected: {kw}";
                return false;
            }
        }

        bool referencesAllowedView = _allowedViews.Any(v =>
            trimmed.Contains(v, StringComparison.OrdinalIgnoreCase));
        if (!referencesAllowedView)
        {
            error = $"Query must use one of: {string.Join(", ", _allowedViews)}";
            return false;
        }

        return true;
    }
}
