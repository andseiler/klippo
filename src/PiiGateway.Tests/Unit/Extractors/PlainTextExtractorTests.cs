using System.Text;
using FluentAssertions;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Infrastructure.Services.Extractors;

namespace PiiGateway.Tests.Unit.Extractors;

public class PlainTextExtractorTests
{
    private readonly PlainTextExtractor _extractor = new();

    [Theory]
    [InlineData(".txt")]
    [InlineData(".csv")]
    [InlineData(".md")]
    [InlineData(".log")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".html")]
    [InlineData(".TXT")]
    public void CanHandle_PlaintextExtensions_ReturnsTrue(string extension)
    {
        _extractor.CanHandle(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData("pdf")]
    [InlineData(".docx")]
    [InlineData("docx")]
    [InlineData(".xlsx")]
    [InlineData("xlsx")]
    [InlineData(".PDF")]
    public void CanHandle_SpecializedExtensions_ReturnsFalse(string extension)
    {
        _extractor.CanHandle(extension).Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_ParagraphsSeparatedByDoubleNewlines_ReturnMultipleSegments()
    {
        var jobId = Guid.NewGuid();
        var text = "First paragraph here.\n\nSecond paragraph here.\n\nThird paragraph here.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCount(3);
        segments[0].TextContent.Should().Be("First paragraph here.");
        segments[1].TextContent.Should().Be("Second paragraph here.");
        segments[2].TextContent.Should().Be("Third paragraph here.");
        segments.Should().AllSatisfy(s =>
        {
            s.JobId.Should().Be(jobId);
            s.SourceType.Should().Be(SourceType.Paragraph);
            s.SourceLocation.Should().Contain("plaintext");
        });
    }

    [Fact]
    public async Task ExtractAsync_WindowsLineEndings_SplitsCorrectly()
    {
        var jobId = Guid.NewGuid();
        var text = "First paragraph.\r\n\r\nSecond paragraph.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCount(2);
        segments[0].TextContent.Should().Be("First paragraph.");
        segments[1].TextContent.Should().Be("Second paragraph.");
    }

    [Fact]
    public async Task ExtractAsync_NoDoubleNewlines_ReturnsSingleSegment()
    {
        var jobId = Guid.NewGuid();
        var text = "This is a single block of text with no double newlines.\nJust a single newline here.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCount(1);
        segments[0].TextContent.Should().Contain("This is a single block");
    }

    [Fact]
    public async Task ExtractAsync_EmptyFile_ReturnsNoSegments()
    {
        var jobId = Guid.NewGuid();
        using var stream = new MemoryStream(Array.Empty<byte>());

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WhitespaceOnly_ReturnsNoSegments()
    {
        var jobId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("   \n\n   \n  "));

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_BinaryContentWithNullBytes_ThrowsClearError()
    {
        var jobId = Guid.NewGuid();
        var binaryContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x0D };
        using var stream = new MemoryStream(binaryContent);

        var act = async () => await _extractor.ExtractAsync(stream, jobId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Binary file not supported*null bytes*");
    }

    [Fact]
    public async Task ExtractAsync_HighRatioNonPrintable_ThrowsClearError()
    {
        var jobId = Guid.NewGuid();
        // Create content with >10% non-printable control characters
        var content = new byte[100];
        for (var i = 0; i < 100; i++)
            content[i] = (byte)(i < 15 ? 0x01 : 0x41); // 15% control chars, rest is 'A'
        using var stream = new MemoryStream(content);

        var act = async () => await _extractor.ExtractAsync(stream, jobId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Binary file not supported*non-printable*");
    }

    [Fact]
    public async Task ExtractAsync_Utf8WithBom_HandledCorrectly()
    {
        var jobId = Guid.NewGuid();
        var bom = Encoding.UTF8.GetPreamble();
        var text = Encoding.UTF8.GetBytes("Hello from a BOM file.\n\nSecond paragraph.");
        var content = bom.Concat(text).ToArray();
        using var stream = new MemoryStream(content);

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCount(2);
        segments[0].TextContent.Should().Be("Hello from a BOM file.");
        segments[1].TextContent.Should().Be("Second paragraph.");
    }

    [Fact]
    public async Task ExtractAsync_SegmentIndicesAreSequential()
    {
        var jobId = Guid.NewGuid();
        var text = "One.\n\nTwo.\n\nThree.\n\nFour.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var segments = await _extractor.ExtractAsync(stream, jobId);

        segments.Should().HaveCount(4);
        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].SegmentIndex.Should().Be(i);
        }
    }
}
