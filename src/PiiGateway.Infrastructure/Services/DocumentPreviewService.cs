using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.DTOs.Jobs;
using PiiGateway.Core.DTOs.Review;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class DocumentPreviewService : IDocumentPreviewService
{
    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly ITextSegmentRepository _textSegmentRepository;

    public DocumentPreviewService(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        ITextSegmentRepository textSegmentRepository)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _textSegmentRepository = textSegmentRepository;
    }

    public async Task<DocumentPreviewResponse> GetDocumentPreviewAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        // For created/processing, document data is not yet available
        if (job.Status == JobStatus.Created || job.Status == JobStatus.Processing)
        {
            return new DocumentPreviewResponse
            {
                JobId = jobId,
                Status = job.Status.ToString().ToLower(),
                HasDocumentData = false
            };
        }

        var segments = await _textSegmentRepository.GetByJobIdAsync(jobId);
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

        var response = new DocumentPreviewResponse
        {
            JobId = jobId,
            Status = job.Status.ToString().ToLower(),
            HasDocumentData = true,
            Segments = segmentDtos,
            Entities = entityDtos
        };

        // For pseudonymized and later states, include pseudonymized text and replacements
        if (job.Status >= JobStatus.Pseudonymized && job.PseudonymizedText != null)
        {
            response.PseudonymizedText = job.PseudonymizedText;

            var activeEntities = entities
                .Where(e => (e.ReviewStatus == ReviewStatus.Confirmed || e.ReviewStatus == ReviewStatus.AddedManual)
                            && e.ReplacementText != null)
                .ToList();

            response.Replacements = activeEntities
                .GroupBy(e => new { Original = e.OriginalTextEnc ?? "", e.ReplacementText, e.EntityType })
                .Select(g => new ReplacementEntry
                {
                    Original = g.Key.Original,
                    Replacement = g.Key.ReplacementText!,
                    EntityType = g.Key.EntityType,
                    OccurrenceCount = g.Count()
                })
                .ToList();
        }

        return response;
    }

    private static string GetConfidenceTier(double confidence)
    {
        if (confidence >= 0.90) return "HIGH";
        if (confidence >= 0.70) return "MEDIUM";
        return "LOW";
    }
}
