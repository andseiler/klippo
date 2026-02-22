using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;
using PiiGateway.Infrastructure.Services;

namespace PiiGateway.Tests.Unit.Services;

public class DocumentProcessorTests
{
    private readonly Mock<IJobRepository> _jobRepoMock = new();
    private readonly Mock<ITextSegmentRepository> _segmentRepoMock = new();
    private readonly Mock<IPiiEntityRepository> _piiEntityRepoMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IDocumentExtractor> _extractorMock = new();
    private readonly Mock<IPiiDetectionClient> _piiClientMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly AuditLogService _auditLogService;
    private readonly DocumentProcessor _processor;

    public DocumentProcessorTests()
    {
        _auditLogService = new AuditLogService(_auditLogRepoMock.Object);
        _extractorMock.Setup(e => e.CanHandle(It.IsAny<string>())).Returns(true);

        _processor = new DocumentProcessor(
            _jobRepoMock.Object,
            _segmentRepoMock.Object,
            _piiEntityRepoMock.Object,
            _fileStorageMock.Object,
            new[] { _extractorMock.Object },
            _piiClientMock.Object,
            _auditLogService,
            Mock.Of<IPseudonymizationService>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<DocumentProcessor>>(),
            Options.Create(new PiiServiceOptions()),
            new JobCancellationRegistry()
        );
    }

    [Fact]
    public async Task ProcessAsync_HappyPath_TransitionsToReadyReview()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Created);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _fileStorageMock.Setup(f => f.OpenReadAsync(jobId, It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());
        _extractorMock.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), jobId))
            .ReturnsAsync(new List<TextSegment>
            {
                new() { Id = Guid.NewGuid(), JobId = jobId, SegmentIndex = 0, TextContent = "Test" }
            });
        _piiClientMock.Setup(p => p.DetectAsync(It.IsAny<DetectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetectResponse());

        await _processor.ProcessAsync(jobId);

        job.Status.Should().Be(JobStatus.ReadyReview);
        _segmentRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<TextSegment>>()), Times.Once);
        _jobRepoMock.Verify(r => r.UpdateAsync(job), Times.Exactly(2)); // Processing + ReadyReview
    }

    [Fact]
    public async Task ProcessAsync_PiiServiceFails_TransitionsToFailed()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Created);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _fileStorageMock.Setup(f => f.OpenReadAsync(jobId, It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());
        _extractorMock.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), jobId))
            .ReturnsAsync(new List<TextSegment>
            {
                new() { Id = Guid.NewGuid(), JobId = jobId, SegmentIndex = 0, TextContent = "Test" }
            });
        _piiClientMock.Setup(p => p.DetectAsync(It.IsAny<DetectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        await _processor.ProcessAsync(jobId);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task ProcessAsync_ExtractionFails_SetsErrorAndTransitionsToFailed()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Created);

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _fileStorageMock.Setup(f => f.OpenReadAsync(jobId, It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream());
        _extractorMock.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), jobId))
            .ThrowsAsync(new InvalidOperationException("Scanned PDF not supported"));

        await _processor.ProcessAsync(jobId);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Contain("Scanned PDF not supported");
    }

    [Fact]
    public async Task ProcessAsync_JobNotFound_ReturnsWithoutError()
    {
        var jobId = Guid.NewGuid();
        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync((Job?)null);

        var act = () => _processor.ProcessAsync(jobId);
        await act.Should().NotThrowAsync();
    }

    private static Job CreateJob(Guid id, JobStatus status)
    {
        return new Job
        {
            Id = id,
            CreatedById = Guid.NewGuid(),
            Status = status,
            FileName = "test.pdf",
            FileType = ".pdf",

            CreatedAt = DateTime.UtcNow
        };
    }
}
