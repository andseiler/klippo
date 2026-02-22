using FluentAssertions;
using Moq;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Services;

namespace PiiGateway.Tests.Unit.Services;

public class DocumentPreviewServiceTests
{
    private readonly Mock<IJobRepository> _jobRepoMock = new();
    private readonly Mock<IPiiEntityRepository> _piiEntityRepoMock = new();
    private readonly Mock<ITextSegmentRepository> _segmentRepoMock = new();
    private readonly DocumentPreviewService _service;

    public DocumentPreviewServiceTests()
    {
        _service = new DocumentPreviewService(
            _jobRepoMock.Object,
            _piiEntityRepoMock.Object,
            _segmentRepoMock.Object
        );
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_JobNotFound_Throws()
    {
        var jobId = Guid.NewGuid();
        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync((Job?)null);

        var act = () => _service.GetDocumentPreviewAsync(jobId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Processing)]
    public async Task GetDocumentPreviewAsync_EarlyStatus_ReturnsNoDocumentData(JobStatus status)
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, status);
        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);

        var result = await _service.GetDocumentPreviewAsync(jobId);

        result.HasDocumentData.Should().BeFalse();
        result.Segments.Should().BeEmpty();
        result.Entities.Should().BeEmpty();
        result.PseudonymizedText.Should().BeNull();
        result.Replacements.Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_ReadyReview_DoesNotTransitionStatus()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.ReadyReview);
        var segment = CreateSegment(jobId);
        var entity = CreateEntity(jobId, segment.Id, ReviewStatus.Pending, 0.95);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.GetDocumentPreviewAsync(jobId);

        // Status must NOT change — this is the key difference from ReviewService
        job.Status.Should().Be(JobStatus.ReadyReview);
        _jobRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Job>()), Times.Never);

        result.HasDocumentData.Should().BeTrue();
        result.Segments.Should().HaveCount(1);
        result.Entities.Should().HaveCount(1);
        result.PseudonymizedText.Should().BeNull();
        result.Replacements.Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_InReview_ReturnsSegmentsAndEntities()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.InReview);
        var segment = CreateSegment(jobId);
        var entity = CreateEntity(jobId, segment.Id, ReviewStatus.Confirmed, 0.92);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.GetDocumentPreviewAsync(jobId);

        result.HasDocumentData.Should().BeTrue();
        result.Segments.Should().HaveCount(1);
        result.Entities.Should().HaveCount(1);
        result.Entities[0].ConfidenceTier.Should().Be("HIGH");
        result.PseudonymizedText.Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_Pseudonymized_IncludesPseudonymizedTextAndReplacements()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        job.PseudonymizedText = "Pseudonymized content here";

        var segment = CreateSegment(jobId);
        var entity = CreateEntity(jobId, segment.Id, ReviewStatus.Confirmed, 0.95);
        entity.ReplacementText = "[PERSON-1]";

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.GetDocumentPreviewAsync(jobId);

        result.HasDocumentData.Should().BeTrue();
        result.PseudonymizedText.Should().Be("Pseudonymized content here");
        result.Replacements.Should().NotBeNull();
        result.Replacements.Should().HaveCount(1);
        result.Replacements![0].Replacement.Should().Be("[PERSON-1]");
        result.Replacements[0].Original.Should().Be("Max Mustermann");
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_Pseudonymized_IncludesPseudonymizedData()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        job.PseudonymizedText = "Pseudonymized text";

        var segment = CreateSegment(jobId);
        var confirmedEntity = CreateEntity(jobId, segment.Id, ReviewStatus.Confirmed, 0.95);
        confirmedEntity.ReplacementText = "[PERSON-1]";
        var rejectedEntity = CreateEntity(jobId, segment.Id, ReviewStatus.Rejected, 0.60);
        rejectedEntity.ReplacementText = null;

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { confirmedEntity, rejectedEntity });

        var result = await _service.GetDocumentPreviewAsync(jobId);

        result.PseudonymizedText.Should().Be("Pseudonymized text");
        // Only confirmed entity with replacement text should appear in replacements
        result.Replacements.Should().HaveCount(1);
        // Both entities should appear in the entities list
        result.Entities.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDocumentPreviewAsync_ConfidenceTiers_MappedCorrectly()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.InReview);
        var segment = CreateSegment(jobId);

        var entities = new[]
        {
            CreateEntity(jobId, segment.Id, ReviewStatus.Pending, 0.95),
            CreateEntity(jobId, segment.Id, ReviewStatus.Pending, 0.80),
            CreateEntity(jobId, segment.Id, ReviewStatus.Pending, 0.50)
        };

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(entities);

        var result = await _service.GetDocumentPreviewAsync(jobId);

        result.Entities[0].ConfidenceTier.Should().Be("HIGH");
        result.Entities[1].ConfidenceTier.Should().Be("MEDIUM");
        result.Entities[2].ConfidenceTier.Should().Be("LOW");
    }

    private static Job CreateJob(Guid id, JobStatus status) => new()
    {
        Id = id,
        CreatedById = Guid.NewGuid(),
        Status = status,
        FileName = "test.pdf",
        FileType = ".pdf",
        CreatedAt = DateTime.UtcNow
    };

    private static TextSegment CreateSegment(Guid jobId) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        SegmentIndex = 0,
        TextContent = "Max Mustermann wohnt in Berlin.",
        SourceType = SourceType.Paragraph,
        CreatedAt = DateTime.UtcNow
    };

    private static PiiEntity CreateEntity(Guid jobId, Guid segmentId, ReviewStatus status, double confidence) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        SegmentId = segmentId,
        OriginalTextEnc = "Max Mustermann",
        EntityType = "PERSON",
        StartOffset = 0,
        EndOffset = 14,
        Confidence = confidence,
        DetectionSources = new[] { "ner" },
        ReviewStatus = status,
        CreatedAt = DateTime.UtcNow
    };
}
