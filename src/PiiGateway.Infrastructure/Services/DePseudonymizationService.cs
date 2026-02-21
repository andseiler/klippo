using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PiiGateway.Core.Domain;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class DePseudonymizationService : IDePseudonymizationService
{
    private static readonly string[] HonorificPrefixes = { "Herr", "Frau", "Mr", "Mrs", "Ms", "Dr" };

    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<DePseudonymizationService> _logger;

    public DePseudonymizationService(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        AuditLogService auditLogService,
        ILogger<DePseudonymizationService> logger)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<DePseudonymizedResponse> DePseudonymizeAsync(
        Guid jobId, string llmResponseText, Guid userId, string? ipAddress)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (job.Status != JobStatus.Pseudonymized && job.Status != JobStatus.DePseudonymized)
            throw new InvalidOperationException("Job must be in Pseudonymized or DePseudonymized status.");

        var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);
        var activeEntities = entities
            .Where(e => (e.ReviewStatus == ReviewStatus.Confirmed || e.ReviewStatus == ReviewStatus.AddedManual)
                        && e.ReplacementText != null)
            .ToList();

        // Build replacement map: pseudonym -> original
        var replacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in activeEntities)
        {
            var replacement = entity.ReplacementText!;
            var original = entity.OriginalTextEnc ?? "";

            if (!replacementMap.ContainsKey(replacement))
                replacementMap[replacement] = original;

            // Generate name variants for PERSON entities
            var type = entity.EntityType.ToUpper();
            if (type is "PERSON" or "NAME" or "PER")
            {
                AddNameVariants(replacementMap, replacement, original);
            }
        }

        // Sort by key length descending (longest first) to avoid partial matches
        var sortedReplacements = replacementMap
            .OrderByDescending(kv => kv.Key.Length)
            .ToList();

        // Perform replacements and track counts
        var result = llmResponseText;
        var replacementsMade = new List<ReplacementMade>();

        foreach (var (pseudonym, original) in sortedReplacements)
        {
            var count = CountOccurrences(result, pseudonym);
            if (count > 0)
            {
                result = result.Replace(pseudonym, original, StringComparison.OrdinalIgnoreCase);
                replacementsMade.Add(new ReplacementMade
                {
                    Pseudonym = pseudonym,
                    Original = original,
                    Count = count
                });
            }
        }

        // Detect unmapped pseudonyms — replacement texts that appear in the result
        // (could be pseudonyms that the LLM rephrased so we didn't match them)
        var unmappedWarnings = new List<string>();
        var allReplacementTexts = activeEntities
            .Select(e => e.ReplacementText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var replacementText in allReplacementTexts)
        {
            if (result.Contains(replacementText, StringComparison.OrdinalIgnoreCase))
            {
                unmappedWarnings.Add($"Pseudonym '{replacementText}' still appears in output — LLM may have rephrased or introduced it.");
            }
        }

        // Log audit
        var totalReplacements = replacementsMade.Sum(r => r.Count);
        await _auditLogService.LogAsync(jobId, ActionType.ResponseDepseudonymized,
            actorId: userId,
            metadata: $"{{\"replacement_count\":{totalReplacements},\"input_length\":{llmResponseText.Length},\"output_length\":{result.Length},\"unmapped_count\":{unmappedWarnings.Count}}}",
            ipAddress: ipAddress);

        // Transition
        if (job.Status == JobStatus.Pseudonymized)
        {
            JobStatusTransitions.Validate(job.Status, JobStatus.DePseudonymized);
            job.Status = JobStatus.DePseudonymized;
            await _jobRepository.UpdateAsync(job);
        }
        else if (job.Status == JobStatus.DePseudonymized)
        {
            JobStatusTransitions.Validate(job.Status, JobStatus.DePseudonymized);
            // Self-transition — no status change needed, just update
        }

        _logger.LogInformation("De-pseudonymized text for job {JobId}: {Count} replacements made",
            jobId, totalReplacements);

        return new DePseudonymizedResponse
        {
            DepseudonymizedText = result,
            ReplacementsMade = replacementsMade,
            UnmappedWarnings = unmappedWarnings
        };
    }

    private static void AddNameVariants(Dictionary<string, string> map, string replacement, string original)
    {
        var replacementParts = replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var originalParts = original.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (replacementParts.Length < 2 || originalParts.Length < 2) return;

        var replacementSurname = replacementParts[^1];
        var originalSurname = originalParts[^1];

        // Add surname-only variant
        if (!map.ContainsKey(replacementSurname))
            map[replacementSurname] = originalSurname;

        // Add honorific + surname variants
        foreach (var prefix in HonorificPrefixes)
        {
            var pseudonymVariant = $"{prefix} {replacementSurname}";
            var originalVariant = $"{prefix} {originalSurname}";
            if (!map.ContainsKey(pseudonymVariant))
                map[pseudonymVariant] = originalVariant;
        }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
