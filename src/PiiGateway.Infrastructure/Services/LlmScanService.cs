using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public interface ILlmScanService
{
    LlmScanResponse StartScan(Guid jobId, Guid userId, string? ipAddress, string? customInstructions = null, string? language = null);
    LlmScanResponse? GetStatus(Guid jobId);
    bool CancelScan(Guid jobId);
}

public class LlmScanService : ILlmScanService
{
    private record ScanEntry(LlmScanResponse Response, CancellationTokenSource Cts);

    private readonly ConcurrentDictionary<Guid, ScanEntry> _scans = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LlmScanService> _logger;

    private const int BatchSize = 5;

    public LlmScanService(IServiceScopeFactory scopeFactory, ILogger<LlmScanService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public LlmScanResponse StartScan(Guid jobId, Guid userId, string? ipAddress, string? customInstructions = null, string? language = null)
    {
        var status = new LlmScanResponse
        {
            Status = "running",
            ProcessedSegments = 0,
            TotalSegments = 0,
        };

        var cts = new CancellationTokenSource();
        _scans[jobId] = new ScanEntry(status, cts);

        // Fire-and-forget the background work
        _ = Task.Run(async () =>
        {
            try
            {
                await RunScanAsync(jobId, userId, ipAddress, status, cts.Token, customInstructions, language);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("LLM scan cancelled for job {JobId}", jobId);
                status.Status = "cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM scan failed for job {JobId}", jobId);
                status.Status = "failed";
                status.Error = ex.Message;
            }
        });

        return status;
    }

    public LlmScanResponse? GetStatus(Guid jobId)
    {
        return _scans.TryGetValue(jobId, out var entry) ? entry.Response : null;
    }

    public bool CancelScan(Guid jobId)
    {
        if (_scans.TryGetValue(jobId, out var entry) && entry.Response.Status == "running")
        {
            entry.Cts.Cancel();
            return true;
        }
        return false;
    }

    private async Task RunScanAsync(Guid jobId, Guid userId, string? ipAddress, LlmScanResponse status, CancellationToken ct, string? customInstructions = null, string? language = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var segmentRepo = scope.ServiceProvider.GetRequiredService<ITextSegmentRepository>();
        var entityRepo = scope.ServiceProvider.GetRequiredService<IPiiEntityRepository>();
        var piiClient = scope.ServiceProvider.GetRequiredService<IPiiDetectionClient>();
        var auditLogService = scope.ServiceProvider.GetRequiredService<AuditLogService>();

        // Load job, segments, and existing entities
        var job = await jobRepo.GetByIdAsync(jobId)
            ?? throw new InvalidOperationException($"Job {jobId} not found");
        var segments = await segmentRepo.GetByJobIdAsync(jobId);
        var existingEntities = await entityRepo.GetByJobIdAsync(jobId);

        status.TotalSegments = segments.Count;

        var allDetections = new List<LlmScanDetection>();

        // Process in batches of 5
        var batches = segments
            .Select((seg, idx) => new { seg, idx })
            .GroupBy(x => x.idx / BatchSize)
            .Select(g => g.Select(x => x.seg).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();

            var detectRequest = new DetectRequest
            {
                JobId = jobId,
                Segments = batch.Select(s => new DetectionSegment
                {
                    SegmentId = s.Id,
                    SegmentIndex = s.SegmentIndex,
                    TextContent = s.TextContent,
                    SourceType = s.SourceType.ToString().ToLower()
                }).ToList(),
                Layers = new List<string> { "llm" },
                ExistingDetections = existingEntities
                    .Where(e => batch.Any(s => s.Id == e.SegmentId))
                    .Select(e => new ExistingDetection
                    {
                        EntityType = e.EntityType,
                        StartOffset = e.StartOffset,
                        EndOffset = e.EndOffset,
                    }).ToList(),
                CustomInstructions = customInstructions,
                LanguageHint = language,
            };

            try
            {
                var response = await piiClient.DetectAsync(detectRequest, ct);

                foreach (var det in response.Detections)
                {
                    allDetections.Add(new LlmScanDetection
                    {
                        SegmentId = det.SegmentId,
                        EntityType = det.EntityType,
                        StartOffset = det.StartOffset,
                        EndOffset = det.EndOffset,
                        Confidence = det.Confidence,
                        OriginalText = det.OriginalText,
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM scan batch failed for job {JobId}", jobId);
            }

            status.ProcessedSegments += batch.Count;
            status.Detections = allDetections.ToList();
        }

        // Persist new entities
        if (allDetections.Count > 0)
        {
            var newEntities = allDetections.Select(d => new PiiEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                SegmentId = d.SegmentId,
                OriginalTextEnc = d.OriginalText,
                EntityType = d.EntityType,
                StartOffset = d.StartOffset,
                EndOffset = d.EndOffset,
                Confidence = d.Confidence,
                DetectionSources = new[] { "llm" },
                ReviewStatus = ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            await entityRepo.AddRangeAsync(newEntities);

            // Map entity IDs back to detections
            for (int i = 0; i < allDetections.Count; i++)
            {
                allDetections[i].EntityId = newEntities[i].Id;
            }
            status.Detections = allDetections.ToList();
        }

        // Audit log
        await auditLogService.LogAsync(
            jobId,
            ActionType.LlmScanCompleted,
            actorId: userId,
            detectionSource: "llm",
            metadata: JsonSerializer.Serialize(new { detectionCount = allDetections.Count }),
            ipAddress: ipAddress);

        status.Status = "completed";

        _logger.LogInformation(
            "LLM scan completed for job {JobId}: {Count} detections",
            jobId, allDetections.Count);
    }
}
