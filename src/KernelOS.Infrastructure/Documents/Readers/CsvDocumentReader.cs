using KernelOS.Core.Documents;
using System.Text;

namespace KernelOS.Infrastructure.Documents.Readers;

public sealed class CsvDocumentReader : DocumentReaderBase, IDocumentReader
{
    public DocumentReaderDescriptor Descriptor { get; } = new("csv", [DocumentFormat.Csv], ["csv"], ["text/csv"]);
    public bool CanRead(DocumentReadRequest request) => request.Format == DocumentFormat.Csv;
    public async Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (bytes, text, encoding) = await ReadTextAsync(request.File.Path, cancellationToken);
            var (limited, status, warnings) = Limit(text, request.Options);
            if (limited is null) return new(status, Warnings: warnings, Error: "The document exceeds the configured text limit.");
            var parsed = Parse(limited);
            if (parsed.UnclosedQuotedField)
            {
                warnings.Add(new("CSV_UNCLOSED_QUOTED_FIELD", "The CSV ends inside a quoted field.", DocumentWarningSeverity.Warning));
                if (!request.Options.AllowPartialResults) return new(DocumentReadStatus.InvalidDocument, Warnings: warnings, Error: "The CSV document is incomplete.");
                status = DocumentReadStatus.PartialSuccess;
            }
            var records = parsed.Records; if (records.Count == 0) return new(status, CreateDocument(request, DocumentFormat.Csv, "text/csv", bytes, limited, encoding, [], [new("table-0", null, [], [], null)], warnings), warnings);
            var headers = records[0]; var rows = new List<DocumentTableRow>();
            foreach (var (record, index) in records.Skip(1).Select((record, index) => (record, index + 1)))
            {
                if (rows.Count >= request.Options.MaxRows) { warnings.Add(new("DOCUMENT_TRUNCATED", "The table reached the configured row limit.", DocumentWarningSeverity.Warning)); status = DocumentReadStatus.PartialSuccess; break; }
                if (record.Count > request.Options.MaxColumns) { warnings.Add(new("IRREGULAR_TABLE", "The table exceeds the configured column limit.", DocumentWarningSeverity.Warning)); status = DocumentReadStatus.PartialSuccess; }
                if (record.Count != headers.Count) warnings.Add(new("IRREGULAR_TABLE", "A row has a different number of columns.", DocumentWarningSeverity.Warning, new(Row: index)));
                rows.Add(new(index, record.Take(request.Options.MaxColumns).ToArray()));
            }
            var table = new DocumentTable("table-0", null, headers.Take(request.Options.MaxColumns).ToArray(), rows, null);
            return new(status, CreateDocument(request, DocumentFormat.Csv, "text/csv", bytes, limited, encoding, [], [table], warnings), warnings);
        }
        catch (DecoderFallbackException) { return new(DocumentReadStatus.InvalidDocument, Error: "The CSV encoding is not supported."); }
    }

    private static CsvParseResult Parse(string text)
    {
        var records = new List<List<string>>(); var row = new List<string>(); var field = new System.Text.StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append(c); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); } else if ((c == '\n' || c == '\r') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(field.ToString()); field.Clear(); records.Add(row); row = []; } else field.Append(c); }
        if (!quoted && (field.Length > 0 || row.Count > 0)) { row.Add(field.ToString()); records.Add(row); }
        return new(records, quoted);
    }

    private sealed record CsvParseResult(List<List<string>> Records, bool UnclosedQuotedField);
}
