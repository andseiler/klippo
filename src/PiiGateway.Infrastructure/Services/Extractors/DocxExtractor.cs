using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services.Extractors;

public class DocxExtractor : IDocumentExtractor
{
    public bool CanHandle(string fileType)
    {
        return fileType.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            || fileType.Equals("docx", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<TextSegment>> ExtractAsync(Stream stream, Guid jobId)
    {
        var segments = new List<TextSegment>();
        var segmentIndex = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
            return Task.FromResult<IReadOnlyList<TextSegment>>(segments);

        // Body paragraphs
        foreach (var para in body.Descendants<Paragraph>())
        {
            var text = GetParagraphText(para);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            segments.Add(CreateSegment(jobId, ref segmentIndex, text, SourceType.Paragraph,
                new { section = "body" }));
        }

        // Tables
        var tableIndex = 0;
        foreach (var table in body.Descendants<Table>())
        {
            var rowIndex = 0;
            foreach (var row in table.Descendants<TableRow>())
            {
                var colIndex = 0;
                foreach (var cell in row.Descendants<TableCell>())
                {
                    var cellText = string.Join(" ", cell.Descendants<Paragraph>()
                        .Select(GetParagraphText)
                        .Where(t => !string.IsNullOrWhiteSpace(t)));

                    if (!string.IsNullOrWhiteSpace(cellText))
                    {
                        segments.Add(CreateSegment(jobId, ref segmentIndex, cellText, SourceType.Cell,
                            new { table = tableIndex, row = rowIndex, col = colIndex }));
                    }
                    colIndex++;
                }
                rowIndex++;
            }
            tableIndex++;
        }

        // Headers
        if (doc.MainDocumentPart != null)
        {
            foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
            {
                foreach (var para in headerPart.Header?.Descendants<Paragraph>() ?? Enumerable.Empty<Paragraph>())
                {
                    var text = GetParagraphText(para);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        segments.Add(CreateSegment(jobId, ref segmentIndex, text, SourceType.Header,
                            new { section = "header" }));
                    }
                }
            }

            // Footers
            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                foreach (var para in footerPart.Footer?.Descendants<Paragraph>() ?? Enumerable.Empty<Paragraph>())
                {
                    var text = GetParagraphText(para);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        segments.Add(CreateSegment(jobId, ref segmentIndex, text, SourceType.Footer,
                            new { section = "footer" }));
                    }
                }
            }

            // Comments
            var commentsPart = doc.MainDocumentPart.WordprocessingCommentsPart;
            if (commentsPart?.Comments != null)
            {
                foreach (var comment in commentsPart.Comments.Descendants<DocumentFormat.OpenXml.Wordprocessing.Comment>())
                {
                    var text = string.Join(" ", comment.Descendants<Paragraph>()
                        .Select(GetParagraphText)
                        .Where(t => !string.IsNullOrWhiteSpace(t)));

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var author = comment.Author?.Value;
                        segments.Add(CreateSegment(jobId, ref segmentIndex, text, SourceType.Comment,
                            new { author = author ?? "unknown" }));
                    }
                }
            }

            // Footnotes
            var footnotesPart = doc.MainDocumentPart.FootnotesPart;
            if (footnotesPart?.Footnotes != null)
            {
                foreach (var footnote in footnotesPart.Footnotes.Descendants<Footnote>())
                {
                    // Skip separator and continuation separator footnotes (type 0 and 1)
                    var footnoteType = footnote.Type?.Value;
                    if (footnoteType == FootnoteEndnoteValues.Separator ||
                        footnoteType == FootnoteEndnoteValues.ContinuationSeparator)
                        continue;

                    var text = string.Join(" ", footnote.Descendants<Paragraph>()
                        .Select(GetParagraphText)
                        .Where(t => !string.IsNullOrWhiteSpace(t)));

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        segments.Add(CreateSegment(jobId, ref segmentIndex, text, SourceType.Footnote,
                            new { id = footnote.Id?.Value }));
                    }
                }
            }
        }

        // Tracked changes — extract deleted text that may contain PII
        foreach (var deletedRun in body.Descendants<DeletedRun>())
        {
            var text = deletedRun.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                segments.Add(CreateSegment(jobId, ref segmentIndex, text.Trim(), SourceType.Paragraph,
                    new { section = "tracked_change", type = "deleted" }));
            }
        }

        return Task.FromResult<IReadOnlyList<TextSegment>>(segments);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        return string.Join("", paragraph.Descendants<Run>()
            .SelectMany(r => r.Descendants<Text>())
            .Select(t => t.Text)).Trim();
    }

    private static TextSegment CreateSegment(Guid jobId, ref int segmentIndex, string text,
        SourceType sourceType, object sourceLocation)
    {
        return new TextSegment
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            SegmentIndex = segmentIndex++,
            TextContent = text,
            SourceType = sourceType,
            SourceLocation = JsonSerializer.Serialize(sourceLocation),
            CreatedAt = DateTime.UtcNow
        };
    }
}
