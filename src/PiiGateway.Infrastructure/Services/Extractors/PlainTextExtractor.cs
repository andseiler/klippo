using System.Text;
using System.Text.Json;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services.Extractors;

public class PlainTextExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> SpecializedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", "pdf", ".docx", "docx", ".xlsx", "xlsx"
    };

    public bool CanHandle(string fileType)
    {
        return !SpecializedExtensions.Contains(fileType);
    }

    public async Task<IReadOnlyList<TextSegment>> ExtractAsync(Stream stream, Guid jobId)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync();

        ValidateIsText(content);

        var segments = new List<TextSegment>();

        if (string.IsNullOrWhiteSpace(content))
            return segments;

        // Split into paragraphs (double newline or significant whitespace gaps)
        var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        var segmentIndex = 0;
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
                SourceLocation = JsonSerializer.Serialize(new { source = "plaintext" }),
                CreatedAt = DateTime.UtcNow
            });
        }

        return segments;
    }

    private static void ValidateIsText(string content)
    {
        if (content.Length == 0)
            return;

        var nonPrintableCount = 0;
        var sampleSize = Math.Min(content.Length, 8192);

        for (var i = 0; i < sampleSize; i++)
        {
            var c = content[i];

            // Null bytes are a strong indicator of binary content
            if (c == '\0')
                throw new InvalidOperationException("Binary file not supported. The file contains null bytes and cannot be processed as text.");

            // Count non-printable characters (excluding common whitespace)
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                nonPrintableCount++;
        }

        var ratio = (double)nonPrintableCount / sampleSize;
        if (ratio > 0.1)
            throw new InvalidOperationException("Binary file not supported. The file contains too many non-printable characters to be processed as text.");
    }
}
