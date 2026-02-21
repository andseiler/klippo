---
date: 2026-02-21T12:00:00+01:00
researcher: Claude
git_commit: 29bb5106f7bfadd4a609a736cbd42642f5e27e4d
branch: master
repository: Ainon
topic: "Complete Codebase Documentation - New Developer Onboarding Guide"
tags: [research, codebase, onboarding, architecture, api, frontend, pii-detection, infrastructure, deployment]
status: complete
last_updated: 2026-02-21
last_updated_by: Claude
---

# Research: Complete Codebase Documentation - New Developer Onboarding Guide

**Date**: 2026-02-21T12:00:00+01:00
**Researcher**: Claude
**Git Commit**: 29bb5106f7bfadd4a609a736cbd42642f5e27e4d
**Branch**: master
**Repository**: Ainon (Klippo, formerly PII Gateway)

## Research Question
Complete documentation of the entire codebase for a new developer who needs to understand and further develop the PII Gateway tool, including detailed use-case-to-file mapping.

---

## 1. What is PII Gateway?

PII Gateway is a **document pseudonymization workbench** that helps organizations safely use LLMs with sensitive documents. The core workflow is:

```
Upload Document → Extract Text → Detect PII → Human Review → Pseudonymize → Copy to LLM → Paste LLM Response → De-pseudonymize
```

**Critical legal distinction**: This tool performs **pseudonymization** (reversible, GDPR still applies), NOT anonymization. The codebase enforces this terminology — there are even automated tests (`I18nTerminologyTests.cs`) that verify the word "anonymization" never appears in any i18n file.

**Supported document types**: PDF, DOCX, XLSX, plain text
**Supported languages**: German (default) and English
**Target region**: DACH (Germany, Austria, Switzerland)

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Docker Network                           │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ Frontend  │  │  .NET    │  │ Python   │  │  PostgreSQL   │  │
│  │  Vue 3    │──│  API     │──│ PII Svc  │  │  + Redis      │  │
│  │  :8090    │  │  :5050   │  │  :8001   │  │  :5433/:6379  │  │
│  └──────────┘  └──────────┘  └──────────┘  └───────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

| Component | Technology | Location |
|---|---|---|
| Frontend | Vue 3 + TypeScript + Pinia + Tailwind CSS v4 | `src/frontend/` |
| API Gateway | ASP.NET Core 8 (Clean Architecture) | `src/PiiGateway.Api/`, `.Core/`, `.Infrastructure/` |
| PII Detection | Python FastAPI + Presidio + HuggingFace NER | `src/pii-detection-service/` |
| Database | PostgreSQL 16 | Docker service |
| Cache | Redis 7 | Docker service |
| Deployment | Docker Compose + Nginx + Let's Encrypt | `docker/`, `deploy/` |

---

## 3. Project Structure

```
Ainon/
├── src/
│   ├── PiiGateway.sln                    # .NET solution (4 projects)
│   ├── PiiGateway.Api/                   # ASP.NET Core Web API
│   │   ├── Controllers/                  # 3 controllers (Auth, Jobs, Health)
│   │   ├── Middleware/                   # SecurityHeadersMiddleware
│   │   ├── Properties/                   # launchSettings.json
│   │   └── Program.cs                    # Application bootstrap
│   ├── PiiGateway.Core/                  # Domain layer (no dependencies)
│   │   ├── Domain/
│   │   │   ├── Entities/                 # 6 entity classes
│   │   │   ├── Enums/                    # 5 enum types
│   │   │   └── JobStatusTransitions.cs   # State machine definition
│   │   ├── DTOs/                         # All request/response DTOs
│   │   │   ├── Auth/
│   │   │   ├── Jobs/
│   │   │   ├── Review/
│   │   │   ├── Export/
│   │   │   ├── Audit/
│   │   │   └── Detection/
│   │   └── Interfaces/                   # Repository & service contracts
│   │       ├── Repositories/             # 6 repository interfaces
│   │       └── Services/                 # 12 service interfaces
│   ├── PiiGateway.Infrastructure/        # Implementation layer
│   │   ├── Data/
│   │   │   ├── PiiGatewayDbContext.cs
│   │   │   ├── Configurations/           # 6 EF Core entity configs
│   │   │   └── Converters/              # EncryptedStringConverter
│   │   ├── Repositories/                 # 6 repository implementations
│   │   ├── Services/                     # 15+ service implementations
│   │   │   └── Extractors/              # PDF, DOCX, XLSX, PlainText
│   │   ├── Options/                      # 6 configuration option classes
│   │   ├── Migrations/                   # 4 EF Core migrations
│   │   └── DependencyInjection.cs        # All service registrations
│   ├── PiiGateway.Tests/                 # All tests
│   │   ├── Unit/Core/                    # Domain logic tests
│   │   ├── Unit/Extractors/              # Document extractor tests
│   │   ├── Unit/Services/                # Service layer tests
│   │   ├── Integration/                  # HTTP integration tests
│   │   ├── Security/                     # Role enforcement tests
│   │   └── Legal/                        # i18n terminology tests
│   ├── frontend/                         # Vue.js SPA
│   │   └── src/
│   │       ├── api/                      # API client + types + mocks
│   │       ├── components/               # UI components
│   │       │   ├── layout/               # App shell (sidebar, topbar)
│   │       │   ├── dashboard/            # Job table, badges
│   │       │   ├── upload/               # File upload, class selector
│   │       │   ├── workbench/            # Review workbench
│   │       │   ├── review/               # Entity highlighting, modals
│   │       │   └── ui/                   # Primitives (Button, Modal, etc.)
│   │       ├── composables/              # 7 Vue composables
│   │       ├── constants/                # Entity types, colors, statuses
│   │       ├── i18n/                     # de + en translations
│   │       ├── router/                   # Vue Router config
│   │       ├── stores/                   # 4 Pinia stores
│   │       └── views/                    # 7 view components
│   └── pii-detection-service/            # Python microservice
│       ├── app/
│       │   ├── main.py                   # FastAPI app + lifespan
│       │   ├── config.py                 # Settings (env vars)
│       │   ├── models.py                 # Pydantic models
│       │   ├── health.py                 # Health endpoint
│       │   └── detection/
│       │       ├── pipeline.py           # 3-layer orchestrator
│       │       ├── merger.py             # Conflict resolution
│       │       ├── layer1_regex/         # Presidio + custom recognizers
│       │       ├── layer2_ner/           # HuggingFace transformer
│       │       └── layer3_llm/           # Ollama/Mistral client
│       ├── evaluation/                   # Synthetic dataset + metrics
│       └── tests/                        # Comprehensive test suite
├── docker/                               # Dockerfiles + nginx + compose
├── deploy/                               # Server setup + deploy scripts
├── docs/adr/                             # ADR directory (empty)
├── technical-plan.md                     # 1627-line planning document
└── docker-compose.yml                    # Local development compose
```

---

## 4. Domain Model (Entity Relationship)

```
Organization (1)
  ├── (n) User
  │         └── referenced as CreatedBy and ReviewedBy
  └── (n) Job
            ├── (n) TextSegment
            │         └── referenced by PiiEntity.SegmentId
            ├── (n) PiiEntity
            │         ├── FK → TextSegment
            │         └── FK → User (ReviewedBy, nullable)
            └── (n) AuditLog
                      └── FK → User (Actor, nullable)
```

### Entities (`src/PiiGateway.Core/Domain/Entities/`)

| Entity | Key Fields | Purpose |
|---|---|---|
| `Organization` | Id, Name, Plan, LlmProvider, Settings | Multi-tenant anchor |
| `User` | Id, Email, PasswordHash, Role, OrganizationId, RefreshToken | Auth + tenant membership |
| `Job` | Id, Status, FileName, FileType, PseudonymizedText, IsGuest | Central aggregate for document processing |
| `TextSegment` | Id, JobId, SegmentIndex, TextContent, SourceType | Extracted document fragment |
| `PiiEntity` | Id, JobId, SegmentId, OriginalTextEnc, ReplacementText, EntityType, Confidence, ReviewStatus | Detected/manual PII span |
| `AuditLog` | Id, JobId, ActionType, EntityHash, IpAddress | Immutable action record |

### Enums (`src/PiiGateway.Core/Domain/Enums/`)

| Enum | Values |
|---|---|
| `JobStatus` | Created, Processing, ReadyReview, InReview, Pseudonymized, ScanPassed, ScanFailed, DePseudonymized, Failed |
| `ReviewStatus` | Pending, Confirmed, Rejected, AddedManual |
| `UserRole` | Admin, Reviewer, User |
| `SourceType` | Paragraph, Cell, Header, Footer, Comment, Footnote, TableOfContents, SheetName |
| `ActionType` | 22 values covering PII detection, review, export, and admin actions |

### Job State Machine (`src/PiiGateway.Core/Domain/JobStatusTransitions.cs`)

```
Created → Processing → ReadyReview → InReview → Pseudonymized → DePseudonymized
                  ↓                      ↑↓           ↑↓
                Failed              ReadyReview    InReview
                                   (reopen)       (reopen/scan fail)
```

Terminal states (no outgoing transitions): ScanPassed, ScanFailed, Failed

---

## 5. Use Case → File Mapping

### USE CASE 1: User Registration

| Step | File | Function/Method |
|---|---|---|
| Frontend form | `src/frontend/src/views/RegisterView.vue` | `handleSubmit()` |
| API call | `src/frontend/src/api/auth.ts` | `register(data)` → POST `/auth/register` |
| Store action | `src/frontend/src/stores/auth.ts` | `register(request)` → calls `apiRegister()` then `setAuth()` |
| Controller | `src/PiiGateway.Api/Controllers/AuthController.cs` | `Register()` (line 38) |
| Business logic | Same controller | Creates Organization, BCrypt-hashes password, creates User |
| Repository | `src/PiiGateway.Infrastructure/Repositories/OrganizationRepository.cs` | `CreateAsync()` |
| Repository | `src/PiiGateway.Infrastructure/Repositories/UserRepository.cs` | `CreateAsync()` |
| JWT generation | `src/PiiGateway.Infrastructure/Services/JwtService.cs` | `GenerateAccessToken(user)` |
| Token storage | `src/frontend/src/stores/auth.ts` | `setAuth()` writes to localStorage |

### USE CASE 2: User Login

| Step | File | Function/Method |
|---|---|---|
| Frontend form | `src/frontend/src/views/LoginView.vue` | `handleSubmit()` |
| API call | `src/frontend/src/api/auth.ts` | `login(data)` → POST `/auth/login` |
| Store action | `src/frontend/src/stores/auth.ts` | `login(request)` |
| Controller | `src/PiiGateway.Api/Controllers/AuthController.cs` | `Login()` (line 84) |
| Password verify | Same controller | `BCrypt.Net.BCrypt.Verify()` |
| JWT generation | `src/PiiGateway.Infrastructure/Services/JwtService.cs` | `GenerateAccessToken(user)` |
| Redirect | `src/frontend/src/views/LoginView.vue` | Navigates to `route.query.redirect` or `/dashboard` |

### USE CASE 3: Guest/Playground Access

| Step | File | Function/Method |
|---|---|---|
| Playground page | `src/frontend/src/views/PlaygroundView.vue` | Three-phase view |
| Guest auth | `src/frontend/src/api/auth.ts` | `guestAuth()` → POST `/auth/guest` |
| Controller | `src/PiiGateway.Api/Controllers/AuthController.cs` | `Guest()` (line 122) — rate limited 3/min |
| Seeded user | Migration `20260220100000_AddIsGuestAndSeedPlayground.cs` | Playground org + user seeded in DB |
| Token swap | `src/frontend/src/views/PlaygroundView.vue` | `ensureGuestToken()` saves real token, uses guest token |
| Router guard | `src/frontend/src/router/index.ts` | Guest users cannot leave playground |
| Cleanup | `src/PiiGateway.Infrastructure/Services/DataRetentionBackgroundService.cs` | `RunGuestCleanupLoopAsync()` every 15min |

### USE CASE 4: Document Upload

| Step | File | Function/Method |
|---|---|---|
| Upload UI (file) | `src/frontend/src/views/UploadView.vue` | File tab with `FileDropZone` |
| Upload UI (text) | `src/frontend/src/views/UploadView.vue` | Text tab with textarea |
| File validation | `src/frontend/src/composables/useFileUpload.ts` | `validateFile()` — 50MB max |
| Store action | `src/frontend/src/stores/jobs.ts` | `submitJob(request)` |
| API call | `src/frontend/src/api/jobs.ts` | `createJob(request)` → multipart POST `/jobs` |
| Controller | `src/PiiGateway.Api/Controllers/JobsController.cs` | `CreateJob()` — rate limited 10/min |
| SHA-256 hash | Same controller | Computes file hash |
| File storage | `src/PiiGateway.Infrastructure/Services/FileStorageService.cs` | `SaveAsync()` → `/app/uploads/{jobId}/original.ext` |
| Job creation | `src/PiiGateway.Infrastructure/Repositories/JobRepository.cs` | `CreateAsync()` |
| Audit log | `src/PiiGateway.Infrastructure/Services/AuditLogService.cs` | `LogAsync()` — ActionType.JobCreated |
| Queue | `src/PiiGateway.Infrastructure/Services/DocumentProcessingQueue.cs` | `EnqueueAsync(jobId)` — Channel<Guid> |
| Response | Controller returns `202 Accepted { id, status: "created" }` | |

### USE CASE 5: Document Processing (Background)

| Step | File | Function/Method |
|---|---|---|
| Queue consumer | `src/PiiGateway.Infrastructure/Services/DocumentProcessingBackgroundService.cs` | `ExecuteAsync()` — reads from Channel |
| Processor | `src/PiiGateway.Infrastructure/Services/DocumentProcessor.cs` | `ProcessAsync(jobId)` |
| Status: Created→Processing | Same file | `JobStatusTransitions.Validate()` |
| Extractor selection | Same file | `extractor.CanHandle(fileType)` — strategy pattern |
| PDF extraction | `src/PiiGateway.Infrastructure/Services/Extractors/PdfExtractor.cs` | Uses PdfPig library |
| DOCX extraction | `src/PiiGateway.Infrastructure/Services/Extractors/DocxExtractor.cs` | Uses DocumentFormat.OpenXml |
| XLSX extraction | `src/PiiGateway.Infrastructure/Services/Extractors/XlsxExtractor.cs` | Uses ClosedXML |
| Text extraction | `src/PiiGateway.Infrastructure/Services/Extractors/PlainTextExtractor.cs` | Fallback for everything else |
| Segment storage | `src/PiiGateway.Infrastructure/Repositories/TextSegmentRepository.cs` | `AddRangeAsync()` |
| PII detection call | `src/PiiGateway.Infrastructure/Services/PiiDetectionClient.cs` | `DetectAsync()` → POST `pii-service:8001/api/detect` |
| Entity creation | `src/PiiGateway.Infrastructure/Repositories/PiiEntityRepository.cs` | `AddRangeAsync()` |
| Preview tokens | `src/PiiGateway.Infrastructure/Services/PseudonymizationService.cs` | `GeneratePreviewTokensAsync()` |
| Status: Processing→ReadyReview | DocumentProcessor | Via `JobStatusTransitions.Validate()` |
| Error handling | DocumentProcessor | Sets `job.Status = Failed`, `job.ErrorMessage = ...` |
| Stuck job recovery | BackgroundService | `RecoverStuckJobsAsync()` on startup + every 5 min |

### USE CASE 6: PII Detection (Python Service)

| Step | File | Function/Method |
|---|---|---|
| API endpoint | `src/pii-detection-service/app/detection/router.py` | POST `/api/detect` |
| Pipeline | `src/pii-detection-service/app/detection/pipeline.py` | `detect()` — orchestrates 3 layers |
| Language detect | `src/pii-detection-service/app/detection/layer2_ner/language_detect.py` | `detect_language()` — langdetect library |
| Layer 1 (Regex) | `src/pii-detection-service/app/detection/layer1_regex/engine.py` | `run_layer1()` — Presidio + 11 custom recognizers |
| Custom recognizers | `src/pii-detection-service/app/detection/layer1_regex/recognizers.py` | DE_STEUER_ID, PHONE_DACH, IBAN, DATE_OF_BIRTH, etc. |
| Checksum validation | `src/pii-detection-service/app/detection/layer1_regex/checksums.py` | steuer_id_checksum, ahv_ean13_checksum, etc. |
| Layer 2 (NER) | `src/pii-detection-service/app/detection/layer2_ner/engine.py` | `run_layer2()` — HuggingFace `bert-base-multilingual-cased-ner-hrl` |
| Layer 3 (LLM) | `src/pii-detection-service/app/detection/layer3_llm/llm_client.py` | `detect_additional_pii()` — Ollama or Mistral API |
| LLM prompts | `src/pii-detection-service/app/detection/layer3_llm/prompts.py` | DE+EN system/user prompts |
| LLM response parse | `src/pii-detection-service/app/detection/layer3_llm/parser.py` | `parse_llm_response()` — JSON extraction |
| Merge & resolve | `src/pii-detection-service/app/detection/merger.py` | `merge_detections()` — 5 conflict resolution rules |

**Detection layers and their entity types:**

| Layer | Source | Entity Types | Confidence Range |
|---|---|---|---|
| Layer 1 (Regex) | regex / regex+checksum | DE_STEUER_ID, DE_SOZIALVERSICHERUNGSNR, PHONE_DACH, IBAN, EMAIL, DATE_OF_BIRTH, CH_AHV_NR, CH_UID, AT_SOZIALVERSICHERUNGSNR, US_SSN, UK_NATIONAL_INSURANCE, PASSPORT_NUMBER, DE_HANDELSREGISTER | 0.40-0.95 |
| Layer 2 (NER) | ner | PERSON, LOCATION, ORGANIZATION, MISC | model-dependent |
| Layer 3 (LLM) | llm | ADDRESS, HEALTH_INFO, FINANCIAL, PERSON, RELATIONSHIP, anything contextual | capped at 0.85 |

**Frontend-registered entity types** (in `entityTypes.ts`): PERSON, ADDRESS, EMAIL, PHONE_DACH, IBAN, DE_STEUER_ID, DATE_OF_BIRTH, DATE, FINANCIAL_AMOUNT, COMPANY, LICENSE_PLATE, HEALTH_INSURANCE_ID, CITY, COUNTRY

**Merge rules:**
1. Checksum detection present → boost to 0.95+
2. NER + LLM agree → boost +0.10
3. Single-source only → penalize -0.10 (floor 0.50) unless checksum
4. Overlapping spans → take widest (min start, max end)
5. Below confidence threshold (0.40) → drop entirely

### USE CASE 7: Dashboard & Job List

| Step | File | Function/Method |
|---|---|---|
| View | `src/frontend/src/views/DashboardView.vue` | `onMounted` → `fetchJobs()` |
| Store | `src/frontend/src/stores/jobs.ts` | `fetchJobs()`, `filteredJobs` (client-side search) |
| API call | `src/frontend/src/api/jobs.ts` | `listJobs(page, pageSize)` → GET `/jobs` |
| Controller | `src/PiiGateway.Api/Controllers/JobsController.cs` | `ListJobs()` (line 171) |
| Repository | `src/PiiGateway.Infrastructure/Repositories/JobRepository.cs` | `GetByOrgAsync()` / `GetByOrgFilteredAsync()` |
| Polling | `DashboardView.vue` | `setInterval(silentFetchJobs, 5000)` when processing jobs exist |
| Job table | `src/frontend/src/components/dashboard/JobTable.vue` | Renders `JobRow` for each job |
| Job row | `src/frontend/src/components/dashboard/JobRow.vue` | Click navigates to `/jobs/:id/review` |
| Delete | `src/frontend/src/stores/jobs.ts` | `deleteJob(id)` — optimistic delete, calls DELETE `/jobs/:id` |

### USE CASE 8: Document Review (The Core Workbench)

| Step | File | Function/Method |
|---|---|---|
| View | `src/frontend/src/views/ReviewView.vue` | Receives `id` prop from route |
| Data fetch | `src/frontend/src/stores/review.ts` | `fetchReviewData(id)` → GET `/jobs/:id/review` |
| API | `src/frontend/src/api/review.ts` | `getReviewData(jobId)` |
| Controller | `src/PiiGateway.Api/Controllers/JobsController.cs` | `GetReviewData()` (line 267) |
| Service | `src/PiiGateway.Infrastructure/Services/ReviewService.cs` | `GetReviewDataAsync()` — transitions ReadyReview→InReview |
| Header | `src/frontend/src/components/workbench/WorkbenchHeader.vue` | Shows status, entity counts, primary CTA |
| Document viewer | `src/frontend/src/components/workbench/WorkbenchDocumentViewer.vue` | Three modes: original/pseudonymized/depseudonymized |
| Segment rendering | `src/frontend/src/components/review/SegmentRenderer.vue` | Splits text into fragments with entity highlights |
| Entity highlight | `src/frontend/src/components/review/EntityHighlight.vue` | Clickable `<mark>` with confidence coloring |
| Right rail | `src/frontend/src/components/workbench/WorkbenchRightRail.vue` | Tabs: entities/rules/export/mapping |
| Keyboard nav | `src/frontend/src/composables/useReviewNavigation.ts` | Tab/Shift+Tab/Escape for entity navigation |
| Training modal | `src/frontend/src/composables/useReviewerTraining.ts` | 4-step first-time reviewer onboarding |

### USE CASE 9: Entity Review Actions

| Action | Frontend | API | Backend Service |
|---|---|---|---|
| **Confirm entity** | `review.ts` → `changeEntityType()` or direct | `updateEntity()` → PATCH `/jobs/:id/entities/:eid` | `ReviewService.UpdateEntityAsync()` |
| **Delete entity** | `review.ts` → `deleteEntityGroup()` | `deleteEntity()` → DELETE `/jobs/:id/entities/:eid` | `ReviewService.DeleteEntityAsync()` |
| **Delete all entities** | `review.ts` → `deleteAllEntities()` | `deleteAllEntities()` → DELETE `/jobs/:id/entities` | `ReviewService.DeleteAllEntitiesAsync()` |
| **Add manual entity** | `review.ts` → `addManualEntity()` | `addEntity()` → POST `/jobs/:id/entities` | `ReviewService.AddManualEntityAsync()` (supports optional `ReplacementText`) |
| **Change entity type** | `review.ts` → `changeEntityType()` | `updateEntity()` → PATCH `/jobs/:id/entities/:eid` | `ReviewService.UpdateEntityAsync()` |
| **Resize entity span** | `review.ts` → `changeEntitySpan()` | `updateEntity()` → PATCH `/jobs/:id/entities/:eid` | `ReviewService.UpdateEntityAsync()` |
| **Edit segment text** | `review.ts` → `updateSegmentText()` | `updateSegment()` → PATCH `/jobs/:id/segments/:sid` | `ReviewService.UpdateSegmentTextAsync()` |
| **Edit replacement token** | `review.ts` → `updateReplacementByToken()` | `updateEntity()` → PATCH `/jobs/:id/entities/:eid` | `ReviewService.UpdateEntityAsync()` |

**Text selection for manual add:**
- Composable: `src/frontend/src/composables/useTextSelection.ts` — `handleMouseUp()` uses DOM TreeWalker to compute character offsets within `[data-segment-id]` elements
- UI: `src/frontend/src/components/workbench/EntitiesTabContent.vue` — quick-tag buttons for PERSON, ADDRESS, PHONE_DACH, DE_STEUER_ID + full dropdown

### USE CASE 10: Complete Review & Pseudonymization

| Step | File | Function/Method |
|---|---|---|
| Complete review button | `WorkbenchHeader.vue` | Primary CTA when status=inreview |
| Modal | `src/frontend/src/components/review/CompleteReviewModal.vue` | 3-4 mandatory checkboxes |
| Store action | `src/frontend/src/stores/review.ts` | `submitCompleteReview(id)` — calls `fetchReviewData` after, which syncs `reviewCompleted` from backend status |
| API | `src/frontend/src/api/review.ts` | `completeReview(jobId)` → POST `/jobs/:id/complete-review` |
| Controller | `src/PiiGateway.Api/Controllers/JobsController.cs` | `CompleteReview()` (line 410) |
| Service | `src/PiiGateway.Infrastructure/Services/ReviewService.cs` | `CompleteReviewAsync()` — auto-confirms pending, pseudonymizes |
| Pseudonymization | `src/PiiGateway.Infrastructure/Services/PseudonymizationService.cs` | `PseudonymizeJobAsync()` |
| Fake data gen | Same service | Uses **Bogus** library with deterministic seed from jobId |
| IBAN generation | Same service | `GenerateSyntheticIban()` with ISO 7064 mod-97 check digits |
| Status transition | InReview → Pseudonymized | `job.PseudonymizedAt = DateTime.UtcNow` |

**Pseudonymization replacement types:**

| Entity Type | Replacement Method | Example |
|---|---|---|
| PERSON/NAME/PER | `faker.Name.FullName()` | Max Mustermann → Felix Bauer |
| ORGANIZATION | `faker.Company.CompanyName()` | ACME GmbH → Müller & Söhne |
| LOCATION/LOC/GPE | `faker.Address.City()` | Berlin → Hamburg |
| ADDRESS | `faker.Address.StreetAddress()` | Hauptstr. 1 → Gartenweg 42 |
| EMAIL | `faker.Internet.Email()` | max@test.de → felix@example.com |
| PHONE | `faker.Phone.PhoneNumber()` | +49 123 → +49 456 |
| IBAN | Synthetic with valid check digits | DE89... → DE12... |
| DATE | `faker.Date.Past(5).ToString("dd.MM.yyyy")` | 15.03.1990 → 22.07.2021 |
| Unknown | `[TYPE_NNN]` placeholder | [STEUER_ID_001] |

### USE CASE 11: LLM Scan (Additional Detection)

| Step | File | Function/Method |
|---|---|---|
| Trigger button | `WorkbenchHeader.vue` | "AI Scan" button for post-processing statuses |
| Modal | `src/frontend/src/components/workbench/LlmScanModal.vue` | Configure → Scanning → Results |
| API start | `src/frontend/src/api/review.ts` | `startLlmScan()` → POST `/jobs/:id/llm-scan` |
| Controller | `src/PiiGateway.Api/Controllers/JobsController.cs` | `StartLlmScan()` (line 523) |
| Singleton service | `src/PiiGateway.Infrastructure/Services/LlmScanService.cs` | `StartScan()` — fire-and-forget `Task.Run` |
| Batch processing | Same service | Processes segments in batches of 5, calls PII service with `layers=["llm"]` |
| Polling | `LlmScanModal.vue` | `getLlmScanStatus()` every 2000ms |
| Status endpoint | `src/PiiGateway.Api/Controllers/JobsController.cs` | `GetLlmScanStatus()` (line 545) |
| Results | `LlmScanModal.vue` | Checkbox table of detections, apply/dismiss |

### USE CASE 12: Export (Copy Pseudonymized Text)

| Step | File | Function/Method |
|---|---|---|
| Mode toggle | `src/frontend/src/components/workbench/WorkbenchModeToggle.vue` | Switch to "Pseudonymized" |
| Full text computed | `src/frontend/src/stores/review.ts` | `pseudonymizedFullText` getter — builds from segments+entities |
| Token mapping | `src/frontend/src/components/workbench/TokenMappingTable.vue` | Shows original→token table |
| Copy button | `src/frontend/src/components/workbench/ExportTabContent.vue` | `navigator.clipboard.writeText()` |
| Inline token edit | `TokenMappingTable.vue` | Click-to-edit token values |

### USE CASE 13: De-pseudonymization

| Step | File | Function/Method |
|---|---|---|
| Mode toggle | `WorkbenchModeToggle.vue` | Switch to "De-pseudonymized" |
| Text input | `WorkbenchDocumentViewer.vue` | Textarea for pasting LLM response |
| Client-side reverse | `src/frontend/src/stores/review.ts` | `depseudoReplacementMap`, `depseudoOutputFragments` — regex-based token replacement |
| Copy output | `src/frontend/src/components/workbench/MappingTabContent.vue` | Copy de-pseudonymized text |
| Server-side API | `src/frontend/src/api/review.ts` | Not currently used; backend endpoint exists at POST `/jobs/:id/deanonymize` |
| Backend service | `src/PiiGateway.Infrastructure/Services/DePseudonymizationService.cs` | `DePseudonymizeAsync()` — longest-first replacement with name variants |

**De-pseudonymization name variant logic** (backend):
- For PERSON entities: registers surname-only and `Herr/Frau/Mr/Mrs/Ms/Dr + surname` variants

### USE CASE 14: Second Scan (Quality Gate)

| Step | File | Function/Method |
|---|---|---|
| Trigger | `src/PiiGateway.Api/Controllers/JobsController.cs` | `TriggerSecondScan()` (line 447) → POST `/jobs/:id/second-scan` |
| Service | `src/PiiGateway.Infrastructure/Services/SecondScanService.cs` | `RunSecondScanAsync()` |
| Allowlist | Same service | Builds set of all known `ReplacementText` values |
| Detection | Same service | Sends pseudonymized text as single segment to PII service |
| Filtering | Same service | Removes detections matching allowlist OR confidence < 0.70 |
| Pass → ScanPassed | Same service | `job.SecondScanPassed = true`, `Status = ScanPassed` |
| Fail → InReview | Same service | Creates new PiiEntity records, returns job to InReview |

### USE CASE 15: Audit Trail

| Step | File | Function/Method |
|---|---|---|
| API endpoint | `src/PiiGateway.Api/Controllers/JobsController.cs` | `GetAuditTrail()` (line 558) → GET `/jobs/:id/audit` |
| Repository | `src/PiiGateway.Infrastructure/Repositories/AuditLogRepository.cs` | `GetByJobIdAsync()` |
| Logging service | `src/PiiGateway.Infrastructure/Services/AuditLogService.cs` | `LogAsync()` — SHA-256 hashes PII text, never stores raw |
| All mutations | Every service that modifies entities | Passes `userId` and `ipAddress` for audit context |

### USE CASE 16: Health Check

| Step | File | Function/Method |
|---|---|---|
| Controller | `src/PiiGateway.Api/Controllers/HealthController.cs` | `GetHealth()` → GET `/health` |
| DB probe | Same controller | `_dbContext.Database.CanConnectAsync()` |
| PII service probe | Same controller | HTTP GET to `pii-service:8001/health` |
| Redis probe | Same controller | Write/read `__health_check__` key |
| Python health | `src/pii-detection-service/app/health.py` | Returns available layers list |

### Workbench UX Features (added 2026-02-21)

**Progress Bar for Playground Processing**
- `composables/useEstimatedProgress.ts` — exponential ease-out curve: `progress = maxProgress * (1 - e^(-elapsed / tau))`. Params: `estimatedDurationMs` (10s), `maxProgress` (95%). Exposes `progress`, `elapsedSeconds`, `start()`, `complete()`, `reset()`
- `PlaygroundView.vue` Phase 2 template shows `<AppProgress>` bar and elapsed time counter during job polling

**Text Search & Highlight (Ctrl+F)**
- `composables/useDocumentSearch.ts` — searches across all segments for a query (case-insensitive), marks each match with `overlapsEntity` flag. Exposes `searchQuery`, `isSearchOpen`, `allMatches`, `actionableMatches` (non-overlapping), `matchesBySegment`, `currentMatchIndex`, `nextMatch()`, `prevMatch()`, `toggleSearch()`, `closeSearch()`
- `components/workbench/DocumentSearchBar.vue` — search input with prev/next navigation, match counter, "Add all as PII" button with entity type selector
- `SegmentRenderer.vue` accepts `searchMatches` prop, renders `'search-match'` fragments as `<mark>` with yellow/orange highlights
- `review.ts` store: `addSearchMatchesAsEntities()` action — batch-adds non-overlapping matches as entities

**PII Selection in Pseudonymized View**
- `useTextSelection.ts` — `handleMouseUp(event, mode)` now accepts `'pseudonymized'` mode; reads `data-offset-start` from text span ancestors to compute original-text offsets; rejects selection inside entity highlights
- `WorkbenchDocumentViewer.vue` — `@mouseup` fires for all modes except depseudonymized
- `WorkbenchRightRail.vue` — pseudonymized mode shows both `entities` and `export` tabs
- `SegmentRenderer.vue` — text `<span>` elements carry `data-offset-start`/`data-offset-end` attributes for offset mapping

**Manual PII Addition via Free Text**
- `components/workbench/AddPiiByTextPanel.vue` — expandable panel with two-column layout (Klartext + Verfremdet inputs), real-time occurrence scanner (marks overlapping matches), entity type selector with auto-generated faker replacement values, bulk "Add all" button that passes custom replacement text to backend
- `utils/fakerGenerator.ts` — client-side faker utility (`generateFakeValue(entityType)`) using `@faker-js/faker/locale/de` to mirror backend's `GenerateReplacementInternal` entity type switch-case
- Integrated into both `EntitiesTabContent.vue` and `ExportTabContent.vue`

**Optional Entity Type in AddPiiByTextPanel**
- Entity type dropdown is no longer required when the user provides both Klartext and Verfremdet text manually. The "Add" button enables when replacement text is non-empty, regardless of entity type selection. When no type is selected, `'CUSTOM'` is used as the entity type label.

**Synchronized Scrolling (Original ↔ Pseudonymized)**
- `composables/useScrollSync.ts` — proportional scroll sync composable. Maps `scrollTop / maxScroll` ratio between two elements using `requestAnimationFrame` and an `isSyncing` flag to prevent feedback loops. Passive scroll listeners for performance.
- Wired into both `ReviewView.vue` and `PlaygroundView.vue` via template refs on `WorkbenchDocumentViewer` and `PseudonymizedTextPanel` components. Both components expose their scrollable container via `defineExpose({ scrollContainer })`.

**Delete All Mappings**
- `WorkbenchRightRail.vue` header includes "Alle löschen" / "Delete all" button with inline confirmation (cancel X + "Wirklich löschen?")
- Calls `DELETE /jobs/{id}/entities` endpoint → `ReviewService.DeleteAllEntitiesAsync` → bulk delete + auto re-pseudonymize

**Unified TokenMappingTable**
- `ExportTabContent.vue` — now shows `TokenMappingTable` with `:show-delete="true"` and `AddPiiByTextPanel` for full add/delete capability in pseudonymized mode

---

## 6. Authentication & Authorization

### JWT Authentication

| Component | File |
|---|---|
| JWT setup | `src/PiiGateway.Api/Program.cs` (lines 19-38) |
| Token generation | `src/PiiGateway.Infrastructure/Services/JwtService.cs` |
| Options | `src/PiiGateway.Infrastructure/Options/JwtOptions.cs` |
| Frontend interceptor | `src/frontend/src/api/client.ts` — attaches Bearer token |
| 401 auto-refresh | `src/frontend/src/api/client.ts` — response interceptor |

**JWT Claims:**
- `sub` = User.Id
- `email` = User.Email
- `ClaimTypes.Role` = User.Role
- `org_id` = User.OrganizationId
- `jti` = unique token ID

### Authorization Policies

| Policy | Required Roles | Used On |
|---|---|---|
| `RequireAdmin` | Admin only | DELETE `/jobs/:id` |
| `RequireReviewer` | Admin or Reviewer | All entity CRUD, confirm, complete, reopen, second-scan, deanonymize, llm-scan |
| `RequireUser` | Any authenticated | Defined but not explicitly used (base `[Authorize]` covers this) |

### Rate Limiting

| Limiter | Limit | Applies To |
|---|---|---|
| `global` | 100 req/min | All endpoints by default |
| `upload` | 10 req/min | POST `/jobs` (file upload) |
| `guest` | 3 req/min | POST `/auth/guest` |

---

## 7. Data Encryption

### PII At-Rest Encryption

| Component | File |
|---|---|
| AES-GCM service | `src/PiiGateway.Infrastructure/Services/AesGcmEncryptionService.cs` |
| EF Core converter | `src/PiiGateway.Infrastructure/Data/Converters/EncryptedStringConverter.cs` |
| Configuration | `EncryptionOptions.Key` — Base64-encoded 256-bit key |
| Applied to | `PiiEntity.OriginalTextEnc` column — transparent encrypt/decrypt |
| Initialization | `DependencyInjection.InitializeEncryption()` — called after `app.Build()`, before any DB access |

**Algorithm**: AES-256-GCM with 12-byte random nonce + 16-byte auth tag. Format: `Base64(nonce || ciphertext || tag)`.

### Audit Log Security
- PII text is never stored in audit logs
- Only SHA-256 hash stored as `EntityHash` (64-char hex)
- `IpAddress` stored for compliance but excluded from API response DTOs

---

## 8. Frontend Architecture

### Routing (`src/frontend/src/router/index.ts`)

| Path | View | Layout | Auth |
|---|---|---|---|
| `/login` | LoginView | bare | No |
| `/register` | RegisterView | bare | No |
| `/dashboard` | DashboardView | AppLayout | Yes |
| `/upload` | UploadView | AppLayout | Yes |
| `/jobs/:id/review` | ReviewView | bare | Yes |
| `/playground` | PlaygroundView | bare | No |
| `/:pathMatch(.*)` | NotFoundView | bare | No |

### Pinia Stores

| Store | File | Purpose |
|---|---|---|
| `useAuthStore` | `stores/auth.ts` | JWT tokens, user info, login/register/logout |
| `useJobsStore` | `stores/jobs.ts` | Job CRUD, pagination, client-side search |
| `useReviewStore` | `stores/review.ts` | **Largest store** — full review workbench state, entity manipulation, pseudonymization views |
| `useUiStore` | `stores/ui.ts` | Sidebar collapse, mobile menu |

### Composables

| Composable | File | Purpose |
|---|---|---|
| `useDarkMode` | `composables/useDarkMode.ts` | Dark/light toggle via @vueuse/core |
| `useFileUpload` | `composables/useFileUpload.ts` | File validation (50MB max), drag state |
| `useTextSelection` | `composables/useTextSelection.ts` | DOM text selection → character offsets for manual entity add |
| `useReviewNavigation` | `composables/useReviewNavigation.ts` | Tab/Shift+Tab/Escape keyboard shortcuts |
| `useReviewerTraining` | `composables/useReviewerTraining.ts` | 4-step onboarding modal, localStorage persistence |
| `useClipboardCopy` | `composables/useClipboardCopy.ts` | Copy with 2s success indicator |
| `useEstimatedProgress` | `composables/useEstimatedProgress.ts` | Fake progress bar with exponential ease-out curve (0→95%→100%) for processing phases |
| `useDocumentSearch` | `composables/useDocumentSearch.ts` | Text search across document segments with entity overlap detection, match navigation |
| `useScrollSync` | `composables/useScrollSync.ts` | Proportional scroll sync between two scrollable elements (original ↔ pseudonymized panels) |

### Component Hierarchy

```
App.vue
├── AppLayout (sidebar routes)
│   ├── AppSidebar → SidebarNavItem (×2)
│   ├── AppTopBar → LanguageToggle, UserMenu
│   └── <slot>
│       ├── DashboardView → JobTable → JobRow (×N)
│       └── UploadView → FileDropZone
└── bare layout routes
    ├── LoginView, RegisterView, NotFoundView
    ├── ReviewView
    │   ├── WorkbenchHeader
    │   ├── WorkbenchDocumentViewer → DocumentSearchBar, SegmentRenderer (×N) → EntityHighlight (×N)
    │   ├── WorkbenchRightRail → EntitiesTab (+ AddPiiByTextPanel) | RulesTab | ExportTab (+ AddPiiByTextPanel) | MappingTab
    │   └── Modals: ReviewerTraining, CompleteReview, LlmScan
    └── PlaygroundView (same workbench components, 3-phase, with progress bar)
```

### i18n

- Default locale: **German** (`de`)
- Fallback: English (`en`)
- 7 namespace files per locale: common, auth, upload, dashboard, job, review, playground
- Setup: `src/frontend/src/i18n/index.ts`

---

## 9. Database

### PostgreSQL Schema (6 tables)

| Table | PK Type | Key Relationships |
|---|---|---|
| `organizations` | uuid | Parent of users and jobs |
| `users` | uuid | FK → organizations (CASCADE), unique email index |
| `jobs` | uuid | FK → organizations (CASCADE), FK → users (RESTRICT), composite index `(org_id, status)` |
| `text_segments` | uuid | FK → jobs (CASCADE) |
| `pii_entities` | uuid | FK → jobs (CASCADE), FK → text_segments (CASCADE), composite index `(job_id, review_status)` |
| `audit_log` | bigint IDENTITY | FK → jobs (RESTRICT), FK → users (SET NULL), index on `job_id` |

### Migrations (`src/PiiGateway.Infrastructure/Migrations/`)

| Migration | Date | Changes |
|---|---|---|
| `InitialCreate` | 2026-02-18 | All 6 tables, indexes, FKs |
| `AddJobErrorMessage` | 2026-02-18 | `error_message varchar(2000)` on jobs |
| `AddPseudonymizedText` | 2026-02-18 | `pseudonymized_text text` on jobs |
| `AddIsGuestAndSeedPlayground` | 2026-02-20 | `is_guest boolean` on jobs + Playground org/user seed data |

### Redis Usage
- Distributed cache (registered but not heavily used in current code)
- Health check probe writes/reads `__health_check__` key

---

## 10. Configuration

### .NET Configuration (`src/PiiGateway.Api/appsettings.json`)

| Section | Key Settings |
|---|---|
| `ConnectionStrings` | PostgreSQL, Redis connection strings |
| `Jwt` | Secret (64+ chars), Issuer/Audience ("PiiGateway"), ExpiryMinutes (60), RefreshExpiryDays (7) |
| `PiiService` | BaseUrl ("http://pii-service:8001"), TimeoutSeconds (120), Layers (["regex","ner"]) |
| `FileStorage` | BasePath ("/app/uploads"), MaxFileSizeMb (50) |
| `GuestDemo` | Enabled (true), OrganizationId, UserId, TokenExpiryMinutes (60), CleanupIntervalMinutes (15) |
| `Cors` | AllowedOrigins ("http://localhost:5173") |

### Python Configuration (`src/pii-detection-service/app/config.py`)

| Env Var | Default | Purpose |
|---|---|---|
| `LLM_BACKEND` | `disabled` | `disabled`, `ollama`, or `mistral` |
| `MISTRAL_API_KEY` | `""` | Mistral cloud API key |
| `OLLAMA_URL` | `http://host.docker.internal:11434` | Local Ollama |
| `NER_MODEL_NAME` | `Davlan/bert-base-multilingual-cased-ner-hrl` | HuggingFace model |
| `LAYER2_ENABLED` | `True` | Toggle NER layer |

---

## 11. Docker & Deployment

### Running Locally

```bash
cp .env.example .env
# Edit .env (optionally set LLM_BACKEND=mistral and MISTRAL_API_KEY)
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:8090 |
| API | http://localhost:5050 |
| Swagger | http://localhost:5050/swagger (Development only) |
| PostgreSQL | localhost:5433 |
| Redis | localhost:6379 |

### Key Docker Files

| File | Purpose |
|---|---|
| `docker-compose.yml` | Development (5 services, exposed ports, hot-reload) |
| `docker/docker-compose.prod.yml` | Production (+ nginx, certbot, memory limits, TLS) |
| `docker/Dockerfile.api` | .NET multi-stage build |
| `docker/Dockerfile.frontend` | Vue dev server |
| `docker/Dockerfile.frontend.prod` | Vue build → nginx |
| `docker/Dockerfile.pii-service` | Python + PyTorch + spaCy + HuggingFace model |
| `docker/nginx/nginx.conf` | Reverse proxy, TLS, rate limiting, SPA routing |

### CI/CD Workflows (`.github/workflows/`)

| Workflow | Trigger | What It Does |
|---|---|---|
| `dotnet.yml` | Push/PR to `src/PiiGateway.*/**` | Build, test .NET |
| `python.yml` | Push/PR to `src/pii-detection-service/**` | Lint, type-check, test Python |
| `frontend.yml` | Push/PR to `src/frontend/**` | Type-check, build Vue |
| `docker.yml` | Push to main | Build + push 3 Docker images to ghcr.io |
| `deploy.yml` | Manual trigger | Build images → SSH deploy to production |

---

## 12. Testing

### Test Structure (`src/PiiGateway.Tests/`)

| Category | Count | What's Tested |
|---|---|---|
| Unit/Core | ~20 tests | JobStatusTransitions |
| Unit/Extractors | ~32 tests | PDF, DOCX, XLSX, PlainText extraction |
| Unit/Services | ~66 tests | Pseudonymization, DePseudonymization, Encryption, DocumentProcessor, ReviewService, DocumentPreview, SecondScan |
| Integration | ~10 tests | HTTP endpoints via WebApplicationFactory |
| Security | ~8 tests | Role enforcement on all protected endpoints |
| Legal | 2 tests | i18n terminology enforcement |

### Python Tests (`src/pii-detection-service/tests/`)

| File | Coverage |
|---|---|
| `test_checksums.py` | All 10 checksum functions |
| `test_recognizers.py` | All 11 custom regex recognizers |
| `test_layer1.py` | Full Presidio engine integration |
| `test_layer2.py` | Language detection + NER |
| `test_layer3.py` | LLM client, parser, prompts |
| `test_merger.py` | All 5 merge rules |
| `test_pipeline.py` | End-to-end pipeline |
| `test_health.py` | HTTP endpoints |
| `test_contract.py` | CamelCase serialization |
| `test_red_team.py` | Adversarial inputs |

### Running Tests

```bash
# .NET tests (requires PostgreSQL at localhost:5433)
dotnet test src/PiiGateway.Tests

# Python tests
cd src/pii-detection-service && pytest tests/ -v

# Frontend type check
cd src/frontend && npx vue-tsc --noEmit
```

---

## 13. Key Architectural Patterns

| Pattern | Where | How |
|---|---|---|
| Clean Architecture | .NET projects | Core (no deps) → Infrastructure (implementations) → Api (controllers) |
| Repository Pattern | Infrastructure/Repositories/ | All DB access through interfaces |
| Strategy Pattern | Document extractors | `IDocumentExtractor.CanHandle()` selects appropriate extractor |
| Channel-based Queue | DocumentProcessingQueue | Bounded `Channel<Guid>` (capacity 100) for async job processing |
| Value Converter | EncryptedStringConverter | Transparent AES-GCM encryption at EF Core layer |
| State Machine | JobStatusTransitions | Explicit allowed-transitions dictionary with `Validate()`/`IsValid()` |
| Scoped-in-Singleton | Background services | `IServiceScopeFactory` for DB access in singleton/hosted services |
| Deterministic Pseudonymization | PseudonymizationService | Bogus seed = `BitConverter.ToInt32(jobId.ToByteArray(), 0)` |
| Optimistic UI Updates | Frontend stores | Delete/update local state first, revert on API error |
| Composable Pattern | Vue composables | Reusable stateful logic (file upload, text selection, dark mode) |
| i18n by Default | Frontend | German default, English fallback, 7 namespace files per locale |

---

## 14. Important Files Quick Reference

### When you need to...

| Task | Go to |
|---|---|
| Add a new API endpoint | `src/PiiGateway.Api/Controllers/JobsController.cs` |
| Add a new entity/table | `src/PiiGateway.Core/Domain/Entities/` + `Infrastructure/Data/Configurations/` + create migration |
| Add a new service | Interface in `Core/Interfaces/Services/`, impl in `Infrastructure/Services/`, register in `Infrastructure/DependencyInjection.cs` |
| Add a new PII recognizer | `src/pii-detection-service/app/detection/layer1_regex/recognizers.py` + `checksums.py` |
| Add a new document extractor | `Infrastructure/Services/Extractors/`, implement `IDocumentExtractor`, register in `DependencyInjection.cs` |
| Add a frontend route | `src/frontend/src/router/index.ts` |
| Add a new view | `src/frontend/src/views/` + register route |
| Add a new store | `src/frontend/src/stores/` |
| Add translations | `src/frontend/src/i18n/en/*.json` + `de/*.json` |
| Add a new entity type | `src/frontend/src/constants/entityTypes.ts` + `entityTypeColors.ts` + i18n `review.entityTypes.*` + `PseudonymizationService.cs` switch |
| Modify job status flow | `src/PiiGateway.Core/Domain/JobStatusTransitions.cs` |
| Change Docker config | `docker-compose.yml` (dev) or `docker/docker-compose.prod.yml` (prod) |
| Create a DB migration | `dotnet ef migrations add MigrationName -p src/PiiGateway.Infrastructure -s src/PiiGateway.Api` |

---

## Code References

### .NET Backend
- `src/PiiGateway.Api/Program.cs` — Application bootstrap, middleware, auth, CORS, rate limiting
- `src/PiiGateway.Api/Controllers/AuthController.cs` — Registration, login, guest, logout
- `src/PiiGateway.Api/Controllers/JobsController.cs` — All job CRUD + review + scan + export operations
- `src/PiiGateway.Api/Controllers/HealthController.cs` — System health probe
- `src/PiiGateway.Core/Domain/Entities/Job.cs` — Central aggregate entity
- `src/PiiGateway.Core/Domain/JobStatusTransitions.cs` — State machine
- `src/PiiGateway.Infrastructure/DependencyInjection.cs` — All service registrations
- `src/PiiGateway.Infrastructure/Services/DocumentProcessor.cs` — 7-step processing pipeline
- `src/PiiGateway.Infrastructure/Services/ReviewService.cs` — Review workflow logic
- `src/PiiGateway.Infrastructure/Services/PseudonymizationService.cs` — Fake data generation
- `src/PiiGateway.Infrastructure/Services/DePseudonymizationService.cs` — Reverse mapping
- `src/PiiGateway.Infrastructure/Services/SecondScanService.cs` — Quality gate scan
- `src/PiiGateway.Infrastructure/Services/LlmScanService.cs` — Additional AI detection
- `src/PiiGateway.Infrastructure/Services/AesGcmEncryptionService.cs` — PII encryption

### Python PII Service
- `src/pii-detection-service/app/main.py` — FastAPI app, 3-layer startup
- `src/pii-detection-service/app/detection/pipeline.py` — Layer orchestration
- `src/pii-detection-service/app/detection/merger.py` — Conflict resolution (5 rules)
- `src/pii-detection-service/app/detection/layer1_regex/recognizers.py` — 11 custom recognizers
- `src/pii-detection-service/app/detection/layer1_regex/checksums.py` — Validation algorithms
- `src/pii-detection-service/app/detection/layer2_ner/engine.py` — HuggingFace NER
- `src/pii-detection-service/app/detection/layer3_llm/llm_client.py` — LLM integration

### Frontend
- `src/frontend/src/router/index.ts` — All routes + navigation guards
- `src/frontend/src/stores/review.ts` — Core review workbench state
- `src/frontend/src/stores/auth.ts` — Authentication state
- `src/frontend/src/api/client.ts` — Axios config + auth interceptors
- `src/frontend/src/views/ReviewView.vue` — Main workbench view
- `src/frontend/src/views/PlaygroundView.vue` — Guest demo flow
- `src/frontend/src/components/review/SegmentRenderer.vue` — Document rendering
- `src/frontend/src/components/review/EntityHighlight.vue` — PII highlighting

---

## Open Questions

1. The `POST /auth/refresh` endpoint is a stub — always returns 401. Token refresh works via the client interceptor but the backend endpoint is not fully implemented.
2. No OCR support — scanned PDFs are explicitly rejected by the PdfExtractor.
3. The `docs/adr/` directory is empty — no Architecture Decision Records have been documented yet.
4. The technical plan mentions React as the frontend framework, but the implementation uses Vue 3 — the plan diverged during implementation.
5. The `Organization.Settings` field (JSON) and `Organization.LlmProvider` field are defined but appear unused in the current codebase.

---

## Addendum: Cancel Scans, Estimated Progress, and Rescan (2026-02-21)

### New `Cancelled` Job Status

- Added `Cancelled` to `JobStatus` enum (`src/PiiGateway.Core/Domain/Enums/JobStatus.cs`)
- Updated `JobStatusTransitions`: `Created -> Cancelled` and `Processing -> Cancelled` are now valid
- No DB migration needed (column is `varchar(50)`)

### `JobCancellationRegistry` Singleton

- New file: `src/PiiGateway.Infrastructure/Services/JobCancellationRegistry.cs`
- `ConcurrentDictionary<Guid, CancellationTokenSource>` with `Register(jobId)`, `Cancel(jobId)`, `Remove(jobId)` methods
- Registered as singleton in `DependencyInjection.cs`

### CancellationToken Threading

- `IPiiDetectionClient.DetectAsync` now accepts `CancellationToken ct = default`
- `PiiDetectionClient` passes token to `PostAsJsonAsync`
- `DocumentProcessor` injects `JobCancellationRegistry`, registers token at start, checks before detection call, catches `OperationCanceledException` → sets status to `Cancelled`, `finally` block removes token

### New API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/jobs/{id}/cancel` | Cancel a job in Created/Processing status |
| `DELETE` | `/api/v1/jobs/{id}/llm-scan` | Cancel a running LLM scan |
| `POST` | `/api/v1/jobs/{id}/rescan` | Start regex/NER rescan |
| `GET` | `/api/v1/jobs/{id}/rescan` | Poll rescan status |
| `DELETE` | `/api/v1/jobs/{id}/rescan` | Cancel running rescan |

### `LlmScanService` Changes

- Now stores `ScanEntry(LlmScanResponse, CancellationTokenSource)` per scan instead of just `LlmScanResponse`
- New `CancelScan(Guid jobId)` method added to `ILlmScanService` interface
- CancellationToken passed through `RunScanAsync`, checked before each batch, passed to `DetectAsync`
- Catches `OperationCanceledException` → sets status to `"cancelled"`

### New `RescanService` Singleton

- New file: `src/PiiGateway.Infrastructure/Services/RescanService.cs`
- Follows same pattern as `LlmScanService`: `ConcurrentDictionary`, `Task.Run`, batch processing, progress tracking
- Uses layers `["regex", "ner"]`, `BatchSize = 10`
- Deduplicates detections against existing entities (same segment + same start/end offsets)
- Includes cancellation support from the start

### Frontend Changes

- `useEstimatedProgress.ts`: Fixed `elapsedSeconds` bug (was `computed` using `Date.now()` which is not reactive; now a `ref` updated in `tick()`). Added `wordCount` parameter to `start()` for word-count-based estimation.
- `LlmScanModal.vue`: Added `mode` prop (`'llm' | 'rescan'`). When `mode === 'rescan'`: skips configure phase, uses rescan API endpoints. Added cancel button during scanning phase.
- `WorkbenchHeader.vue`: Added rescan button next to LLM scan button, emits `rescan` event
- `PlaygroundView.vue`: Cancel button in processing phase, handles `'cancelled'` polling status, word count estimation, rescan modal wiring
- `ReviewView.vue`: Rescan modal wiring
- `api/types.ts`: Added `'cancelled'` to `JobStatus` type
- `api/jobs.ts`: Added `cancelJob()` function
- `api/review.ts`: Added `cancelLlmScan()`, `startRescan()`, `getRescanStatus()`, `cancelRescan()` functions

### i18n Updates

- `playground.json` (en + de): Added `cancel`, `cancelling`, `cancelled` keys
- `review.json` (en + de): Added `llmScan.cancel`, `llmScan.cancelled`, `header.rescan`, and full `rescan` section (title, scanning, completed, noResults, error)

---

## Addendum: Bug Fixes, Entity Types & LLM Availability (2026-02-21)

### New Entity Types: CITY and COUNTRY

- Added to `entityTypes.ts`, `entityTypeColors.ts` (violet / emerald), i18n files
- `PseudonymizationService.cs`: CITY → `faker.Address.City()`, COUNTRY → `faker.Address.Country()`
- No DB changes needed — `PiiEntity.EntityType` is a free-form string
- Full list of frontend entity types: PERSON, ADDRESS, EMAIL, PHONE_DACH, IBAN, DE_STEUER_ID, DATE_OF_BIRTH, DATE, FINANCIAL_AMOUNT, COMPANY, LICENSE_PLATE, HEALTH_INSURANCE_ID, CITY, COUNTRY

### LLM Not-Configured Modal

- `HealthController.cs`: Now parses the Python service `/health` response, extracts `layers_available` array, exposes `llmAvailable` boolean in health response
- New `src/frontend/src/api/health.ts`: `getHealthInfo()` calls `GET /api/v1/health`
- `ReviewView.vue` and `PlaygroundView.vue`: On mount, check `llmAvailable`; if false, clicking "Scan with AI model" shows info modal instead of scan modal
- i18n keys: `review.workbench.llmScan.notConfiguredTitle` / `notConfiguredMessage`

### Ghost Modal Fix (LlmScanModal)

- **Root cause**: `handleApply()` called `fetchReviewData()` before emitting `update:open, false`. `fetchReviewData` sets `loading=true` in the review store, which triggers `v-if/v-else` in the parent view, unmounting the modal component. The subsequent `emit('update:open', false)` was silently ignored from the destroyed component.
- **Fix**: Emit `scanComplete` and `update:open, false` BEFORE calling `fetchReviewData()`

### Logout Redirect Fix

- **Root cause**: `UserMenu.vue` was not awaiting `authStore.logout()`. `router.push('/login')` ran before `localStorage` was cleared, causing the router guard to redirect back to dashboard.
- **Fix**: Added `await` before `authStore.logout()`

### Playground Layout Flash Fix

- **Root cause**: `App.vue`'s `useBareLayout` computed returned `false` during initial route resolution (when `route.name` is undefined), briefly showing the `AppLayout` shell.
- **Fix**: Changed to `!route.name || route.meta.layout === 'bare'` — treats unresolved routes as bare layout

### Test Fixes

- Removed stale `documentClass` form field from `FullFlowTests.cs` (UploadJob_ReturnsAccepted, UploadAndGetJob_RoundTrip) and `RoleEnforcementTests.cs` (CreateJob_WithUserRole_IsAllowed)
- Removed stale `documentClass` assertion from UploadAndGetJob_RoundTrip

---

## Deployment Safeguards & Branding Update (2026-02-21)

### Rebrand: PII Gateway → Klippo
- All user-facing references changed from "PII Gateway" to "Klippo"
- Files affected: `index.html`, `i18n/{de,en}/common.json`, `AppSidebar.vue`, `i18n/{de,en}/playground.json`
- Backend namespaces (`PiiGateway.*`) remain unchanged — internal only

### Playground Reload Bug Fix
- **Root cause**: `PlaygroundView.vue` called `restoreToken()` in `onUnmounted`, which cleared `isGuest` from `localStorage` on page reload. Router guard then redirected to dashboard.
- **Fix**: Removed `restoreToken()` from `onUnmounted` (kept in `handleStartOver()`), removed guard blocking logged-in users from playground, added `&& !isGuest` to login/register redirect guard.

### Email Notification on Registration
- New interface: `IEmailService` with `SendRegistrationNotificationAsync()`
- Implementation: `SmtpEmailService` using MailKit — logs warning when disabled
- Config section `Email` in `appsettings.json` (disabled by default)
- Fire-and-forget call in `AuthController.Register()`

### Playground Daily Usage Limit (3x/day per IP)
- `PlaygroundUsageTracker` singleton with `ConcurrentDictionary` tracking per-IP daily usage
- Resets at midnight UTC, auto-cleanup on each check
- `AuthController.Guest()` returns 429 when limit exceeded
- Frontend shows translated `dailyLimitReached` message

### Auth Rate Limiting
- New `"auth"` sliding window policy: 10 requests/minute
- Applied to `Register()` and `Login()` endpoints via `[EnableRateLimiting("auth")]`

### Language Selector on Public Pages
- `LanguageToggle` component added to `PlaygroundView`, `LoginView`, `RegisterView`
- Positioned top-right of page

### Descriptive Text on Public Pages
- Login: AI-focused description + "Try without an account" link to playground
- Register: Feature-focused description + "Try without an account" link
- Playground: Feature-focused description below subtitle

### Deployment Security Notes
1. JWT secret is hardcoded in dev — override via `Jwt:Secret` env var in production
2. DB password is `changeme_dev_only` — override via `ConnectionStrings:PostgreSQL` env var
3. CORS uses `AllowAnyMethod()`/`AllowAnyHeader()` — restrict in production
4. Refresh token endpoint is a stub (returns 401) — known limitation
5. 50MB body size limit — consider reducing for production
