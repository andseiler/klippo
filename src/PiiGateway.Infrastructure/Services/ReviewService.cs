using Microsoft.Extensions.Logging;
using PiiGateway.Core.Domain;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Review;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly ITextSegmentRepository _textSegmentRepository;
    private readonly IPseudonymizationService _pseudonymizationService;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        ITextSegmentRepository textSegmentRepository,
        IPseudonymizationService pseudonymizationService,
        AuditLogService auditLogService,
        ILogger<ReviewService> logger)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _textSegmentRepository = textSegmentRepository;
        _pseudonymizationService = pseudonymizationService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ReviewDataResponse> GetReviewDataAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        // Transition ReadyReview → InReview on first access
        if (job.Status == JobStatus.ReadyReview)
        {
            JobStatusTransitions.Validate(job.Status, JobStatus.InReview);
            job.Status = JobStatus.InReview;
            job.ReviewStartedAt = DateTime.UtcNow;
            await _jobRepository.UpdateAsync(job);

            await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
                metadata: "{\"from\":\"readyreview\",\"to\":\"inreview\"}");
        }

        var segments = await _textSegmentRepository.GetByJobIdAsync(jobId);

        // Generate preview tokens so reviewers can see replacements during InReview
        await _pseudonymizationService.GeneratePreviewTokensAsync(jobId);
        var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);

        var segmentDtos = segments.Select(s => new SegmentDto
        {
            Id = s.Id,
            SegmentIndex = s.SegmentIndex,
            TextContent = s.TextContent,
            SourceType = s.SourceType.ToString().ToLower(),
            SourceLocation = s.SourceLocation
        }).ToList();

        var entityDtos = entities.Select(e => new EntityDto
        {
            Id = e.Id,
            SegmentId = e.SegmentId,
            Text = e.OriginalTextEnc ?? string.Empty,
            EntityType = e.EntityType,
            StartOffset = e.StartOffset,
            EndOffset = e.EndOffset,
            Confidence = e.Confidence,
            DetectionSources = e.DetectionSources,
            ConfidenceTier = GetConfidenceTier(e.Confidence),
            ReplacementPreview = e.ReplacementText,
            ReviewStatus = e.ReviewStatus.ToString().ToLower()
        }).ToList();

        var summary = new ReviewSummary
        {
            TotalEntities = entities.Count,
            HighConfidence = entities.Count(e => e.Confidence >= 0.90),
            MediumConfidence = entities.Count(e => e.Confidence >= 0.70 && e.Confidence < 0.90),
            LowConfidence = entities.Count(e => e.Confidence < 0.70),
            Confirmed = entities.Count(e => e.ReviewStatus == ReviewStatus.Confirmed),
            ManuallyAdded = entities.Count(e => e.ReviewStatus == ReviewStatus.AddedManual),
            Pending = entities.Count(e => e.ReviewStatus == ReviewStatus.Pending)
        };

        return new ReviewDataResponse
        {
            JobId = jobId,
            Status = job.Status.ToString().ToLower(),
            Segments = segmentDtos,
            Entities = entityDtos,
            Summary = summary
        };
    }

    public async Task UpdateEntityAsync(Guid jobId, Guid entityId, UpdateEntityRequest request, Guid userId, string? ipAddress)
    {
        var entity = await _piiEntityRepository.GetByIdAsync(entityId)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found.");

        if (entity.JobId != jobId)
            throw new InvalidOperationException("Entity does not belong to this job.");

        if (request.ReviewStatus != null)
        {
            if (!Enum.TryParse<ReviewStatus>(request.ReviewStatus, ignoreCase: true, out var status)
                || status != ReviewStatus.Confirmed)
            {
                throw new ArgumentException("ReviewStatus must be 'confirmed'.");
            }
            entity.ReviewStatus = status;
        }

        if (request.EntityType != null)
            entity.EntityType = request.EntityType;

        if (request.StartOffset.HasValue)
            entity.StartOffset = request.StartOffset.Value;

        if (request.EndOffset.HasValue)
            entity.EndOffset = request.EndOffset.Value;

        if (request.ReplacementText != null)
            entity.ReplacementText = request.ReplacementText;

        entity.ReviewedById = userId;
        entity.ReviewedAt = DateTime.UtcNow;

        await _piiEntityRepository.UpdateAsync(entity);

        var actionType = entity.ReviewStatus == ReviewStatus.Confirmed
            ? ActionType.PiiConfirmed
            : ActionType.JobStatusChanged;

        await _auditLogService.LogAsync(jobId, actionType,
            actorId: userId,
            entityType: entity.EntityType,
            rawPiiText: entity.OriginalTextEnc,
            confidence: entity.Confidence,
            ipAddress: ipAddress);

        // Auto re-pseudonymize if job is already pseudonymized
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job != null && job.Status == JobStatus.Pseudonymized)
        {
            await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);
        }
    }

    public async Task DeleteEntityAsync(Guid jobId, Guid entityId, Guid userId, string? ipAddress)
    {
        var entity = await _piiEntityRepository.GetByIdAsync(entityId)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found.");

        if (entity.JobId != jobId)
            throw new InvalidOperationException("Entity does not belong to this job.");

        await _auditLogService.LogAsync(jobId, ActionType.PiiRejected,
            actorId: userId,
            entityType: entity.EntityType,
            rawPiiText: entity.OriginalTextEnc,
            confidence: entity.Confidence,
            ipAddress: ipAddress);

        await _piiEntityRepository.DeleteAsync(entity);

        // Auto re-pseudonymize if job is already pseudonymized
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job != null && job.Status == JobStatus.Pseudonymized)
        {
            await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);
        }
    }

    public async Task DeleteAllEntitiesAsync(Guid jobId, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var entities = (await _piiEntityRepository.GetByJobIdAsync(jobId)).ToList();
        if (entities.Count == 0) return;

        await _auditLogService.LogAsync(jobId, ActionType.PiiRejected,
            actorId: userId,
            metadata: $"{{\"batch_deleted\":true,\"count\":{entities.Count}}}",
            ipAddress: ipAddress);

        await _piiEntityRepository.DeleteRangeAsync(entities);

        // Auto re-pseudonymize if job is already pseudonymized
        if (job.Status == JobStatus.Pseudonymized)
        {
            await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);
        }
    }

    public async Task<PiiEntity> AddManualEntityAsync(Guid jobId, AddEntityRequest request, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var entity = new PiiEntity
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            SegmentId = request.SegmentId,
            OriginalTextEnc = request.Text,
            EntityType = request.EntityType,
            StartOffset = request.StartOffset,
            EndOffset = request.EndOffset,
            Confidence = 1.0,
            DetectionSources = new[] { "human" },
            ReviewStatus = ReviewStatus.AddedManual,
            ReplacementText = request.ReplacementText,
            ReviewedById = userId,
            ReviewedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _piiEntityRepository.AddRangeAsync(new[] { entity });

        await _auditLogService.LogAsync(jobId, ActionType.PiiAddedManual,
            actorId: userId,
            entityType: entity.EntityType,
            rawPiiText: entity.OriginalTextEnc,
            confidence: 1.0,
            ipAddress: ipAddress);

        // Generate preview token for the new entity
        await _pseudonymizationService.GeneratePreviewTokensAsync(jobId);
        var updated = await _piiEntityRepository.GetByIdAsync(entity.Id);

        // Auto re-pseudonymize if job is already pseudonymized or exported
        if (job.Status == JobStatus.Pseudonymized )
        {
            await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);
        }

        return updated ?? entity;
    }

    public async Task CompleteReviewAsync(Guid jobId, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (job.Status != JobStatus.InReview)
            throw new InvalidOperationException("Job must be in review to complete.");

        // Auto-confirm all remaining pending entities
        var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);
        var pendingEntities = entities.Where(e => e.ReviewStatus == ReviewStatus.Pending).ToList();
        foreach (var entity in pendingEntities)
        {
            entity.ReviewStatus = ReviewStatus.Confirmed;
            entity.ReviewedById = userId;
            entity.ReviewedAt = DateTime.UtcNow;
        }
        if (pendingEntities.Count > 0)
            await _piiEntityRepository.UpdateRangeAsync(pendingEntities);

        // Trigger pseudonymization
        await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);

        // Transition InReview → Pseudonymized
        JobStatusTransitions.Validate(job.Status, JobStatus.Pseudonymized);
        job.Status = JobStatus.Pseudonymized;
        job.PseudonymizedAt = DateTime.UtcNow;
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
            actorId: userId,
            metadata: "{\"from\":\"inreview\",\"to\":\"pseudonymized\"}",
            ipAddress: ipAddress);
    }

    public async Task ReopenReviewAsync(Guid jobId, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (job.Status != JobStatus.Pseudonymized)
            throw new InvalidOperationException("Job must be in Pseudonymized status to reopen review.");

        // Transition Pseudonymized → InReview
        JobStatusTransitions.Validate(job.Status, JobStatus.InReview);
        job.Status = JobStatus.InReview;
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
            actorId: userId,
            metadata: "{\"from\":\"pseudonymized\",\"to\":\"inreview\"}",
            ipAddress: ipAddress);
    }

    public async Task UpdateSegmentTextAsync(Guid jobId, Guid segmentId, UpdateSegmentRequest request, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var segments = await _textSegmentRepository.GetByJobIdAsync(jobId);
        var segment = segments.FirstOrDefault(s => s.Id == segmentId)
            ?? throw new KeyNotFoundException($"Segment {segmentId} not found.");

        // Update segment text
        segment.TextContent = request.TextContent;
        await _textSegmentRepository.UpdateAsync(segment);

        // Update entity offsets
        if (request.EntityOffsets.Count > 0)
        {
            var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);
            var segmentEntities = entities.Where(e => e.SegmentId == segmentId).ToDictionary(e => e.Id);

            foreach (var offset in request.EntityOffsets)
            {
                if (segmentEntities.TryGetValue(offset.EntityId, out var entity))
                {
                    entity.StartOffset = offset.StartOffset;
                    entity.EndOffset = offset.EndOffset;
                    entity.OriginalTextEnc = offset.Text;
                }
            }

            await _piiEntityRepository.UpdateRangeAsync(segmentEntities.Values.ToList());
        }

        await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
            actorId: userId,
            metadata: $"{{\"action\":\"segment_text_updated\",\"segmentId\":\"{segmentId}\"}}",
            ipAddress: ipAddress);

        // Re-pseudonymize if already pseudonymized
        if (job.Status == JobStatus.Pseudonymized)
        {
            await _pseudonymizationService.PseudonymizeJobAsync(jobId, userId);
        }
    }

    public async Task UpdatePseudonymizedTextAsync(Guid jobId, UpdatePseudonymizedTextRequest request, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        job.PseudonymizedText = request.Text;
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
            actorId: userId,
            metadata: "{\"action\":\"pseudonymized_text_updated\"}",
            ipAddress: ipAddress);
    }

    private static string GetConfidenceTier(double confidence)
    {
        if (confidence >= 0.90) return "HIGH";
        if (confidence >= 0.70) return "MEDIUM";
        return "LOW";
    }
}
