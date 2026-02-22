using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Services;

namespace PiiGateway.Tests.Unit.Services;

public class PseudonymizationServiceTests
{
    private readonly Mock<IJobRepository> _jobRepoMock = new();
    private readonly Mock<IPiiEntityRepository> _piiEntityRepoMock = new();
    private readonly Mock<ITextSegmentRepository> _segmentRepoMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly PseudonymizationService _service;

    public PseudonymizationServiceTests()
    {
        var auditLogService = new AuditLogService(_auditLogRepoMock.Object);
        _service = new PseudonymizationService(
            _jobRepoMock.Object,
            _piiEntityRepoMock.Object,
            _segmentRepoMock.Object,
            auditLogService,
            Mock.Of<ILogger<PseudonymizationService>>()
        );
    }

    [Fact]
    public async Task PseudonymizeJobAsync_ConsistentMapping_SameInputSameOutput()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId);
        var segment = CreateSegment(jobId, "Max Mustermann und Max Mustermann arbeiten zusammen.");

        var entity1 = CreateEntity(jobId, segment.Id, "Max Mustermann", "PERSON", 0, 14, ReviewStatus.Confirmed);
        var entity2 = CreateEntity(jobId, segment.Id, "Max Mustermann", "PERSON", 19, 33, ReviewStatus.Confirmed);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity1, entity2 });
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });

        await _service.PseudonymizeJobAsync(jobId, Guid.NewGuid());

        entity1.ReplacementText.Should().NotBeNullOrEmpty();
        entity2.ReplacementText.Should().Be(entity1.ReplacementText);
    }

    [Fact]
    public async Task PseudonymizeJobAsync_SkipsRejectedEntities()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId);
        var segment = CreateSegment(jobId, "Text with Max and Berlin");

        var confirmed = CreateEntity(jobId, segment.Id, "Max", "PERSON", 10, 13, ReviewStatus.Confirmed);
        var rejected = CreateEntity(jobId, segment.Id, "Berlin", "LOCATION", 18, 24, ReviewStatus.Rejected);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { confirmed, rejected });
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });

        await _service.PseudonymizeJobAsync(jobId, Guid.NewGuid());

        confirmed.ReplacementText.Should().NotBeNull();
        rejected.ReplacementText.Should().BeNull();
    }

    [Fact]
    public async Task PseudonymizeJobAsync_SetsPseudonymizedTextOnJob()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId);
        var segment = CreateSegment(jobId, "Hello Max Mustermann");

        var entity = CreateEntity(jobId, segment.Id, "Max Mustermann", "PERSON", 6, 20, ReviewStatus.Confirmed);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });
        _segmentRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { segment });

        await _service.PseudonymizeJobAsync(jobId, Guid.NewGuid());

        job.PseudonymizedText.Should().NotBeNull();
        job.PseudonymizedText.Should().NotContain("Max Mustermann");
    }

    [Fact]
    public void GenerateReplacement_PersonType_ReturnsName()
    {
        var result = _service.GenerateReplacement("PERSON", "Max Mustermann", "de");
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe("Max Mustermann");
    }

    [Fact]
    public void GenerateReplacement_EmailType_ReturnsEmail()
    {
        var result = _service.GenerateReplacement("EMAIL", "test@example.com", "de");
        result.Should().Contain("@");
    }

    [Fact]
    public void GenerateReplacement_IbanType_ReturnsValidStructure()
    {
        var result = _service.GenerateReplacement("IBAN", "DE89370400440532013000", "de");
        result.Should().StartWith("DE");
        result.Length.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void GenerateReplacement_UnknownType_ReturnsPlaceholder()
    {
        var result = _service.GenerateReplacement("STEUER_ID", "12345", "de");
        result.Should().Match("[STEUER_ID_*]");
    }

    [Fact]
    public void GenerateReplacement_OrganizationType_ReturnsCompanyName()
    {
        var result = _service.GenerateReplacement("ORGANIZATION", "Muster GmbH", "de");
        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe("Muster GmbH");
    }

    private static Job CreateJob(Guid id) => new()
    {
        Id = id,
        CreatedById = Guid.NewGuid(),
        Status = JobStatus.InReview,
        FileName = "test.pdf",
        FileType = ".pdf",
        CreatedAt = DateTime.UtcNow
    };

    private static TextSegment CreateSegment(Guid jobId, string text) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        SegmentIndex = 0,
        TextContent = text,
        SourceType = SourceType.Paragraph,
        CreatedAt = DateTime.UtcNow
    };

    private static PiiEntity CreateEntity(Guid jobId, Guid segmentId, string text, string type, int start, int end, ReviewStatus status) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        SegmentId = segmentId,
        OriginalTextEnc = text,
        EntityType = type,
        StartOffset = start,
        EndOffset = end,
        Confidence = 0.95,
        DetectionSources = new[] { "ner" },
        ReviewStatus = status,
        CreatedAt = DateTime.UtcNow,
        Segment = new TextSegment { Id = segmentId, JobId = jobId, SegmentIndex = 0, TextContent = text }
    };
}
