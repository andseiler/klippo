using ClosedXML.Excel;
using FluentAssertions;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Infrastructure.Services.Extractors;

namespace PiiGateway.Tests.Unit.Extractors;

public class XlsxExtractorTests
{
    private readonly XlsxExtractor _extractor = new();

    [Fact]
    public void CanHandle_Xlsx_ReturnsTrue()
    {
        _extractor.CanHandle(".xlsx").Should().BeTrue();
        _extractor.CanHandle("xlsx").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonXlsx_ReturnsFalse()
    {
        _extractor.CanHandle(".pdf").Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_CellValues_ExtractsCells()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithCells(new Dictionary<string, string>
        {
            ["A1"] = "Hello",
            ["B1"] = "World",
            ["A2"] = "123"
        });

        var segments = await _extractor.ExtractAsync(stream, jobId);

        var cellSegments = segments.Where(s => s.SourceType == SourceType.Cell).ToList();
        cellSegments.Should().HaveCountGreaterThanOrEqualTo(3);
        cellSegments.Should().Contain(s => s.TextContent == "Hello");
        cellSegments.Should().Contain(s => s.TextContent == "World");
    }

    [Fact]
    public async Task ExtractAsync_SheetName_ExtractedAsSheetNameType()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithSheetName("PII Data");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().Contain(s => s.SourceType == SourceType.SheetName && s.TextContent == "PII Data");
    }

    [Fact]
    public async Task ExtractAsync_MergedCells_DeduplicatesCorrectly()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithMergedCells();

        var segments = await _extractor.ExtractAsync(stream, jobId);

        // "Merged Value" should appear only once, not for each cell in the range
        var mergedSegments = segments.Where(s => s.TextContent == "Merged Value").ToList();
        mergedSegments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtractAsync_Comments_ExtractsComments()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithComment("A1", "Cell Value", "This is a comment");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        var commentSegments = segments.Where(s => s.SourceType == SourceType.Comment).ToList();
        commentSegments.Should().NotBeEmpty();
        commentSegments.Should().Contain(s => s.TextContent == "This is a comment");
    }

    [Fact]
    public async Task ExtractAsync_MultiSheet_ExtractsFromAllSheets()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithMultipleSheets();

        var segments = await _extractor.ExtractAsync(stream, jobId);

        // Should have sheet name segments for both sheets
        var sheetNames = segments.Where(s => s.SourceType == SourceType.SheetName).ToList();
        sheetNames.Should().HaveCount(2);

        // Should have cell data from both sheets
        segments.Should().Contain(s => s.TextContent == "Sheet1 Data");
        segments.Should().Contain(s => s.TextContent == "Sheet2 Data");
    }

    [Fact]
    public async Task ExtractAsync_SegmentIndicesAreSequential()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateXlsxWithCells(new Dictionary<string, string>
        {
            ["A1"] = "One", ["B1"] = "Two", ["A2"] = "Three"
        });

        var segments = await _extractor.ExtractAsync(stream, jobId);

        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].SegmentIndex.Should().Be(i);
        }
    }

    private static MemoryStream CreateXlsxWithCells(Dictionary<string, string> cells, string sheetName = "Sheet1")
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add(sheetName);
            foreach (var (address, value) in cells)
            {
                ws.Cell(address).Value = value;
            }
            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateXlsxWithSheetName(string name)
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add(name);
            ws.Cell("A1").Value = "Data";
            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateXlsxWithMergedCells()
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Sheet1");
            ws.Cell("A1").Value = "Merged Value";
            ws.Range("A1:B2").Merge();
            ws.Cell("C1").Value = "Normal";
            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateXlsxWithComment(string cellAddress, string cellValue, string commentText)
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Sheet1");
            var cell = ws.Cell(cellAddress);
            cell.Value = cellValue;
            cell.GetComment().AddText(commentText);
            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateXlsxWithMultipleSheets()
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws1 = workbook.Worksheets.Add("First Sheet");
            ws1.Cell("A1").Value = "Sheet1 Data";

            var ws2 = workbook.Worksheets.Add("Second Sheet");
            ws2.Cell("A1").Value = "Sheet2 Data";

            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }
}
