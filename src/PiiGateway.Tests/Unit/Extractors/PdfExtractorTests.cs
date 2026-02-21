using FluentAssertions;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Infrastructure.Services.Extractors;
using UglyToad.PdfPig.Writer;

namespace PiiGateway.Tests.Unit.Extractors;

public class PdfExtractorTests
{
    private readonly PdfExtractor _extractor = new();

    [Fact]
    public void CanHandle_Pdf_ReturnsTrue()
    {
        _extractor.CanHandle(".pdf").Should().BeTrue();
        _extractor.CanHandle("pdf").Should().BeTrue();
        _extractor.CanHandle(".PDF").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonPdf_ReturnsFalse()
    {
        _extractor.CanHandle(".docx").Should().BeFalse();
        _extractor.CanHandle(".xlsx").Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_SimplePdf_ReturnsParagraphs()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreatePdfWithText("Hello World. This is a test paragraph.");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().NotBeEmpty();
        segments.Should().AllSatisfy(s =>
        {
            s.JobId.Should().Be(jobId);
            s.SourceType.Should().Be(SourceType.Paragraph);
            s.SourceLocation.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ExtractAsync_EmptyPdf_ReturnsEmptyList()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateEmptyPdf();

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_MultiPagePdf_IncrementsSegmentIndex()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateMultiPagePdf();

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCountGreaterThanOrEqualTo(2);
        // Segment indices should be sequential
        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].SegmentIndex.Should().Be(i);
        }
    }

    private static MemoryStream CreatePdfWithText(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateEmptyPdf()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842); // empty page
        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateMultiPagePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        var page1 = builder.AddPage(595, 842);
        page1.AddText("Page 1 content", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);

        var page2 = builder.AddPage(595, 842);
        page2.AddText("Page 2 content", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);

        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }
}
