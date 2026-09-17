using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AfterSalesAI.Infrastructure;

internal static class PdfKnowledgeSourceReader
{
    public static PdfKnowledgeSource Read(string rootPath, string file, int chunkSize, CancellationToken cancellationToken)
    {
        using var pdf = PdfDocument.Open(file);
        var chunks = new List<PdfKnowledgeChunk>();
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = ContentOrderTextExtractor.GetText(page).Trim();
            for (var start = 0; start < content.Length;)
            {
                var length = Math.Min(chunkSize, content.Length - start);
                if (start + length < content.Length)
                {
                    var boundary = content.LastIndexOfAny([' ', '\n', '\r', '\t'], start + length - 1, length);
                    if (boundary > start) length = boundary - start + 1;
                }
                var text = content.Substring(start, length).Trim();
                if (!string.IsNullOrWhiteSpace(text)) chunks.Add(new PdfKnowledgeChunk(page.Number, text));
                start += length;
            }
        }
        if (chunks.Count == 0)
            throw new InvalidDataException("The PDF contains no extractable text. Supply a text-based PDF or perform approved OCR before ingestion.");

        var name = Path.GetFileNameWithoutExtension(file);
        var category = name.ToUpperInvariant() switch
        {
            "STANDARD_OPERATING_PROCEDURE" => "SOP",
            "KNOWLEDGE_BASE" => "KNOWLEDGE_BASE",
            "BUSINESS_PROCESS_DOCUMENT" => "BUSINESS_PROCESS",
            "ERROR_CATALOGUES" => "ERROR_CATALOGUE",
            "FAQS" => "FAQ",
            "POLICY_DOCUMENT" => "POLICY",
            "USER_MANUAL" => "USER_MANUAL",
            _ => "KNOWLEDGE"
        };
        return new PdfKnowledgeSource(Path.GetRelativePath(rootPath, file).Replace('\\', '/'), name.Replace('_', ' '), category, chunks);
    }
}

internal sealed record PdfKnowledgeSource(string RelativePath, string Title, string Category, IReadOnlyList<PdfKnowledgeChunk> Chunks);
internal sealed record PdfKnowledgeChunk(int PageNumber, string Content);
