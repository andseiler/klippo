using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Audit;
using PiiGateway.Core.DTOs.Detection;
using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.DTOs.Jobs;
using PiiGateway.Core.DTOs.Review;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Options;
using PiiGateway.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace PiiGateway.Api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDocumentProcessingQueue _processingQueue;
    private readonly AuditLogService _auditLogService;
    private readonly IReviewService _reviewService;
    private readonly ISecondScanService _secondScanService;
    private readonly IDePseudonymizationService _dePseudonymizationService;
    private readonly IDocumentPreviewService _documentPreviewService;
    private readonly ILlmScanService _llmScanService;
    private readonly GuestDemoOptions _guestOptions;
    private readonly JobCancellationRegistry _cancellationRegistry;
    private readonly RescanService _rescanService;

    public JobsController(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        IAuditLogRepository auditLogRepository,
        IFileStorageService fileStorageService,
        IDocumentProcessingQueue processingQueue,
        AuditLogService auditLogService,
        IReviewService reviewService,
        ISecondScanService secondScanService,
        IDePseudonymizationService dePseudonymizationService,
        IDocumentPreviewService documentPreviewService,
        ILlmScanService llmScanService,
        IOptions<GuestDemoOptions> guestOptions,
        JobCancellationRegistry cancellationRegistry,
        RescanService rescanService)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _auditLogRepository = auditLogRepository;
        _fileStorageService = fileStorageService;
        _processingQueue = processingQueue;
        _auditLogService = auditLogService;
        _reviewService = reviewService;
        _secondScanService = secondScanService;
        _dePseudonymizationService = dePseudonymizationService;
        _documentPreviewService = documentPreviewService;
        _llmScanService = llmScanService;
        _guestOptions = guestOptions.Value;
        _cancellationRegistry = cancellationRegistry;
        _rescanService = rescanService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException());

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> CreateJob(IFormFile file)
    {
        // Validate file presence
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        // Determine file extension (default to .txt if none)
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            extension = ".txt";

        // Validate file extension
        var allowedExtensions = new HashSet<string> { ".pdf", ".docx", ".xlsx", ".txt", ".csv" };
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = $"File type '{extension}' is not allowed. Accepted types: {string.Join(", ", allowedExtensions.Order())}." });

        // Validate file size (50 MB)
        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(new { message = "File size exceeds the 50 MB limit." });

        var userId = GetUserId();

        // Compute SHA-256 hash
        string fileHash;
        await using (var hashStream = file.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(hashStream);
            fileHash = Convert.ToHexString(hashBytes).ToLower();
        }

        // Create Job entity
        var job = new Job
        {
            Id = Guid.NewGuid(),
            CreatedById = userId,
            Status = JobStatus.Created,
            FileName = file.FileName,
            FileType = extension.ToLower(),
            FileHash = fileHash,
            FileSizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        // Mark as guest job if created by the demo user
        if (userId == _guestOptions.UserId)
        {
            job.IsGuest = true;
        }

        // Save file
        await using (var fileStream = file.OpenReadStream())
        {
            await _fileStorageService.SaveAsync(job.Id, extension, fileStream);
        }

        // Persist job
        await _jobRepository.CreateAsync(job);

        // Audit log
        await _auditLogService.LogAsync(
            job.Id,
            ActionType.JobCreated,
            actorId: userId,
            ipAddress: GetIpAddress());

        // Enqueue for background processing
        await _processingQueue.EnqueueAsync(job.Id);

        return Accepted(new { id = job.Id, status = "created" });
    }

    [HttpGet]
    public async Task<IActionResult> ListJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var userId = GetUserId();

        JobStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsedStatus))
            statusFilter = parsedStatus;

        var hasFilters = statusFilter.HasValue || dateFrom.HasValue || dateTo.HasValue;

        var (items, totalCount) = hasFilters
            ? await _jobRepository.GetByUserFilteredAsync(userId, page, pageSize, statusFilter, dateFrom, dateTo)
            : await _jobRepository.GetByUserAsync(userId, page, pageSize);

        var responseItems = new List<JobResponse>();
        foreach (var j in items)
        {
            responseItems.Add(await MapToResponseAsync(j));
        }

        var response = new JobListResponse
        {
            Items = responseItems.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        // Delete file from storage (ignore errors if file already gone)
        try { await _fileStorageService.DeleteAsync(job.Id, job.FileType); } catch { }

        // Delete audit logs first (FK restrict)
        await _auditLogRepository.DeleteByJobIdAsync(job.Id);

        // Delete job (cascades to text_segments, pii_entities)
        await _jobRepository.DeleteAsync(job);

        return NoContent();
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateJobRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        if (request.FileName != null)
        {
            var trimmed = request.FileName.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return BadRequest(new { message = "File name cannot be empty." });
            job.FileName = trimmed;
        }

        await _jobRepository.UpdateAsync(job);
        return Ok(await MapToResponseAsync(job));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null)
            return NotFound(new { message = "Job not found." });

        if (job.CreatedById != GetUserId())
            return NotFound(new { message = "Job not found." });

        return Ok(await MapToResponseAsync(job));
    }

    [HttpGet("{id:guid}/review")]
    public async Task<IActionResult> GetReviewData(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            var result = await _reviewService.GetReviewDataAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/document-preview")]
    public async Task<IActionResult> GetDocumentPreview(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            var result = await _documentPreviewService.GetDocumentPreviewAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/entities/{eid:guid}")]
    public async Task<IActionResult> UpdateEntity(Guid id, Guid eid, [FromBody] UpdateEntityRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.UpdateEntityAsync(id, eid, request, GetUserId(), GetIpAddress());
            return Ok(new { message = "Entity updated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/entities/{eid:guid}")]
    public async Task<IActionResult> DeleteEntity(Guid id, Guid eid)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.DeleteEntityAsync(id, eid, GetUserId(), GetIpAddress());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/entities")]
    public async Task<IActionResult> DeleteAllEntities(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.DeleteAllEntitiesAsync(id, GetUserId(), GetIpAddress());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/entities")]
    public async Task<IActionResult> AddEntity(Guid id, [FromBody] AddEntityRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            var entity = await _reviewService.AddManualEntityAsync(id, request, GetUserId(), GetIpAddress());
            var dto = new EntityDto
            {
                Id = entity.Id,
                SegmentId = entity.SegmentId,
                Text = entity.OriginalTextEnc ?? string.Empty,
                EntityType = entity.EntityType,
                StartOffset = entity.StartOffset,
                EndOffset = entity.EndOffset,
                Confidence = entity.Confidence,
                DetectionSources = entity.DetectionSources,
                ConfidenceTier = entity.Confidence >= 0.90 ? "HIGH" : entity.Confidence >= 0.70 ? "MEDIUM" : "LOW",
                ReplacementPreview = entity.ReplacementText,
                ReviewStatus = entity.ReviewStatus.ToString().ToLower()
            };
            return Created($"/api/v1/jobs/{id}/review", dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete-review")]
    public async Task<IActionResult> CompleteReview(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.CompleteReviewAsync(id, GetUserId(), GetIpAddress());
            return Ok(new { message = "Review completed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reopen-review")]
    public async Task<IActionResult> ReopenReview(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.ReopenReviewAsync(id, GetUserId(), GetIpAddress());
            return Ok(new { message = "Review reopened." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/second-scan")]
    public async Task<IActionResult> TriggerSecondScan(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            var result = await _secondScanService.RunSecondScanAsync(id, GetUserId(), GetIpAddress());
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deanonymize")]
    public async Task<IActionResult> Deanonymize(Guid id, [FromBody] DePseudonymizeRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            var result = await _dePseudonymizationService.DePseudonymizeAsync(id, request.LlmResponseText, GetUserId(), GetIpAddress());
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/segments/{segId:guid}")]
    public async Task<IActionResult> UpdateSegment(Guid id, Guid segId, [FromBody] UpdateSegmentRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.UpdateSegmentTextAsync(id, segId, request, GetUserId(), GetIpAddress());
            return Ok(new { message = "Segment updated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/pseudonymized-text")]
    public async Task<IActionResult> UpdatePseudonymizedText(Guid id, [FromBody] UpdatePseudonymizedTextRequest request)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        try
        {
            await _reviewService.UpdatePseudonymizedTextAsync(id, request, GetUserId(), GetIpAddress());
            return Ok(new { message = "Pseudonymized text updated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/llm-scan")]
    public async Task<IActionResult> StartLlmScan(Guid id, [FromBody] LlmScanStartRequest? request = null)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        if (job.Status != JobStatus.ReadyReview && job.Status != JobStatus.InReview
            && job.Status != JobStatus.Pseudonymized && job.Status != JobStatus.ScanPassed
            && job.Status != JobStatus.ScanFailed && job.Status != JobStatus.DePseudonymized)
            return BadRequest(new { message = "LLM scan is only available for jobs in post-processing statuses." });

        // Don't start if already running
        var existing = _llmScanService.GetStatus(id);
        if (existing != null && existing.Status == "running")
            return Ok(existing);

        var result = _llmScanService.StartScan(id, GetUserId(), GetIpAddress(), request?.Instructions, request?.Language);
        return Accepted(result);
    }

    [HttpGet("{id:guid}/llm-scan")]
    public async Task<IActionResult> GetLlmScanStatus(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        var status = _llmScanService.GetStatus(id);
        if (status == null) return NotFound(new { message = "No LLM scan found for this job." });

        return Ok(status);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelJob(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        if (job.Status != JobStatus.Created && job.Status != JobStatus.Processing)
            return BadRequest(new { message = "Job can only be cancelled when in Created or Processing status." });

        _cancellationRegistry.Cancel(id);

        job.Status = JobStatus.Cancelled;
        job.ErrorMessage = "Cancelled by user";
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(id, ActionType.JobStatusChanged,
            actorId: GetUserId(),
            metadata: $"{{\"from\":\"{job.Status.ToString().ToLower()}\",\"to\":\"cancelled\"}}",
            ipAddress: GetIpAddress());

        return Ok(new { message = "Job cancelled." });
    }

    [HttpDelete("{id:guid}/llm-scan")]
    public async Task<IActionResult> CancelLlmScan(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        var cancelled = _llmScanService.CancelScan(id);
        if (!cancelled)
            return BadRequest(new { message = "No running LLM scan found for this job." });

        return Ok(new { message = "LLM scan cancelled." });
    }

    [HttpPost("{id:guid}/rescan")]
    public async Task<IActionResult> StartRescan(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        if (job.Status != JobStatus.ReadyReview && job.Status != JobStatus.InReview
            && job.Status != JobStatus.Pseudonymized && job.Status != JobStatus.ScanPassed
            && job.Status != JobStatus.ScanFailed && job.Status != JobStatus.DePseudonymized)
            return BadRequest(new { message = "Rescan is only available for jobs in post-processing statuses." });

        var existing = _rescanService.GetStatus(id);
        if (existing != null && existing.Status == "running")
            return Ok(existing);

        var result = _rescanService.StartScan(id, GetUserId(), GetIpAddress());
        return Accepted(result);
    }

    [HttpGet("{id:guid}/rescan")]
    public async Task<IActionResult> GetRescanStatus(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        var status = _rescanService.GetStatus(id);
        if (status == null) return NotFound(new { message = "No rescan found for this job." });

        return Ok(status);
    }

    [HttpDelete("{id:guid}/rescan")]
    public async Task<IActionResult> CancelRescan(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        var cancelled = _rescanService.CancelScan(id);
        if (!cancelled)
            return BadRequest(new { message = "No running rescan found for this job." });

        return Ok(new { message = "Rescan cancelled." });
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAuditTrail(Guid id)
    {
        var job = await _jobRepository.GetByIdAsync(id);
        if (job == null) return NotFound(new { message = "Job not found." });
        if (job.CreatedById != GetUserId()) return NotFound(new { message = "Job not found." });

        var auditLogs = await _auditLogRepository.GetByJobIdAsync(id);

        var response = new AuditLogResponse
        {
            Entries = auditLogs.Select(al => new AuditEntryDto
            {
                Id = al.Id,
                Timestamp = al.Timestamp,
                ActorId = al.ActorId,
                ActionType = al.ActionType.ToString().ToLower(),
                EntityType = al.EntityType,
                EntityHash = al.EntityHash,
                Confidence = al.Confidence,
                DetectionSource = al.DetectionSource,
                Metadata = al.Metadata
            }).ToList()
        };

        return Ok(response);
    }

    private async Task<JobResponse> MapToResponseAsync(Job j)
    {
        var response = new JobResponse
        {
            Id = j.Id,
            CreatedById = j.CreatedById,
            Status = j.Status.ToString().ToLower(),
            FileName = j.FileName,
            FileType = j.FileType,
            FileHash = j.FileHash,
            FileSizeBytes = j.FileSizeBytes,
            SecondScanPassed = j.SecondScanPassed,
            ErrorMessage = j.ErrorMessage,
            CreatedAt = j.CreatedAt,
            ProcessingStartedAt = j.ProcessingStartedAt,
            ReviewStartedAt = j.ReviewStartedAt,
            PseudonymizedAt = j.PseudonymizedAt
        };

        // Add entity summary counts for jobs beyond Processing
        if (j.Status > JobStatus.Processing)
        {
            var statusCounts = await _piiEntityRepository.GetStatusCountsAsync(j.Id);
            response.TotalEntities = statusCounts.Values.Sum();
            response.ConfirmedEntities = statusCounts.GetValueOrDefault(ReviewStatus.Confirmed);
            response.ManualEntities = statusCounts.GetValueOrDefault(ReviewStatus.AddedManual);
            response.PendingEntities = statusCounts.GetValueOrDefault(ReviewStatus.Pending);
        }

        return response;
    }
}
