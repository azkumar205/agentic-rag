using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using System.Text;

namespace AgenticRag.Tools;

/// <summary>
/// Extracts text and tables from PDFs/images using Azure Document Intelligence (prebuilt-layout).
/// Registered as a FunctionTool for the Executor agent.
/// </summary>
public sealed class DocumentIntelligenceTool
{
    private readonly DocumentIntelligenceClient _client;

    public DocumentIntelligenceTool(string endpoint, DefaultAzureCredential credential)
    {
        _client = new DocumentIntelligenceClient(new Uri(endpoint), credential);
    }

    /// <summary>Extract text and tables from a PDF or image file.</summary>
    public async Task<string> ExtractAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);

        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            BinaryData.FromStream(stream),
            contentType: "application/octet-stream");

        var result = operation.Value;
        var sb = new StringBuilder();

        // Extract page text
        if (result.Pages is not null)
        {
            foreach (var page in result.Pages)
            {
                sb.AppendLine($"--- Page {page.PageNumber} ---");
                if (page.Lines is not null)
                {
                    foreach (var line in page.Lines)
                        sb.AppendLine(line.Content);
                }
            }
        }

        // Extract tables
        if (result.Tables is not null)
        {
            for (int t = 0; t < result.Tables.Count; t++)
            {
                var table = result.Tables[t];
                sb.AppendLine($"\n--- Table {t + 1} ---");

                var rows = new SortedDictionary<int, SortedDictionary<int, string>>();
                foreach (var cell in table.Cells)
                {
                    if (!rows.ContainsKey(cell.RowIndex))
                        rows[cell.RowIndex] = new SortedDictionary<int, string>();
                    rows[cell.RowIndex][cell.ColumnIndex] = cell.Content;
                }

                foreach (var (_, cols) in rows)
                {
                    var values = Enumerable.Range(0, table.ColumnCount)
                        .Select(c => cols.TryGetValue(c, out var v) ? v : "")
                        .ToArray();
                    sb.AppendLine(string.Join(" | ", values));
                }
            }
        }

        return sb.ToString();
    }
}
