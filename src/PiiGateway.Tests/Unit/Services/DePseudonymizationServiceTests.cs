using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Services;

namespace PiiGateway.Tests.Unit.Services;

public class DePseudonymizationServiceTests
{
    private readonly Mock<IJobRepository> _jobRepoMock = new();
    private readonly Mock<IPiiEntityRepository> _piiEntityRepoMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly DePseudonymizationService _service;

    public DePseudonymizationServiceTests()
    {
        var auditLogService = new AuditLogService(_auditLogRepoMock.Object);
        _service = new DePseudonymizationService(
            _jobRepoMock.Object,
            _piiEntityRepoMock.Object,
            auditLogService,
            Mock.Of<ILogger<DePseudonymizationService>>()
        );
    }

    [Fact]
    public async Task DePseudonymizeAsync_BasicReplacement_Works()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.DePseudonymizeAsync(jobId, "Felix Bauer hat den Vertrag unterschrieben.", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Be("Max Mustermann hat den Vertrag unterschrieben.");
        result.ReplacementsMade.Should().ContainSingle(r => r.Pseudonym == "Felix Bauer" && r.Original == "Max Mustermann");
    }

    [Fact]
    public async Task DePseudonymizeAsync_SurnameVariant_Replaced()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.DePseudonymizeAsync(jobId, "Herr Bauer hat den Vertrag unterschrieben.", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Be("Herr Mustermann hat den Vertrag unterschrieben.");
    }

    [Fact]
    public async Task DePseudonymizeAsync_HonorificVariants_AllHandled()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.DePseudonymizeAsync(jobId, "Frau Bauer und Dr Bauer waren anwesend.", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Be("Frau Mustermann und Dr Mustermann waren anwesend.");
    }

    [Fact]
    public async Task DePseudonymizeAsync_LongestFirstReplacement_AvoidesPartialMatch()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);

        var entity1 = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");
        var entity2 = CreateEntity(jobId, "Mustermann GmbH", "Bauer AG", "ORGANIZATION");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity1, entity2 });

        var result = await _service.DePseudonymizeAsync(jobId, "Felix Bauer arbeitet bei Bauer AG.", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Contain("Max Mustermann");
        result.DepseudonymizedText.Should().Contain("Mustermann GmbH");
    }

    [Fact]
    public async Task DePseudonymizeAsync_MultipleOccurrences_CountedCorrectly()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.DePseudonymizeAsync(jobId, "Felix Bauer traf Felix Bauer.", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Be("Max Mustermann traf Max Mustermann.");
        result.ReplacementsMade.Should().ContainSingle(r => r.Count == 2);
    }

    [Fact]
    public async Task DePseudonymizeAsync_NotPseudonymized_Throws()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.InReview);
        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);

        var act = () => _service.DePseudonymizeAsync(jobId, "text", Guid.NewGuid(), null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pseudonymized or DePseudonymized*");
    }

    [Fact]
    public async Task DePseudonymizeAsync_DePseudonymizedStatus_AllowsMultiplePasses()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.DePseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var act = () => _service.DePseudonymizeAsync(jobId, "Felix Bauer", Guid.NewGuid(), null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DePseudonymizeAsync_PseudonymizedStatus_TransitionsToDePseudonymized()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "Max Mustermann", "Felix Bauer", "PERSON");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        await _service.DePseudonymizeAsync(jobId, "Felix Bauer", Guid.NewGuid(), null);

        job.Status.Should().Be(JobStatus.DePseudonymized);
        _jobRepoMock.Verify(r => r.UpdateAsync(job), Times.Once);
    }

    [Fact]
    public async Task DePseudonymizeAsync_NonPersonEntities_NoVariantsGenerated()
    {
        var jobId = Guid.NewGuid();
        var job = CreateJob(jobId, JobStatus.Pseudonymized);
        var entity = CreateEntity(jobId, "test@example.com", "fake@example.com", "EMAIL");

        _jobRepoMock.Setup(r => r.GetByIdAsync(jobId)).ReturnsAsync(job);
        _piiEntityRepoMock.Setup(r => r.GetByJobIdAsync(jobId)).ReturnsAsync(new[] { entity });

        var result = await _service.DePseudonymizeAsync(jobId, "Contact: fake@example.com", Guid.NewGuid(), null);

        result.DepseudonymizedText.Should().Be("Contact: test@example.com");
    }

    private static Job CreateJob(Guid id, JobStatus status) => new()
    {
        Id = id,
        OrganizationId = Guid.NewGuid(),
        CreatedById = Guid.NewGuid(),
        Status = status,
        FileName = "test.pdf",
        FileType = ".pdf",
        CreatedAt = DateTime.UtcNow
    };

    private static PiiEntity CreateEntity(Guid jobId, string original, string replacement, string entityType) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        SegmentId = Guid.NewGuid(),
        OriginalTextEnc = original,
        ReplacementText = replacement,
        EntityType = entityType,
        StartOffset = 0,
        EndOffset = original.Length,
        Confidence = 0.95,
        DetectionSources = new[] { "ner" },
        ReviewStatus = ReviewStatus.Confirmed,
        CreatedAt = DateTime.UtcNow
    };
}
