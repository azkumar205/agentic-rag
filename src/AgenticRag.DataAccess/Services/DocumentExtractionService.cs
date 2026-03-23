using Azure;
using Azure.AI.DocumentIntelligence;
using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticRag.DataAccess.Services;

/// <summary>
/// Extracts text, images, tables, and OCR content from documents using Azure Document Intelligence.
/// Supports PDFs, Word documents, images, and scanned documents.
/// </summary>
public class DocumentExtractionService : IDocumentExtractionService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<DocumentExtractionService> _logger;
    private const int ChunkSize = 500;
    private const int ChunkOverlap = 50;

    public DocumentExtractionService(
        IOptions<DocumentIntelligenceOptions> options,
        ILogger<DocumentExtractionService> logger)
    {
        _client = new DocumentIntelligenceClient(
            new Uri(options.Value.Endpoint),
            new AzureKeyCredential(options.Value.ApiKey));
        _logger = logger;
    }

    public async Task<IEnumerable<DocumentChunk>> ExtractChunksAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting content from document '{FileName}'", fileName);

        var contentType = DetectContentType(fileName);
        var chunks = new List<DocumentChunk>();
        var documentId = Guid.NewGuid().ToString();

        var binaryData = BinaryData.FromStream(documentStream);
        var analyzeOptions = new AnalyzeDocumentOptions("prebuilt-layout", binaryData);

        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            analyzeOptions,
            cancellationToken: cancellationToken);

        var result = operation.Value;

        // Extract text by page
        foreach (var page in result.Pages)
        {
            var pageText = string.Join(" ", page.Lines.Select(l => l.Content));
            var pageChunks = ChunkText(pageText, documentId, fileName, page.PageNumber, string.Empty, contentType);
            chunks.AddRange(pageChunks);
        }

        // Extract tables separately
        int tableIndex = 0;
        foreach (var table in result.Tables)
        {
            var tableText = ConvertTableToText(table);
            var pageNumber = table.BoundingRegions.Count > 0 ? table.BoundingRegions[0].PageNumber : 0;
            chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                DocumentId = documentId,
                FileName = fileName,
                Content = tableText,
                ContentType = ContentType.Table,
                ChunkIndex = chunks.Count + tableIndex,
                PageNumber = pageNumber,
                Section = $"Table {++tableIndex}"
            });
        }

        _logger.LogInformation("Extracted {ChunkCount} chunks from '{FileName}'", chunks.Count, fileName);
        return chunks;
    }

    private static ContentType DetectContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => ContentType.Pdf,
            ".docx" or ".doc" => ContentType.Word,
            ".png" or ".jpg" or ".jpeg" or ".tiff" or ".bmp" => ContentType.Image,
            _ => ContentType.Text
        };
    }

    private static List<DocumentChunk> ChunkText(
        string text,
        string documentId,
        string fileName,
        int pageNumber,
        string section,
        ContentType contentType)
    {
        var chunks = new List<DocumentChunk>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int chunkIndex = 0;

        for (int i = 0; i < words.Length; i += ChunkSize - ChunkOverlap)
        {
            var chunkWords = words.Skip(i).Take(ChunkSize).ToArray();
            if (chunkWords.Length == 0) break;

            chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                DocumentId = documentId,
                FileName = fileName,
                Content = string.Join(" ", chunkWords),
                ContentType = contentType,
                ChunkIndex = chunkIndex++,
                PageNumber = pageNumber,
                Section = section
            });
        }

        return chunks;
    }

    private static string ConvertTableToText(DocumentTable table)
    {
        var rows = table.Cells
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key)
            .Select(row => string.Join(" | ", row.OrderBy(c => c.ColumnIndex).Select(c => c.Content)));
        return string.Join("\n", rows);
    }
}
