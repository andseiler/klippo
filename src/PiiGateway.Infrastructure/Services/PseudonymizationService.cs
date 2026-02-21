using System.Text;
using Bogus;
using Microsoft.Extensions.Logging;
using PiiGateway.Core.Domain.Enums;
using PiiGateway.Core.DTOs.Export;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class PseudonymizationService : IPseudonymizationService
{
    private readonly IJobRepository _jobRepository;
    private readonly IPiiEntityRepository _piiEntityRepository;
    private readonly ITextSegmentRepository _textSegmentRepository;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<PseudonymizationService> _logger;

    public PseudonymizationService(
        IJobRepository jobRepository,
        IPiiEntityRepository piiEntityRepository,
        ITextSegmentRepository textSegmentRepository,
        AuditLogService auditLogService,
        ILogger<PseudonymizationService> logger)
    {
        _jobRepository = jobRepository;
        _piiEntityRepository = piiEntityRepository;
        _textSegmentRepository = textSegmentRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<string> PseudonymizeJobAsync(Guid jobId, Guid userId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);
        var activeEntities = entities
            .Where(e => e.ReviewStatus == ReviewStatus.Confirmed || e.ReviewStatus == ReviewStatus.AddedManual)
            .ToList();

        var locale = DetectLocale(job);
        var seed = BitConverter.ToInt32(jobId.ToByteArray(), 0);

        // Build consistency map: same original text always gets same replacement
        var consistencyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var typeCounters = new Dictionary<string, int>();

        // First pass: collect existing (user-edited) ReplacementText values
        foreach (var entity in activeEntities)
        {
            if (entity.ReplacementText != null)
            {
                var key = entity.OriginalTextEnc?.ToLower() ?? entity.Id.ToString();
                consistencyMap.TryAdd(key, entity.ReplacementText);
            }
        }

        // Second pass: generate replacements only for entries missing from map
        foreach (var entity in activeEntities)
        {
            var originalKey = entity.OriginalTextEnc?.ToLower() ?? entity.Id.ToString();
            if (!consistencyMap.ContainsKey(originalKey))
            {
                var replacement = GenerateReplacementInternal(
                    entity.EntityType, entity.OriginalTextEnc ?? "", locale, seed, consistencyMap.Count, typeCounters);
                consistencyMap[originalKey] = replacement;
            }

            entity.ReplacementText = consistencyMap[originalKey];
        }

        if (activeEntities.Count > 0)
        {
            await _piiEntityRepository.UpdateRangeAsync(activeEntities);
        }

        // Build pseudonymized full text
        var segments = await _textSegmentRepository.GetByJobIdAsync(jobId);
        var pseudonymizedText = BuildPseudonymizedText(segments, activeEntities);

        job.PseudonymizedText = pseudonymizedText;
        await _jobRepository.UpdateAsync(job);

        await _auditLogService.LogAsync(jobId, ActionType.DocumentPseudonymized,
            actorId: userId,
            metadata: $"{{\"entity_count\":{activeEntities.Count},\"locale\":\"{locale}\"}}");

        _logger.LogInformation("Pseudonymized {Count} entities for job {JobId}", activeEntities.Count, jobId);

        return pseudonymizedText;
    }

    public async Task GeneratePreviewTokensAsync(Guid jobId)
    {
        var entities = (await _piiEntityRepository.GetByJobIdAsync(jobId)).ToList();
        if (entities.Count == 0) return;

        // Short-circuit if all entities already have tokens (idempotent)
        if (entities.All(e => e.ReplacementText != null)) return;

        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var locale = DetectLocale(job);
        var seed = BitConverter.ToInt32(jobId.ToByteArray(), 0);

        var consistencyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var typeCounters = new Dictionary<string, int>();
        var updated = new List<Core.Domain.Entities.PiiEntity>();

        // First pass: collect existing tokens into consistency map
        foreach (var entity in entities)
        {
            if (entity.ReplacementText != null)
            {
                var key = entity.OriginalTextEnc?.ToLower() ?? entity.Id.ToString();
                consistencyMap.TryAdd(key, entity.ReplacementText);
            }
        }

        // Second pass: generate tokens for entities missing them
        foreach (var entity in entities)
        {
            if (entity.ReplacementText != null) continue;

            var originalKey = entity.OriginalTextEnc?.ToLower() ?? entity.Id.ToString();
            if (!consistencyMap.ContainsKey(originalKey))
            {
                var replacement = GenerateReplacementInternal(
                    entity.EntityType, entity.OriginalTextEnc ?? "", locale, seed, consistencyMap.Count, typeCounters);
                consistencyMap[originalKey] = replacement;
            }

            entity.ReplacementText = consistencyMap[originalKey];
            updated.Add(entity);
        }

        if (updated.Count > 0)
        {
            await _piiEntityRepository.UpdateRangeAsync(updated);
        }
    }

    public string GenerateReplacement(string entityType, string originalText, string locale)
    {
        var typeCounters = new Dictionary<string, int>();
        return GenerateReplacementInternal(entityType, originalText, locale, 42, 0, typeCounters);
    }

    public async Task<PseudonymizedOutputResponse> GetPseudonymizedOutputAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var entities = await _piiEntityRepository.GetByJobIdAsync(jobId);
        var activeEntities = entities
            .Where(e => (e.ReviewStatus == ReviewStatus.Confirmed || e.ReviewStatus == ReviewStatus.AddedManual)
                        && e.ReplacementText != null)
            .ToList();

        var replacementGroups = activeEntities
            .GroupBy(e => new { Original = e.OriginalTextEnc ?? "", e.ReplacementText, e.EntityType })
            .Select(g => new ReplacementEntry
            {
                Original = g.Key.Original,
                Replacement = g.Key.ReplacementText!,
                EntityType = g.Key.EntityType,
                OccurrenceCount = g.Count()
            })
            .ToList();

        return new PseudonymizedOutputResponse
        {
            PseudonymizedText = job.PseudonymizedText ?? string.Empty,
            Replacements = replacementGroups
        };
    }

    private string GenerateReplacementInternal(
        string entityType, string originalText, string locale, int baseSeed, int index,
        Dictionary<string, int> typeCounters)
    {
        var faker = new Faker(locale);
        faker.Random = new Randomizer(baseSeed + index);

        var type = entityType.ToUpper();
        return type switch
        {
            "PERSON" or "NAME" or "PER" => faker.Name.FullName(),
            "ORGANIZATION" or "ORG" or "COMPANY" => faker.Company.CompanyName(),
            "LOCATION" or "LOC" or "GPE" or "CITY" => faker.Address.City(),
            "COUNTRY" => faker.Address.Country(),
            "ADDRESS" => faker.Address.StreetAddress(),
            "IBAN" => GenerateSyntheticIban(faker),
            "EMAIL" or "EMAIL_ADDRESS" => faker.Internet.Email(),
            "PHONE" or "PHONE_NUMBER" => faker.Phone.PhoneNumber(),
            "DATE" or "DATE_TIME" => faker.Date.Past(5).ToString("dd.MM.yyyy"),
            _ => GeneratePlaceholder(type, typeCounters)
        };
    }

    private static string GeneratePlaceholder(string type, Dictionary<string, int> counters)
    {
        if (!counters.TryGetValue(type, out var count))
            count = 0;
        counters[type] = count + 1;
        return $"[{type}_{count + 1:D3}]";
    }

    private static string GenerateSyntheticIban(Faker faker)
    {
        var bban = faker.Random.Long(100000000000000000, 999999999999999999).ToString();
        // Calculate check digits using mod-97 per ISO 7064
        var numericCountry = "131400"; // DE = 13 14, 00 = placeholder for check digits
        var checkInput = bban + numericCountry;
        var remainder = Mod97(checkInput);
        var checkDigits = (98 - remainder).ToString("D2");
        return $"DE{checkDigits}{bban}";
    }

    private static int Mod97(string numericString)
    {
        int remainder = 0;
        foreach (var c in numericString)
        {
            remainder = (remainder * 10 + (c - '0')) % 97;
        }
        return remainder;
    }

    private static string DetectLocale(Core.Domain.Entities.Job job)
    {
        // Default to German for DACH market
        return "de";
    }

    private static string BuildPseudonymizedText(
        IReadOnlyList<Core.Domain.Entities.TextSegment> segments,
        List<Core.Domain.Entities.PiiEntity> entities)
    {
        var sb = new StringBuilder();

        foreach (var segment in segments.OrderBy(s => s.SegmentIndex))
        {
            var segmentEntities = entities
                .Where(e => e.SegmentId == segment.Id)
                .OrderBy(e => e.StartOffset)
                .ToList();

            if (segmentEntities.Count == 0)
            {
                sb.AppendLine(segment.TextContent);
                continue;
            }

            var text = segment.TextContent;
            var offset = 0;

            foreach (var entity in segmentEntities)
            {
                if (entity.StartOffset > offset && entity.StartOffset <= text.Length)
                {
                    sb.Append(text.AsSpan(offset, entity.StartOffset - offset));
                }

                sb.Append(entity.ReplacementText);
                offset = Math.Min(entity.EndOffset, text.Length);
            }

            if (offset < text.Length)
            {
                sb.Append(text.AsSpan(offset));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
