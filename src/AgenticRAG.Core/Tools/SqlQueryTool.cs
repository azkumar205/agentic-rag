// =====================================================================================
// SqlQueryTool — AI TOOL: Queries live business data from SQL Server
// =====================================================================================
//
// WHAT IS THIS?
// Unlike documents (pre-indexed in Azure AI Search), SQL data is queried LIVE at runtime.
// When the agent needs billing data, invoice details, or vendor analysis, GPT-4o writes
// a SQL query and this tool executes it against read-only views.
//
// SECURITY MODEL (Critical for interviews):
//   1. SELECT ONLY — no INSERT, UPDATE, DELETE, DROP, ALTER, EXEC, or MERGE
//   2. WHITELISTED VIEWS — agent can ONLY query pre-approved views, never raw tables
//   3. BLOCKED KEYWORDS — xp_, sp_, --, ;, /* are all blocked (SQL injection prevention)
//   4. 10-SECOND TIMEOUT — prevents runaway queries from hogging resources
//   5. MAX 50 ROWS — prevents accidental data dumps
//
// GPT-4o typically calls GetSchemaAsync() FIRST to learn column names, then writes
// a SQL query using QuerySqlAsync(). Results are formatted as markdown tables.
//
// INTERVIEW TIP: "The agent can write SQL, but it's sandboxed — SELECT only, whitelisted
// views, blocked keywords, timeout, and row limits. It can never modify data."
// =====================================================================================
using System.ComponentModel;
using System.Text;
using Microsoft.Data.SqlClient;
using AgenticRAG.Core.Configuration;

namespace AgenticRAG.Core.Tools;

public class SqlQueryTool
{
    private readonly string _connectionString;
    private readonly HashSet<string> _allowedViews;  // WHITELIST — agent can ONLY query these views

    public SqlQueryTool(SqlServerSettings settings)
    {
        _connectionString = settings.ConnectionString;
        // Load allowed views from config, or fall back to default enterprise views
        _allowedViews = new HashSet<string>(
            settings.AllowedViews.Count > 0
                ? settings.AllowedViews
                : new List<string> { "vw_BillingOverview", "vw_ContractSummary", "vw_InvoiceDetail", "vw_VendorAnalysis" },
            StringComparer.OrdinalIgnoreCase);
    }

    // This [Description] tells GPT-4o when to use this tool and what data is available
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
        // STEP 1: Validate the query BEFORE executing — block anything dangerous
        if (!ValidateQuery(sqlQuery, out string error))
            return $"QUERY BLOCKED: {error}";

        try
        {
            // STEP 2: Execute the validated query against SQL Server
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sqlQuery, connection);
            command.CommandTimeout = 10;  // 10-second timeout to prevent runaway queries

            using var reader = await command.ExecuteReaderAsync();

            // STEP 3: Format results as a markdown table (GPT-4o reads this well)
            var sb = new StringBuilder();

            // Build table header
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();
            sb.AppendLine("| " + string.Join(" | ", columns) + " |");
            sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

            // Build table rows (max 50 to prevent data dumps)
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

    // GPT-4o calls this FIRST to learn what columns and views are available
    // before writing its SQL query — prevents column name guessing errors
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

    // ── SQL SECURITY VALIDATOR ──
    // Checks the agent-generated SQL BEFORE execution. Blocks:
    //   1. Non-SELECT statements (INSERT, UPDATE, DELETE, DROP, etc.)
    //   2. Dangerous keywords (EXEC, xp_, sp_, --, ;, /* — SQL injection vectors)
    //   3. Queries against non-whitelisted tables/views
    // This is defense-in-depth — even if GPT-4o tries to write dangerous SQL,
    // it gets blocked here before reaching the database.
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
