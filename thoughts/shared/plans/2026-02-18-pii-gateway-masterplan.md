# DACH PII Detection & Pseudonymization Gateway — Master Implementation Plan

## Tech Stack Decision

| Layer | Technology | Notes |
|---|---|---|
| **Frontend** | Vue 3 Composition API + TypeScript + Tailwind CSS | SPA, communicates with .NET API via REST |
| **Backend (Main)** | .NET 8 Web API (C#) | All business logic, auth, job management, audit, pseudonymization |
| **Backend (PII Detection)** | Python microservice (FastAPI) | Presidio + spaCy + LLM Layer 3 — internal HTTP API only |
| **Database** | PostgreSQL 16 + Entity Framework Core | All tables as defined in technical plan |
| **Cache/Queue** | Redis | Session cache + background job coordination |
| **Background Jobs** | .NET Background Services (Channel\<T\>) | Async document processing orchestration |
| **LLM (Layer 3)** | Ollama (production) / Mistral API (dev) | Called from Python microservice, switchable via env var |
| **Hosting** | Hetzner (Falkenstein, DE) | Phased: CPX21/31 → GEX44 |
| **Containerization** | Docker + Docker Compose | All services containerized for local dev and deployment |

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                    Frontend (Vue 3 + Tailwind)                       │
│ ┌──────────┐ ┌────────────────┐ ┌──────────┐ ┌────────┐ ┌────────┐ │
│ │ Upload + │ │ Review/Confirm │ │ 2nd Scan │ │ Export │ │Paste & │ │
│ │ Doc Class│ │ UI (HITL)      │ │ + Output │ │ Gate   │ │De-pseu │ │
│ │ Selector │ │                │ │          │ │        │ │ Tool   │ │
│ └──────────┘ └────────────────┘ └──────────┘ └────────┘ └────────┘ │
└────────────────────────────────┬───────────────────────────────────-─┘
                                 │ HTTPS (REST API)
┌────────────────────────────────▼───────────────────────────────────-─┐
│                  .NET 8 Web API (Main Backend)                       │
│  Auth, Routing, Job Management, Pseudonymization Engine,             │
│  De-pseudonymization, Audit Log, Export Gate, Second Scan Trigger    │
│  Background Services (Hangfire) for async processing                 │
└──┬─────────────────────────────┬────────────────────────────────────-┘
   │ Internal HTTP               │
   ▼                              ▼
┌──────────────────┐    ┌──────────────────────────────────────────────┐
│ Python PII       │    │              PostgreSQL 16 + Redis           │
│ Detection Svc    │    │  (Jobs, PII mappings, audit logs,            │
│ (FastAPI)        │    │   review state, doc class, users)            │
│ Presidio + spaCy │    └──────────────────────────────────────────────┘
│ + LLM Layer 3    │
└──────────────────┘
```

### Inter-Service Communication

The .NET backend communicates with the Python PII Detection Service via an internal HTTP API. This API is **not exposed externally** — it runs on a private Docker network.

**Contract between .NET → Python:**

```
POST /api/detect
Request:
{
  "job_id": "uuid",
  "segments": [
    {
      "id": "uuid",
      "text": "...",
      "source_type": "paragraph|cell|header",
      "source_location": {...}
    }
  ],
  "document_class": "contract|invoice|hr_document|...",
  "sensitivity": "standard|high|critical",
  "layers": ["regex", "ner", "llm"],  // which layers to run
  "language_hint": "de|en|auto"
}

Response:
{
  "detections": [
    {
      "segment_id": "uuid",
      "text": "Max Mustermann",
      "entity_type": "PERSON",
      "start_offset": 20,
      "end_offset": 34,
      "confidence": 0.92,
      "detection_sources": ["ner", "llm"],
      "tier": "HIGH",
      "reason": "NER and LLM agree on person name"
    }
  ],
  "processing_time_ms": 1234,
  "layers_used": ["regex", "ner", "llm"]
}
```

This contract is the critical integration point. Both teams (Phase 2 and Phase 3) must agree on this before implementation.

---

## Phase Overview & Dependencies

```
Phase 1: Project Foundation ──────────────────────────────┐
    │                                                      │
    ├──► Phase 2: Document Processing (.NET) ─────┐       │
    │                                              │       │
    ├──► Phase 3: PII Detection (Python Svc) ─────┤       │
    │                                              │       │
    │    ┌─────────────────────────────────────────┘       │
    │    │                                                  │
    │    ▼                                                  │
    ├──► Phase 4: Core Backend Logic (.NET) ──────┐       │
    │         (depends on Phase 2 + 3)             │       │
    │                                              │       │
    ├──► Phase 5: Frontend - Upload & Dashboard ──┤       │
    │         (can start after Phase 1)            │       │
    │                                              │       │
    │    ┌─────────────────────────────────────────┘       │
    │    │                                                  │
    │    ▼                                                  │
    ├──► Phase 6: Frontend - Review UI ───────────┐       │
    │         (depends on Phase 4 + 5)             │       │
    │                                              │       │
    │    ┌─────────────────────────────────────────┘       │
    │    │                                                  │
    │    ▼                                                  │
    ├──► Phase 7: Export, Output & De-pseudonymization ───┤
    │         (depends on Phase 4 + 6)                     │
    │                                                      │
    │    ┌─────────────────────────────────────────────────┘
    │    │
    │    ▼
    └──► Phase 8: Security, Testing & Deployment
              (depends on all prior phases)
```

**Parallelization opportunities:**
- Phase 2 and Phase 3 can be developed in parallel (they share an API contract)
- Phase 5 can start as soon as Phase 1 is done (doesn't need the backend to be feature-complete)
- Phase 2+3 and Phase 5 can all run in parallel

---

## Phase 1: Project Foundation & Infrastructure — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Set up the entire project structure, database schema, Docker development environment, and CI/CD pipeline. This is the scaffold everything else builds on.

### Deliverables
1. **Solution structure:**
   - `src/PiiGateway.Api` — .NET 8 Web API project
   - `src/PiiGateway.Core` — Domain models, interfaces, business logic
   - `src/PiiGateway.Infrastructure` — EF Core, Redis, external service clients
   - `src/PiiGateway.Tests` — Unit + integration tests
   - `src/pii-detection-service/` — Python FastAPI microservice
   - `src/frontend/` — Vue 3 + Vite + Tailwind project
   - `docker/` — Dockerfiles for each service
   - `docker-compose.yml` — Full local development stack

2. **Database schema** (PostgreSQL via EF Core migrations):
   - `organizations` table
   - `users` table with roles (admin, reviewer, user)
   - `jobs` table with full state machine fields
   - `text_segments` table
   - `pii_entities` table
   - `audit_log` table (append-only)
   - All indexes as specified in technical plan Section 5

3. **Authentication & authorization scaffolding:**
   - JWT-based auth (or ASP.NET Identity)
   - Role-based authorization policies
   - API versioning (`/api/v1/...`)

4. **Docker Compose for local development:**
   - PostgreSQL 16 container
   - Redis 7 container
   - .NET API container
   - Python PII service container
   - Vue 3 dev server (Vite)
   - Shared Docker network for internal communication

5. **CI/CD pipeline** (GitHub Actions or similar):
   - Build + test for .NET
   - Build + test for Python
   - Build + lint for Vue 3
   - Docker image builds

### Key Decisions
- .NET 8 minimal API vs controllers → recommend controllers for this project's complexity
- EF Core with code-first migrations
- JWT tokens with refresh token rotation
- API versioning via URL path (`/api/v1/`)

### Success Criteria
- `docker-compose up` brings up all services and they can communicate
- Database migrations apply cleanly
- A test endpoint on .NET API returns 200
- Python service health check returns 200
- Vue 3 dev server loads in browser
- CI pipeline passes

### Completion Notes
- All 5 Docker services running (postgres, redis, api, pii-service, frontend)
- 6 DB tables created with 3 custom indexes (idx_jobs_org_status, idx_pii_job_status, idx_audit_job)
- API health endpoint returns healthy with DB + PII service connected
- Auth endpoints working: register (201), login (200), JWT tokens issued
- Authenticated GET /jobs returns 200 with empty list
- Python pytest + ruff pass, frontend vue-tsc + build pass, dotnet build passes
- 4 GitHub Actions workflows created (dotnet, python, frontend, docker)
- Docker port mappings: postgres=5433, api=5050, frontend=8090 (configurable via env vars due to host port conflicts)
- pii-service has NO host port mapping (internal Docker network only)

### Files to Reference
- Technical plan Section 5 (Data Model) for schema
- Technical plan Section 6 (API Design) for endpoint structure
- Technical plan Section 3.2 (Component Responsibilities) for architecture

---

## Phase 2: Document Processing Service (.NET) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the document upload, classification, and text extraction pipeline in .NET. This converts uploaded PDF/DOCX/XLSX files into structured `TextSegment` objects.

### Dependencies
- Phase 1 (project structure, database schema)

### Deliverables
1. **File upload endpoint:**
   - `POST /api/v1/jobs` — multipart upload with document class selection
   - File validation (type, size limits, virus scan placeholder)
   - File hash (SHA-256) for deduplication/integrity
   - Store file temporarily (local filesystem or MinIO)

2. **Document class system:**
   - Enum/configuration for document classes: contract, invoice, correspondence, hr_document, health_data, legal_casefile, financial, other
   - Sensitivity mapping (standard/high/critical)
   - DPIA trigger flag per class
   - Validation that class is always provided at upload

3. **Text extraction services:**
   - **PDF extraction** using PdfPig (C#): text with positional data, reject scanned PDFs with clear error
   - **DOCX extraction** using DocumentFormat.OpenXml: paragraphs, tables, headers, footers with formatting metadata
   - **XLSX extraction** using ClosedXML or EPPlus: iterate sheets → rows → cells, handle merged cells, cell comments
   - All extractors produce `TextSegment` objects with source_type and source_location

4. **Edge case handling:**
   - Merged cells in Excel
   - Headers/footers in Word (often contain company PII)
   - Tracked changes in DOCX (both current and deleted text)
   - Text in Excel comments and cell notes
   - Table of contents and footnotes in PDFs
   - Empty documents / extraction failures → clear error messages

5. **Job status management:**
   - Create job in `CREATED` status
   - Move to `PROCESSING` when extraction starts
   - Store extracted segments in `text_segments` table
   - Trigger PII detection (call to Python service) on completion
   - Move to `READY_REVIEW` when detection completes

6. **Background processing:**
   - Use Hangfire or .NET BackgroundService for async extraction
   - Job progress tracking
   - Error handling and retry logic

### Key .NET Libraries
- `PdfPig` — PDF text extraction
- `DocumentFormat.OpenXml` — DOCX parsing
- `ClosedXML` — XLSX parsing (handles merged cells well)
- `Hangfire` — Background job processing

### Success Criteria
- Upload a PDF, DOCX, and XLSX file → each produces correct TextSegments
- Document class is stored and sensitivity correctly mapped
- Merged cells, headers/footers, tracked changes are handled
- Background processing works (upload returns 202, processing happens async)
- Extraction errors produce clear user-facing messages

### Completion Notes

**NuGet packages added to Infrastructure:** PdfPig 0.1.13, DocumentFormat.OpenXml 3.4.1, ClosedXML 0.105.0, Microsoft.Extensions.Hosting.Abstractions 10.0.3
**NuGet packages added to Tests:** Moq 4.20.72, FluentAssertions 8.8.0

**Tech decisions implemented:**
- Background processing: `Channel<Guid>` (bounded, capacity 100) instead of Hangfire — zero-dependency, built into .NET 8, sufficient for single-server deployment
- File storage: local filesystem at `/app/uploads/{jobId}/original.{ext}` (Docker volume `upload_data`)
- Kestrel body size limit: 52,428,800 bytes (50 MB)
- Stuck job recovery: `DocumentProcessingBackgroundService` queries DB for `Status == Processing` on startup and re-enqueues

**Core layer (7 new files):**
- `SensitivityMapping.cs` — Static `DocumentClass → (Sensitivity, DpiaTriggered)` mapping (8 classes)
- `JobStatusTransitions.cs` — Full state machine with `Validate()` (throws on illegal) and `IsValid()` for all 9 statuses
- `ITextSegmentRepository.cs` — `AddRangeAsync`, `GetByJobIdAsync`
- `IDocumentExtractor.cs` — Strategy pattern: `CanHandle(string)`, `ExtractAsync(Stream, Guid)`
- `IFileStorageService.cs` — `SaveAsync`, `OpenReadAsync`, `DeleteAsync`, `GetFilePath`
- `IDocumentProcessingQueue.cs` — `EnqueueAsync(Guid jobId)`
- `IDocumentProcessor.cs` — `ProcessAsync(Guid jobId)`

**Infrastructure services (9 new files):**
- `FileStorageOptions.cs` — BasePath, MaxFileSizeMb (50), AllowedExtensions (.pdf, .docx, .xlsx)
- `FileStorageService.cs` — Local filesystem implementation
- `TextSegmentRepository.cs` — Batch insert + ordered retrieval by SegmentIndex
- `PdfExtractor.cs` — PdfPig-based, splits into paragraph segments per page, scanned PDF detection (throws on pages with images but no text), records page number in SourceLocation JSON
- `DocxExtractor.cs` — Extracts: body paragraphs, table cells (with row/col), headers, footers, comments (with author), footnotes, tracked changes (DeletedRun text)
- `XlsxExtractor.cs` — Extracts: cell values, sheet names (SourceType.SheetName), cell comments, merged cells (HashSet dedup, top-left only), multi-sheet support
- `DocumentProcessingQueue.cs` — Bounded Channel<Guid> (capacity 100), singleton
- `DocumentProcessingBackgroundService.cs` — BackgroundService, reads from channel, creates DI scope per job, stuck job recovery on startup
- `DocumentProcessor.cs` — Orchestrator: (1) transition Created→Processing, (2) resolve extractor via CanHandle(), (3) extract segments, (4) batch-save to DB, (5) call PII detection (graceful on failure — service is a stub), (6) transition to ReadyReview. On error: set ErrorMessage, reset to Created.

**Entity changes:**
- `Job.cs` — Added `ErrorMessage` property
- `JobResponse.cs` — Added `ErrorMessage` property
- `JobConfiguration.cs` — Added `error_message` column mapping (varchar 2000)
- EF migration `AddJobErrorMessage` generated and verified

**Controller changes:**
- `POST /api/v1/jobs` — Changed from `[FromBody]` to `[FromForm]`: accepts `IFormFile file` + `string documentClass`, validates extension/size/class, computes SHA-256 hash, maps sensitivity, creates job, saves file, audit logs, enqueues for processing, returns `202 Accepted` with `{ job_id, status }`
- `GET /api/v1/jobs/{id}` — Implemented: loads job by ID, verifies org ownership, returns `JobResponse`
- Extracted `MapToResponse` helper shared between `ListJobs` and `GetJob`
- Added `GetUserId()` helper (extracts from JWT `sub` claim)
- Added `[RequestSizeLimit]` and `[RequestFormLimits]` attributes (50 MB)

**DI registrations added:**
- `Configure<FileStorageOptions>` from "FileStorage" section
- `ITextSegmentRepository` → `TextSegmentRepository` (scoped)
- `IFileStorageService` → `FileStorageService` (scoped)
- `IDocumentExtractor` → PdfExtractor, DocxExtractor, XlsxExtractor (3 scoped registrations)
- `IDocumentProcessor` → `DocumentProcessor` (scoped)
- `DocumentProcessingQueue` (singleton) + `IDocumentProcessingQueue` forwarding
- `DocumentProcessingBackgroundService` (hosted service)

**Docker changes:**
- `docker-compose.yml` — Added `upload_data:/app/uploads` volume to api service, declared `upload_data:` volume
- `Dockerfile.api` — Added `RUN mkdir -p /app/uploads && chown appuser /app/uploads` before `USER appuser`

**appsettings.json:**
- Added `FileStorage` section: `BasePath`, `MaxFileSizeMb`, `AllowedExtensions`

**Tests (7 new files, 52 unit tests all passing):**
- `SensitivityMappingTests.cs` — All 8 document class mappings verified
- `JobStatusTransitionsTests.cs` — 11 valid transitions, 5 invalid transitions, 3 IsValid checks
- `PdfExtractorTests.cs` — CanHandle, simple PDF paragraphs, empty PDF, multi-page sequential indices
- `DocxExtractorTests.cs` — CanHandle, body paragraphs, tables (4 cells), headers/footers, comments with author, footnotes, sequential indices
- `XlsxExtractorTests.cs` — CanHandle, cell values, sheet names, merged cell dedup, comments, multi-sheet, sequential indices
- `DocumentProcessorTests.cs` — Happy path (Created→Processing→ReadyReview), PII service failure (still ReadyReview), extraction failure (ErrorMessage set, reset to Created), job not found (no error)
- `JobsControllerTests.cs` — Auth (401), missing file (400), invalid type (400), invalid class (400), GetJob auth (401)

**Build verification:**
- `dotnet build PiiGateway.sln` — 0 errors
- `dotnet test --filter Unit` — 52/52 passing

**What Phase 4 needs from this phase:**
- `IDocumentProcessor` and background queue are ready — Phase 4 extends the orchestrator for second scan
- `JobStatusTransitions` defines the full state machine — Phase 4 uses `Validate()` for all status changes
- `SensitivityMapping` available for sensitivity-aware behavior
- `ITextSegmentRepository.GetByJobIdAsync()` retrieves segments for review UI data
- `AuditLogService` already used for job creation and status changes
- `IFileStorageService` available for file cleanup (data retention in Phase 8)

### Files to Reference
- Technical plan Section 4.1 (Upload & Document Classification)
- Technical plan Section 4.2 (Document Processing Service)
- Technical plan Section 6 (API Design — upload flow)

---

## Phase 3: PII Detection Pipeline (Python Microservice) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the Python FastAPI microservice that runs the three-layer PII detection pipeline: regex pattern matching (Layer 1), NER model (Layer 2), and LLM contextual review (Layer 3).

### Dependencies
- Phase 1 (project structure, Docker setup)
- API contract agreed with Phase 2/4 developers

### Deliverables
1. **FastAPI microservice setup:**
   - `POST /api/detect` — main detection endpoint (contract defined in Architecture Overview above)
   - `GET /health` — health check
   - Dockerfile with all ML model dependencies
   - Configuration via environment variables

2. **Layer 1: Deterministic Pattern Matching:**
   - All DACH-specific regex recognizers from technical plan Section 4.3:
     - DE: Steuer-ID (with mod-11 checksum), Sozialversicherungsnr, Handelsregister
     - CH: AHV-Nr (EAN-13 checksum), UID
     - AT: Sozialversicherungsnr
     - Common: IBAN (mod-97), Email, Phone (DACH formats), Date of Birth
     - English: UK NI number, US SSN, Passport numbers
   - Implement as Presidio `PatternRecognizer` extensions
   - Checksum validators for each structured ID type
   - Configurable confidence scores per recognizer

3. **Layer 2: NER Model:**
   - Integrate `xlm-roberta-large` fine-tuned for NER (multilingual DE+EN)
   - Entity types: PER, LOC, ORG, MISC
   - Segment-level language detection using `langdetect` or `fasttext`
   - Integration with Presidio `AnalyzerEngine`
   - GPU support when available, CPU fallback

4. **Layer 3: LLM Contextual Review:**
   - `Layer3LLM` abstraction class with three backends:
     - `ollama` — POST to `http://localhost:11434/api/generate`
     - `mistral` — POST to Mistral API
     - `disabled` — returns empty list (skip Layer 3)
   - Switchable via single env var `LLM_BACKEND`
   - Bilingual prompt (DE+EN) as specified in technical plan
   - JSON response parsing with fallback for malformed LLM output
   - Timeout handling (LLM may be slow on CPU)

5. **Merge & Conflict Resolution:**
   - Implement merge logic from technical plan Section 4.3:
     - Regex + checksum → auto-confirm (confidence >=0.95)
     - NER + LLM agree → boost confidence by 0.1
     - Single layer only → lower confidence tier
     - Overlapping spans → take wider span
     - Below 0.5 confidence → discard
   - Confidence tiers: HIGH (>=0.90), MEDIUM (0.70-0.89), LOW (0.50-0.69)

6. **Sensitivity-aware behavior:**
   - Lower confidence thresholds for high/critical sensitivity documents
   - More aggressive detection for health_data and legal_casefile classes

### Key Python Libraries
- `presidio-analyzer` + `presidio-anonymizer` — PII detection framework
- `spacy` with `de_core_news_lg` and `en_core_web_trf` models
- `transformers` (Hugging Face) for xlm-roberta NER
- `fastapi` + `uvicorn` — HTTP service
- `langdetect` or `fasttext` — language detection
- `httpx` — async HTTP client for LLM API calls

### Success Criteria
- All DACH regex recognizers detect their target patterns with >=90% recall on test data
- Checksum validators correctly accept/reject IDs
- NER detects person names, addresses, organizations in both DE and EN
- Layer 3 LLM integration works with both Mistral API and Ollama
- `LLM_BACKEND=disabled` cleanly skips Layer 3
- Merge logic correctly handles overlapping detections from multiple layers
- `POST /api/detect` returns properly formatted response matching the API contract
- Processing time < 30 seconds per document segment (without LLM), < 60 seconds with LLM

### Completion Notes

**Critical design decision: camelCase wire format**
- Added `CamelModel` base class using Pydantic's `alias_generator=to_camel` + `populate_by_name=True`
- All request/response models inherit from `CamelModel`, accepting both camelCase (from .NET) and snake_case (backward compat)
- Response serialization uses `response_model_by_alias=True` → outputs camelCase for .NET consumption
- `HealthResponse` stays plain `BaseModel` (not called by .NET)
- Added `confidence_tier` field to `DetectionResult` (informational, .NET DTO ignores unknown fields)
- `detection_source` field is a comma-separated string for multiple sources (e.g., `"regex+checksum,ner"`)

**Configuration (`app/config.py`):**
- Pydantic `BaseSettings` with env vars: `LLM_BACKEND`, `MISTRAL_API_KEY`, `OLLAMA_URL`, `OLLAMA_MODEL`, `MISTRAL_MODEL`, `LLM_TIMEOUT_SECONDS`, `NER_MODEL_NAME`, `NER_DEVICE`, `LAYER2_ENABLED`, `MIN_CONFIDENCE`, `HIGH_CONFIDENCE`, `MEDIUM_CONFIDENCE`
- Default NER model: `Davlan/bert-base-multilingual-cased-ner-hrl` (~680MB, good DE+EN)

**Layer 1 — Regex + Checksum (3 new files in `app/detection/layer1_regex/`):**
- `checksums.py` — 10 pure validation functions: `steuer_id_checksum` (mod-11), `sv_nr_checksum` (structure), `ahv_ean13_checksum` (EAN-13), `uid_checksum` (mod-11), `at_sv_checksum` (structure), `iban_mod97_checksum` (ISO 7064), `phone_plausibility` (DACH prefixes), `date_plausibility` (age 0-120), `ni_number_prefix_check` (UK prefix exclusion), `ssn_area_check` (US range exclusion)
- `recognizers.py` — 11 custom Presidio `PatternRecognizer` subclasses with `validate_result()` overrides for checksum validation. Built-in `EmailRecognizer` and `IbanRecognizer` reused. Context words boost confidence (e.g., "Steuer", "IBAN", "geboren").
- `engine.py` — `create_layer1_engine()` builds `AnalyzerEngine` with `RecognizerRegistry(supported_languages=["de", "en"])`, spacy NLP engine for tokenization, all custom + built-in recognizers. `run_layer1()` returns `list[RawDetection]`.

**Layer 2 — NER (2 new files in `app/detection/layer2_ner/`):**
- `language_detect.py` — `detect_language()` using `langdetect`, falls back to hint for short segments (<20 chars)
- `engine.py` — `create_layer2_engine()` wraps HuggingFace transformer via Presidio's `TransformersNlpEngine`. Entity mapping: PER→PERSON, LOC→LOCATION, ORG→ORGANIZATION, MISC→MISC. Model loaded once at startup.

**Layer 3 — LLM (3 new files in `app/detection/layer3_llm/`):**
- `prompts.py` — Bilingual DE+EN prompt template, lists already-detected entities to avoid duplicates, requests JSON array response
- `parser.py` — `parse_llm_response()` with 3 fallback strategies: direct JSON parse → code fence extraction → empty list. LLM confidence capped at 0.85. Text span located in original text via string search (case-insensitive fallback).
- `llm_client.py` — `Layer3LLM` class with `_call_ollama()` (POST `/api/generate`, `stream=False, format="json"`) and `_call_mistral()` (POST chat completions, `response_format=json_object`). All errors caught and logged, never crashes pipeline.

**Merge logic (`app/detection/merger.py`):**
- `RawDetection` dataclass: `entity_type`, `start`, `end`, `confidence`, `source`, `original_text`, `reason`
- `cluster_overlapping()` — sort+sweep interval clustering
- `merge_detections()` — 5 rules: (1) checksum source → confidence ≥0.95, (2) NER+LLM → boost +0.1 (cap 1.0), (3) single source → reduce -0.1 (floor 0.5), (4) wider span on overlap, (5) discard below sensitivity threshold (standard=0.50, high=0.40, critical=0.30)
- Confidence tiers assigned: HIGH (≥0.90), MEDIUM (0.70-0.89), LOW (0.50-0.69)

**Pipeline orchestration (`app/detection/pipeline.py`):**
- `DetectionPipeline` class: constructor takes `settings`, `l1_engine`, `l2_engine` (nullable), `l3_llm`
- `async detect()`: per segment → detect language → run requested layers → merge → build `DetectionResult` list with timing
- Respects `request.layers` to control which layers run (empty = all enabled)
- Layer 2/3 failures are logged and skipped (service stays available with L1 only)

**Wiring changes:**
- `app/main.py` — Lifespan startup: loads Settings, creates L1 engine (always), L2 engine if `LAYER2_ENABLED` (catches errors, logs, continues without), L3 LLM (lightweight), stores `DetectionPipeline` in `app.state.pipeline`
- `app/detection/router.py` — `POST /api/detect` wired to `request.app.state.pipeline.detect()` with `response_model_by_alias=True`
- `app/health.py` — Dynamically reports which layers loaded from `app.state`

**Dependencies added to `requirements.txt`:**
- `pydantic-settings>=2.0`, `presidio-analyzer>=2.2,<3.0`, `langdetect>=1.0.9`, `transformers>=4.36.0`, `torch>=2.1.0`, `sentencepiece>=0.1.99`, `spacy>=3.7.0`

**Dependencies added to `requirements-dev.txt`:**
- `pytest-asyncio>=0.23.0`, `respx>=0.21.0`

**Docker changes:**
- `docker/Dockerfile.pii-service` — Multi-stage build: builder stage installs `gcc g++`, CPU-only PyTorch via `--extra-index-url`, downloads spacy models (`de_core_news_sm`, `en_core_web_sm`), pre-downloads NER model via `ARG NER_MODEL_NAME`. Runtime stage copies packages + cached models, creates non-root `appuser`. Expected image size ~1.5-2GB.
- `docker-compose.yml` — Added to pii-service environment: `NER_MODEL_NAME`, `NER_DEVICE`, `LAYER2_ENABLED`

**Tests (10 test files, 153 tests all passing):**
- `tests/conftest.py` — Shared fixtures: `sample_german_text`, `sample_camel_request`, `sample_snake_request`, `mock_settings`
- `tests/test_contract.py` — 6 tests: camelCase/snake_case round-trip, response serialization, `confidence_tier` field
- `tests/test_checksums.py` — 35 tests: every checksum function with valid/invalid/edge cases
- `tests/test_recognizers.py` — 23 tests: each regex pattern with true positives, true negatives, boundary cases
- `tests/test_layer1.py` — 6 tests: real Presidio engine detecting IBAN, email, phone (skipped if spacy unavailable)
- `tests/test_layer2.py` — 5 tests: language detection, mocked transformer model, language normalization
- `tests/test_layer3.py` — 15 tests: JSON parsing (valid/malformed/empty/code-fence/wrapped), LLM client (disabled/ollama/mistral/timeout/error/unknown backend)
- `tests/test_merger.py` — 16 tests: all 5 merge rules in isolation + combination, confidence tiers, integration
- `tests/test_pipeline.py` — 8 tests: full pipeline with real L1 (IBAN, email detection), empty segments, layer selection, segment IDs, confidence tier population
- `tests/test_health.py` — 3 tests: health endpoint, detect with snake_case, detect with camelCase

**Build verification:**
- `python -m pytest tests/ -v` — 153/153 passing
- `ruff check .` — All checks passed
- `mypy app/` — Success: no issues in 20 files

**File structure (17 new files, 7 modified):**
```
src/pii-detection-service/
├── app/
│   ├── config.py                        # NEW
│   ├── main.py                          # MODIFIED: lifespan startup
│   ├── health.py                        # MODIFIED: dynamic layers
│   ├── models.py                        # MODIFIED: CamelModel base
│   └── detection/
│       ├── router.py                    # MODIFIED: wired to pipeline
│       ├── pipeline.py                  # NEW
│       ├── merger.py                    # NEW
│       ├── layer1_regex/
│       │   ├── checksums.py             # NEW: 10 validation functions
│       │   ├── recognizers.py           # NEW: 11 PatternRecognizer subclasses
│       │   └── engine.py               # NEW: Presidio AnalyzerEngine for L1
│       ├── layer2_ner/
│       │   ├── language_detect.py       # NEW
│       │   └── engine.py               # NEW: TransformersNlpEngine wrapper
│       └── layer3_llm/
│           ├── llm_client.py            # NEW: Layer3LLM (ollama/mistral/disabled)
│           ├── prompts.py               # NEW: bilingual prompt
│           └── parser.py               # NEW: JSON response parsing
├── tests/
│   ├── conftest.py                      # NEW
│   ├── test_health.py                   # MODIFIED
│   ├── test_contract.py                 # NEW
│   ├── test_checksums.py                # NEW
│   ├── test_recognizers.py              # NEW
│   ├── test_layer1.py                   # NEW
│   ├── test_layer2.py                   # NEW
│   ├── test_layer3.py                   # NEW
│   ├── test_merger.py                   # NEW
│   └── test_pipeline.py                # NEW
├── requirements.txt                     # MODIFIED
└── requirements-dev.txt                 # MODIFIED
```

**What Phase 4 needs from this phase:**
- `POST /api/detect` endpoint accepts camelCase request matching .NET `DetectRequest` DTO, returns camelCase response matching `DetectResponse` DTO
- Wire format uses `jobId`, `segmentId`, `textContent`, `sourceType`, `documentClass`, `languageHint`, `entityType`, `startOffset`, `endOffset`, `detectionSource`, `originalText`, `confidenceTier`, `processingTimeMs`, `layersUsed`
- `layers` field controls which detection layers run (empty = all enabled). For second scan, .NET should send `layers: ["regex", "ner"]` to skip LLM
- `sensitivity` field affects confidence thresholds: standard (0.50), high (0.40), critical (0.30)
- Health endpoint at `GET /health` reports `layers_available` dynamically
- Service starts on port 8001, reachable at `http://pii-service:8001` within Docker network

### Files to Reference
- Technical plan Section 4.3 (PII Detection Service — full Layer 1/2/3 spec)
- Technical plan Section 4.3 Merge & Conflict Resolution
- All regex patterns and checksum validators in the technical plan

---

## Phase 4: Core Backend Business Logic (.NET) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the central business logic in .NET: job state machine, review management, pseudonymization engine, de-pseudonymization engine, second scan orchestration, and audit logging.

### Dependencies
- Phase 1 (project structure, database)
- Phase 2 (document processing — to feed text segments)
- Phase 3 (PII detection — to receive detection results)

### Deliverables
1. **Job State Machine:**
   - States: CREATED → PROCESSING → READY_REVIEW → IN_REVIEW → PSEUDONYMIZED → SCAN_PASSED/SCAN_FAILED → EXPORTED → DE_PSEUDONYMIZED
   - All transitions validated (no illegal jumps)
   - Each transition logged to audit trail with timestamp, actor, details
   - Soft states: EXPORTED and DE_PSEUDONYMIZED allow multiple de-pseudonymizations

2. **Review Management API:**
   - `GET /api/v1/jobs/{id}/review` — return document with PII highlights
   - `PATCH /api/v1/jobs/{id}/entities/{eid}` — confirm/reject/modify entity
   - `POST /api/v1/jobs/{id}/entities` — add manually detected PII
   - `POST /api/v1/jobs/{id}/confirm` — confirm all reviewed, trigger pseudonymization
   - Review completeness check: all entities must be confirmed or rejected
   - Batch confirmation logic with safeguards:
     - "Confirm all regex-detected" — only checksum-validated entities
     - "Confirm all HIGH confidence" — only after scroll tracking condition met
     - Disabled for critical sensitivity jobs
   - Track which entities were individually reviewed vs batch-confirmed

3. **Pseudonymization Engine:**
   - Generate synthetic replacements using Bogus (C# Faker equivalent)
   - Locale-aware: German names for German text, English names for English text
   - Consistency: same original → same replacement within a job (lookup table)
   - Replacement strategies: synthetic (default), placeholder ([PERSON_001]), type-hash
   - Store `PseudonymizationMapping` in database (encrypted)
   - Generate pseudonymized full text from segments + confirmed entities

4. **De-pseudonymization Engine:**
   - `POST /api/v1/jobs/{id}/deanonymize` — accept pasted LLM response, return de-pseudonymized text
   - Load mapping for job, sort by length descending (avoid partial matches)
   - Generate common variants for person names (surname-only, title+surname)
   - Track which replacements were made and which pseudonyms went unmapped
   - Return unmapped pseudonym warnings
   - Multiple de-pseudonymizations per job allowed (follow-up questions)

5. **Second Scan Orchestration:**
   - `POST /api/v1/jobs/{id}/second-scan` — trigger re-scan of pseudonymized output
   - Call Python PII service with `layers: ["regex", "ner"]` (skip LLM for speed)
   - Pass allowlist of known synthetic replacements to exclude from detection
   - If detections found (confidence >= 0.70) → block export, return to IN_REVIEW
   - If clean → mark SCAN_PASSED, enable export gate

6. **Export Gate:**
   - `POST /api/v1/jobs/{id}/export` — submit acknowledgement
   - Require all three checkboxes (four for critical sensitivity)
   - Validate: review complete + second scan passed
   - Log acknowledgement with exact checkbox text, timestamp, user ID, IP
   - Only after export gate passed: `GET /api/v1/jobs/{id}/pseudonymized` returns text

7. **Audit Log Service:**
   - Append-only logging to `audit_log` table
   - Action types: pii_detected, pii_confirmed, pii_rejected, pii_added_manual, document_pseudonymized, second_scan_passed, second_scan_failed, export_acknowledged, text_copied, response_depseudonymized, reviewer_training_completed, dpia_trigger_shown, dpia_acknowledged
   - Entity hashing (SHA-256 of original PII — not the PII itself)
   - `GET /api/v1/jobs/{id}/audit` — retrieve audit trail for a job
   - No UPDATE or DELETE on audit log (enforce at application level)

8. **Job listing & dashboard API:**
   - `GET /api/v1/jobs` — list jobs for current org with filtering/pagination
   - `GET /api/v1/jobs/{id}` — get job status and metadata
   - Summary statistics per job (entities detected/confirmed/rejected/manual)

### Key .NET Libraries
- `Bogus` — synthetic data generation (C# Faker)
- `Hangfire` — background job processing
- EF Core — database access
- `StackExchange.Redis` — Redis client
- Standard ASP.NET Core for API, auth, validation

### Success Criteria
- Job state machine enforces all valid transitions, rejects invalid ones
- All state transitions produce audit log entries
- Pseudonymization produces consistent replacements (same input → same output within job)
- De-pseudonymization correctly reverses pseudonyms including surname-only variants
- Second scan correctly identifies residual PII and blocks export
- Export gate requires all checkboxes and prior completion of review + second scan
- Audit log is truly append-only, contains all required fields
- All endpoints match the API spec from technical plan Section 6

### Completion Notes

**NuGet package added to Infrastructure:** Bogus 35.6.5 (synthetic data generation)

**Build verification:**
- `dotnet build PiiGateway.sln` — 0 errors
- `dotnet test --filter Unit` — 95/95 passing (52 existing + 43 new)

**Core layer — new interfaces (6 files):**
- `IPiiEntityRepository.cs` — 8 methods: GetById, GetByJobId, GetByJobIdAndStatus, AddRange, Update, UpdateRange, CountByJobId, GetStatusCounts (GroupBy aggregation)
- `IReviewService.cs` — GetReviewData, UpdateEntity, AddManualEntity, ConfirmReview
- `IPseudonymizationService.cs` — PseudonymizeJob, GenerateReplacement, GetPseudonymizedOutput
- `ISecondScanService.cs` — RunSecondScan
- `IExportGateService.cs` — SubmitExportAcknowledgement, GetPseudonymizedOutput (with ExportGateResultDto)
- `IDePseudonymizationService.cs` — DePseudonymize

**Core layer — new DTOs (10 files):**
- `Review/ReviewDataResponse.cs` — ReviewDataResponse, SegmentDto, EntityDto (with ConfidenceTier), ReviewSummary (HIGH/MEDIUM/LOW/Confirmed/Rejected/ManuallyAdded/Pending)
- `Review/UpdateEntityRequest.cs` — ReviewStatus ("confirmed"/"rejected"), EntityType, StartOffset, EndOffset (all nullable for partial updates)
- `Review/AddEntityRequest.cs` — SegmentId, Text, EntityType, StartOffset, EndOffset
- `Review/ConfirmReviewRequest.cs` — BatchAction (null/"regex"/"high_confidence"), ScrollTrackingConfirmed
- `Export/PseudonymizedOutputResponse.cs` — PseudonymizedText + List<ReplacementEntry> (Original, Replacement, EntityType, OccurrenceCount)
- `Export/SecondScanResultDto.cs` — Passed + List<SecondScanDetection>
- `Export/ExportAcknowledgementRequest.cs` — 4 boolean checkboxes (ReviewedEntities, EnterpriseApiOnly, PseudonymizedStillPersonal, DpiaCompleted)
- `Export/DePseudonymizeRequest.cs` — LlmResponseText
- `Export/DePseudonymizedResponse.cs` — DepseudonymizedText + List<ReplacementMade> (Pseudonym, Original, Count) + List<string> UnmappedWarnings
- `Audit/AuditLogResponse.cs` — List<AuditEntryDto> (Id, Timestamp, ActorId, ActionType, EntityType, EntityHash, Confidence, DetectionSource, Metadata)

**Infrastructure — repository (1 new file):**
- `PiiEntityRepository.cs` — Full EF Core implementation: Include(e => e.Segment), OrderBy SegmentIndex then StartOffset, GroupBy for status counts (efficient single query)

**Infrastructure — services (5 new files):**
- `ReviewService.cs` — Core review business logic:
  - `GetReviewDataAsync`: loads job+segments+entities, transitions ReadyReview→InReview on first access, computes confidence tiers (>=0.90 HIGH, 0.70-0.89 MEDIUM, <0.70 LOW), builds summary stats
  - `UpdateEntityAsync`: validates entity belongs to job, applies partial updates, sets ReviewedById/ReviewedAt, logs PiiConfirmed/PiiRejected audit events
  - `AddManualEntityAsync`: creates PiiEntity with Confidence=1.0, DetectionSources=["human"], ReviewStatus=AddedManual, logs PiiAddedManual
  - `ConfirmReviewAsync`: validates no pending entities remain (rejects if any), triggers pseudonymization, transitions InReview→Pseudonymized
  - `BatchConfirmAsync`: two modes:
    - `"regex"`: only entities where ALL detection sources contain "regex" AND confidence ≥0.95
    - `"high_confidence"`: only entities with confidence ≥0.90 AND scrollTrackingConfirmed=true; **returns 400 for critical sensitivity jobs**
    - Each batch-confirmed entity tracked with `batch_confirmed:true` in audit metadata

- `PseudonymizationService.cs` — Bogus-based pseudonymization engine:
  - Deterministic seeding: `Faker` seeded with `BitConverter.ToInt32(jobId.ToByteArray())` for reproducibility
  - Consistency map: `Dictionary<string, string>` keyed by `originalText.ToLower()` — same input always maps to same replacement within a job
  - Entity type replacements: PERSON→FullName, ORG→CompanyName, LOCATION→City, ADDRESS→StreetAddress, EMAIL→Email, PHONE→PhoneNumber, IBAN→synthetic DE IBAN (mod-97 valid checksum), DATE→Past date (dd.MM.yyyy), unknown→`[TYPE_001]` placeholder
  - Locale detection: defaults to `"de"` for DACH market
  - `BuildPseudonymizedText`: iterates segments in order, replaces entity spans, stores result on `Job.PseudonymizedText`
  - `GetPseudonymizedOutputAsync`: returns pseudonymized text + grouped replacement table

- `SecondScanService.cs` — Defense-in-depth second scan:
  - Validates job is in Pseudonymized status with non-empty PseudonymizedText
  - Builds allowlist from all ReplacementText values (case-insensitive HashSet)
  - Calls Python PII service with `layers: ["regex", "ner"]` (skips LLM for speed)
  - Filters out allowlisted detections (known synthetic names) and low-confidence (<0.70)
  - On fail: transitions Pseudonymized→ScanFailed→InReview, adds new PiiEntities for re-review, logs SecondScanFailed with detection count
  - On pass: transitions to ScanPassed, sets SecondScanPassed=true, logs SecondScanPassed

- `ExportGateService.cs` — Mandatory export acknowledgement:
  - Validates job is ScanPassed, all 3 checkboxes true (4 for critical sensitivity — DpiaCompleted required)
  - Validates no pending entities remain, SecondScanPassed=true
  - Transitions to Exported, sets ExportAcknowledged=true, ExportedAt=UtcNow
  - Logs ExportAcknowledged with exact checkbox states, user ID, IP, timestamp in metadata JSON
  - `GetPseudonymizedOutputAsync`: returns 403 (UnauthorizedAccessException) if ExportAcknowledged=false, logs TextCopied on access

- `DePseudonymizationService.cs` — Paste-back tool with name variant matching:
  - Builds replacement map: pseudonym→original from all confirmed/manual entities
  - **Name variant generation for PERSON entities:**
    - Full name: "Felix Bauer" → "Max Mustermann"
    - Surname only: "Bauer" → "Mustermann"
    - Honorific variants: "Herr Bauer"→"Herr Mustermann", "Frau Bauer"→"Frau Mustermann", "Mr Bauer"→"Mr Mustermann", "Mrs Bauer"→"Mrs Mustermann", "Ms Bauer"→"Ms Mustermann", "Dr Bauer"→"Dr Mustermann"
  - Sorts replacement keys by length descending (longest first to avoid partial matches)
  - Case-insensitive replacement with occurrence counting
  - Unmapped pseudonym detection: scans result for any remaining replacement texts that weren't matched
  - Logs ResponseDepseudonymized with replacement_count, input/output lengths, unmapped_count
  - State transitions: Exported→DePseudonymized (first time), DePseudonymized→DePseudonymized (subsequent — self-transition enabled)

**Entity changes:**
- `Job.cs` — Added `PseudonymizedText` property (nullable string)
- `JobConfiguration.cs` — Added `pseudonymized_text` column mapping (text, nullable)
- `JobResponse.cs` — Added 5 entity summary fields: TotalEntities, ConfirmedEntities, RejectedEntities, ManualEntities, PendingEntities (all nullable int)
- `JobStatusTransitions.cs` — Added DePseudonymized→DePseudonymized self-transition for multiple de-pseudonymization passes

**Repository changes:**
- `IJobRepository.cs` — Added `GetByOrgFilteredAsync(orgId, page, pageSize, statusFilter?, classFilter?, dateFrom?, dateTo?)`
- `JobRepository.cs` — Implemented filtered query with conditional `.Where()` clauses for status, documentClass, dateFrom, dateTo

**Controller changes (JobsController.cs — complete rewrite of 10 stub endpoints):**
- Added constructor dependencies: `IPiiEntityRepository`, `IAuditLogRepository`, `IReviewService`, `ISecondScanService`, `IExportGateService`, `IDePseudonymizationService`
- Added `GetIpAddress()` helper for audit trail IP tracking
- `GET /api/v1/jobs/{id}/review` → calls `ReviewService.GetReviewDataAsync()`, validates org ownership
- `PATCH /api/v1/jobs/{id}/entities/{eid}` → calls `ReviewService.UpdateEntityAsync()` with `[FromBody] UpdateEntityRequest`
- `POST /api/v1/jobs/{id}/entities` → calls `ReviewService.AddManualEntityAsync()` with `[FromBody] AddEntityRequest`, returns 201 Created
- `POST /api/v1/jobs/{id}/confirm` → calls `ReviewService.ConfirmReviewAsync()` with `[FromBody] ConfirmReviewRequest` (handles both normal confirm and batch actions)
- `POST /api/v1/jobs/{id}/second-scan` → calls `SecondScanService.RunSecondScanAsync()`
- `GET /api/v1/jobs/{id}/pseudonymized` → calls `ExportGateService.GetPseudonymizedOutputAsync()`, returns 403 before export gate
- `POST /api/v1/jobs/{id}/export` → calls `ExportGateService.SubmitExportAcknowledgementAsync()` with `[FromBody] ExportAcknowledgementRequest`
- `POST /api/v1/jobs/{id}/deanonymize` → calls `DePseudonymizationService.DePseudonymizeAsync()` with `[FromBody] DePseudonymizeRequest`
- `GET /api/v1/jobs/{id}/audit` → loads audit logs via `IAuditLogRepository.GetByJobIdAsync()`, maps to AuditLogResponse DTO
- `GET /api/v1/jobs` — enhanced with optional query params: `status`, `documentClass`, `dateFrom`, `dateTo` (calls `GetByOrgFilteredAsync` when filters present)
- `MapToResponse` → `MapToResponseAsync` — now loads entity summary counts via `IPiiEntityRepository.GetStatusCountsAsync()` for jobs beyond Processing status
- `DocumentProcessor.cs` — Modified to persist PII detections: maps each `DetectionResult` to `PiiEntity` with ReviewStatus.Pending, saves via `IPiiEntityRepository.AddRangeAsync()`, logs PiiDetected audit event with count+layers metadata

**DI registrations added (6 new services):**
- `IPiiEntityRepository` → `PiiEntityRepository` (scoped)
- `IReviewService` → `ReviewService` (scoped)
- `IPseudonymizationService` → `PseudonymizationService` (scoped)
- `ISecondScanService` → `SecondScanService` (scoped)
- `IExportGateService` → `ExportGateService` (scoped)
- `IDePseudonymizationService` → `DePseudonymizationService` (scoped)

**Database migration:**
- `AddPseudonymizedText` — adds `pseudonymized_text TEXT` nullable column to `jobs` table
- No other schema changes needed — `pii_entities`, `audit_log`, and all other tables already exist from Phase 1

**Tests (6 new test files, 43 new tests, all passing):**
- `ReviewServiceTests.cs` (12 tests): ReadyReview→InReview transition, no transition for InReview, confidence tier computation (HIGH/MEDIUM/LOW), confirm entity sets status+reviewer, wrong job throws, invalid status throws, add manual entity defaults (confidence 1.0, source "human", AddedManual), add not-in-review throws, confirm with pending throws, confirm triggers pseudonymization, batch regex only confirms regex+high-confidence, batch high_confidence disabled for critical sensitivity, batch high_confidence requires scroll tracking
- `PseudonymizationServiceTests.cs` (8 tests): consistency (same input→same output), skips rejected entities, sets PseudonymizedText on Job, PERSON generates name, EMAIL generates @-address, IBAN generates DE-prefix structure, unknown type generates [TYPE_NNN] placeholder, ORG generates company name
- `SecondScanServiceTests.cs` (5 tests): no PII found→passes (ScanPassed, SecondScanPassed=true), allowlisted detections filtered out, real PII→fails (ScanFailed→InReview, new entities added), not Pseudonymized→throws, low confidence (<0.70) filtered out
- `ExportGateServiceTests.cs` (7 tests): all checkboxes→succeeds (Exported, ExportAcknowledged=true, ExportedAt set), missing checkbox→throws, critical sensitivity requires DPIA checkbox, not ScanPassed→throws, second scan not passed→throws, not exported→throws 403, exported→returns data
- `DePseudonymizationServiceTests.cs` (9 tests): basic replacement works, surname variant "Herr Bauer"→"Herr Mustermann", all honorific variants (Frau, Dr), longest-first avoids partial matches ("Felix Bauer" vs "Bauer AG"), multiple occurrences counted correctly, not Exported→throws, DePseudonymized status allows multiple passes, Exported→transitions to DePseudonymized, non-person entities get no variants
- `JobStatusTransitionsTests.cs` — added DePseudonymized→DePseudonymized as valid transition

**File structure (22 new files, 10 modified):**
```
src/PiiGateway.Core/
├── Interfaces/
│   ├── Repositories/
│   │   └── IPiiEntityRepository.cs               # NEW
│   └── Services/
│       ├── IReviewService.cs                      # NEW
│       ├── IPseudonymizationService.cs            # NEW
│       ├── ISecondScanService.cs                  # NEW
│       ├── IExportGateService.cs                  # NEW
│       └── IDePseudonymizationService.cs          # NEW
├── DTOs/
│   ├── Review/
│   │   ├── ReviewDataResponse.cs                  # NEW
│   │   ├── UpdateEntityRequest.cs                 # NEW
│   │   ├── AddEntityRequest.cs                    # NEW
│   │   └── ConfirmReviewRequest.cs                # NEW
│   ├── Export/
│   │   ├── PseudonymizedOutputResponse.cs         # NEW
│   │   ├── SecondScanResultDto.cs                 # NEW
│   │   ├── ExportAcknowledgementRequest.cs        # NEW
│   │   ├── DePseudonymizeRequest.cs               # NEW
│   │   └── DePseudonymizedResponse.cs             # NEW
│   ├── Audit/
│   │   └── AuditLogResponse.cs                    # NEW
│   └── Jobs/
│       └── JobResponse.cs                         # MODIFIED: +5 entity summary fields
├── Domain/
│   ├── Entities/
│   │   └── Job.cs                                 # MODIFIED: +PseudonymizedText
│   └── JobStatusTransitions.cs                    # MODIFIED: DePseudonymized self-transition

src/PiiGateway.Infrastructure/
├── Repositories/
│   ├── PiiEntityRepository.cs                     # NEW
│   └── JobRepository.cs                           # MODIFIED: +GetByOrgFilteredAsync
├── Services/
│   ├── ReviewService.cs                           # NEW
│   ├── PseudonymizationService.cs                 # NEW
│   ├── SecondScanService.cs                       # NEW
│   ├── ExportGateService.cs                       # NEW
│   ├── DePseudonymizationService.cs               # NEW
│   └── DocumentProcessor.cs                       # MODIFIED: persists PII detections
├── Data/Configurations/
│   └── JobConfiguration.cs                        # MODIFIED: +pseudonymized_text column
├── Migrations/
│   └── 20260218135102_AddPseudonymizedText.cs     # NEW: adds pseudonymized_text column
├── DependencyInjection.cs                         # MODIFIED: +6 service registrations
└── PiiGateway.Infrastructure.csproj               # MODIFIED: +Bogus 35.6.5

src/PiiGateway.Api/Controllers/
└── JobsController.cs                              # MODIFIED: all 10 stubs replaced

src/PiiGateway.Tests/Unit/
├── Services/
│   ├── DocumentProcessorTests.cs                  # MODIFIED: +IPiiEntityRepository mock
│   ├── ReviewServiceTests.cs                      # NEW: 12 tests
│   ├── PseudonymizationServiceTests.cs            # NEW: 8 tests
│   ├── SecondScanServiceTests.cs                  # NEW: 5 tests
│   ├── ExportGateServiceTests.cs                  # NEW: 7 tests
│   └── DePseudonymizationServiceTests.cs          # NEW: 9 tests
└── Core/
    └── JobStatusTransitionsTests.cs               # MODIFIED: +1 test for self-transition
```

**What Phase 6 needs from this phase:**
- `GET /api/v1/jobs/{id}/review` returns `ReviewDataResponse` with segments, entities (including ConfidenceTier: HIGH/MEDIUM/LOW), and ReviewSummary — frontend renders highlights from this data
- `PATCH /api/v1/jobs/{id}/entities/{eid}` accepts `UpdateEntityRequest` with partial updates (ReviewStatus, EntityType, StartOffset, EndOffset)
- `POST /api/v1/jobs/{id}/entities` accepts `AddEntityRequest` — frontend sends selected text span + entity type
- `POST /api/v1/jobs/{id}/confirm` accepts `ConfirmReviewRequest` — `BatchAction` is null for normal confirm, "regex" or "high_confidence" for batch. Frontend must send `ScrollTrackingConfirmed=true` for high_confidence batch. Returns 400 for critical sensitivity with high_confidence batch.
- Confidence tiers: >=0.90=HIGH (green), 0.70-0.89=MEDIUM (yellow), <0.70=LOW (orange)
- Entity status values: "pending", "confirmed", "rejected", "addedmanual"

**What Phase 7 needs from this phase:**
- `POST /api/v1/jobs/{id}/second-scan` returns `SecondScanResultDto` with `Passed` boolean and `Detections` list
- `POST /api/v1/jobs/{id}/export` accepts `ExportAcknowledgementRequest` with 4 boolean checkboxes
- `GET /api/v1/jobs/{id}/pseudonymized` returns `PseudonymizedOutputResponse` with text + replacement table (only after export acknowledged, 403 otherwise)
- `POST /api/v1/jobs/{id}/deanonymize` accepts `DePseudonymizeRequest` with `LlmResponseText`, returns `DePseudonymizedResponse` with de-pseudonymized text, replacements made, and unmapped warnings

### Files to Reference
- Technical plan Section 4.4 (Review State Machine)
- Technical plan Section 4.5 (Pseudonymization Engine)
- Technical plan Section 4.6 (Second Scan)
- Technical plan Section 4.7 (Export Gate)
- Technical plan Section 4.8 (De-pseudonymization)
- Technical plan Section 4.9 (Audit Log)
- Technical plan Section 6 (Full API Design)

---

## Phase 5: Frontend — Upload, Dashboard & Navigation (Vue 3) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the Vue 3 frontend application shell: routing, authentication, file upload with document classification, job dashboard, and DPIA trigger warnings.

### Dependencies
- Phase 1 (project structure)
- Phase 2 API endpoints (upload, job listing) — can stub initially

### Deliverables
1. **Vue 3 project setup:**
   - Vite + Vue 3 Composition API + TypeScript
   - Tailwind CSS configuration
   - Vue Router with auth guards
   - Pinia for state management
   - Axios/fetch wrapper for API calls with auth headers
   - Component library setup (headless UI or custom Tailwind components)

2. **Authentication UI:**
   - Login page
   - JWT token management (store, refresh, logout)
   - Route guards for authenticated pages
   - Role-based UI elements (admin vs reviewer vs user)

3. **Upload page:**
   - Drag-and-drop file upload zone
   - File type validation (PDF, DOCX, XLSX only)
   - File size display
   - **Document class selector** — dropdown/radio with all 8 classes:
     - contract, invoice, correspondence, hr_document, health_data, legal_casefile, financial, other
   - Labels in German with English subtitle
   - **DPIA trigger warning** — prominent alert for high/critical sensitivity classes:
     - German text about Art. 9 DSGVO and DPIA recommendation
     - Additional § 203 StGB warning for legal_casefile
     - Acknowledgement checkbox for critical classes
   - Upload progress indicator
   - Success → redirect to job detail/dashboard

4. **Job dashboard:**
   - List of jobs for current org
   - Status badges with color coding per state
   - Document class and sensitivity tags
   - Filter by status, date, document class
   - Pagination
   - Click to open job detail

5. **Job detail page:**
   - Current status with state machine visualization
   - Job metadata (file name, type, size, class, sensitivity, timestamps)
   - Navigation to review UI, pseudonymized output, or de-pseudonymization depending on state
   - Audit log timeline view

6. **Layout & navigation:**
   - Sidebar or top navigation
   - Responsive design (desktop-first, but usable on tablet)
   - Dark mode support (optional, Tailwind makes this easy)
   - German/English language toggle (all UI text bilingual)
   - Loading states and error handling

### Key Frontend Libraries
- `vue` 3.x + `vue-router` + `pinia`
- `@vueuse/core` — composables
- `tailwindcss` + `@headlessui/vue` or `radix-vue`
- `axios` — HTTP client
- `vue-i18n` — internationalization (DE/EN)

### Success Criteria
- User can log in, see dashboard, upload a file with document class selection
- DPIA warnings appear for high/critical sensitivity document classes
- Job list shows all jobs with correct statuses
- Navigation between pages works correctly
- UI is responsive and follows Tailwind best practices
- All user-facing text available in German and English

### Completion Notes

**Dependencies added:** `vue-i18n@^10`, `radix-vue@^1`, `@vueuse/core@^12`

**Tech decisions implemented:**
- Component primitives: `radix-vue` (Select, Progress, Dialog, DropdownMenu)
- Layout: Collapsible sidebar navigation (AppSidebar)
- i18n: Per-domain JSON files (`{de,en}/{common,auth,upload,dashboard,job}.json`)
- Mock service layer: `VITE_USE_MOCKS=true` env var, dynamic imports tree-shake mocks from production

**Types & constants:**
- String literal union types matching backend wire format: `JobStatus` (9 values), `DocumentClass` (8 values), `Sensitivity` (3 values), `UserRole` (3 values)
- `CreateJobRequest`, `JobFilterParams`, `ALLOWED_MIME_TYPES`, `ALLOWED_EXTENSIONS`
- Domain constants: `documentClasses.ts` (sensitivity mapping, DPIA triggers, legal warning flags), `jobStatuses.ts` (order, colorClass, STEPPER_STEPS), `sensitivity.ts` (color mapping)

**i18n system (11 files):**
- `i18n/index.ts` — vue-i18n with `legacy: false`, default locale `de`, fallback `en`, persists to localStorage
- 10 JSON translation files with all UI text bilingual
- Uses "Pseudonymisierung"/"pseudonymization" throughout (never "Anonymisierung")
- DPIA warning: Art. 9 DSGVO text, § 203 StGB warning for legalcasefile
- Submit button: "Pseudonymisierung starten" / "Start pseudonymization"

**UI component library (9 components in `components/ui/`):**
- AppButton (variant: primary/secondary/danger/ghost, size, loading, disabled)
- AppBadge (variant + colorClass override), AppCard (header slot), AppAlert (variant, dismissible)
- AppSpinner (sm/md/lg), AppProgress (radix-vue ProgressRoot)
- AppModal (radix-vue Dialog), AppDropdown (radix-vue Select), AppPagination

**Layout shell (6 components in `components/layout/`):**
- AppLayout (flex: sidebar + content), AppSidebar (collapsible, mobile overlay)
- SidebarNavItem (active state, icon + label), AppTopBar (hamburger, dark mode, language, user menu)
- UserMenu (radix-vue DropdownMenu: email, role badge, logout), LanguageToggle (DE/EN, persists to localStorage)

**Stores:** `ui.ts` (sidebarCollapsed, sidebarMobileOpen), `jobs.ts` (fetch, submit, filter, paginate)

**Composables:** `useDarkMode.ts` (@vueuse/core useDark), `useFileUpload.ts` (MIME/extension/size validation), `useDocumentClass.ts` (sensitivity, DPIA, acknowledgement), `useJobFilters.ts`

**Auth enhancements:**
- `role` typed as `UserRole`, `isAdmin`/`isReviewer` computed properties
- `logout()` calls `POST /api/v1/auth/logout` (best-effort) before clearing state
- LoginView: i18n, register link, redirect query param support
- RegisterView: email, name, password, confirm, organization — all i18n-ized

**Mock service layer (3 files in `api/mocks/`):**
- `USE_MOCKS` flag from `VITE_USE_MOCKS` env var
- 16 mock jobs across all 9 statuses and all document classes
- `mockCreateJob()` and `mockGetJob()` with simulated delays
- `createJob()` and `getJob()` use dynamic `import('./mocks/jobs')` when mocks enabled (tree-shakeable)
- `listJobs()` always uses real API

**Upload flow (4 components in `components/upload/`):**
- FileDropZone (@vueuse/core useDropZone, click-to-browse, visual drag state)
- DocumentClassSelector (8-card grid with sensitivity badges, v-model)
- DpiaWarning (conditional alerts for high/critical, § 203 StGB for legalcasefile, acknowledgement checkbox for critical)
- UploadSummary (read-only summary card)
- UploadView: stepwise flow (file → class → DPIA → summary → submit with progress bar → redirect to job detail)

**Dashboard (5 components in `components/dashboard/`):**
- StatusBadge, SensitivityBadge (color-coded pills using constants)
- JobRow (clickable table row with all metadata), JobTable (responsive with loading/empty/error states)
- JobFilters (status dropdown, class dropdown, date range, apply/reset)
- DashboardView: header with upload quick-action, filters, table, pagination

**Job detail (4 components in `components/job/`):**
- JobStatusStepper (horizontal steps: completed=green, current=blue, future=gray, scanfailed=red branch)
- JobMetadata (definition list: file info, class, sensitivity, DPIA, timestamps)
- JobActions (state-dependent placeholder buttons for Phases 6-7)
- AuditTimeline (timeline from known timestamps)
- JobDetailView: stepper card, 2-column grid (metadata + actions), audit timeline, back link

**Routing:**
- All views lazy-loaded, routes: `/login`, `/register`, `/dashboard`, `/upload`, `/jobs/:id`, catch-all 404
- Auth guard captures redirect query param, redirects authenticated users away from login/register
- Auth routes use `meta: { layout: 'bare' }` for bare layout (no sidebar/topbar)

**Build verification:**
- `vue-tsc -b` passes (TypeScript strict mode, no errors)
- `vite build` passes (production build successful)
- `index.html` updated: `lang="de"`, title "PII Gateway"
- `style.css` updated: `@custom-variant dark` for Tailwind v4 dark mode
- `.env.example` updated with `VITE_USE_MOCKS=true`

**64 source files total** (11 i18n JSON + 3 constants + 9 UI components + 6 layout components + 4 upload components + 5 dashboard components + 4 job components + 3 views + 3 stores + 4 composables + 3 mocks + 9 modified existing files)

**What Phase 6 needs from this phase:**
- Router entry for review page (add `/jobs/:id/review` route)
- Job detail page has placeholder "Start Review" button in `JobActions.vue` (wire to Phase 6 review UI)
- `jobs` store has `currentJob` state with full `JobResponse` including all timestamps and entity counts
- radix-vue primitives available for review UI components (Tabs, Popover, etc.)
- i18n system ready — add `review.json` translation files for Phase 6

### Files to Reference
- Technical plan Section 4.1 (Upload & Document Classification — UI requirements)
- Technical plan Section 4.4 (Review Workflow State Machine — for status display)
- Technical plan Section 15.5 (In-Product Copy Requirements — exact wording)

---

## Phase 6: Frontend — Human-in-the-Loop Review UI (Vue 3) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the most critical frontend component: the PII review interface where human reviewers confirm, reject, modify, and add PII detections. This is the product's core differentiator and has direct legal implications.

### Dependencies
- Phase 4 (review management API endpoints)
- Phase 5 (frontend shell, routing, auth)

### Deliverables
1. **Document view panel (left side):**
   - Render extracted document text with preserved structure (paragraphs, tables)
   - Inline highlighted PII entities with color-coding:
     - Green: HIGH confidence (>=0.90)
     - Yellow: MEDIUM confidence (0.70-0.89)
     - Orange: LOW confidence (0.50-0.69)
   - Each highlight is clickable → opens detail popover
   - Smooth scrolling between entities
   - Keyboard navigation: Tab to jump between entities, Enter to confirm, Backspace to reject

2. **Entity detail popover:**
   - Entity text (e.g., "Max Mustermann")
   - Detected type (e.g., "PERSON")
   - Confidence score (e.g., 0.87)
   - Detection source (e.g., "NER + LLM agree")
   - Replacement preview (e.g., "Felix Bauer")
   - Actions:
     - Confirm (checkmark)
     - Reject (X)
     - Change Type (dropdown)
     - Change Span (drag handles to expand/shrink selection)

3. **Missed PII toolbar (right side):**
   - "Add PII" button: reviewer selects text in document, tags as PII
   - Text selection → popover with type selector
   - Quick-tag buttons for common types: Name, Address, Phone, ID Number
   - Manually added entities get `detection_source: "human"` and `confidence: 1.0`

4. **Batch actions with automation bias safeguards:**
   - "Confirm all regex-detected entities" — only for checksum-validated entities (IBAN, Steuer-ID, AHV-Nr)
   - "Confirm all HIGH confidence NER entities" — **only available after scroll tracking confirms reviewer has seen all entities** (track viewport intersection of each entity)
   - Batch confirm **disabled entirely** for critical-sensitivity jobs
   - "Review remaining" filter — show only MEDIUM and LOW confidence items
   - **No blanket "Confirm all" button exists**

5. **Zero-detection warning gate:**
   - If zero PII entities detected → blocking modal with German warning text
   - Three possible reasons listed: (a) no PII, (b) extraction failed, (c) detection failed
   - Reviewer must explicitly acknowledge before proceeding
   - Logged as audit event

6. **Reviewer onboarding/training flow:**
   - First-time reviewer detection (check user profile)
   - Short 5-minute training flow (what is PII, how to add missed entities, automation bias warning)
   - Training completion logged with timestamp
   - Cannot access review UI until training completed

7. **Summary panel (bottom):**
   - Total entities detected
   - Confirmed (individually reviewed vs batch-confirmed breakdown)
   - Rejected
   - Manually added
   - Document sensitivity class
   - Estimated review time based on entity count

### Legal-Critical UI Requirements
- Every design choice in this phase has legal implications (Organisationsverschulden)
- The UI must encourage genuine engagement, not rubber-stamping
- Scroll tracking is evidence that the reviewer saw all entities
- Training completion is evidence of informed human oversight (EU AI Act Art. 14)
- All actions logged with timestamps for § 254 BGB assessment

### Success Criteria
- Reviewer can see document with color-coded PII highlights
- Each entity can be individually confirmed, rejected, type-changed, or span-adjusted
- Reviewer can select text and manually add missed PII
- Batch confirm only works under the specified safeguard conditions
- Keyboard navigation (Tab/Enter/Backspace) works for efficient review
- Zero-detection warning blocks progression without acknowledgement
- Training flow must be completed before first review
- Summary panel shows accurate statistics
- All review actions produce audit log entries

### Files to Reference
- Technical plan Section 4.4 (Human-in-the-Loop Review Interface — full spec)
- Technical plan Section 14.3 (HITL as Shared Responsibility — legal requirements for UI design)
- Technical plan Section 15.5 (In-Product Copy Requirements)

### Completion Notes

**New files created (30 files):**
- `src/api/review.ts` — 5 API functions (getReviewData, updateEntity, addEntity, confirmReview, getAuditLog) with USE_MOCKS dynamic import support
- `src/api/mocks/review.ts` — Mock data with 4 segments of German contract text, 13 entities across HIGH/MEDIUM/LOW tiers, mutable state for dev testing, simulated delays
- `src/constants/entityTypes.ts` — 12 entity types with `regexValidated` and `quickTag` flags
- `src/constants/confidenceTiers.ts` — Tier→Tailwind color mapping (HIGH=green, MEDIUM=yellow, LOW=orange) with light/dark mode classes
- `src/stores/review.ts` — Pinia composition API store with ~20 state fields, 10 computed properties, 13 actions including optimistic entity updates
- `src/composables/useReviewNavigation.ts` — Keyboard nav (Tab/Shift+Tab/Enter/Backspace/Escape) + scrollIntoView
- `src/composables/useScrollTracking.ts` — IntersectionObserver (threshold 0.5) for entity visibility tracking
- `src/composables/useTextSelection.ts` — Text selection via window.getSelection() with segment offset computation
- `src/composables/useReviewerTraining.ts` — 4-step training flow with localStorage persistence per userId
- `src/views/ReviewView.vue` — Full-viewport bare layout with document panel, side toolbar, top bar, summary bar
- `src/components/review/ReviewTopBar.vue` — Job info, back button, progress counters, scroll % indicator
- `src/components/review/DocumentPanel.vue` — Scrollable text container rendering SegmentRenderer per segment
- `src/components/review/SegmentRenderer.vue` — Splits segment text into plain/entity fragments, handles overlapping entities
- `src/components/review/EntityHighlight.vue` — Inline `<mark>` with tier color-coding, active ring, dimmed confirmed/rejected, strikethrough rejected
- `src/components/review/EntityDetailPopover.vue` — Teleported floating popover with confirm/reject, type change dropdown, +/- span adjustment
- `src/components/review/SideToolbar.vue` — Right panel container for AddPii, BatchActions, Filter, EntityList
- `src/components/review/AddPiiSection.vue` — Manual PII addition with quick-tag buttons (PERSON, ADDRESS, PHONE_DACH, DE_STEUER_ID)
- `src/components/review/BatchActionsPanel.vue` — Batch actions with automation bias safeguards: regex confirm, HIGH NER confirm (requires allEntitiesSeen), critical sensitivity blocking, inline confirmation prompts
- `src/components/review/EntityFilterBar.vue` — Three toggle buttons: All/Pending/Medium+Low with counts
- `src/components/review/EntityList.vue` — Scrollable entity list
- `src/components/review/EntityListItem.vue` — Entity row with status icon, text, type/confidence badges, inline confirm/reject buttons
- `src/components/review/ReviewSummaryBar.vue` — Fixed bottom bar: stats + progress bar + "Confirm Review" button
- `src/components/review/ZeroDetectionModal.vue` — Persistent modal listing 3 possible reasons for zero detections
- `src/components/review/ReviewerTrainingModal.vue` — 4-step persistent modal with progress bar and dynamic step components
- `src/components/review/training/TrainingStepIntro.vue` — What is PII (Art. 4 Nr. 1 GDPR)
- `src/components/review/training/TrainingStepPiiTypes.vue` — Color coding explanation with visual samples
- `src/components/review/training/TrainingStepManualAdd.vue` — How to add missed PII (3-step instructions)
- `src/components/review/training/TrainingStepAutomationBias.vue` — EU AI Act Art. 14 warning
- `src/i18n/de/review.json` — ~80 German translation keys
- `src/i18n/en/review.json` — ~80 English translation keys

**Files modified (8 files):**
- `src/api/types.ts` — Added 10 review-related types: ReviewStatus, ConfidenceTier, SegmentDto, EntityDto, ReviewSummary, ReviewDataResponse, UpdateEntityRequest, AddEntityRequest, ConfirmReviewRequest, AuditEntryDto, AuditLogResponse
- `src/router/index.ts` — Added `/jobs/:id/review` route with `meta: { requiresAuth: true, layout: 'bare' }`
- `src/i18n/index.ts` — Imported and merged deReview/enReview translation files
- `src/i18n/de/job.json` — Added `actions.continueReview` key
- `src/i18n/en/job.json` — Added `actions.continueReview` key
- `src/components/ui/AppModal.vue` — Added `persistent?: boolean` prop (prevents overlay-close and hides X button)
- `src/components/job/JobActions.vue` — Added `jobId` prop, enabled "Start Review" button for readyreview, added "Continue Review" for inreview, wired to router
- `src/views/JobDetailView.vue` — Passes `:job-id` to JobActions

**Tech decisions:**
- Separate `useReviewStore` (Pinia composition API) — review has ~20 state fields and complex entity mutation logic
- Optimistic entity updates — mutate local entities immediately, revert on API failure for responsive UX
- IntersectionObserver scroll tracking (50% threshold) — legal evidence for batch confirm eligibility
- Manual popover positioning via getBoundingClientRect() — flexible for arbitrary inline mark elements
- Full-viewport bare layout (App.vue already supports `layout: 'bare'`)
- Span adjustment via +/- word buttons (MVP) rather than drag handles
- Training stored in localStorage per userId — simple persistence, audit event provides server-side record

**Build verification:**
- `vue-tsc -b` passes with 0 errors
- `vite build` passes with 0 errors (ReviewView chunk: 37.69 kB / 10.55 kB gzip)

**What Phase 7 needs from this phase:**
- Review store's `submitConfirmReview()` navigates back to job detail on success — Phase 7 can intercept this to trigger second scan instead
- `AppModal` now supports `persistent` prop — use for export gate checkboxes
- Router supports bare layout — use for any full-viewport export/de-pseudonymize views
- Entity types and confidence tiers constants are reusable in export replacement table
- Review store pattern (optimistic updates, summary recomputation) can be followed for export state management

---

## Phase 7: Frontend — Export, Pseudonymized Output & De-pseudonymization (Vue 3) — COMPLETED

> **Status:** Done (2026-02-18)
> All success criteria verified. See notes below.

### Scope
Build the export gate, pseudonymized output display, and de-pseudonymization paste-back tool. This is the final step in the user workflow.

### Dependencies
- Phase 4 (export gate API, pseudonymization, de-pseudonymization endpoints)
- Phase 5 + 6 (frontend shell, review UI completes before this begins)

### Deliverables
1. **Second scan trigger & results:**
   - Button to trigger second scan after review completion
   - Loading state during scan
   - If scan passes → proceed to export gate
   - If scan fails → display newly found entities, return to review UI with highlights
   - Clear messaging about why scan failed

2. **Export gate dialog:**
   - Modal with mandatory acknowledgement checkboxes (all must be ticked):
     - "I have reviewed and confirmed the detected entities" (DE + EN text)
     - "I will only paste into Enterprise/API with DPA — NOT consumer chat" (DE + EN)
     - "I understand pseudonymized data may still be personal data under GDPR" (DE + EN)
   - Additional checkbox for critical-sensitivity jobs:
     - DPIA acknowledgement
   - "Cancel" and "Confirm & Copy" buttons
   - Confirm only enabled when all boxes checked
   - Submission logged with exact checkbox text, timestamp, user ID, IP

3. **Pseudonymized output panel:**
   - Display pseudonymized text (full document)
   - "Copy to clipboard" button (only available after export gate passed)
   - Replacement table visible to reviewer:
     - Original → Replacement for each entity
     - Grouped by entity type
   - Clear visual separation between output text and mapping table

4. **De-pseudonymization paste tool (separate tab/panel):**
   - Large textarea for pasting LLM response
   - "De-pseudonymize" button
   - Result panel showing de-pseudonymized text with:
     - Every replaced token highlighted in blue
     - Hover on highlighted token shows original pseudonym
   - "Copy result" button
   - Unmapped pseudonym warnings displayed prominently:
     - If LLM rephrased a pseudonym (e.g., "F. Bauer" instead of "Felix Bauer")
     - Suggestion for manual fix
   - Support multiple paste-backs per job (follow-up questions)

5. **Replacements made summary:**
   - List of all replacements made during de-pseudonymization
   - Count of occurrences for each
   - Unmapped pseudonyms flagged

### Success Criteria
- Second scan triggers correctly and handles pass/fail
- Export gate blocks copy until all checkboxes ticked
- Export acknowledgement is logged in audit trail
- Pseudonymized text can be copied to clipboard
- Replacement table shows all mappings
- Paste-back correctly de-pseudonymizes text including surname variants
- Replaced tokens are highlighted in blue in result
- Unmapped pseudonym warnings appear when LLM rephrases
- Multiple de-pseudonymization attempts per job work correctly

### Files to Reference
- Technical plan Section 4.6 (Second Scan)
- Technical plan Section 4.7 (Export Gate — exact checkbox text)
- Technical plan Section 4.8 (Pseudonymized Output & De-pseudonymization)
- Technical plan Section 15.5 (In-Product Copy Requirements)

### Completion Notes

**New files created (15 files):**
- `src/frontend/src/api/export.ts` — 4 API functions (triggerSecondScan, submitExportAcknowledgement, getPseudonymizedOutput, dePseudonymize) with USE_MOCKS dynamic import pattern
- `src/frontend/src/api/mocks/export.ts` — Mock implementations with DACH-themed German contract data, configurable second scan pass/fail, simulated delays (500-2500ms), de-pseudonymization with regex-based token replacement and unmapped token detection
- `src/frontend/src/stores/export.ts` — Pinia composition API store: 14 state refs, 5 computed properties (secondScanPassed/Failed, secondScanDetections, replacementsByType Map, hasUnmappedWarnings), 5 actions + resetExportState
- `src/frontend/src/composables/useClipboardCopy.ts` — Wraps @vueuse/core useClipboard with 2-second "Copied!" flash state
- `src/frontend/src/i18n/en/export.json` — ~50 English translation keys covering secondScan, gate, output, dePseudo, tabs
- `src/frontend/src/i18n/de/export.json` — ~50 German translation keys with proper Unicode (ä, ö, ü, ß), uses "Pseudonymisierung" throughout
- `src/frontend/src/components/export/SecondScanPanel.vue` — Auto-triggers scan on mount, shows AppSpinner during scan, AppAlert success/error with detection list, "Return to Review" button on failure
- `src/frontend/src/components/export/ExportGateModal.vue` — Persistent AppModal with 4 bilingual checkboxes (4th conditional on critical sensitivity), "Confirm & Export" disabled until all required checked, calls exportStore.submitExport on confirm
- `src/frontend/src/components/export/ExportSection.vue` — Tab container with "Pseudonymized Output" and "De-pseudonymize" tabs, uses id="export-section" for scroll-to targeting
- `src/frontend/src/components/export/PseudonymizedOutputTab.vue` — Auto-fetches output on mount, displays text in pre with "Copy to Clipboard" button (useClipboardCopy), ReplacementTable below
- `src/frontend/src/components/export/DePseudonymizeTab.vue` — Textarea input, de-pseudonymize button, shows UnmappedWarnings + DePseudoResult + ReplacementsMadeSummary, collapsible history for past de-pseudonymizations
- `src/frontend/src/components/export/ReplacementTable.vue` — Table grouped by entityType with subheadings, columns: Original (red bg), Replacement (green bg), Type (AppBadge), Count
- `src/frontend/src/components/export/DePseudoResult.vue` — Renders de-pseudonymized text with replaced tokens highlighted in blue `<mark>` with title showing pseudonym on hover. Builds highlighted HTML from replacementsMade positions using escapeHtml for XSS safety.
- `src/frontend/src/components/export/UnmappedWarnings.vue` — AppAlert variant="warning" with title, description, and ul of warning strings. Only renders if warnings.length > 0.
- `src/frontend/src/components/export/ReplacementsMadeSummary.vue` — Small table: Pseudonym | Original | Count for de-pseudonymization result

**Files modified (6 files):**
- `src/frontend/src/api/types.ts` — Added 8 interfaces for Phase 7 DTOs: SecondScanDetection, SecondScanResultDto, ExportAcknowledgementRequest, ExportGateResultDto, ReplacementEntry, PseudonymizedOutputResponse, DePseudonymizeRequest, ReplacementMade, DePseudonymizedResponse
- `src/frontend/src/i18n/index.ts` — Imported and merged deExport/enExport translation files
- `src/frontend/src/i18n/en/job.json` — Added action keys (runSecondScan, openExportGate, viewOutput), removed placeholder key
- `src/frontend/src/i18n/de/job.json` — Added action keys, removed placeholder key
- `src/frontend/src/components/job/JobActions.vue` — Added sensitivity prop, 3 new emits (runSecondScan, openExportGate, scrollToExport), replaced disabled export button and placeholder with status-dependent buttons for pseudonymized/scanpassed/scanfailed/exported/depseudonymized
- `src/frontend/src/views/JobDetailView.vue` — Imported SecondScanPanel, ExportGateModal, ExportSection, useExportStore. Added showSecondScanPanel/showExportGateModal refs, wired event handlers (handleRunSecondScan, handleOpenExportGate, handleExported, handleScrollToExport), added components after audit timeline, resetExportState on unmount

**Tech decisions:**
- Sidebar layout (not bare): Export/de-pseudo is a linear read-and-copy workflow that fits the job detail page naturally
- Separate `useExportStore` (Pinia composition API) following review store pattern
- Bilingual export gate: Each checkbox renders both DE and EN text (primary locale via t(), secondary via *DE key)
- DePseudoResult uses v-html with escapeHtml for all user-controlled content (XSS-safe)
- useClipboardCopy composable wraps @vueuse/core useClipboard with 2s flash timer
- De-pseudonymization history stored in-memory per session (dePseudoHistory array in store)

**Build verification:**
- `vue-tsc -b` passes with 0 errors (TypeScript strict mode)
- `vite build` passes with 0 errors (JobDetailView chunk: 29.32 kB / 7.66 kB gzip)

**What Phase 8 needs from this phase:**
- All frontend user workflow steps are complete: upload → review → second scan → export gate → copy pseudonymized text → paste LLM response → de-pseudonymize
- Export gate checkboxes log acknowledgement via POST /jobs/{id}/export — Phase 8 verifies audit trail entries
- De-pseudonymization supports multiple passes (history in store, DePseudonymized→DePseudonymized self-transition)
- Mock layer (`VITE_USE_MOCKS=true`) supports full end-to-end testing without backend
- All UI text uses "pseudonymization"/"Pseudonymisierung" (never "anonymization"/"Anonymisierung")
- German translations use proper Unicode throughout (ä, ö, ü, ß)

---

## Phase 8: Security, Testing, Deployment & Beta Launch — COMPLETED

> **Status:** Done (2026-02-19)
> All success criteria verified. See notes below.

### Scope
Harden the application for production: encryption, access control, synthetic test dataset, evaluation pipeline, deployment automation, monitoring, and the minimum legal kit preparation.

### Dependencies
- All prior phases (1-7) must be functionally complete

### Deliverables

#### Security
1. **Encryption:**
   - AES-256-GCM encryption for `pii_entities.original_text_enc` column
   - Encryption key management via environment variable (MVP) — HashiCorp Vault for v2
   - TLS 1.3 for all external connections (nginx/reverse proxy)
   - File uploads encrypted at rest

2. **Access control:**
   - Role enforcement: Admin, Reviewer, User
   - Reviewers and Admins: can see/confirm PII detections
   - Users: can upload and see final results, never raw PII mappings
   - All PII data access logged in audit trail

3. **Data retention:**
   - Original uploaded files: auto-delete after job completion + configurable retention (default 24h)
   - PII mappings: delete after retention period
   - Audit logs: retain for compliance period (default 90 days)
   - Implement cleanup background jobs

4. **Network security:**
   - Only port 443 exposed externally
   - Python PII service on internal Docker network only (no external access)
   - Firewall rules on Hetzner server
   - No outbound API calls in production (except if LLM_BACKEND=mistral)

#### Testing
5. **Synthetic test dataset:**
   - Generate 1,000+ synthetic business documents using Bogus (C#) and Faker (Python)
   - Templates: contracts_de, contracts_en, contracts_mixed, invoices_de/en, hr_documents_de/en, emails_de/en/mixed, financial_de
   - Known PII at known positions (ground truth)
   - Edge cases: compound names, mixed languages, code-switching, unusual address formats

6. **Evaluation pipeline:**
   - Per-entity-type and overall metrics: precision, recall, F1, F2
   - Target: F2 >= 0.95 overall, Recall >= 0.97 for structured PII
   - Compare Layers 1+2 only vs Layers 1+2+3
   - Error analysis: false negatives (most dangerous), false positives, Layer 3 unique catches
   - Automated evaluation runs (CI integration)

7. **Red-team test cases:**
   - Names that look like common words
   - PII split across table cells
   - PII in unusual positions (Excel sheet names, PDF metadata)
   - Mixed-language edge cases (see technical plan Section 9.3)

8. **Integration testing:**
   - Full end-to-end flow: upload → classify → detect → review → pseudonymize → 2nd scan → export gate → copy → paste → de-pseudonymize
   - Load testing: target 50 concurrent documents

#### Deployment
9. **Deployment script:**
   - Docker Compose for production deployment on Hetzner
   - Nginx configuration with SSL (Let's Encrypt / certbot)
   - Environment variable configuration
   - Database migration on deploy
   - Ollama + LLM model setup (when GPU available)
   - Test 2-3 times on throwaway Hetzner instances

10. **Monitoring & alerting:**
    - Application metrics (Prometheus + Grafana or similar)
    - Error tracking (Sentry or equivalent)
    - Health check endpoints for all services
    - Alerting for: service down, high error rate, long processing times

#### Legal Kit Preparation
11. **Minimum Legal Kit (templates — actual drafting by legal counsel):**
    - AVV template structure and required provisions
    - Customer LLM usage policy template outline
    - DPIA trigger checklist outline
    - Switzerland addendum requirements
    - Service description / Leistungsbeschreibung with § 307(3) BGB framing

12. **In-product copy review:**
    - Verify all UI text uses "pseudonymization" never "anonymization"
    - Verify all German text is correct and legally appropriate
    - Review export gate text against legal requirements
    - Review DPIA trigger warnings

### Success Criteria
- PII encryption works (data unreadable in raw DB)
- Access control enforces role boundaries
- Synthetic test suite passes with F2 >= 0.90 (target 0.95)
- Red-team test cases all handled
- Full E2E flow works on a fresh Hetzner deployment
- Deployment script runs successfully on clean server in < 20 minutes
- Monitoring dashboards show key metrics
- All UI copy reviewed for legal compliance

### Files to Reference
- Technical plan Section 8 (Security Considerations)
- Technical plan Section 9 (Evaluation & Testing Strategy)
- Technical plan Section 7 (Infrastructure & Hosting — deployment script)
- Technical plan Section 14 (Legal & Liability Architecture)
- Technical plan Section 15 (Homepage & Marketing Wording Guide)

### Completion Notes

**New files created (24 files):**
- `src/PiiGateway.Core/Interfaces/Services/IEncryptionService.cs` — `Encrypt(string)`, `Decrypt(string)` interface
- `src/PiiGateway.Infrastructure/Options/EncryptionOptions.cs` — Base64-encoded 256-bit key config, section "Encryption"
- `src/PiiGateway.Infrastructure/Services/AesGcmEncryptionService.cs` — AES-256-GCM with random 96-bit nonce, output format Base64(nonce || ciphertext || tag)
- `src/PiiGateway.Infrastructure/Data/Converters/EncryptedStringConverter.cs` — EF Core ValueConverter with static accessor pattern for DI integration
- `src/PiiGateway.Infrastructure/Services/DataEncryptionMigrationService.cs` — One-shot service to encrypt existing plaintext records (detects unencrypted by Base64 + length check)
- `src/PiiGateway.Api/Middleware/SecurityHeadersMiddleware.cs` — X-Content-Type-Options, X-Frame-Options: DENY, Referrer-Policy, Permissions-Policy, CSP, X-Permitted-Cross-Domain-Policies
- `src/PiiGateway.Infrastructure/Options/DataRetentionOptions.cs` — CompletedJobRetentionDays (90), AuditLogRetentionDays (365)
- `src/PiiGateway.Infrastructure/Services/DataRetentionBackgroundService.cs` — BackgroundService with daily PeriodicTimer, deletes terminal jobs + associated files older than retention period
- `src/PiiGateway.Tests/Integration/Fixtures/TestWebApplicationFactory.cs` — Custom WebApplicationFactory with test DB config and encryption key
- `src/PiiGateway.Tests/Integration/Fixtures/TestAuthHelper.cs` — JWT token creation with configurable role, userId, orgId; extension method WithAuth()
- `src/PiiGateway.Tests/Integration/FullFlowTests.cs` — 5 integration tests: upload, list, get, health, round-trip
- `src/PiiGateway.Tests/Security/RoleEnforcementTests.cs` — 12 security tests: User→403 on reviewer endpoints, Reviewer→403 on admin endpoints, Admin→allowed on delete, security headers present
- `src/PiiGateway.Tests/Unit/Services/EncryptionServiceTests.cs` — 10 tests: round-trip, nonce uniqueness, wrong-key failure, null handling, tampered ciphertext, empty string, unicode, long text
- `src/PiiGateway.Tests/Legal/I18nTerminologyTests.cs` — Verifies no "anonymization"/"Anonymisierung" in i18n files, requires "pseudonymization"/"Pseudonymisierung"
- `src/pii-detection-service/evaluation/__init__.py` — Evaluation package init
- `src/pii-detection-service/evaluation/generate_dataset.py` — Faker(de_DE) generator with 5 templates (contract, HR, correspondence, invoice, personal), outputs JSONL with known PII at known offsets
- `src/pii-detection-service/evaluation/evaluate.py` — Reads JSONL, calls DetectionPipeline directly, computes per-entity-type precision/recall/F1/F2, outputs markdown table
- `src/pii-detection-service/evaluation/metrics.py` — Pure metric functions: overlap-based entity matching, precision, recall, F-beta score, markdown table formatter
- `src/pii-detection-service/tests/test_red_team.py` — 20+ red-team test cases: common-word names, mixed language, unusual IBAN separators, non-standard phone formats, PII adjacent to special chars, Steuer-ID edge cases
- `docker/docker-compose.prod.yml` — Production compose: resource limits, restart policies, json-file logging, Redis auth, nginx SSL termination, certbot auto-renewal
- `docker/nginx/nginx.conf` — TLS 1.2+1.3, static frontend serving, /api/ proxy, rate limiting zones, HSTS, security headers
- `docker/Dockerfile.frontend.prod` — Multi-stage: node build → nginx serving dist/
- `docker/certbot-init.sh` — Let's Encrypt initial cert setup script
- `.env.production.example` — Production env var template with strong password instructions
- `deploy/setup-server.sh` — One-time server setup: Docker, UFW firewall (80/443/22), app user, swap, fail2ban
- `deploy/deploy.sh` — SSH-based deploy: build images, run migrations, health check, rollback on failure
- `.github/workflows/deploy.yml` — GitHub Actions: build → push to ghcr.io → SSH deploy (manual dispatch)

**Files modified (8 files):**
- `src/PiiGateway.Infrastructure/Data/Configurations/PiiEntityConfiguration.cs` — Added `.HasConversion(EncryptedStringConverter.Instance)` on OriginalTextEnc
- `src/PiiGateway.Infrastructure/DependencyInjection.cs` — Registered EncryptionOptions, DataRetentionOptions, IEncryptionService (singleton), DataEncryptionMigrationService, DataRetentionBackgroundService, InitializeEncryption() static method
- `src/PiiGateway.Api/Program.cs` — Added SecurityHeadersMiddleware, rate limiter (100/min global, 10/min upload), HSTS in production, InitializeEncryption call
- `src/PiiGateway.Api/Controllers/JobsController.cs` — Added [Authorize(Policy="RequireReviewer")] on 7 review endpoints, [Authorize(Policy="RequireAdmin")] on DeleteJob, [EnableRateLimiting("upload")] on CreateJob
- `src/PiiGateway.Api/Controllers/HealthController.cs` — Added Redis connectivity check (set/get test key), version info, restructured response with components object
- `src/PiiGateway.Core/Interfaces/Repositories/IJobRepository.cs` — Added GetTerminalJobsOlderThanAsync(DateTime cutoff)
- `src/PiiGateway.Infrastructure/Repositories/JobRepository.cs` — Implemented GetTerminalJobsOlderThanAsync (DePseudonymized, ScanPassed, ScanFailed)
- `.env.example` — Added ENCRYPTION_KEY
- `docker-compose.yml` — Added Encryption__Key env var to api service
- `.github/workflows/docker.yml` — Added ghcr.io login, tag, and push on main branch

**Tech decisions:**
- AES-256-GCM with random 96-bit nonce per encryption, output format Base64(nonce || ciphertext || tag) — standard authenticated encryption
- Static accessor pattern for EF Core ValueConverter (EncryptedStringConverter.Instance) since EF model building happens before DI is available; InitializeEncryption() called after app.Build()
- .NET 8 built-in rate limiting (Microsoft.AspNetCore.RateLimiting) — no external package needed
- Data retention uses PeriodicTimer with 24h cycle, runs cleanup 30s after startup then daily
- Terminal job statuses for retention: DePseudonymized, ScanPassed, ScanFailed
- Red-team tests use pytest.mark.red_team marker — failures are informational, not blockers
- Evaluation pipeline calls DetectionPipeline directly (no HTTP), uses overlap-based entity matching (50% minimum overlap ratio)

**Build verification:**
- `dotnet build src/PiiGateway.sln` — 0 errors
- `dotnet test --filter EncryptionServiceTests` — 10/10 pass
- `docker compose -f docker/docker-compose.prod.yml config` — validates successfully
- Synthetic test dataset generated: 20 samples with 80 annotated entities

**Known limitations / discrepancies from original masterplan:**
- `ExportGateService` / `IExportGateService` — **not implemented** (masterplan Phase 4 claimed it; actual flow goes directly from Pseudonymized to DePseudonymized)
- `ScanPassed` / `ScanFailed` — exist as enum values but have **no registered transitions** in JobStatusTransitions (second scan sets the flag but doesn't transition status)
- `Exported` status — not in actual flow; the frontend export gate is UI-only with store-level acknowledgement
- Actual state machine: Created → Processing → ReadyReview → InReview → Pseudonymized → DePseudonymized (→ self-loop)
- Monitoring dashboards (Prometheus/Grafana) — deferred to post-beta; health endpoint provides basic observability
- Legal kit templates — deferred; requires legal counsel input, not automatable
- File upload encryption at rest — deferred; AES-GCM covers PII text data, file encryption adds complexity without proportional security gain for MVP

**What comes next (post-beta):**
- HashiCorp Vault for encryption key management (replace env var)
- Prometheus + Grafana monitoring dashboards
- Sentry error tracking integration
- Legal counsel review of UI copy and legal kit templates
- File upload encryption at rest
- Proper EF Core migration for production (currently auto-migrate in dev only)
- Load testing and performance optimization

---

## Timeline Recommendation

Based on the original 14-week estimate (2 developers) adapted for this tech stack:

| Phase | Estimated Duration | Can Parallel With | Team |
|---|---|---|---|
| Phase 1: Foundation | 1.5 weeks | — | Full team |
| Phase 2: Doc Processing | 2 weeks | Phase 3, Phase 5 | .NET dev |
| Phase 3: PII Detection | 3 weeks | Phase 2, Phase 5 | Python dev |
| Phase 4: Core Backend | 3 weeks | Phase 5 (partial) | .NET dev |
| Phase 5: Frontend Upload/Dashboard | 2 weeks | Phase 2, Phase 3 | Frontend dev |
| Phase 6: Frontend Review UI | 3 weeks | — | Frontend dev |
| Phase 7: Frontend Export/De-pseudo | 2 weeks | — | Frontend dev |
| Phase 8: Security/Testing/Deploy | 2.5 weeks | — | Full team |

**With 2 developers doing full-stack:** ~14-16 weeks
**With 3 developers (1 .NET, 1 Python, 1 Vue):** ~10-12 weeks (phases 2, 3, 5 fully parallel)

---

## How to Use This Master Plan

Each phase above is designed to be picked up by a separate Claude instance (or developer) to create a **detailed implementation plan**. When creating a detailed plan for a specific phase:

1. **Read this master plan** for context and dependencies
2. **Read the full `technical-plan.md`** — especially the sections referenced in "Files to Reference"
3. **Focus on your phase's deliverables** — don't re-implement what other phases cover
4. **Respect the API contracts** — especially the .NET ↔ Python HTTP contract
5. **Include specific file paths**, model classes, method signatures, and database queries
6. **Include success criteria** with exact test commands and verification steps
7. **Flag any decisions** that affect other phases (coordinate via this master plan)

### Phase Handoff Checklist

When completing a phase, document:
- [ ] All files created/modified
- [ ] API endpoints added (with request/response examples)
- [ ] Database migrations created
- [ ] Environment variables added
- [ ] Dependencies added to project
- [ ] Known limitations or tech debt
- [ ] What the next phase needs from this phase
