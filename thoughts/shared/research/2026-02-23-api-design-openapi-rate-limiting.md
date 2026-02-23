---
date: 2026-02-23T09:38:03+01:00
researcher: claude
git_commit: 57b4183
branch: master
repository: klippo
topic: "API Redesign: Anonymize/Deanonymize Endpoints, Rate Limiting, OpenAPI, Cleanup"
tags: [research, codebase, api, authentication, rate-limiting, openapi, swagger, cleanup, anonymize, deanonymize]
status: complete
last_updated: 2026-02-23
last_updated_by: claude
last_updated_note: "Complete rework incorporating ANDI feedback, endpoint removal, new anonymize/deanonymize design, guest elimination"
---

# Research: API Redesign — Anonymize/Deanonymize, Rate Limiting, OpenAPI, Full Cleanup

**Date**: 2026-02-23T09:38:03+01:00
**Researcher**: claude
**Git Commit**: 57b4183
**Branch**: master
**Repository**: klippo

## Research Question

1. How to expose a clean API with authenticated endpoints (higher rate limits, ~100/day) and unauthenticated/anonymous endpoints (configurable per-IP limits, currently 3/day)?
2. How to use Swagger/OpenAPI to provide the API contract AND auto-generate frontend TypeScript clients/models?
3. New `/anonymize` and `/deanonymize` endpoints (stateless for anonymous, job-based for authenticated).
4. Eliminate guest jobs entirely (security concerns) — replace with stateless anonymous endpoints.
5. Remove all unused endpoints, services, DTOs, DB columns, and enum values.
6. File size limit reduced to 5 MB.

---

## Summary

The redesign introduces a two-tier API architecture:

- **Anonymous users**: Stateless `POST /api/v1/anonymize` and `POST /api/v1/deanonymize` — zero PII stored on the server, rate-limited per IP (configurable, default 3/day).
- **Authenticated users** (JWT or API Key): Job-based `POST /api/v1/jobs/{id}/anonymize` and `POST /api/v1/jobs/{id}/deanonymize` — mapping persisted on the job, rate-limited per user/key (100/day).

This eliminates the guest job system entirely, closing security holes while keeping the playground functional. The frontend will no longer do client-side pseudonymization/de-pseudonymization — all logic moves to the backend.

Rate limiting is restructured into two layers: per-minute burst protection (DoS defense) + per-day quotas (usage tiers). OpenAPI annotations + `openapi-typescript`/`openapi-fetch` provide the API contract and auto-generated TypeScript clients.

8 endpoints, 6 services, 5 DB columns, multiple DTOs, and several enum values are identified for removal.

---

## 1. New Endpoint Design

### 1.1 Anonymous Stateless Endpoints (no auth required)

These replace the entire guest job flow. Zero PII is stored on the server.

#### `POST /api/v1/anonymize`

Takes text and a list of replacement mappings. Each mapping specifies original text and either a user-defined replacement string or an entity type (backend generates replacement via Faker/Bogus).

**Request:**

```json
{
  "text": "Peter Smith lives at Hauptstraße 12 in Berlin.",
  "mappings": [
    {
      "originalText": "Peter Smith",
      "replacement": "John Doe"
    },
    {
      "originalText": "Hauptstraße 12",
      "entityType": "Address"
    },
    {
      "originalText": "Berlin",
      "entityType": "Location"
    }
  ]
}
```

**Response:**

```json
{
  "anonymizedText": "John Doe lives at Musterweg 7 in Hamburg.",
  "resolvedMappings": [
    {
      "originalText": "Peter Smith",
      "replacement": "John Doe",
      "source": "user"
    },
    {
      "originalText": "Hauptstraße 12",
      "replacement": "Musterweg 7",
      "source": "generated",
      "entityType": "Address"
    },
    {
      "originalText": "Berlin",
      "replacement": "Hamburg",
      "source": "generated",
      "entityType": "Location"
    }
  ]
}
```

**Key design decisions:**

- `replacement` and `entityType` are mutually exclusive in the request — if `replacement` is provided, it's used directly; if `entityType` is provided, Faker generates the replacement.
- The response always returns the full resolved mapping so the frontend can use it for de-anonymization.
- The replacement generation uses the existing `PseudonymizationService` which already has Bogus/Faker configured with German locale (`de`).
- **Nothing is stored** — the frontend must keep the mapping for de-anonymization.

#### `POST /api/v1/deanonymize`

Takes anonymized text and the resolved mappings (as returned by `/anonymize`). Reverses the process.

**Request:**

```json
{
  "text": "John Doe lives at Musterweg 7 in Hamburg.",
  "mappings": [
    {
      "originalText": "Peter Smith",
      "replacement": "John Doe"
    },
    {
      "originalText": "Hauptstraße 12",
      "replacement": "Musterweg 7"
    },
    {
      "originalText": "Berlin",
      "replacement": "Hamburg"
    }
  ]
}
```

**Response:**

```json
{
  "deanonymizedText": "Peter Smith lives at Hauptstraße 12 in Berlin."
}
```

**Rate limiting:** Both endpoints are rate-limited per IP (configurable, default 3/day + per-minute burst protection).

### 1.2 Authenticated Job-Based Endpoints

For authenticated users, the mapping is persisted on the job so the user doesn't need to manage it.

#### `POST /api/v1/jobs/{id}/anonymize`

Same request format as the stateless version, but the resolved mapping is stored on the job. Has an additional `confirm` parameter.

**Request:**
```json
{
  "text": "Peter Smith lives at Hauptstraße 12 in Berlin.",
  "mappings": [
    {
      "originalText": "Peter Smith",
      "replacement": "John Doe"
    },
    {
      "originalText": "Hauptstraße 12",
      "entityType": "Address"
    }
  ],
  "confirm": false
}
```

**The `confirm` parameter:**
- `confirm: false` (default) — preview mode. Anonymizes the text and saves the mapping, but keeps the job in `InReview` status. The user can call this repeatedly to adjust mappings.
- `confirm: true` — the user has accepted the risks. Anonymizes, saves the mapping, AND transitions the job to `Pseudonymized` status. Only after this can the user copy the pseudonymized text.

This replaces the old `complete-review` endpoint. The status transition is the "gate" — the frontend only allows copying the anonymized text once the job is in `Pseudonymized` status, which requires the user to explicitly confirm.

**Additional behavior:**
- Saves the resolved mapping to the job (new `AnonymizationMap` JSON column on `Job`)
- Returns the same response format as the stateless version
- Calling again replaces the previous mapping
- When `confirm: true`, also sets `Job.PseudonymizedAt = DateTime.UtcNow`

#### `POST /api/v1/jobs/{id}/deanonymize`

For authenticated users, can be called either with mappings (explicit) or without (uses the stored mapping from the job's last anonymize call).

**Request (without mappings — uses stored map):**
```json
{
  "text": "John Doe lives at Musterweg 7 in Hamburg."
}
```

**Request (with explicit mappings — same as stateless):**
```json
{
  "text": "John Doe lives at Musterweg 7 in Hamburg.",
  "mappings": [...]
}
```

### 1.3 Simplified Job Status Flow

**Old flow (6 statuses for the review path):**
```
Created → Processing → ReadyReview → InReview → Pseudonymized → DePseudonymized
                                         ↕ (reopen-review)
```

**New flow (4 statuses):**
```
Created → Processing → InReview → Pseudonymized
```

- `ReadyReview` is removed — after processing, the job goes straight to `InReview`.
- The user browses detected entities (read-only via `GET /review`) and builds their anonymization mapping in the frontend.
- The user calls `POST /jobs/{id}/anonymize` (with `confirm: false`) to preview the anonymized text.
- When ready, the user calls `POST /jobs/{id}/anonymize` (with `confirm: true`) to accept risks and transition to `Pseudonymized`.
- `DePseudonymized` status is kept — set when the user calls `POST /jobs/{id}/deanonymize`.
- Entity CRUD endpoints (`PATCH/DELETE/POST entities`, `PATCH segments`, `complete-review`) are all removed — the anonymization mapping IS the user's decision about what to anonymize and how.

---

## 2. Complete Endpoint Inventory — Keep vs Remove

### 2.1 Endpoints to REMOVE

| Endpoint                              | File:Line                   | Reason                                                                                                       |
| ------------------------------------- | --------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `POST /auth/refresh`                  | `AuthController.cs:65-74`   | Stub — always returns 401, never functional                                                                  |
| `POST /auth/guest`                    | `AuthController.cs:76-101`  | Guest system eliminated — replaced by stateless anonymous endpoints                                          |
| `POST /auth/logout`                   | `AuthController.cs:103-122` | Only clears refresh token fields, which are being removed                                                    |
| `POST /jobs/{id}/reopen-review`       | `JobsController.cs:395-411` | Never called by any frontend component                                                                       |
| `POST /jobs/{id}/second-scan`         | `JobsController.cs:413-429` | Never called by frontend; state transitions are broken (ScanPassed/ScanFailed not in AllowedTransitions map) |
| `PATCH /jobs/{id}/pseudonymized-text` | `JobsController.cs:467-483` | Never called by frontend                                                                                     |
| `GET /jobs/{id}/document-preview`     | `JobsController.cs:261-277` | Replaced by new `/anonymize` endpoint                                                                        |
| `GET /jobs/{id}/audit`                | `JobsController.cs:604-630` | Never called by frontend                                                                                     |
| `PATCH /jobs/{id}/entities/{eid}`     | `JobsController.cs:279-303` | Replaced by anonymization mapping in `/anonymize` — no per-entity review on the backend anymore              |
| `DELETE /jobs/{id}/entities/{eid}`    | `JobsController.cs:305-325` | Replaced by anonymization mapping in `/anonymize`                                                            |
| `DELETE /jobs/{id}/entities`          | `JobsController.cs:327-343` | Replaced by anonymization mapping in `/anonymize`                                                            |
| `POST /jobs/{id}/entities`            | `JobsController.cs:345-375` | Replaced by anonymization mapping in `/anonymize`                                                            |
| `POST /jobs/{id}/complete-review`     | `JobsController.cs:377-393` | Replaced by `confirm: true` parameter on `/jobs/{id}/anonymize`                                              |
| `PATCH /jobs/{id}/segments/{segId}`   | `JobsController.cs:449-465` | Replaced by anonymization mapping in `/anonymize`                                                            |

### 2.2 Endpoints to KEEP

#### Auth

| Endpoint           | What it does                                                                                                                         | Role in the flow                                                                                                                                                                                                                                                              | Cleanup needed                                                                                                                                                                  |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /auth/login` | Validates email/password against `users` table (BCrypt), returns JWT access token with `sub`, `email`, `jti` claims (60 min expiry). | **Entry point** for authenticated users. The frontend calls this on the login page; the returned JWT is stored in `localStorage` and attached to all subsequent requests via the Axios interceptor. External API consumers skip this entirely — they use `X-API-Key` instead. | Remove refresh token generation (lines 49-53), remove `RefreshToken` from `AuthResponse`, remove `GuestDemoOptions` and `PlaygroundUsageTracker` dependencies from constructor. |

#### Job Lifecycle — Core CRUD

| Endpoint            | What it does                                                                                                                                                                                                                                                                                 | Role in the flow                                                                                                                                                                                                                                                                                                                                                  | Cleanup needed                                                                                                                                       |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /jobs`        | Accepts a multipart file upload (`.pdf`, `.docx`, `.xlsx`, `.txt`, `.csv`). Validates file extension and size. Computes SHA-256 hash. Creates a `Job` entity in `Created` status. Saves the file to disk via `FileStorageService`. Enqueues the job for background PII detection processing. | **Step 1 of the main flow.** The user uploads a document, which triggers the PII detection pipeline (regex + NER layers via the external pii-service). The background `DocumentProcessingBackgroundService` picks up the job, extracts text, sends it for PII detection, creates `TextSegment` and `PiiEntity` records, and transitions the job to `ReadyReview`. | Update file size limit from 50 MB to 5 MB (attributes + validation). Remove `IsGuest` marking (lines 127-131). Remove `GuestDemoOptions` dependency. |
| `GET /jobs`         | Returns a paginated list of jobs owned by the authenticated user. Supports optional filters: `status`, `dateFrom`, `dateTo`. Each job includes entity summary counts (total, confirmed, manual, pending).                                                                                    | **Dashboard view.** The frontend job list page polls this to show all the user's documents and their processing status.                                                                                                                                                                                                                                           | Remove `!j.IsGuest` filter in `JobRepository.GetByUserAsync` and `GetByUserFilteredAsync`.                                                           |
| `GET /jobs/{id}`    | Returns a single job with its current status, file metadata, timestamps, and entity summary counts. Enforces ownership (job must belong to the authenticated user).                                                                                                                          | **Job detail / status polling.** After upload, the frontend polls this endpoint to track processing progress (`Created` → `Processing` → `ReadyReview`). Also used to refresh job state after any action.                                                                                                                                                         | Remove `SecondScanPassed` from `MapToResponseAsync`.                                                                                                 |
| `PATCH /jobs/{id}`  | Updates the job's `FileName` field. Only the owner can modify.                                                                                                                                                                                                                               | **Rename document.** Allows the user to change the display name of an uploaded file.                                                                                                                                                                                                                                                                              | None.                                                                                                                                                |
| `DELETE /jobs/{id}` | Deletes the job, its file from disk, all audit logs (FK constraint), and cascading deletes for text segments and PII entities. Only the owner can delete.                                                                                                                                    | **Cleanup.** User can delete a document and all associated data at any time.                                                                                                                                                                                                                                                                                      | None.                                                                                                                                                |

#### Review Flow — Read-Only

| Endpoint             | What it does                                                                                                                                                                                                                                                                           | Role in the flow                                                                                                                                                                                                                                                                                                                                                                       | Cleanup needed                                                                                                                                                       |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /jobs/{id}/review` | Returns the full review data: all text segments (the document broken into chunks), all detected PII entities (with offsets, confidence scores, entity types, replacement previews), and a summary (counts by confidence tier and review status). Triggers preview token generation. | **Step 2 of the main flow.** After PII detection completes, the user opens the review screen. This endpoint provides everything the frontend needs to render the document with highlighted PII entities. The frontend uses this data to let the user build their anonymization mapping (which entities to anonymize, what replacements to use) — all client-side, no backend CRUD needed. | Remove the `ReadyReview` → `InReview` auto-transition (processing now goes directly to `InReview`). Remove `ReviewStatus`-based logic since entity review status is no longer tracked. |

**Note:** The 6 entity management endpoints (`PATCH/DELETE/POST entities`, `DELETE entities`, `PATCH segments`, `complete-review`) are all **removed** — see Section 2.1. The anonymization mapping sent to `POST /jobs/{id}/anonymize` replaces all per-entity review actions. The `confirm` parameter on `/anonymize` replaces `complete-review`.

#### LLM Scan (AI-assisted PII detection)

| Endpoint                     | What it does                                                                                                                                                                                                                                     | Role in the flow                                                                                                                                                                                                                       | Cleanup needed                                                                 |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `POST /jobs/{id}/llm-scan`   | Starts an async background LLM-based PII scan. Processes segments in batches of 5. Accepts optional `instructions` (custom prompt) and `language`. Returns immediately with status `"running"`. If already running, returns the existing status. | **Optional enhancement.** After initial regex+NER detection, the user can trigger an additional AI scan to catch PII that pattern-based detection missed. Runs in-memory via `LlmScanService` (singleton, fire-and-forget `Task.Run`). | Remove `ScanPassed`/`ScanFailed` from the allowed status check (line 493-494). |
| `GET /jobs/{id}/llm-scan`    | Returns the current LLM scan status: `"running"`, `"completed"`, `"failed"`, or `"cancelled"`, plus `processedSegments`/`totalSegments` progress and any new entities found.                                                                     | **Poll scan progress.** The frontend polls this while the LLM scan runs to show a progress indicator and display newly found entities.                                                                                                 | None.                                                                          |
| `DELETE /jobs/{id}/llm-scan` | Cancels a running LLM scan via `CancellationTokenSource`.                                                                                                                                                                                        | **Cancel long-running scan.** If the user doesn't want to wait.                                                                                                                                                                        | None.                                                                          |

#### Rescan (Re-run PII detection)

| Endpoint                   | What it does                                                                                                                                                                                             | Role in the flow                                                                                                                                                                                               | Cleanup needed                                                                 |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `POST /jobs/{id}/rescan`   | Starts an async background rescan of all segments using the standard PII detection pipeline (regex + NER, same as initial processing). Processes in batches of 10. Returns immediately with `"running"`. | **Re-detect after edits.** If the user has edited segment text or wants to re-run detection with updated settings, they can trigger a rescan. New entities are added without removing existing confirmed ones. | Remove `ScanPassed`/`ScanFailed` from the allowed status check (line 565-566). |
| `GET /jobs/{id}/rescan`    | Returns rescan progress: status, processed/total segments, newly found entities.                                                                                                                         | **Poll rescan progress.** Same pattern as LLM scan polling.                                                                                                                                                    | None.                                                                          |
| `DELETE /jobs/{id}/rescan` | Cancels a running rescan.                                                                                                                                                                                | **Cancel rescan.**                                                                                                                                                                                             | None.                                                                          |

#### Job Control

| Endpoint                 | What it does                                                                                                                                                               | Role in the flow                                                                                                                                       | Cleanup needed |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------- |
| `POST /jobs/{id}/cancel` | Cancels a job that is in `Created` or `Processing` status. Signals the `JobCancellationRegistry` (checked by the background processor) and sets job status to `Cancelled`. | **Abort processing.** If the user uploaded the wrong file or doesn't want to wait for processing to finish. Only works before the review phase begins. | None.          |

#### Health

| Endpoint      | What it does                                                                                               | Role in the flow                                                                                                     | Cleanup needed    |
| ------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ----------------- |
| `GET /health` | Returns basic health status. Unauthenticated users get minimal info; authenticated users get full details. | **Monitoring / uptime checks.** Used by infrastructure monitoring and the frontend's health check (`api/health.ts`). | None (unchanged). |

### 2.3 NEW Endpoints

| Endpoint                             | Auth             | Description                                     |
| ------------------------------------ | ---------------- | ----------------------------------------------- |
| `POST /api/v1/anonymize`             | None (anonymous) | Stateless anonymization — zero PII stored       |
| `POST /api/v1/deanonymize`           | None (anonymous) | Stateless de-anonymization — zero PII stored    |
| `POST /api/v1/jobs/{id}/anonymize`   | JWT or API Key   | Job-based anonymization — mapping persisted     |
| `POST /api/v1/jobs/{id}/deanonymize` | JWT or API Key   | Job-based de-anonymization (rework of existing) |

---

## 3. Complete Removal Inventory

### 3.1 Backend Services to REMOVE

| Service                                              | File                                                | Reason                                             |
| ---------------------------------------------------- | --------------------------------------------------- | -------------------------------------------------- |
| `SecondScanService` / `ISecondScanService`           | `Infrastructure/Services/SecondScanService.cs`      | Endpoint removed; state transitions broken         |
| `DocumentPreviewService` / `IDocumentPreviewService` | `Infrastructure/Services/DocumentPreviewService.cs` | Endpoint removed; replaced by `/anonymize`         |
| `PlaygroundUsageTracker`                             | `Infrastructure/Services/PlaygroundUsageTracker.cs` | Guest system eliminated; rate limiting replaces it |

### 3.2 Backend DTOs to REMOVE

| DTO                                           | File                                                 | Reason                                    |
| --------------------------------------------- | ---------------------------------------------------- | ----------------------------------------- |
| `RefreshTokenRequest`                         | `Core/DTOs/Auth/RefreshTokenRequest.cs`              | Entire file — refresh endpoint removed    |
| `CompleteReviewResultDto`                     | `Core/DTOs/Review/CompleteReviewResultDto.cs`        | Entire file — never instantiated anywhere |
| `SecondScanResultDto` + `SecondScanDetection` | `Core/DTOs/Export/SecondScanResultDto.cs`            | Entire file — second scan removed         |
| `UpdatePseudonymizedTextRequest`              | `Core/DTOs/Review/UpdatePseudonymizedTextRequest.cs` | Entire file — endpoint removed            |
| `UpdateEntityRequest`                         | `Core/DTOs/Review/UpdateEntityRequest.cs`            | Entire file — entity CRUD endpoints removed |
| `AddEntityRequest`                            | `Core/DTOs/Review/AddEntityRequest.cs`               | Entire file — entity CRUD endpoints removed |
| `UpdateSegmentRequest`                        | `Core/DTOs/Review/UpdateSegmentRequest.cs`           | Entire file — segment edit endpoint removed |

### 3.3 Backend DTOs to MODIFY

| DTO            | File                             | Change                                       |
| -------------- | -------------------------------- | -------------------------------------------- |
| `AuthResponse` | `Core/DTOs/Auth/AuthResponse.cs` | Remove `RefreshToken` property (line 6)      |
| `JobResponse`  | `Core/DTOs/Jobs/JobResponse.cs`  | Remove `SecondScanPassed` property (line 12) |

### 3.4 Options/Config to REMOVE

| Class              | File                                         | Change                                        |
| ------------------ | -------------------------------------------- | --------------------------------------------- |
| `GuestDemoOptions` | `Infrastructure/Options/GuestDemoOptions.cs` | Entire file — guest system eliminated         |
| `JwtOptions`       | `Infrastructure/Options/JwtOptions.cs`       | Remove `RefreshExpiryDays` property (line 11) |

### 3.5 DB Entity Changes (requires EF Core migration)

| Entity | Property                 | File:Line    | Action                                                     |
| ------ | ------------------------ | ------------ | ---------------------------------------------------------- |
| `Job`  | `SecondScanPassed`       | `Job.cs:14`  | Remove — only written by SecondScanService (being removed) |
| `Job`  | `ExportAcknowledged`     | `Job.cs:15`  | Remove — never read, never written                         |
| `Job`  | `IsGuest`                | `Job.cs:16`  | Remove — guest system eliminated                           |
| `User` | `RefreshToken`           | `User.cs:10` | Remove — refresh token never functional                    |
| `User` | `RefreshTokenExpiryTime` | `User.cs:11` | Remove — refresh token never functional                    |

**New column to ADD:**
| Entity | Property | Type | Action |
|--------|----------|------|--------|
| `Job` | `AnonymizationMap` | `string?` (JSON) | Add — stores resolved mapping for job-based anonymize |

**EF Configuration files to update:**

- `Infrastructure/Data/Configurations/UserConfiguration.cs` — Remove RefreshToken mappings (lines 19-20)
- `Infrastructure/Data/Configurations/JobConfiguration.cs` — Remove SecondScanPassed, ExportAcknowledged, IsGuest mappings (lines 24-26); add AnonymizationMap

### 3.6 Enum Values to REMOVE

**`JobStatus` enum (`Core/Domain/Enums/JobStatus.cs`):**

- `ReadyReview` (line 7) — removed; processing now transitions directly to `InReview`
- `ScanPassed` (line 10) — unreachable without SecondScanService; not in AllowedTransitions map
- `ScanFailed` (line 11) — unreachable without SecondScanService; not in AllowedTransitions map

**`ActionType` enum (`Core/Domain/Enums/ActionType.cs`):**

- `SecondScanPassed` (line 10) — only used by SecondScanService
- `SecondScanFailed` (line 11) — only used by SecondScanService
- `TextCopied` (line 12) — never logged anywhere
- `ReviewerTrainingCompleted` (line 14) — never logged anywhere
- `DpiaTriggerShown` (line 15) — never logged anywhere
- `DpiaAcknowledged` (line 16) — never logged anywhere

**`ReviewStatus` enum:**

- The entire `ReviewStatus` enum may be removable now — entity review status is no longer tracked via backend CRUD. Entities are still created during PII detection (with `Pending` status) and displayed read-only via `GET /review`, but the user no longer confirms/rejects entities on the backend. Evaluate whether to keep for display purposes or remove entirely.
- `Rejected` — unused in production, only referenced in 2 test files.

### 3.7 DI Registration Changes (`Infrastructure/DependencyInjection.cs`)

| Line | Registration                                                 | Action |
| ---- | ------------------------------------------------------------ | ------ |
| 25   | `Configure<GuestDemoOptions>`                                | Remove |
| 55   | `AddScoped<ISecondScanService, SecondScanService>`           | Remove |
| 57   | `AddScoped<IDocumentPreviewService, DocumentPreviewService>` | Remove |
| 75   | `AddSingleton<PlaygroundUsageTracker>`                       | Remove |

### 3.8 Service Code Changes

**`JwtService.cs`:**

- Remove `GenerateRefreshToken()` method (lines 44-50)
- Remove `ValidateRefreshToken()` method (lines 52-55 approximately)

**`DataRetentionBackgroundService.cs`:**

- Remove `GuestDemoOptions` constructor dependency (lines 16, 21, 27)
- Remove `RunGuestCleanupLoopAsync` method (lines 50-65)
- Remove `RunGuestCleanupAsync` method (lines 67-103)
- Remove the line launching guest cleanup from `ExecuteAsync` (line 36)

**`JobRepository.cs`:**

- Remove `!j.IsGuest` clauses in `GetByUserAsync` (line 28) and `GetByUserFilteredAsync` (line 46)
- Remove `GetExpiredGuestJobsAsync` method (lines 87-92)
- Remove `ScanPassed`/`ScanFailed` from terminal statuses array in `GetTerminalJobsOlderThanAsync` (lines 99-100)

**`IJobRepository` interface:**

- Remove `GetExpiredGuestJobsAsync` method signature

**`IReviewService` interface:**

- Remove `ReopenReviewAsync` method signature
- Remove `UpdatePseudonymizedTextAsync` method signature
- Remove `UpdateEntityAsync` method signature
- Remove `DeleteEntityAsync` method signature
- Remove `DeleteAllEntitiesAsync` method signature
- Remove `AddManualEntityAsync` method signature
- Remove `UpdateSegmentTextAsync` method signature
- Remove `CompleteReviewAsync` method signature
- Keep only `GetReviewDataAsync` (read-only, for `GET /review`)

**`ReviewService`:**

- Remove `ReopenReviewAsync` implementation
- Remove `UpdatePseudonymizedTextAsync` implementation
- Remove `UpdateEntityAsync` implementation
- Remove `DeleteEntityAsync` implementation
- Remove `DeleteAllEntitiesAsync` implementation
- Remove `AddManualEntityAsync` implementation
- Remove `UpdateSegmentTextAsync` implementation
- Remove `CompleteReviewAsync` implementation
- Remove `IPseudonymizationService` dependency (no longer needed for auto-re-pseudonymize)
- Keep only `GetReviewDataAsync` — and simplify it: remove `ReadyReview` → `InReview` transition, just return data

**`IJwtService` interface:**

- Remove `GenerateRefreshToken()` method signature
- Remove `ValidateRefreshToken()` method signature

**`JobsController.cs`:**

- Remove `ISecondScanService` dependency (line 33, 49, 64)
- Remove `IDocumentPreviewService` dependency (line 35, 51, 66)
- Remove `GuestDemoOptions` dependency (line 37, 53, 68)
- Remove `IReviewService` dependency for entity CRUD (only keep if `GET /review` stays on this controller)
- Remove `IPiiEntityRepository` dependency (entity CRUD gone)
- Remove `IsGuest` marking in `CreateJob` (lines 127-131)
- Remove `SecondScanPassed` from `MapToResponseAsync` (line 643)
- Remove ScanPassed/ScanFailed from status checks in llm-scan (line 493-494) and rescan (line 565-566)
- Remove all 14 deleted endpoint methods (8 previously identified + 6 entity/review endpoints)

**`AuthController.cs`:**

- Remove `GuestDemoOptions` dependency (line 20, 28, 35)
- Remove `PlaygroundUsageTracker` dependency (line 22, 30, 37)
- Remove refresh token generation from `Login` (lines 49-53)
- Remove `RefreshToken` from `AuthResponse` assignment (line 58)
- Remove `Refresh()`, `Guest()`, `Logout()` methods entirely

### 3.9 `appsettings.json` Changes

**Remove:**

```json
"RefreshExpiryDays": 7           // line 18
"GuestDemo": { ... }             // lines 43-48
```

**Change:**

```json
"MaxFileSizeMb": 5               // was 50, line 31
```

**Add:**

```json
"RateLimiting": {
  "BurstPermitLimit": 30,
  "BurstWindowSeconds": 60,
  "AuthenticatedDailyLimit": 100,
  "AnonymousDailyLimit": 3
},
"ApiKeys": {
  "Keys": [
    { "Key": "your-api-key", "Name": "External Frontend", "DailyLimit": 100 }
  ]
}
```

### 3.10 Frontend Code to REMOVE/CHANGE

**Remove:**

- `api/auth.ts`: `guestAuth()` function (lines 9-11)
- `stores/auth.ts`: all `refreshToken` references (lines 9, 18, 24, 30, 49, 55, 63); `apiClient.post('/auth/logout')` call (line 43)
- `api/client.ts`: 401 refresh interceptor logic (lines 22-44) — simplify to just redirect to login on 401
- `api/types.ts`: `refreshToken` in `AuthResponse` interface (line 20). Eventually replace entire file with generated types.
- `views/PlaygroundView.vue`: `ensureGuestToken` (lines 80-105), `restoreToken` (lines 205-214), guest cleanup on `onUnmounted` (lines 270-276) — rework for stateless anonymous flow
- `router/index.ts`: guest navigation guard logic (lines 52, 55-65, 72)
- `stores/review.ts`: client-side de-pseudonymization logic (lines 125-181) — replace with backend API calls

**Change:**

- `composables/useFileUpload.ts`: 50 MB → 5 MB (line 3)
- `i18n/*/playground.json`: review `guestError`, `dailyLimitReached`, `serviceUnavailable` strings — may need rewording for stateless anonymous model

### 3.11 Infrastructure Changes

- `docker/nginx/nginx.conf`: `client_max_body_size 50m` → `5m`

---

## 4. Authentication Architecture

### 4.1 Three-Tier Auth Model

```
┌─────────────┐       ┌────────────────────────────────────────┐
│ Web Frontend │──────►│ JWT Auth (existing)                    │
│ (Vue SPA)    │       │ Rate: 100/day per user + burst/min    │
└─────────────┘       └────────────────────────────────────────┘

┌─────────────┐       ┌────────────────────────────────────────┐
│ External     │──────►│ API Key Auth (X-API-Key header)        │
│ Consumer     │       │ Rate: 100/day per API key + burst/min │
└─────────────┘       └────────────────────────────────────────┘

┌─────────────┐       ┌────────────────────────────────────────┐
│ Anonymous    │──────►│ No auth                                │
│ (Playground) │       │ Rate: N/day per IP + burst/min        │
└─────────────┘       └────────────────────────────────────────┘
```

### 4.2 API Key Authentication Handler

New `src/PiiGateway.Api/Authentication/ApiKeyAuthenticationHandler.cs`:

```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    // Reads X-API-Key header
    // Validates against configured keys (appsettings.json → ApiKeys:Keys)
    // Sets ClaimsPrincipal with key name as identity
    // Returns AuthenticateResult.NoResult() if no header (falls through to JWT)
}
```

Multi-scheme setup in `Program.cs`:

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

### 4.3 Login Endpoint Simplification

After removing refresh tokens, `POST /auth/login` returns:

```json
{
  "accessToken": "eyJhbG...",
  "userId": "guid",
  "email": "user@example.com",
  "name": "User Name"
}
```

No `refreshToken` field. The frontend stores the access token and redirects to login when it expires (401 response).

---

## 5. Rate Limiting Architecture

### 5.1 Two-Layer Rate Limiting

**Layer 1 — Per-minute burst protection (DoS defense):**

- Applies to ALL requests regardless of authentication
- Sliding window, e.g. 30 requests per minute
- Partitioned by IP (anonymous) or by user/key (authenticated)

**Layer 2 — Per-day usage quotas:**

- Authenticated (JWT or API Key): 100 requests/day (configurable)
- Anonymous: 3 requests/day per IP (configurable — this replaces the hardcoded `PlaygroundUsageTracker` limit)

### 5.2 Configuration

New `appsettings.json` section:

```json
{
  "RateLimiting": {
    "BurstPermitLimit": 30,
    "BurstWindowSeconds": 60,
    "AuthenticatedDailyLimit": 100,
    "AnonymousDailyLimit": 3
  }
}
```

New `RateLimitingOptions.cs`:

```csharp
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public int BurstPermitLimit { get; set; } = 30;
    public int BurstWindowSeconds { get; set; } = 60;
    public int AuthenticatedDailyLimit { get; set; } = 100;
    public int AnonymousDailyLimit { get; set; } = 3;
}
```

### 5.3 Implementation

Replace the current 4 named sliding-window policies with 2 chained policies:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"message":"Too many requests. Please try again later."}""", ct);
    };

    // Layer 1: Per-minute burst protection
    options.AddPolicy("burst", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var key = httpContext.User.Identity?.IsAuthenticated == true
            ? $"burst:user:{httpContext.User.Identity.Name}"
            : $"burst:ip:{GetClientIp(httpContext)}";

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = opts.BurstPermitLimit,
            Window = TimeSpan.FromSeconds(opts.BurstWindowSeconds),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // Layer 2: Per-day usage quota
    options.AddPolicy("daily", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var isAuth = httpContext.User.Identity?.IsAuthenticated == true;

        var key = isAuth
            ? $"daily:user:{httpContext.User.Identity!.Name}"
            : $"daily:ip:{GetClientIp(httpContext)}";

        var limit = isAuth ? opts.AuthenticatedDailyLimit : opts.AnonymousDailyLimit;

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromHours(24),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});
```

Apply both policies to controllers:

```csharp
app.MapControllers()
    .RequireRateLimiting("burst")
    .RequireRateLimiting("daily");
```

Or use `[EnableRateLimiting("daily")]` on specific endpoints that need the daily quota (the anonymous and job anonymize/deanonymize endpoints) while the burst limiter applies globally.

### 5.4 Critical Middleware Ordering Fix

**Current (broken) — `Program.cs:125-138`:**

```
UseSecurityHeaders → UseHsts → UseHttpsRedirection → UseCors → UseRateLimiter → UseAuthentication → UseAuthorization
```

**Required:**

```
UseSecurityHeaders → UseHsts → UseHttpsRedirection → UseForwardedHeaders → UseCors → UseAuthentication → UseRateLimiter → UseAuthorization
```

Key issues to fix:

1. **`UseRateLimiter()` must come AFTER `UseAuthentication()`** — otherwise the rate limiter cannot distinguish authenticated from anonymous requests.
2. **`UseForwardedHeaders()` is missing entirely** — required for correct client IP extraction behind Nginx. Without it, all anonymous users get the same rate limit bucket (the Nginx proxy IP).

Add to `Program.cs`:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

### 5.5 Rate Limiting on Upload Endpoint

Keep a separate stricter burst limit on `POST /jobs` (file upload):

```csharp
options.AddPolicy("upload", httpContext =>
    RateLimitPartition.GetSlidingWindowLimiter(
        $"upload:{GetClientIp(httpContext)}",
        _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 2,
            QueueLimit = 0
        }));
```

---

## 6. OpenAPI / Swagger Strategy

### 6.1 Current State

- **Backend:** Swashbuckle.AspNetCore 6.* installed, enabled in Development. No rich annotations.
- **Frontend:** All TypeScript types in `api/types.ts` manually maintained (78 lines). API service files (`auth.ts`, `jobs.ts`, `review.ts`, `health.ts`) manually wrap Axios calls.

### 6.2 Recommended: `openapi-typescript` + `openapi-fetch`

**Why this over NSwag:**

1. **Lightweight** — types only, zero bundle size increase
2. **Type-safe** — compile-time verification that API calls match the spec
3. **Vue-native** — works naturally with composables and Pinia
4. **No .NET build dependency** — frontend team regenerates from a static spec file

### 6.3 Backend: Enrich the OpenAPI Spec

**Step 1 — Enable XML docs in `PiiGateway.Api.csproj`:**

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

**Step 2 — Configure Swashbuckle in `Program.cs`:**

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

**Step 3 — Annotate all controller actions** with `[ProducesResponseType]`, `[Consumes]`, and XML doc comments. Add `/// <summary>` to all DTOs.

### 6.4 Frontend: Code Generation

```bash
npm install openapi-fetch
npm install -D openapi-typescript
```

`package.json` scripts:

```json
{
  "scripts": {
    "generate:api": "openapi-typescript http://localhost:5050/swagger/v1/swagger.json --output src/api/generated/v1.d.ts --root-types",
    "generate:api:file": "openapi-typescript ../../openapi.json --output src/api/generated/v1.d.ts --root-types"
  }
}
```

Replace `api/client.ts` (Axios) with `openapi-fetch`:

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

This eliminates `api/types.ts` entirely — all types come from the generated spec.

---

## 7. File Size Limit Changes (50 MB → 5 MB)

All locations that need updating:

| Location              | Current                    | New               | File                      |
| --------------------- | -------------------------- | ----------------- | ------------------------- |
| Kestrel global        | 50 MB                      | 5 MB              | `Program.cs:14`           |
| `[RequestSizeLimit]`  | 52,428,800                 | 5,242,880         | `JobsController.cs:81`    |
| `[RequestFormLimits]` | 52,428,800                 | 5,242,880         | `JobsController.cs:82`    |
| Controller validation | `50 * 1024 * 1024`         | `5 * 1024 * 1024` | `JobsController.cs:101`   |
| appsettings.json      | 50                         | 5                 | `appsettings.json:31`     |
| Frontend composable   | 50 MB                      | 5 MB              | `useFileUpload.ts:3`      |
| Nginx                 | `client_max_body_size 50m` | `5m`              | `docker/nginx/nginx.conf` |

---

## 8. New Controller Structure (Post-Cleanup)

### `AnonymizeController.cs` (NEW)

```csharp
[ApiController]
[Route("api/v1")]
public class AnonymizeController : ControllerBase
{
    [HttpPost("anonymize")]
    [EnableRateLimiting("daily")]
    [AllowAnonymous]
    public async Task<IActionResult> Anonymize([FromBody] AnonymizeRequest request) { ... }

    [HttpPost("deanonymize")]
    [EnableRateLimiting("daily")]
    [AllowAnonymous]
    public async Task<IActionResult> Deanonymize([FromBody] DeanonymizeRequest request) { ... }
}
```

### `AuthController.cs` (Simplified)

Only `POST /auth/login` remains. No guest, no refresh, no logout.

### `JobsController.cs` (Cleaned)

- Removed 14 endpoints: reopen-review, second-scan, pseudonymized-text, document-preview, audit, all 5 entity CRUD endpoints, complete-review, segment update
- Added `POST /jobs/{id}/anonymize` (with `confirm` parameter for status transition)
- Reworked `POST /jobs/{id}/deanonymize`
- Remaining endpoints: CRUD (create/list/get/update/delete job), GET review (read-only), LLM scan (start/poll/cancel), rescan (start/poll/cancel), cancel job
- Removed dependencies on SecondScanService, DocumentPreviewService, GuestDemoOptions, IPiiEntityRepository

### `HealthController.cs` (Unchanged)

---

## 9. New DTOs

```csharp
// Anonymous stateless
public class AnonymizeRequest
{
    public required string Text { get; set; }
    public required List<AnonymizeMappingItem> Mappings { get; set; }
}

public class AnonymizeMappingItem
{
    public required string OriginalText { get; set; }
    public string? Replacement { get; set; }     // user-defined replacement
    public string? EntityType { get; set; }       // e.g. "Person", "Address" — Faker generates replacement
}

public class AnonymizeResponse
{
    public required string AnonymizedText { get; set; }
    public required List<ResolvedMapping> ResolvedMappings { get; set; }
}

public class ResolvedMapping
{
    public required string OriginalText { get; set; }
    public required string Replacement { get; set; }
    public required string Source { get; set; }    // "user" or "generated"
    public string? EntityType { get; set; }
}

public class DeanonymizeRequest
{
    public required string Text { get; set; }
    public List<DeanonymizeMappingItem>? Mappings { get; set; }  // optional for job-based
}

public class DeanonymizeMappingItem
{
    public required string OriginalText { get; set; }
    public required string Replacement { get; set; }
}

public class DeanonymizeResponse
{
    public required string DeanonymizedText { get; set; }
}
```

---

## 10. Implementation Roadmap

### Phase 1: Cleanup (Remove Dead Code)

1. Remove 8 endpoints from controllers
2. Remove SecondScanService, DocumentPreviewService, PlaygroundUsageTracker
3. Remove GuestDemoOptions, RefreshTokenRequest, CompleteReviewResultDto, SecondScanResultDto, UpdatePseudonymizedTextRequest
4. Remove refresh token code from JwtService, AuthController, Login flow
5. Remove guest code from DataRetentionBackgroundService, JobRepository, JobsController
6. Remove unused properties from Job, User, JobResponse entities
7. Remove unused enum values (JobStatus.ScanPassed/ScanFailed, 6 ActionType values)
8. Update DI registrations in DependencyInjection.cs
9. Create EF Core migration to drop columns (SecondScanPassed, ExportAcknowledged, IsGuest, RefreshToken, RefreshTokenExpiryTime)
10. Update appsettings.json (remove GuestDemo, RefreshExpiryDays)

### Phase 2: New Endpoints + Rate Limiting

1. Add `ForwardedHeaders` middleware and fix middleware ordering
2. Create `RateLimitingOptions` config class
3. Implement two-layer rate limiting (burst + daily)
4. Create `ApiKeyAuthenticationHandler` with multi-scheme auth
5. Create new DTOs for anonymize/deanonymize
6. Create `AnonymizeController` with stateless endpoints
7. Add job-based `POST /jobs/{id}/anonymize` to JobsController
8. Rework `POST /jobs/{id}/deanonymize` for new contract
9. Add `AnonymizationMap` column to Job entity (migration)
10. Update file size limits everywhere (50 MB → 5 MB)

### Phase 3: OpenAPI + Frontend Generation

1. Enrich Swashbuckle with XML docs, security definitions, `[ProducesResponseType]` annotations
2. Install `openapi-typescript` + `openapi-fetch` in frontend
3. Generate TypeScript types from OpenAPI spec
4. Replace Axios client with `openapi-fetch`
5. Migrate API service files to use generated types
6. Delete manual `types.ts`
7. Add `generate:api` npm script

### Phase 4: Frontend Rework

1. Remove guest auth flow (PlaygroundView, router guards, auth store)
2. Remove client-side de-pseudonymization logic from review store
3. Implement stateless anonymous flow in PlaygroundView (call `/api/v1/anonymize` directly)
4. Update authenticated flow to use job-based `/jobs/{id}/anonymize` and `/jobs/{id}/deanonymize`
5. Update i18n strings for new anonymous model
6. Simplify 401 interceptor (just redirect to login, no refresh attempt)

---

## Code References

### Backend — Keep

- `src/PiiGateway.Api/Program.cs` — App config, middleware (needs reordering)
- `src/PiiGateway.Api/Controllers/JobsController.cs` — Job endpoints (needs cleanup + new endpoints)
- `src/PiiGateway.Api/Controllers/AuthController.cs` — Auth (simplify to login only)
- `src/PiiGateway.Api/Controllers/HealthController.cs` — Health (unchanged)
- `src/PiiGateway.Infrastructure/Services/PseudonymizationService.cs` — Faker/Bogus generation (reuse for anonymize)
- `src/PiiGateway.Infrastructure/Services/DePseudonymizationService.cs` — Rework for new contract

### Backend — Remove Entirely

- `src/PiiGateway.Infrastructure/Services/SecondScanService.cs`
- `src/PiiGateway.Infrastructure/Services/DocumentPreviewService.cs`
- `src/PiiGateway.Infrastructure/Services/PlaygroundUsageTracker.cs`
- `src/PiiGateway.Infrastructure/Options/GuestDemoOptions.cs`
- `src/PiiGateway.Core/DTOs/Auth/RefreshTokenRequest.cs`
- `src/PiiGateway.Core/DTOs/Review/CompleteReviewResultDto.cs`
- `src/PiiGateway.Core/DTOs/Review/UpdatePseudonymizedTextRequest.cs`
- `src/PiiGateway.Core/DTOs/Export/SecondScanResultDto.cs`

### Frontend — Key Files

- `src/frontend/src/api/client.ts` — Replace Axios with openapi-fetch
- `src/frontend/src/api/types.ts` — Delete (replaced by generated types)
- `src/frontend/src/views/PlaygroundView.vue` — Rework for stateless anonymous
- `src/frontend/src/stores/review.ts` — Remove client-side de-pseudo
- `src/frontend/src/stores/auth.ts` — Remove refresh token logic
- `src/frontend/src/router/index.ts` — Remove guest guards

### Infrastructure

- `docker/nginx/nginx.conf` — File size limit
- `src/PiiGateway.Api/appsettings.json` — Config changes

---

## Open Questions

1. **API Key storage:** Start with `appsettings.json` (simple) or DB table (scalable)? Recommendation: Start with appsettings.

2. **Anonymize endpoint — PII detection:** Should the stateless `/anonymize` endpoint also offer automatic PII detection (i.e., user sends plain text, backend detects entities AND anonymizes)? Or is that always a job-based operation? Current design assumes the frontend/consumer provides the mappings.

3. **Rate limit headers:** Add `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` response headers? Recommendation: Yes, for both tiers.

4. **Swagger UI in production:** Currently disabled. Enable behind auth for partner support? Recommendation: Yes, behind API key.

5. **DB migration for existing data:** Jobs with `IsGuest=true` in the database — delete them in the migration or leave them orphaned? Recommendation: Delete all guest jobs in the migration (they're meant to be temporary anyway).

6. **Enum removal and existing audit log data:** Removing `ActionType` enum values means existing audit log records with those values will still exist in the DB. Since `ActionType` is stored as a string/varchar, this is safe — they just won't map to a C# enum value. Consider: keep the enum values as-is and just stop writing them, or clean up the audit log table too?
