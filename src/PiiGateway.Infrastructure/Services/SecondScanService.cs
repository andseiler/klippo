using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiiGateway.Core.Domain;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class SecondScanService : ISecondScanService
{
    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly IPiiDetectionClient _piiDetectionClient;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<SecondScanService> _logger;
    private readonly PiiServiceOptions _piiServiceOptions;

    public SecondScanService(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        IPiiDetectionClient piiDetectionClient,
        AuditLogService auditLogService,
        ILogger<SecondScanService> logger,
        IOptions<PiiServiceOptions> piiServiceOptions)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _piiDetectionClient = piiDetectionClient;
        _auditLogService = auditLogService;
        _logger = logger;
        _piiServiceOptions = piiServiceOptions.Value;
    }

    public async Task<SecondScanResultDto> RunSecondScanAsync(Guid jobId, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (job.Status != JobStatus.Pseudonymized)
            throw new InvalidOperationException("Job must be in Pseudonymized status to run second scan.");

        var (realDetections, entities) = await ScanPseudonymizedTextCoreAsync(job);

        if (realDetections.Count > 0)
        {
            // Scan failed — new PII found in pseudonymized text
            JobStatusTransitions.Validate(job.Status, JobStatus.ScanFailed);
            job.Status = JobStatus.ScanFailed;
            await _jobRepository.UpdateAsync(job);

            // Add new entities for review
            var newEntities = realDetections.Select(d => new PiiEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                SegmentId = entities.FirstOrDefault()?.SegmentId ?? Guid.Empty,
                OriginalTextEnc = d.OriginalText,
                EntityType = d.EntityType,
                StartOffset = d.StartOffset,
                EndOffset = d.EndOffset,
                Confidence = d.Confidence,
                DetectionSources = new[] { d.DetectionSource },
                ReviewStatus = ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _piiEntityRepository.AddRangeAsync(newEntities);

            // Transition to InReview for re-review
            JobStatusTransitions.Validate(job.Status, JobStatus.InReview);
            job.Status = JobStatus.InReview;
            await _jobRepository.UpdateAsync(job);

            await _auditLogService.LogAsync(jobId, ActionType.SecondScanFailed,
                actorId: userId,
                metadata: $"{{\"detection_count\":{realDetections.Count}}}",
                ipAddress: ipAddress);

            _logger.LogWarning("Second scan failed for job {JobId}: {Count} new detections", jobId, realDetections.Count);

            return new SecondScanResultDto
            {
                Passed = false,
                Detections = MapDetections(realDetections)
            };
        }

        // Scan passed
        JobStatusTransitions.Validate(job.Status, JobStatus.ScanPassed);
        job.Status = JobStatus.ScanPassed;
        job.SecondScanPassed = true;
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(jobId, ActionType.SecondScanPassed,
            actorId: userId,
            ipAddress: ipAddress);

        _logger.LogInformation("Second scan passed for job {JobId}", jobId);

        return new SecondScanResultDto
        {
            Passed = true,
            Detections = new()
        };
    }

    public async Task<SecondScanResultDto> ScanPseudonymizedTextOnlyAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (string.IsNullOrEmpty(job.PseudonymizedText))
            throw new InvalidOperationException("No pseudonymized text available for scan.");

        var (realDetections, _) = await ScanPseudonymizedTextCoreAsync(job);

        return new SecondScanResultDto
        {
            Passed = realDetections.Count == 0,
            Detections = MapDetections(realDetections)
        };
    }

    private async Task<(List<DetectionResult> realDetections, IReadOnlyList<PiiEntity> entities)> ScanPseudonymizedTextCoreAsync(Job job)
    {
        if (string.IsNullOrEmpty(job.PseudonymizedText))
            throw new InvalidOperationException("No pseudonymized text available for second scan.");

        // Build allowlist of known synthetic replacements
        var entities = await _piiEntityRepository.GetByJobIdAsync(job.Id);
        var allowlist = entities
            .Where(e => e.ReplacementText != null)
            .Select(e => e.ReplacementText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Create a single segment from pseudonymized text
        var segmentId = Guid.NewGuid();
        var detectRequest = new DetectRequest
        {
            JobId = job.Id,
            Segments = new List<DetectionSegment>
            {
                new()
                {
                    SegmentId = segmentId,
                    SegmentIndex = 0,
                    TextContent = job.PseudonymizedText,
                    SourceType = "paragraph"
                }
            },
            Layers = _piiServiceOptions.Layers
        };

        var detectResponse = await _piiDetectionClient.DetectAsync(detectRequest);

        // Filter out allowlisted detections
        var realDetections = detectResponse.Detections
            .Where(d => !allowlist.Contains(d.OriginalText ?? ""))
            .Where(d => d.Confidence >= 0.70)
            .ToList();

        return (realDetections, entities);
    }

    private static List<SecondScanDetection> MapDetections(List<DetectionResult> detections)
    {
        return detections.Select(d => new SecondScanDetection
        {
            EntityType = d.EntityType,
            Text = d.OriginalText ?? "",
            Confidence = d.Confidence,
            StartOffset = d.StartOffset,
            EndOffset = d.EndOffset
        }).ToList();
    }
}
