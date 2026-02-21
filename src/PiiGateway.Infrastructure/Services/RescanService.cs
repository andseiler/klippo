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

public class RescanService
{
    private record ScanEntry(LlmScanResponse Response, CancellationTokenSource Cts);

    private readonly ConcurrentDictionary<Guid, ScanEntry> _scans = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RescanService> _logger;

    private const int BatchSize = 10;

    public RescanService(IServiceScopeFactory scopeFactory, ILogger<RescanService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public LlmScanResponse StartScan(Guid jobId, Guid userId, string? ipAddress)
    {
        var status = new LlmScanResponse
        {
            Status = "running",
            ProcessedSegments = 0,
            TotalSegments = 0,
        };

        var cts = new CancellationTokenSource();
        _scans[jobId] = new ScanEntry(status, cts);

        _ = Task.Run(async () =>
        {
            try
            {
                await RunScanAsync(jobId, userId, ipAddress, status, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Rescan cancelled for job {JobId}", jobId);
                status.Status = "cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rescan failed for job {JobId}", jobId);
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

    private async Task RunScanAsync(Guid jobId, Guid userId, string? ipAddress, LlmScanResponse status, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var segmentRepo = scope.ServiceProvider.GetRequiredService<ITextSegmentRepository>();
        var entityRepo = scope.ServiceProvider.GetRequiredService<IPiiEntityRepository>();
        var piiClient = scope.ServiceProvider.GetRequiredService<IPiiDetectionClient>();
        var auditLogService = scope.ServiceProvider.GetRequiredService<AuditLogService>();

        var segments = await segmentRepo.GetByJobIdAsync(jobId);
        var existingEntities = await entityRepo.GetByJobIdAsync(jobId);

        status.TotalSegments = segments.Count;

        var allDetections = new List<LlmScanDetection>();

        // Build a set of existing detection keys for deduplication
        var existingKeys = new HashSet<string>(
            existingEntities.Select(e => $"{e.SegmentId}:{e.StartOffset}:{e.EndOffset}"));

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
                Layers = new List<string> { "regex", "ner" },
            };

            try
            {
                var response = await piiClient.DetectAsync(detectRequest, ct);

                foreach (var det in response.Detections)
                {
                    var key = $"{det.SegmentId}:{det.StartOffset}:{det.EndOffset}";
                    if (existingKeys.Contains(key))
                        continue;

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
                _logger.LogWarning(ex, "Rescan batch failed for job {JobId}", jobId);
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
                DetectionSources = new[] { "rescan" },
                ReviewStatus = ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            }).ToList();

            await entityRepo.AddRangeAsync(newEntities);

            for (int i = 0; i < allDetections.Count; i++)
            {
                allDetections[i].EntityId = newEntities[i].Id;
            }
            status.Detections = allDetections.ToList();
        }

        await auditLogService.LogAsync(
            jobId,
            ActionType.LlmScanCompleted,
            actorId: userId,
            detectionSource: "rescan",
            metadata: JsonSerializer.Serialize(new { detectionCount = allDetections.Count }),
            ipAddress: ipAddress);

        status.Status = "completed";

        _logger.LogInformation(
            "Rescan completed for job {JobId}: {Count} new detections",
            jobId, allDetections.Count);
    }
}
