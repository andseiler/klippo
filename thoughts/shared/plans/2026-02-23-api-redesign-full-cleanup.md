# API Redesign: Full Cleanup, New Endpoints, Rate Limiting, OpenAPI, Frontend Rework

## Overview

Complete API redesign for PiiGateway: eliminate the guest job system, remove dead code, introduce stateless anonymous endpoints, add API key authentication, restructure rate limiting, enrich the OpenAPI spec with auto-generated TypeScript clients, and rework the frontend for the new backend contract.

## Current State Analysis

### Architecture
- .NET 8 backend (4 projects: Api, Core, Infrastructure, Tests)
- Vue 3 + Vite + Pinia + TypeScript frontend
- PostgreSQL + Redis + external pii-service (Python)
- Nginx reverse proxy with Let's Encrypt
- JWT-only authentication, guest system via special user ID
- 25 endpoints on JobsController, 4 on AuthController, 1 on HealthController
- Client-side pseudonymization/de-pseudonymization rendering in the Vue frontend
- Manual TypeScript types, Axios HTTP client

### Key Discoveries
- `SecondScanService` has broken state transitions — `ScanPassed`/`ScanFailed` not in `AllowedTransitions` map (`JobStatusTransitions.cs:7-15`)
- `POST /auth/refresh` is a stub that always returns 401 (`AuthController.cs:66-73`)
- Rate limiter runs BEFORE authentication in middleware pipeline (`Program.cs:135-136`) — cannot distinguish auth'd vs anon
- `ForwardedHeaders` middleware is missing — all anonymous users share one rate limit bucket behind nginx
- `PlaygroundUsageTracker` is an in-memory `ConcurrentDictionary` — resets on app restart
- `ReviewService.GetReviewDataAsync` has a side effect: transitions `ReadyReview` → `InReview` on GET (`ReviewService.cs:36-40`)
- Frontend de-pseudonymization is entirely client-side regex replacement (`stores/review.ts:125-181`)

## Desired End State

### Two-tier API:
- **Anonymous**: Stateless `POST /api/v1/anonymize`, `POST /api/v1/deanonymize`, `POST /api/v1/detect` (standard only) — zero PII stored, rate-limited per IP (3/day default)
- **Authenticated** (JWT or API Key): Job-based endpoints with persisted mappings, rate-limited per user/key (100/day)

### Simplified job status flow:
```
Created → Processing → InReview → Pseudonymized
```
- `ReadyReview`, `ScanPassed`, `ScanFailed`, `DePseudonymized` all removed
- `Pseudonymized` is the terminal status (added to retention cleanup)
- Deanonymize is a stateless operation — no status transition, no status gate

### Clean codebase:
- 20 endpoints removed, 8 new endpoints added
- 5 services removed, 1 new unified `DetectionService`
- `ReviewStatus` enum removed entirely from `PiiEntity`
- Guest system eliminated (no `IsGuest`, no `GuestDemoOptions`, no `PlaygroundUsageTracker`)
- Refresh tokens removed (no `RefreshToken` on `User`, no refresh endpoint)
- File size limit: 50 MB → 5 MB

### Frontend:
- `openapi-typescript` + `openapi-fetch` replaces manual Axios + types
- Playground uses stateless anonymous endpoints (no guest token)
- Review UI becomes read-only entity display + mapping builder + anonymize preview/confirm
- LLM detect button shows upsell modal for anonymous users

## What We're NOT Doing

- Database migration of existing job data to new schema (existing jobs will be cleaned up by retention)
- Self-service API key management UI (keys are in appsettings.json for now)
- WebSocket/SSE for real-time detection progress (polling is sufficient)
- Multi-language locale support beyond `de` and `en`
- Frontend component library upgrade or design system changes
- Performance optimization of the PII detection pipeline itself

## Resolved Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `DePseudonymized` status | **Remove entirely** | Users can deanonymize multiple times; no status gate needed |
| Job-based deanonymize status check | **None** | Deanonymize works regardless of job status — user has mappings, let them use them |
| `ReviewStatus` enum | **Remove entirely** | Entities are detection results, not review items. No confirm/reject on backend |
| `Pseudonymized` in retention cleanup | **Yes** | Auto-delete after 90 days. Clean lifecycle |
| Stateless detect `method` field | **Keep** | Must be `"standard"` — 403 for `"llm"`/`"all"`. Consistent with job-based detect |
| Plan scope | **One unified plan** | All 4 phases in one document |

---

## Phase 1: Backend Cleanup — Remove Dead Code

### Overview
Remove all unused endpoints, services, DTOs, enum values, DB columns, and guest system code. This is pure deletion with a DB migration — no new functionality.

### Changes Required:

#### 1. Remove Endpoints from Controllers

**File**: `src/PiiGateway.Api/Controllers/AuthController.cs`
- Remove `Refresh()` method (lines 66-73)
- Remove `Guest()` method (lines 78-101)
- Remove `Logout()` method (lines 103-122)
- Remove constructor dependencies: `GuestDemoOptions`, `PlaygroundUsageTracker`
- Remove refresh token generation from `Login()` — remove lines that set `user.RefreshToken` and `user.RefreshTokenExpiryTime` (around lines 49-53)
- Remove `RefreshToken` from `AuthResponse` construction (line 58)

**File**: `src/PiiGateway.Api/Controllers/JobsController.cs`
- Remove these 20 endpoint methods:
  - `ReopenReview` (line 395)
  - `TriggerSecondScan` (line 413)
  - `UpdatePseudonymizedText` (line 467)
  - `GetDocumentPreview` (line 261)
  - `GetAuditTrail` (line 604)
  - `UpdateEntity` (line 279)
  - `DeleteEntity` (line 305)
  - `DeleteAllEntities` (line 327)
  - `AddEntity` (line 345)
  - `CompleteReview` (line 377)
  - `UpdateSegment` (line 449)
  - `StartLlmScan` (line 485)
  - `GetLlmScanStatus` (line 506)
  - `CancelLlmScan` (line 543)
  - `StartRescan` (line 557)
  - `GetRescanStatus` (line 577)
  - `CancelRescan` (line 590)
- Remove constructor dependencies: `ISecondScanService`, `IDocumentPreviewService`, `GuestDemoOptions`, `ILlmScanService`, `RescanService`, `IPiiEntityRepository`
- Remove `IsGuest` marking in `CreateJob` (lines 127-131)
- Remove `SecondScanPassed` from `MapToResponseAsync` (line 643)
- Keep: `CreateJob`, `ListJobs`, `DeleteJob`, `UpdateJob`, `GetJob`, `GetReviewData`, `Deanonymize`, `CancelJob`

#### 2. Remove Services (entire files)

| File | Service |
|------|---------|
| `src/PiiGateway.Infrastructure/Services/SecondScanService.cs` | `SecondScanService` |
| `src/PiiGateway.Infrastructure/Services/DocumentPreviewService.cs` | `DocumentPreviewService` |
| `src/PiiGateway.Infrastructure/Services/PlaygroundUsageTracker.cs` | `PlaygroundUsageTracker` |

**Note:** `LlmScanService` and `RescanService` are kept for now — they'll be replaced by `DetectionService` in Phase 2.

#### 3. Remove Service Interfaces

| File | Interface |
|------|-----------|
| `src/PiiGateway.Core/Interfaces/Services/ISecondScanService.cs` | `ISecondScanService` |
| `src/PiiGateway.Core/Interfaces/Services/IDocumentPreviewService.cs` | `IDocumentPreviewService` |

#### 4. Remove DTOs (entire files)

| File |
|------|
| `src/PiiGateway.Core/DTOs/Auth/RefreshTokenRequest.cs` |
| `src/PiiGateway.Core/DTOs/Review/CompleteReviewResultDto.cs` |
| `src/PiiGateway.Core/DTOs/Review/UpdatePseudonymizedTextRequest.cs` |
| `src/PiiGateway.Core/DTOs/Review/UpdateEntityRequest.cs` |
| `src/PiiGateway.Core/DTOs/Review/AddEntityRequest.cs` |
| `src/PiiGateway.Core/DTOs/Review/UpdateSegmentRequest.cs` |
| `src/PiiGateway.Core/DTOs/Export/SecondScanResultDto.cs` |

#### 5. Modify DTOs

**File**: `src/PiiGateway.Core/DTOs/Auth/AuthResponse.cs`
- Remove `RefreshToken` property

**File**: `src/PiiGateway.Core/DTOs/Jobs/JobResponse.cs`
- Remove `SecondScanPassed` property

**File**: `src/PiiGateway.Core/DTOs/Review/ReviewDataResponse.cs`
- Remove `ReviewStatus` from `EntityDto` class
- Remove `Confirmed`, `ManuallyAdded`, `Pending` from `ReviewSummary` (keep only `TotalEntities` and confidence tier counts)

**File**: `src/PiiGateway.Core/DTOs/Jobs/JobResponse.cs`
- Remove `ConfirmedEntities`, `ManualEntities`, `PendingEntities` computed fields (these depend on ReviewStatus)

#### 6. Remove Options/Config (entire files)

| File |
|------|
| `src/PiiGateway.Infrastructure/Options/GuestDemoOptions.cs` |

**File**: `src/PiiGateway.Infrastructure/Options/JwtOptions.cs`
- Remove `RefreshExpiryDays` property

#### 7. Modify Domain Entities

**File**: `src/PiiGateway.Core/Domain/Entities/Job.cs`
- Remove `SecondScanPassed` (line 14)
- Remove `ExportAcknowledged` (line 15)
- Remove `IsGuest` (line 16)
- Remove `PseudonymizedText` (line 22) — replaced by `AnonymizationMap` in Phase 2

**File**: `src/PiiGateway.Core/Domain/Entities/User.cs`
- Remove `RefreshToken` (line 10)
- Remove `RefreshTokenExpiryTime` (line 11)

**File**: `src/PiiGateway.Core/Domain/Entities/PiiEntity.cs`
- Remove `ReviewStatus` (line 17)
- Remove `ReviewedById` (line 18)
- Remove `ReviewedAt` (line 19)
- Remove `ReviewedBy` navigation property (line 24)

#### 8. Modify Enums

**File**: `src/PiiGateway.Core/Domain/Enums/JobStatus.cs`
- Remove `ReadyReview`
- Remove `ScanPassed`
- Remove `ScanFailed`
- Remove `DePseudonymized`

**File**: `src/PiiGateway.Core/Domain/Enums/ActionType.cs`
- Remove `SecondScanPassed`
- Remove `SecondScanFailed`
- Remove `TextCopied`
- Remove `ReviewerTrainingCompleted`
- Remove `DpiaTriggerShown`
- Remove `DpiaAcknowledged`

**File**: `src/PiiGateway.Core/Domain/Enums/ReviewStatus.cs`
- **Delete entire file**

#### 9. Update Job Status Transitions

**File**: `src/PiiGateway.Core/Domain/JobStatusTransitions.cs`

Replace the entire transitions dictionary with:

```csharp
public static readonly Dictionary<JobStatus, HashSet<JobStatus>> AllowedTransitions = new()
{
    [JobStatus.Created] = new() { JobStatus.Processing, JobStatus.Cancelled },
    [JobStatus.Processing] = new() { JobStatus.InReview, JobStatus.Failed, JobStatus.Cancelled },
    [JobStatus.InReview] = new() { JobStatus.Pseudonymized },
    [JobStatus.Pseudonymized] = new() { JobStatus.InReview },
};
```

Key changes:
- `Processing` → `InReview` directly (was `Processing` → `ReadyReview`)
- `InReview` → `Pseudonymized` only (removed `ReadyReview` back-transition)
- `Pseudonymized` → `InReview` kept (user can re-enter review to adjust mapping)
- `DePseudonymized` removed entirely

#### 10. Update DI Registrations

**File**: `src/PiiGateway.Infrastructure/DependencyInjection.cs`

Remove:
- `Configure<GuestDemoOptions>` (line 25)
- `AddScoped<ISecondScanService, SecondScanService>` (line 55)
- `AddScoped<IDocumentPreviewService, DocumentPreviewService>` (line 57)
- `AddSingleton<PlaygroundUsageTracker>` (line 75)

#### 11. Gut ReviewService to Read-Only

**File**: `src/PiiGateway.Core/Interfaces/Services/IReviewService.cs`
- Remove ALL method signatures except `GetReviewDataAsync`

**File**: `src/PiiGateway.Infrastructure/Services/ReviewService.cs`
- Remove ALL methods except `GetReviewDataAsync`
- Remove `IPseudonymizationService` constructor dependency
- In `GetReviewDataAsync`: remove the `ReadyReview` → `InReview` auto-transition (it's a GET — no side effects)
- Remove `ReviewStatus`-based logic from entity mapping (just return entities without review status)
- Remove `ReviewSummary` counts that depend on `ReviewStatus` (Confirmed, ManuallyAdded, Pending)

#### 12. Update JwtService

**File**: `src/PiiGateway.Infrastructure/Services/JwtService.cs`
- Remove `GenerateRefreshToken()` method (lines 44-50)
- Remove `ValidateRefreshToken()` method (lines 52-55)

**File**: `src/PiiGateway.Core/Interfaces/Services/IJwtService.cs`
- Remove `GenerateRefreshToken()` method signature
- Remove `ValidateRefreshToken()` method signature

#### 13. Update DataRetentionBackgroundService

**File**: `src/PiiGateway.Infrastructure/Services/DataRetentionBackgroundService.cs`
- Remove `GuestDemoOptions` constructor dependency
- Remove `RunGuestCleanupLoopAsync` method
- Remove `RunGuestCleanupAsync` method
- Remove the line launching guest cleanup from `ExecuteAsync`

#### 14. Update JobRepository

**File**: `src/PiiGateway.Infrastructure/Repositories/JobRepository.cs`
- Remove `!j.IsGuest` filter from `GetByUserAsync` (line 28) and `GetByUserFilteredAsync` (line 46)
- Remove `GetExpiredGuestJobsAsync` method (lines 87-92)
- In `GetTerminalJobsOlderThanAsync`: replace `DePseudonymized, ScanPassed, ScanFailed, Failed` with `Pseudonymized, Failed` as terminal statuses

**File**: `src/PiiGateway.Core/Interfaces/Repositories/IJobRepository.cs`
- Remove `GetExpiredGuestJobsAsync` method signature

#### 15. Update DocumentProcessor

**File**: `src/PiiGateway.Infrastructure/Services/DocumentProcessor.cs`
- Change transition from `ReadyReview` to `InReview` (line 185): `JobStatusTransitions.Validate(job.Status, JobStatus.InReview)`

#### 16. Update DePseudonymizationService

**File**: `src/PiiGateway.Infrastructure/Services/DePseudonymizationService.cs`
- Remove status check (line 38) — deanonymize works regardless of job status
- Remove status transition logic (the lines that transition to `DePseudonymized`)
- Remove `ReviewStatus` filtering when loading entities — load all entities with non-null `ReplacementText`

#### 17. Update PseudonymizationService

**File**: `src/PiiGateway.Infrastructure/Services/PseudonymizationService.cs`
- Remove `ReviewStatus` filtering in `PseudonymizeJobAsync` — was filtering to `Confirmed` or `AddedManual` only. Now use all entities.
- Remove `ReviewStatus` filtering in `GeneratePreviewTokensAsync` — same change

#### 18. Update EF Configurations

**File**: `src/PiiGateway.Infrastructure/Data/Configurations/JobConfiguration.cs`
- Remove mappings for `SecondScanPassed`, `ExportAcknowledged`, `IsGuest` (lines 24-26)
- Remove `PseudonymizedText` mapping
- Remove legacy `"exported"` → `Pseudonymized` mapping if `DePseudonymized` removal makes it unreachable

**File**: `src/PiiGateway.Infrastructure/Data/Configurations/UserConfiguration.cs`
- Remove `RefreshToken` and `RefreshTokenExpiryTime` mappings (lines 19-20)

**File**: `src/PiiGateway.Infrastructure/Data/Configurations/PiiEntityConfiguration.cs`
- Remove `ReviewStatus` column mapping (line 28)
- Remove `idx_pii_job_status` index on `(job_id, review_status)` (line 34)
- Remove FK to `users` for `reviewed_by_id` (lines 45-49)
- Remove `reviewed_by_id`, `reviewed_at` column mappings

**File**: `src/PiiGateway.Infrastructure/Data/Configurations/AuditLogConfiguration.cs`
- Remove legacy `"exportacknowledged"` mapping if the corresponding `ActionType` values are removed

#### 19. Update appsettings.json

**File**: `src/PiiGateway.Api/appsettings.json`

Remove:
```json
"RefreshExpiryDays": 7
"GuestDemo": { ... }
```

#### 20. Update PiiEntityRepository

**File**: `src/PiiGateway.Infrastructure/Repositories/PiiEntityRepository.cs`
- Remove `GetByJobIdAndStatusAsync` method (uses `ReviewStatus`)
- Remove `GetStatusCountsAsync` method (returns `Dictionary<ReviewStatus, int>`)

**File**: `src/PiiGateway.Core/Interfaces/Repositories/IPiiEntityRepository.cs`
- Remove `GetByJobIdAndStatusAsync` method signature
- Remove `GetStatusCountsAsync` method signature

#### 21. Create EF Core Migration

Create migration to:
- Drop columns from `jobs`: `second_scan_passed`, `export_acknowledged`, `is_guest`, `pseudonymized_text`
- Drop columns from `users`: `refresh_token`, `refresh_token_expiry_time`
- Drop columns from `pii_entities`: `review_status`, `reviewed_by_id`, `reviewed_at`
- Drop index `idx_pii_job_status`
- Drop FK from `pii_entities` to `users` for `reviewed_by_id`
- Delete all guest jobs: `DELETE FROM jobs WHERE is_guest = true`
- Delete audit log records with removed ActionType values: `DELETE FROM audit_log WHERE action_type IN ('secondscanpassed', 'secondscanfailed', 'textcopied', 'reviewertrainingcompleted', 'dpiatriggershown', 'dpiaacknowledged')`

#### 22. Update Tests

**File**: `src/PiiGateway.Tests/Unit/Core/JobStatusTransitionsTests.cs`
- Update all `[InlineData]` to reflect new transitions (remove `ReadyReview`, `ScanPassed`, `ScanFailed`, `DePseudonymized`)
- Add test for `Processing` → `InReview` (direct)
- Add test for `Pseudonymized` → `InReview` (re-review)

**File**: `src/PiiGateway.Tests/Unit/Services/DocumentPreviewServiceTests.cs`
- **Delete entire file** — `DocumentPreviewService` is removed

**File**: `src/PiiGateway.Tests/Unit/Services/PseudonymizationServiceTests.cs`
- Update: remove `ReviewStatus` filtering assertions (e.g. "rejected entities skipped")

**File**: `src/PiiGateway.Tests/Unit/Services/DePseudonymizationServiceTests.cs`
- Update: remove status-check assertions (no longer throws when status isn't `Pseudonymized`)
- Remove test for `DePseudonymized` status transition
- Remove test for "allows multiple passes from DePseudonymized" (now always allowed)

**File**: `src/PiiGateway.Tests/Unit/Services/DocumentProcessorTests.cs`
- Update: transition assertion from `ReadyReview` to `InReview`

**File**: `src/PiiGateway.Tests/Integration/JobsControllerTests.cs`
- Update if any tests reference removed endpoints

### Success Criteria:

#### Automated Verification:
- [ ] EF Core migration generates cleanly: `dotnet ef migrations add CleanupPhase1 --project src/PiiGateway.Infrastructure --startup-project src/PiiGateway.Api`
- [ ] Solution builds with no errors: `dotnet build`
- [ ] All unit tests pass: `dotnet test src/PiiGateway.Tests --filter "Category!=Integration"`
- [ ] No remaining references to removed types (grep for `SecondScan`, `DocumentPreview`, `PlaygroundUsageTracker`, `GuestDemo`, `RefreshToken`, `ReadyReview`, `ScanPassed`, `ScanFailed`, `DePseudonymized`, `ReviewStatus`, `IsGuest`, `ExportAcknowledged`)

#### Manual Verification:
- [ ] API starts and responds to `GET /api/v1/health`
- [ ] Login still works (`POST /api/v1/auth/login`)
- [ ] Job upload, processing, and review data retrieval still work
- [ ] Swagger UI shows only remaining endpoints (8 on Jobs, 1 on Auth, 1 on Health)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation before proceeding to Phase 2.

---

## Phase 2: New Endpoints, Rate Limiting, API Key Auth

### Overview
Add the new anonymous and job-based endpoints, implement two-layer rate limiting, add API key authentication, and fix middleware ordering.

### Changes Required:

#### 1. Add New DTOs

**File**: `src/PiiGateway.Core/DTOs/Anonymize/AnonymizeRequest.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class AnonymizeRequest
{
    public required string Text { get; set; }
    public List<AnonymizeMappingItem>? Mappings { get; set; }
    public bool Confirm { get; set; } // job-based only
}

public class AnonymizeMappingItem
{
    public required string OriginalText { get; set; }
    public string? Replacement { get; set; }
    public string? EntityType { get; set; }
}
```

**File**: `src/PiiGateway.Core/DTOs/Anonymize/AnonymizeResponse.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class AnonymizeResponse
{
    public required string AnonymizedText { get; set; }
    public required List<ResolvedMapping> ResolvedMappings { get; set; }
}

public class ResolvedMapping
{
    public required string OriginalText { get; set; }
    public required string Replacement { get; set; }
    public required string Source { get; set; } // "user" or "generated"
    public string? EntityType { get; set; }
}
```

**File**: `src/PiiGateway.Core/DTOs/Anonymize/DeanonymizeRequest.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class DeanonymizeRequest
{
    public required string Text { get; set; }
    public List<DeanonymizeMappingItem>? Mappings { get; set; } // optional for job-based
}

public class DeanonymizeMappingItem
{
    public required string OriginalText { get; set; }
    public required string Replacement { get; set; }
}
```

**File**: `src/PiiGateway.Core/DTOs/Anonymize/DeanonymizeResponse.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class DeanonymizeResponse
{
    public required string DeanonymizedText { get; set; }
}
```

**File**: `src/PiiGateway.Core/DTOs/Anonymize/StatelessDetectRequest.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class StatelessDetectRequest
{
    public required string Text { get; set; }
    public required string Method { get; set; } // must be "standard"
}
```

**File**: `src/PiiGateway.Core/DTOs/Anonymize/StatelessDetectResponse.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Anonymize;

public class StatelessDetectResponse
{
    public required List<SuggestedMapping> SuggestedMappings { get; set; }
}

public class SuggestedMapping
{
    public required string OriginalText { get; set; }
    public required string EntityType { get; set; }
    public double Confidence { get; set; }
    public required string Source { get; set; } // "regex", "ner", "llm"
}
```

**File**: `src/PiiGateway.Core/DTOs/Detection/UnifiedDetectRequest.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Detection;

public class UnifiedDetectRequest
{
    public required string Method { get; set; } // "standard", "llm", "all"
    public string? Instructions { get; set; }
    public string? Language { get; set; }
}
```

**File**: `src/PiiGateway.Core/DTOs/Detection/UnifiedDetectResponse.cs` (NEW)
```csharp
namespace PiiGateway.Core.DTOs.Detection;

public class UnifiedDetectResponse
{
    public required string Status { get; set; } // "running", "completed", "failed", "cancelled"
    public int ProcessedSegments { get; set; }
    public int TotalSegments { get; set; }
    public List<SuggestedMapping>? SuggestedMappings { get; set; }
}
```

#### 2. Add AnonymizationMap to Job Entity

**File**: `src/PiiGateway.Core/Domain/Entities/Job.cs`
- Add: `public string? AnonymizationMap { get; set; }` — JSON column storing resolved mappings

**File**: `src/PiiGateway.Infrastructure/Data/Configurations/JobConfiguration.cs`
- Add mapping for `AnonymizationMap` as `jsonb` column

#### 3. Create AnonymizationService

**File**: `src/PiiGateway.Infrastructure/Services/AnonymizationService.cs` (NEW)

Interface: `src/PiiGateway.Core/Interfaces/Services/IAnonymizationService.cs` (NEW)

```csharp
public interface IAnonymizationService
{
    // Stateless — auto-detect PII and anonymize
    Task<AnonymizeResponse> AnonymizeStatelessAsync(string text, List<AnonymizeMappingItem>? mappings);

    // Stateless — deanonymize using provided mappings
    DeanonymizeResponse DeanonymizeStateless(string text, List<DeanonymizeMappingItem> mappings);

    // Stateless — detect PII (standard only)
    Task<StatelessDetectResponse> DetectStatelessAsync(string text);

    // Job-based — anonymize with optional confirm
    Task<AnonymizeResponse> AnonymizeJobAsync(Guid jobId, string text, List<AnonymizeMappingItem>? mappings, bool confirm, Guid userId);

    // Job-based — deanonymize (uses stored or provided mappings)
    Task<DeanonymizeResponse> DeanonymizeJobAsync(Guid jobId, string text, List<DeanonymizeMappingItem>? mappings);
}
```

Implementation details:
- **Stateless anonymize (auto-detect)**: When `mappings` is null, call `IPiiDetectionClient.DetectAsync` with `Layers = ["regex", "ner"]` on a single segment, then generate Faker replacements via `PseudonymizationService.GenerateReplacement` for each detection, then do string replacement. Return anonymized text + resolved mappings.
- **Stateless anonymize (explicit)**: When `mappings` is provided, for each item: if `Replacement` is set, use it directly (source: "user"); if `EntityType` is set, generate via `PseudonymizationService.GenerateReplacement` (source: "generated"). Do string replacement. Return anonymized text + resolved mappings.
- **Text length validation**: Check `text.Length <= MaxAnonymousTextLength` (from `RateLimitingOptions`) for stateless endpoints. Return 400 if exceeded.
- **Job-based anonymize**: Same logic but also saves `AnonymizationMap` JSON to job. If `mappings` is null, read existing `PiiEntity` records and generate mappings from them. If `confirm: true`, transition job to `Pseudonymized` and set `PseudonymizedAt`.
- **Job-based deanonymize**: If `mappings` is null, read `AnonymizationMap` from job. Reverse the replacement (replace each `Replacement` with `OriginalText`). Sort by replacement length descending to avoid partial matches.
- **Stateless detect**: Call `IPiiDetectionClient.DetectAsync` with `Layers = ["regex", "ner"]` on a single segment. Return suggested mappings.

#### 4. Create Unified DetectionService

**File**: `src/PiiGateway.Infrastructure/Services/DetectionService.cs` (NEW)

Interface: `src/PiiGateway.Core/Interfaces/Services/IDetectionService.cs` (NEW)

```csharp
public interface IDetectionService
{
    void StartDetection(Guid jobId, Guid userId, string? ipAddress, string method, string? instructions, string? language);
    UnifiedDetectResponse? GetStatus(Guid jobId);
    bool CancelDetection(Guid jobId);
}
```

Implementation:
- Merges `LlmScanService` and `RescanService` logic
- `method: "standard"` runs regex+NER (batch size 10, like RescanService)
- `method: "llm"` runs LLM detection (batch size 5, like LlmScanService)
- `method: "all"` runs both and merges/deduplicates results
- Results returned as `SuggestedMapping` objects (NOT written as `PiiEntity` records)
- Same `ConcurrentDictionary<Guid, ScanEntry>` pattern for async tracking

After creating `DetectionService`:
- Remove `LlmScanService.cs` and `RescanService.cs`
- Remove `ILlmScanService` interface (inline in `LlmScanService.cs`)
- Update DI: remove old registrations, add `AddSingleton<IDetectionService, DetectionService>`

#### 5. Create AnonymizeController

**File**: `src/PiiGateway.Api/Controllers/AnonymizeController.cs` (NEW)

```csharp
[ApiController]
[Route("api/v1")]
public class AnonymizeController : ControllerBase
{
    [HttpPost("anonymize")]
    [AllowAnonymous]
    [EnableRateLimiting("daily")]
    public async Task<IActionResult> Anonymize([FromBody] AnonymizeRequest request) { ... }

    [HttpPost("deanonymize")]
    [AllowAnonymous]
    [EnableRateLimiting("daily")]
    public async Task<IActionResult> Deanonymize([FromBody] DeanonymizeRequest request) { ... }

    [HttpPost("detect")]
    [AllowAnonymous]
    [EnableRateLimiting("daily")]
    public async Task<IActionResult> Detect([FromBody] StatelessDetectRequest request)
    {
        if (request.Method != "standard")
            return StatusCode(403, new { message = "LLM-based detection requires authentication. Contact mail@andreas-seiler.net for access." });
        // ...
    }
}
```

#### 6. Add Job-Based Endpoints to JobsController

**File**: `src/PiiGateway.Api/Controllers/JobsController.cs`

Add new endpoints (replace removed Deanonymize with new contract):

```csharp
[HttpPost("{id:guid}/anonymize")]
public async Task<IActionResult> Anonymize(Guid id, [FromBody] AnonymizeRequest request) { ... }

[HttpPost("{id:guid}/deanonymize")]
public async Task<IActionResult> Deanonymize(Guid id, [FromBody] DeanonymizeRequest request) { ... }

[HttpPost("{id:guid}/detect")]
public async Task<IActionResult> StartDetection(Guid id, [FromBody] UnifiedDetectRequest request) { ... }

[HttpGet("{id:guid}/detect")]
public async Task<IActionResult> GetDetectionStatus(Guid id) { ... }

[HttpDelete("{id:guid}/detect")]
public async Task<IActionResult> CancelDetection(Guid id) { ... }
```

Add constructor dependencies: `IAnonymizationService`, `IDetectionService`

#### 7. API Key Authentication

**File**: `src/PiiGateway.Api/Authentication/ApiKeyAuthenticationHandler.cs` (NEW)
**File**: `src/PiiGateway.Api/Authentication/ApiKeyAuthenticationOptions.cs` (NEW)
**File**: `src/PiiGateway.Infrastructure/Options/ApiKeyOptions.cs` (NEW)

```csharp
public class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";
    public List<ApiKeyEntry> Keys { get; set; } = new();
}

public class ApiKeyEntry
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public int DailyLimit { get; set; } = 100;
}
```

Handler reads `X-API-Key` header, validates against configured keys, creates `ClaimsPrincipal` with key name. Returns `AuthenticateResult.NoResult()` if no header present (falls through to JWT).

**File**: `src/PiiGateway.Api/Program.cs`

Replace authentication setup with multi-scheme:

```csharp
builder.Services.AddAuthentication("Smart")
    .AddPolicyScheme("Smart", "Smart", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey("X-API-Key"))
                return "ApiKey";
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(/* existing config */)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });
```

#### 8. Rate Limiting Restructure

**File**: `src/PiiGateway.Infrastructure/Options/RateLimitingOptions.cs` (NEW)

```csharp
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public int BurstPermitLimit { get; set; } = 30;
    public int BurstWindowSeconds { get; set; } = 60;
    public int AuthenticatedDailyLimit { get; set; } = 100;
    public int AnonymousDailyLimit { get; set; } = 3;
    public int MaxAnonymousTextLength { get; set; } = 50000;
}
```

**File**: `src/PiiGateway.Api/Program.cs`

Replace the 4 named sliding-window policies with:
- `"burst"` — per-minute burst protection (SlidingWindow, partitioned by user/key or IP)
- `"daily"` — per-day usage quota (FixedWindow, partitioned by user/key or IP)
- `"upload"` — keep existing stricter burst limit for file upload
- `"auth"` — keep existing for login endpoint

Remove `"global"` and `"guest"` policies.

Apply `"burst"` globally via `MapControllers().RequireRateLimiting("burst")`. Apply `"daily"` selectively via `[EnableRateLimiting("daily")]` on anonymous endpoints and job anonymize/deanonymize/detect.

#### 9. Fix Middleware Ordering

**File**: `src/PiiGateway.Api/Program.cs`

Add ForwardedHeaders configuration:
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

Fix middleware order to:
```
UseSecurityHeaders → UseHsts → UseHttpsRedirection → UseForwardedHeaders → UseCors → UseAuthentication → UseRateLimiter → UseAuthorization
```

Key fix: `UseAuthentication()` BEFORE `UseRateLimiter()` so rate limiter can distinguish authenticated vs anonymous.

#### 10. Update File Size Limits

| Location | Change |
|----------|--------|
| `Program.cs:14` | `52_428_800` → `5_242_880` |
| `JobsController.cs:81-82` | `52_428_800` → `5_242_880` |
| `JobsController.cs:101` | `50 * 1024 * 1024` → `5 * 1024 * 1024` |
| `appsettings.json:31` | `"MaxFileSizeMb": 50` → `"MaxFileSizeMb": 5` |

#### 11. Update appsettings.json

Add new sections:
```json
"RateLimiting": {
    "BurstPermitLimit": 30,
    "BurstWindowSeconds": 60,
    "AuthenticatedDailyLimit": 100,
    "AnonymousDailyLimit": 3,
    "MaxAnonymousTextLength": 50000
},
"ApiKeys": {
    "Keys": []
}
```

#### 12. Update DI Registrations

**File**: `src/PiiGateway.Infrastructure/DependencyInjection.cs`

Add:
- `Configure<RateLimitingOptions>` bound to `"RateLimiting"`
- `Configure<ApiKeyOptions>` bound to `"ApiKeys"`
- `AddScoped<IAnonymizationService, AnonymizationService>`
- `AddSingleton<IDetectionService, DetectionService>`

Remove:
- `AddSingleton<ILlmScanService, LlmScanService>`
- `AddSingleton<RescanService>`

#### 13. Update nginx

**File**: `docker/nginx/nginx.conf`
- `client_max_body_size 50m` → `client_max_body_size 5m`

#### 14. Create EF Core Migration for AnonymizationMap

Add `anonymization_map` column (jsonb, nullable) to `jobs` table.

#### 15. Write Tests for New Endpoints

New test files:
- `src/PiiGateway.Tests/Unit/Services/AnonymizationServiceTests.cs` — test stateless anonymize (auto-detect + explicit), deanonymize, detect, job-based variants
- `src/PiiGateway.Tests/Unit/Services/DetectionServiceTests.cs` — test start/poll/cancel, method routing (standard/llm/all)
- Update `src/PiiGateway.Tests/Integration/JobsControllerTests.cs` — add tests for new endpoints

### Success Criteria:

#### Automated Verification:
- [ ] Solution builds: `dotnet build`
- [ ] Migration applies: `dotnet ef database update`
- [ ] All unit tests pass: `dotnet test src/PiiGateway.Tests --filter "Category!=Integration"`
- [ ] `POST /api/v1/anonymize` returns anonymized text for a simple request (no auth)
- [ ] `POST /api/v1/deanonymize` reverses the anonymization (no auth)
- [ ] `POST /api/v1/detect` with `method: "standard"` returns suggested mappings (no auth)
- [ ] `POST /api/v1/detect` with `method: "llm"` returns 403
- [ ] Authenticated endpoints require JWT or API key
- [ ] Rate limiting returns 429 after exceeding daily limit

#### Manual Verification:
- [ ] Anonymous endpoints work from curl without any auth
- [ ] API key auth works with `X-API-Key` header
- [ ] Rate limit headers present: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- [ ] Forwarded headers correctly extract client IP behind nginx
- [ ] Job-based anonymize with `confirm: true` transitions to Pseudonymized
- [ ] Job-based deanonymize works from any job status
- [ ] Unified detect endpoint tracks progress and returns suggested mappings

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation before proceeding to Phase 3.

---

## Phase 3: OpenAPI Enrichment + Frontend Type Generation

### Overview
Enrich the backend OpenAPI spec with XML docs, security definitions, and response type annotations. Set up `openapi-typescript` + `openapi-fetch` in the frontend to auto-generate TypeScript types and replace Axios.

### Changes Required:

#### 1. Enable XML Documentation

**File**: `src/PiiGateway.Api/PiiGateway.Api.csproj`

Add to `<PropertyGroup>`:
```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

#### 2. Enrich Swagger Configuration

**File**: `src/PiiGateway.Api/Program.cs`

Replace `AddSwaggerGen()` with:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PiiGateway API",
        Version = "v1",
        Description = "PII Detection and Pseudonymization API"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API key for external consumers"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});
```

Enable Swagger in production (behind existing auth):
```csharp
// Remove the if (app.Environment.IsDevelopment()) guard
app.UseSwagger();
app.UseSwaggerUI();
```

#### 3. Annotate All Controller Actions

Add to every controller action:
- `[ProducesResponseType(typeof(ResponseType), StatusCodes.Status200OK)]`
- `[ProducesResponseType(StatusCodes.Status400BadRequest)]`
- `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` (where applicable)
- `[ProducesResponseType(StatusCodes.Status404NotFound)]` (where applicable)
- `[ProducesResponseType(StatusCodes.Status429TooManyRequests)]`
- `[Consumes("application/json")]` or `[Consumes("multipart/form-data")]`
- `[Produces("application/json")]`

Add `/// <summary>` XML doc comments to all controller actions and all DTO classes.

#### 4. Install Frontend Dependencies

```bash
cd src/frontend
npm install openapi-fetch
npm install -D openapi-typescript
```

#### 5. Add Generation Script

**File**: `src/frontend/package.json`

Add to `scripts`:
```json
"generate:api": "openapi-typescript http://localhost:5050/swagger/v1/swagger.json --output src/api/generated/v1.d.ts --root-types",
"generate:api:file": "openapi-typescript ../../openapi.json --output src/api/generated/v1.d.ts --root-types"
```

#### 6. Generate Types

Run `npm run generate:api` to produce `src/frontend/src/api/generated/v1.d.ts`.

#### 7. Replace Axios Client with openapi-fetch

**File**: `src/frontend/src/api/client.ts` — rewrite:

```typescript
import createClient from 'openapi-fetch'
import type { paths } from './generated/v1'

export const apiClient = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_BASE_URL || '/api/v1',
})

apiClient.use({
  async onRequest({ request }) {
    const token = localStorage.getItem('accessToken')
    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`)
    }
    return request
  },
  async onResponse({ response }) {
    if (response.status === 401) {
      localStorage.removeItem('accessToken')
      window.location.href = '/login'
    }
    return response
  },
})
```

#### 8. Migrate API Service Files

Rewrite each API module to use `apiClient` from openapi-fetch instead of Axios:

**File**: `src/frontend/src/api/auth.ts` — use typed `apiClient.POST('/auth/login', ...)`
**File**: `src/frontend/src/api/jobs.ts` — use typed calls for all CRUD operations
**File**: `src/frontend/src/api/review.ts` — keep only `getReviewData`, add new anonymize/deanonymize/detect calls
**File**: `src/frontend/src/api/health.ts` — typed health call

Create new:
**File**: `src/frontend/src/api/anonymize.ts` (NEW) — stateless anonymize/deanonymize/detect calls

#### 9. Delete Manual Types

**File**: `src/frontend/src/api/types.ts` — **DELETE** (replaced by generated types)

Update all imports across the frontend from `'@/api/types'` to `'@/api/generated/v1'` or use the inferred types from openapi-fetch calls.

### Success Criteria:

#### Automated Verification:
- [ ] Backend builds with XML docs: `dotnet build` (no warnings from 1591)
- [ ] Swagger UI loads and shows all endpoints with descriptions
- [ ] `npm run generate:api` produces `v1.d.ts` without errors
- [ ] Frontend builds: `npm run build` (includes vue-tsc type checking)
- [ ] No TypeScript errors from generated types

#### Manual Verification:
- [ ] Swagger UI shows JWT and API Key authorization buttons
- [ ] Try-it-out works in Swagger for anonymous endpoints
- [ ] Frontend API calls use correct types (verify with IDE hover)
- [ ] No regressions in existing frontend functionality

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation before proceeding to Phase 4.

---

## Phase 4: Frontend Rework

### Overview
Remove guest flow, rework PlaygroundView for stateless anonymous API, rework review UI to mapping-builder pattern, add LLM upsell modal.

### Changes Required:

#### 1. Remove Guest Auth Flow

**File**: `src/frontend/src/api/auth.ts`
- Remove `guestAuth()` function

**File**: `src/frontend/src/stores/auth.ts`
- Remove `refreshTokenValue` ref and all `refreshToken` localStorage references
- Simplify `logout()` — just clear localStorage and redirect, no `POST /auth/logout`
- Remove `setAuth` handling of `refreshToken`

**File**: `src/frontend/src/router/index.ts`
- Remove guest lock-in logic (lines 55-66) — no more `isGuest` checks
- Remove `previousToken` restore logic
- Keep: auth guard for `requiresAuth` routes, login redirect for authenticated users

#### 2. Rework PlaygroundView for Stateless Anonymous

**File**: `src/frontend/src/views/PlaygroundView.vue`

Major rewrite — the playground no longer creates jobs or uses guest tokens.

New flow:
1. **Upload phase**: User pastes text or selects file (≤5 MB). Text extraction happens client-side for .txt files; for other formats, show "Upload requires an account" message.
2. **Quick anonymize**: User hits "Anonymize" → `POST /api/v1/anonymize` with just `{ text }` (auto-detect mode) → shows anonymized text + resolved mappings
3. **Refined mode**: User sees detected entities → adjusts mapping (remove false positives, change replacements) → calls `POST /api/v1/anonymize` with explicit mappings → shows updated result
4. **Detect more**: "Standard" button calls `POST /api/v1/detect` → shows additional suggestions. "AI" button opens upsell modal.
5. **De-anonymize**: User pastes LLM response → `POST /api/v1/deanonymize` with stored resolved mappings → shows restored text
6. **No auth, no job, no server-side storage** — resolved mappings held in component state or sessionStorage

Remove:
- `ensureGuestToken()` function
- `restoreToken()` function
- `onUnmounted` guest cleanup
- `startPolling()` / `stopPolling()` for job status
- `jobId` ref and job-based API calls
- `phase` state machine (`upload` → `processing` → `workbench`) — replace with simpler tab/step state

#### 3. Create LLM Upsell Modal Component

**File**: `src/frontend/src/components/playground/LlmUpsellModal.vue` (NEW)

Simple modal with:
- Title: "AI-powered detection" (i18n key: `playground.llmUpsell.title`)
- Description: "AI-powered detection requires a full account." (i18n key: `playground.llmUpsell.description`)
- CTA: "Contact mail@andreas-seiler.net for access" (i18n key: `playground.llmUpsell.cta`)
- Close button

#### 4. Rework Review Store

**File**: `src/frontend/src/stores/review.ts`

Major changes:
- Remove client-side pseudonymization logic (`pseudonymizedFullText` computed)
- Remove client-side de-pseudonymization logic (`depseudoReplacementMap`, `depseudoOutputFragments`, etc.)
- Replace with API-driven anonymize/deanonymize calls
- Add `anonymizationMapping` ref — the user's working mapping (built from detected entities, modified by user)
- Add `resolvedMappings` ref — the last response from `/anonymize`
- Add `anonymizedText` ref — the last anonymized text from `/anonymize`
- Add actions: `previewAnonymize(jobId)`, `confirmAnonymize(jobId)`, `deanonymize(jobId, text)`
- Keep `fetchReviewData(id)` — now read-only, populates entities for display

Remove:
- `deleteByToken`, `addSearchMatchesAsEntities`, `updateReplacementByToken` — entity CRUD is gone
- All entity modification actions (confirm, reject, etc.)

#### 5. Remove Entity CRUD API Calls

**File**: `src/frontend/src/api/review.ts`
- Remove: `updateEntity`, `deleteEntity`, `addEntity`, `deleteAllEntities`, `completeReview`, `reopenReview`, `updateSegment`, `startLlmScan`, `getLlmScanStatus`, `cancelLlmScan`, `startRescan`, `getRescanStatus`, `cancelRescan`
- Keep: `getReviewData`
- Add: `startDetection(jobId, method, instructions?, language?)`, `getDetectionStatus(jobId)`, `cancelDetection(jobId)`

#### 6. Simplify 401 Interceptor

Already done in Phase 3 (openapi-fetch client) — just redirect to `/login` on 401, no refresh attempt.

#### 7. Rework Review UI Components

The review screen becomes a mapping builder:

- **Document viewer**: Shows text with highlighted detected entities (read-only from `GET /review`)
- **Mapping builder panel**: User selects which entities to include in anonymization, adjusts replacement text or picks entity type for Faker generation
- **"Detect more" button**: Opens method selector (Standard / AI / Both), calls `POST /jobs/{id}/detect`, shows progress, diffs suggestions
- **"Preview" button**: Calls `POST /jobs/{id}/anonymize` with `confirm: false`, shows anonymized text preview
- **"Confirm & Pseudonymize" button**: Calls `POST /jobs/{id}/anonymize` with `confirm: true`, transitions to Pseudonymized, enables copy
- **"De-anonymize" section**: User pastes LLM response, calls `POST /jobs/{id}/deanonymize`, shows restored text

#### 8. Update File Size in Frontend

**File**: `src/frontend/src/composables/useFileUpload.ts`
- `MAX_FILE_SIZE` = `5 * 1024 * 1024` (was `50 * 1024 * 1024`)

#### 9. Update i18n Strings

**Files**: `src/frontend/src/i18n/de/playground.json`, `src/frontend/src/i18n/en/playground.json`

- Remove: `guestError` key
- Update: `dailyLimitReached` — reword for stateless anonymous model
- Update: `serviceUnavailable` — generic error
- Add: `llmUpsell.title`, `llmUpsell.description`, `llmUpsell.cta`

**Files**: `src/frontend/src/i18n/de/upload.json`, `src/frontend/src/i18n/en/upload.json`
- Update `fileTooLarge` and `maxSize` strings: 50 MB → 5 MB

#### 10. Remove Mock System (Optional)

The mock system in `src/frontend/src/api/mocks/` references the old API contract. Either update mocks to match new contract or remove them if not actively used in development.

### Success Criteria:

#### Automated Verification:
- [ ] Frontend builds: `npm run build`
- [ ] No TypeScript errors
- [ ] No dead imports (old API calls, removed types)
- [ ] i18n terminology tests still pass (no forbidden "anonymiz" terms)

#### Manual Verification:
- [ ] **Playground — quick mode**: Paste text → "Anonymize" → see anonymized text + mappings → copy
- [ ] **Playground — refined mode**: Paste text → "Anonymize" → adjust mapping → re-anonymize → copy
- [ ] **Playground — detect**: "Standard" detect returns suggestions, "AI" button shows upsell modal
- [ ] **Playground — de-anonymize**: Paste LLM response → see restored text
- [ ] **Playground — rate limit**: 4th anonymous request shows daily limit error
- [ ] **Review — mapping builder**: Upload document → processing → review shows entities read-only → build mapping → preview → confirm
- [ ] **Review — detect more**: Standard/LLM/Both detection with progress polling
- [ ] **Review — de-anonymize**: Paste LLM response → restored text using stored mapping
- [ ] **Auth**: Login works, dashboard shows jobs, no guest artifacts
- [ ] **Router**: No guest lock-in, direct navigation to /playground works without auth

**Implementation Note**: After completing this phase and all verification passes, the full redesign is complete.

---

## Testing Strategy

### Unit Tests:
- `AnonymizationService`: auto-detect mode, explicit mode, text length validation, job-based with confirm, deanonymize with stored vs explicit mappings
- `DetectionService`: start/poll/cancel for each method, merge logic for "all" method
- `JobStatusTransitions`: new transition graph
- Update existing `PseudonymizationService` and `DePseudonymizationService` tests for removed ReviewStatus

### Integration Tests:
- Anonymous endpoints: anonymize, deanonymize, detect — no auth required
- Rate limiting: verify 429 after daily limit
- API key auth: valid key passes, invalid key returns 401
- Job lifecycle: upload → process → review → anonymize (preview) → anonymize (confirm) → deanonymize

### Manual Testing Steps:
1. Start fresh with migrated database
2. Test playground end-to-end (paste text → anonymize → copy → paste back → deanonymize)
3. Test authenticated flow end-to-end (upload → process → review → detect more → anonymize → confirm → deanonymize)
4. Test API key authentication from curl
5. Test rate limiting (exceed daily limit, verify 429)
6. Test behind nginx (verify forwarded headers, correct IP extraction)

## Performance Considerations

- Stateless auto-detect anonymize is synchronous — keep `MaxAnonymousTextLength` conservative (50,000 chars default) to bound response time
- Detection service uses the same batching as the old services (5 for LLM, 10 for standard)
- `AnonymizationMap` JSON column avoids extra table — single read/write per job

## Migration Notes

- Phase 1 migration deletes all guest jobs (`is_guest = true`) and audit log records with removed ActionType values
- Phase 2 migration adds `anonymization_map` column
- Existing non-guest jobs with `ReadyReview` status will need to be handled — the migration should update them to `InReview`
- Existing `DePseudonymized` jobs should be updated to `Pseudonymized`
- Existing `ScanPassed`/`ScanFailed` jobs should be updated to `Pseudonymized` (or deleted by retention)

**Add to Phase 1 migration SQL:**
```sql
UPDATE jobs SET status = 'inreview' WHERE status = 'readyreview';
UPDATE jobs SET status = 'pseudonymized' WHERE status IN ('depseudonymized', 'scanpassed', 'scanfailed');
DELETE FROM jobs WHERE is_guest = true;
DELETE FROM audit_log WHERE action_type IN ('secondscanpassed', 'secondscanfailed', 'textcopied', 'reviewertrainingcompleted', 'dpiatriggershown', 'dpiaacknowledged');
```

## References

- Research document: `thoughts/shared/research/2026-02-23-api-design-openapi-rate-limiting.md`
- Original masterplan: `thoughts/shared/plans/2026-02-18-pii-gateway-masterplan.md`
- Backend entry point: `src/PiiGateway.Api/Program.cs`
- Main controllers: `src/PiiGateway.Api/Controllers/JobsController.cs`, `AuthController.cs`
- DI wiring: `src/PiiGateway.Infrastructure/DependencyInjection.cs`
- Status transitions: `src/PiiGateway.Core/Domain/JobStatusTransitions.cs`
- Frontend entry: `src/frontend/src/views/PlaygroundView.vue`
- Frontend stores: `src/frontend/src/stores/review.ts`, `auth.ts`
