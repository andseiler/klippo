using System.Text.Json;
using ClosedXML.Excel;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services.Extractors;

public class XlsxExtractor : IDocumentExtractor
{
    public bool CanHandle(string fileType)
    {
        return fileType.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || fileType.Equals("xlsx", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<TextSegment>> ExtractAsync(Stream stream, Guid jobId)
    {
        var segments = new List<TextSegment>();
        var segmentIndex = 0;

        using var workbook = new XLWorkbook(stream);

        foreach (var worksheet in workbook.Worksheets)
        {
            // Sheet name as a segment
            if (!string.IsNullOrWhiteSpace(worksheet.Name))
            {
                segments.Add(CreateSegment(jobId, ref segmentIndex, worksheet.Name, SourceType.SheetName,
                    new { sheet = worksheet.Name }));
            }

            // Track merged cell ranges to avoid duplicate extraction
            var processedMergedRanges = new HashSet<string>();

            // Cell values
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
                continue;

            foreach (var row in usedRange.Rows())
            {
                foreach (var cell in row.Cells())
                {
                    // Handle merged cells — only process top-left cell
                    if (cell.IsMerged())
                    {
                        var mergedRange = cell.MergedRange();
                        var rangeKey = mergedRange.RangeAddress.ToString()!;

                        if (!processedMergedRanges.Add(rangeKey))
                            continue; // Already processed this merged range

                        var mergedText = cell.GetFormattedString();
                        if (!string.IsNullOrWhiteSpace(mergedText))
                        {
                            segments.Add(CreateSegment(jobId, ref segmentIndex, mergedText, SourceType.Cell,
                                new { sheet = worksheet.Name, cell = cell.Address.ToString(), merged = rangeKey }));
                        }
                        continue;
                    }

                    var cellValue = cell.GetFormattedString();
                    if (string.IsNullOrWhiteSpace(cellValue))
                        continue;

                    segments.Add(CreateSegment(jobId, ref segmentIndex, cellValue, SourceType.Cell,
                        new { sheet = worksheet.Name, cell = cell.Address.ToString() }));
                }
            }

            // Cell comments
            foreach (var cell in usedRange.Cells())
            {
                if (!cell.HasComment)
                    continue;

                var commentText = cell.GetComment().Text;
                if (!string.IsNullOrWhiteSpace(commentText))
                {
                    segments.Add(CreateSegment(jobId, ref segmentIndex, commentText, SourceType.Comment,
                        new { sheet = worksheet.Name, cell = cell.Address.ToString(), type = "comment" }));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<TextSegment>>(segments);
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
