using System.Text.Json;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PiiGateway.Infrastructure.Services.Extractors;

public class PdfExtractor : IDocumentExtractor
{
    public bool CanHandle(string fileType)
    {
        return fileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || fileType.Equals("pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<TextSegment>> ExtractAsync(Stream stream, Guid jobId)
    {
        var segments = new List<TextSegment>();
        var segmentIndex = 0;

        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page, addDoubleNewline: true);

            if (string.IsNullOrWhiteSpace(pageText))
            {
                // Check if page has images but no text (likely scanned)
                if (page.GetImages().Any())
                {
                    throw new InvalidOperationException(
                        $"Page {page.Number} appears to be a scanned image with no extractable text. OCR is not supported.");
                }
                continue;
            }

            // Split into paragraphs (double newline or significant whitespace gaps)
            var paragraphs = pageText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                var trimmed = paragraph.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                segments.Add(new TextSegment
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    SegmentIndex = segmentIndex++,
                    TextContent = trimmed,
                    SourceType = SourceType.Paragraph,
                    SourceLocation = JsonSerializer.Serialize(new { page = page.Number }),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return Task.FromResult<IReadOnlyList<TextSegment>>(segments);
    }
}
