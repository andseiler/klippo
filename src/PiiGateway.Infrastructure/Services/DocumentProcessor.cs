using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiiGateway.Core.Domain;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Data;
using PiiGateway.Infrastructure.Options;

namespace PiiGateway.Infrastructure.Services;

public class DocumentProcessor : IDocumentProcessor
{
    private readonly IJobRepository _jobRepository;
    private readonly ITextSegmentRepository _textSegmentRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEnumerable<IDocumentExtractor> _extractors;
    private readonly IPiiDetectionClient _piiDetectionClient;
    private readonly AuditLogService _auditLogService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPseudonymizationService _pseudonymizationService;
    private readonly ILogger<DocumentProcessor> _logger;
    private readonly PiiServiceOptions _piiServiceOptions;
    private readonly JobCancellationRegistry _cancellationRegistry;

    public DocumentProcessor(
        IJobRepository jobRepository,
        ITextSegmentRepository textSegmentRepository,
        IPiiEntityRepository piiEntityRepository,
        IFileStorageService fileStorageService,
        IEnumerable<IDocumentExtractor> extractors,
        IPiiDetectionClient piiDetectionClient,
        AuditLogService auditLogService,
        IPseudonymizationService pseudonymizationService,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessor> logger,
        IOptions<PiiServiceOptions> piiServiceOptions,
        JobCancellationRegistry cancellationRegistry)
    {
        _jobRepository = jobRepository;
        _textSegmentRepository = textSegmentRepository;
        _piiEntityRepository = piiEntityRepository;
        _fileStorageService = fileStorageService;
        _extractors = extractors;
        _piiDetectionClient = piiDetectionClient;
        _auditLogService = auditLogService;
        _pseudonymizationService = pseudonymizationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _piiServiceOptions = piiServiceOptions.Value;
        _cancellationRegistry = cancellationRegistry;
    }

    public async Task ProcessAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found, skipping processing", jobId);
            return;
        }

        var ct = _cancellationRegistry.Register(jobId);

        try
        {
            // 1. Transition to Processing
            if (job.Status == JobStatus.Created)
            {
                JobStatusTransitions.Validate(job.Status, JobStatus.Processing);
                job.Status = JobStatus.Processing;
                job.ProcessingStartedAt = DateTime.UtcNow;
                job.ErrorMessage = null;
                await _jobRepository.UpdateAsync(job);

                await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
                    metadata: "{\"from\":\"created\",\"to\":\"processing\"}");
            }

            // 2. Find the correct extractor
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(job.FileType));
            if (extractor == null)
            {
                throw new InvalidOperationException($"No extractor found for file type '{job.FileType}'");
            }

            // 3. Extract text segments
            await using var fileStream = await _fileStorageService.OpenReadAsync(jobId, job.FileType);
            var segments = await extractor.ExtractAsync(fileStream, jobId);

            _logger.LogInformation("Extracted {Count} segments from job {JobId}", segments.Count, jobId);

            // 4. Batch-save segments to DB
            if (segments.Count > 0)
            {
                await _textSegmentRepository.AddRangeAsync(segments);
            }

            // 5. Call PII detection and persist results
            var detectRequest = new DetectRequest
            {
                JobId = jobId,
                Segments = segments.Select(s => new DetectionSegment
                {
                    SegmentId = s.Id,
                    SegmentIndex = s.SegmentIndex,
                    TextContent = s.TextContent,
                    SourceType = s.SourceType.ToString().ToLower()
                }).ToList(),
                Layers = _piiServiceOptions.Layers
            };

            ct.ThrowIfCancellationRequested();
            var detectResponse = await _piiDetectionClient.DetectAsync(detectRequest, ct);
            _logger.LogInformation("PII detection returned {Count} detections for job {JobId}",
                detectResponse.Detections.Count, jobId);

            // Persist detection results
            if (detectResponse.Detections.Count > 0)
            {
                var piiEntities = detectResponse.Detections.Select(d => new PiiEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    SegmentId = d.SegmentId,
                    OriginalTextEnc = d.OriginalText,
                    ReplacementText = null,
                    EntityType = d.EntityType,
                    StartOffset = d.StartOffset,
                    EndOffset = d.EndOffset,
                    Confidence = d.Confidence,
                    DetectionSources = new[] { d.DetectionSource },
                    ReviewStatus = ReviewStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _piiEntityRepository.AddRangeAsync(piiEntities);

                var auditMeta = new
                {
                    count = piiEntities.Count,
                    layers = detectResponse.LayersUsed,
                    layerStats = detectResponse.LayerStats.Select(ls => new
                    {
                        layer = ls.Layer,
                        status = ls.Status,
                        detectionCount = ls.DetectionCount,
                        skipReason = ls.SkipReason
                    }),
                    processingTimeMs = detectResponse.ProcessingTimeMs
                };
                await _auditLogService.LogAsync(jobId, ActionType.PiiDetected,
                    metadata: JsonSerializer.Serialize(auditMeta));

                _logger.LogInformation("Persisted {Count} PII entities for job {JobId}",
                    piiEntities.Count, jobId);
            }
            else
            {
                await _auditLogService.LogAsync(jobId, ActionType.PiiDetected,
                    metadata: JsonSerializer.Serialize(new
                    {
                        count = 0,
                        layers = detectResponse.LayersUsed,
                        layerStats = detectResponse.LayerStats.Select(ls => new
                        {
                            layer = ls.Layer,
                            status = ls.Status,
                            detectionCount = ls.DetectionCount,
                            skipReason = ls.SkipReason
                        }),
                        processingTimeMs = detectResponse.ProcessingTimeMs
                    }));
            }

            // 6. Generate pseudonymization tokens
            await _pseudonymizationService.GeneratePreviewTokensAsync(jobId);

            // 7. Transition to ReadyReview
            JobStatusTransitions.Validate(job.Status, JobStatus.ReadyReview);
            job.Status = JobStatus.ReadyReview;
            await _jobRepository.UpdateAsync(job);

            await _auditLogService.LogAsync(jobId, ActionType.JobStatusChanged,
                metadata: "{\"from\":\"processing\",\"to\":\"readyreview\"}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job {JobId} was cancelled", jobId);
            try
            {
                job.Status = JobStatus.Cancelled;
                job.ErrorMessage = "Cancelled by user";
                await _jobRepository.UpdateAsync(job);
            }
            catch
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var freshDb = scope.ServiceProvider.GetRequiredService<PiiGatewayDbContext>();
                var freshJob = await freshDb.Jobs.FindAsync(jobId);
                if (freshJob != null)
                {
                    freshJob.Status = JobStatus.Cancelled;
                    freshJob.ErrorMessage = "Cancelled by user";
                    await freshDb.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);

            try
            {
                job.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                job.Status = JobStatus.Failed;
                await _jobRepository.UpdateAsync(job);
            }
            catch (Exception resetEx)
            {
                _logger.LogError(resetEx, "Failed to reset job {JobId} via tracked entity, using fresh context", jobId);

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var freshDb = scope.ServiceProvider.GetRequiredService<PiiGatewayDbContext>();
                    var freshJob = await freshDb.Jobs.FindAsync(jobId);
                    if (freshJob != null)
                    {
                        freshJob.Status = JobStatus.Failed;
                        freshJob.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                        await freshDb.SaveChangesAsync();
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogCritical(fallbackEx,
                        "CRITICAL: Could not reset job {JobId} to Failed. Job is stuck in Processing.", jobId);
                }
            }
        }
        finally
        {
            _cancellationRegistry.Remove(jobId);
        }
    }
}
