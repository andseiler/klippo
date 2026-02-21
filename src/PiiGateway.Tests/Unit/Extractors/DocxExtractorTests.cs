using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Infrastructure.Services.Extractors;

namespace PiiGateway.Tests.Unit.Extractors;

public class DocxExtractorTests
{
    private readonly DocxExtractor _extractor = new();

    [Fact]
    public void CanHandle_Docx_ReturnsTrue()
    {
        _extractor.CanHandle(".docx").Should().BeTrue();
        _extractor.CanHandle("docx").Should().BeTrue();
    }

    [Fact]
    public void CanHandle_NonDocx_ReturnsFalse()
    {
        _extractor.CanHandle(".pdf").Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_BodyParagraphs_ExtractsText()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithParagraphs("Hello World", "Second paragraph");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCountGreaterThanOrEqualTo(2);
        segments.Should().Contain(s => s.TextContent == "Hello World");
        segments.Should().Contain(s => s.TextContent == "Second paragraph");
        segments.Should().AllSatisfy(s => s.JobId.Should().Be(jobId));
    }

    [Fact]
    public async Task ExtractAsync_Table_ExtractsCells()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithTable(new[,] { { "A1", "B1" }, { "A2", "B2" } });

        var segments = await _extractor.ExtractAsync(stream, jobId);

        var cellSegments = segments.Where(s => s.SourceType == SourceType.Cell).ToList();
        cellSegments.Should().HaveCount(4);
        cellSegments.Should().Contain(s => s.TextContent == "A1");
        cellSegments.Should().Contain(s => s.TextContent == "B2");
    }

    [Fact]
    public async Task ExtractAsync_HeadersAndFooters_ExtractsText()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithHeaderFooter("My Header", "My Footer");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().Contain(s => s.SourceType == SourceType.Header && s.TextContent == "My Header");
        segments.Should().Contain(s => s.SourceType == SourceType.Footer && s.TextContent == "My Footer");
    }

    [Fact]
    public async Task ExtractAsync_Comments_ExtractsWithAuthor()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithComment("Main text", "This is a comment", "TestAuthor");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        var commentSegments = segments.Where(s => s.SourceType == SourceType.Comment).ToList();
        commentSegments.Should().NotBeEmpty();
        commentSegments.Should().Contain(s => s.TextContent == "This is a comment");
        commentSegments.First().SourceLocation.Should().Contain("TestAuthor");
    }

    [Fact]
    public async Task ExtractAsync_Footnotes_ExtractsText()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithFootnote("Body text", "This is a footnote");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        var footnoteSegments = segments.Where(s => s.SourceType == SourceType.Footnote).ToList();
        footnoteSegments.Should().NotBeEmpty();
        footnoteSegments.Should().Contain(s => s.TextContent == "This is a footnote");
    }

    [Fact]
    public async Task ExtractAsync_SegmentIndicesAreSequential()
    {
        var jobId = Guid.NewGuid();
        using var stream = CreateDocxWithParagraphs("First", "Second", "Third");

        var segments = await _extractor.ExtractAsync(stream, jobId);

        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].SegmentIndex.Should().Be(i);
        }
    }

    private static MemoryStream CreateDocxWithParagraphs(params string[] texts)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                texts.Select(t => new Paragraph(new Run(new Text(t)))).ToArray()
            ));
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateDocxWithTable(string[,] cells)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            var table = new Table();
            for (var r = 0; r < cells.GetLength(0); r++)
            {
                var row = new TableRow();
                for (var c = 0; c < cells.GetLength(1); c++)
                {
                    var cell = new TableCell(new Paragraph(new Run(new Text(cells[r, c]))));
                    row.Append(cell);
                }
                table.Append(row);
            }
            body.Append(table);

            mainPart.Document = new Document(body);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateDocxWithHeaderFooter(string headerText, string footerText)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();

            // Header
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new Header(new Paragraph(new Run(new Text(headerText))));
            var headerRef = new HeaderReference { Id = mainPart.GetIdOfPart(headerPart), Type = HeaderFooterValues.Default };

            // Footer
            var footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(new Paragraph(new Run(new Text(footerText))));
            var footerRef = new FooterReference { Id = mainPart.GetIdOfPart(footerPart), Type = HeaderFooterValues.Default };

            var sectionProps = new SectionProperties();
            sectionProps.Append(headerRef);
            sectionProps.Append(footerRef);

            var body = new Body(new Paragraph(new Run(new Text("Body content"))));
            body.Append(sectionProps);

            mainPart.Document = new Document(body);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateDocxWithComment(string bodyText, string commentText, string author)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();

            var commentsPart = mainPart.AddNewPart<WordprocessingCommentsPart>();
            var comment = new DocumentFormat.OpenXml.Wordprocessing.Comment
            {
                Id = new StringValue("1"),
                Author = new StringValue(author)
            };
            comment.Append(new Paragraph(new Run(new Text(commentText))));
            commentsPart.Comments = new Comments(comment);

            var body = new Body(new Paragraph(new Run(new Text(bodyText))));
            mainPart.Document = new Document(body);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateDocxWithFootnote(string bodyText, string footnoteText)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();

            var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
            var footnote = new Footnote { Id = 1 };
            footnote.Append(new Paragraph(new Run(new Text(footnoteText))));
            footnotesPart.Footnotes = new Footnotes(footnote);

            var body = new Body(new Paragraph(new Run(new Text(bodyText))));
            mainPart.Document = new Document(body);
        }
        ms.Position = 0;
        return ms;
    }
}
