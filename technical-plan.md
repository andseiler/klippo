# Technical Plan: DACH PII Detection & Pseudonymization Gateway MVP

## Human-in-the-Loop Architecture for Safe LLM Usage

---

## 1. Product Vision

A web-based service where DACH-region businesses upload documents (Excel, Word, PDF), the system detects and highlights PII with confidence scores, a human reviewer confirms or corrects the detections, and only then is the pseudonymized output made available for the user to copy into their own LLM account. The LLM response can be pasted back for automated de-pseudonymization.

> **Critical terminology note:** What this tool performs is **pseudonymization** (reversible replacement with a stored mapping), NOT anonymization in the GDPR sense. Truly anonymized data is no longer personal data and falls outside GDPR scope — but because we retain a re-identification mapping, GDPR continues to apply to the processed data. This distinction must be reflected consistently in all product copy, documentation, marketing, contracts, and UI text. We use "PII detection" and "pseudonymization" throughout. Never claim the output is "anonymous" or "GDPR no longer applies."

> **Service definition (§ 307(3) BGB Leistungsbeschreibung):** The product is a **probabilistic PII detection assistant** that supports human reviewers in identifying personal data. It does **not** guarantee complete PII removal. The human review step is an independent obligation of the customer (data controller). This framing is load-bearing for the entire liability architecture — see Section 14.

**Core flow:**

```
Upload → Select Document Class → Extract Text → Detect PII (multi-layer) →
Human Review UI → Pseudonymize → Second Scan (Defense-in-Depth) →
Export Gate (Acknowledgement) → Copy pseudonymized text →
User pastes to their own LLM (Enterprise/API) →
Paste LLM response back → Click "De-pseudonymize" → Done
```

---

## 2. MVP Scope Decisions

### In Scope (MVP)
- File types: PDF, DOCX, XLSX (covers 90% of business use cases)
- Languages: German (DE-DE, DE-AT, DE-CH) and English (EN)
- PII types: Names, addresses, IBANs, phone numbers, email addresses, dates of birth, Steuer-ID (DE), AHV-Nr (CH), Sozialversicherungsnummer (AT), company names, Handelsregisternummer
- Document class selection at upload (Vertrag, Rechnung, HR, Gesundheit, Rechtsakte, etc.) with DPIA trigger warnings for sensitive categories
- Multi-layer PII detection with confidence scoring
- Human-in-the-loop review UI with automation bias safeguards
- Second scan (Defense-in-Depth) over pseudonymized output before export
- Export gate with mandatory acknowledgement (Enterprise/API LLM usage, no consumer chat)
- One-click copy of pseudonymized output to clipboard
- Paste-back text field with one-click de-pseudonymization (string replacement using stored mapping)
- Single-tenant deployment (one customer = one instance)
- Web-based review UI with reviewer onboarding/training requirement
- Audit log of all detection, review, export, and de-pseudonymization events
- Minimum Legal Kit: AVV template, customer LLM usage policy template, DPIA trigger checklist, Switzerland addendum

### Out of Scope (MVP)
- Scanned/image-based PDFs (OCR) — add in v2
- Languages beyond German and English
- Direct LLM API integration / automatic proxy to Claude/GPT — add in v2 once de-pseudonymization logic is battle-tested
- Multi-tenant SaaS with shared infrastructure
- Real-time/streaming pseudonymization
- Quasi-identifier detection (combinatorial re-identification risk)
- Mobile apps
- On-premise customer deployment (MVP runs on your DACH-hosted cloud)

---

## 3. System Architecture

### 3.1 High-Level Components

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Frontend (React)                             │
│ ┌──────────┐ ┌────────────────┐ ┌──────────┐ ┌────────┐ ┌────────┐ │
│ │ Upload + │ │ Review/Confirm │ │ 2nd Scan │ │ Export │ │Paste & │ │
│ │ Doc Class│ │ UI (HITL)      │ │ + Output │ │ Gate   │ │De-pseu │ │
│ │ Selector │ │                │ │          │ │        │ │ Tool   │ │
│ └──────────┘ └────────────────┘ └──────────┘ └────────┘ └────────┘ │
└────────────────────────────────┬───────────────────────────────────-┘
                                 │ HTTPS
┌────────────────────────────────▼───────────────────────────────────-┐
│                      API Gateway (FastAPI)                           │
│  /upload  /jobs/{id}/review  /jobs/{id}/confirm                     │
│  /jobs/{id}/second-scan  /jobs/{id}/export  /jobs/{id}/deanonymize  │
└──┬──────────┬──────────────┬───────────┬──────────┬────────────────-┘
   │          │              │           │          │
   ▼          ▼              ▼           ▼          ▼
┌──────┐ ┌────────┐  ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Doc  │ │  PII   │  │ Review   │ │ De-pseu  │ │ Second   │
│ Proc │ │ Detect │  │ State    │ │ Service  │ │ Scan     │
│ Svc  │ │ Svc    │  │ Manager  │ │ (mapping)│ │ Service  │
└──┬───┘ └───┬────┘  └────┬─────┘ └────┬─────┘ └────┬─────┘
   │         │            │            │             │
   ▼         ▼            ▼            ▼             ▼
┌──────────────────────────────────────────────────────────┐
│                   PostgreSQL + Redis                      │
│ (Jobs, PII mappings, audit logs, review state, doc class)│
└──────────────────────────────────────────────────────────┘
```

### 3.2 Component Responsibilities

| Component | Tech | Role |
|---|---|---|
| **Frontend** | React + TypeScript | Upload with document class selector, review UI with inline PII highlighting, export gate with acknowledgement, de-pseudonymization paste tool |
| **API Gateway** | FastAPI (Python) | Auth, routing, rate limiting, request validation |
| **Document Processor** | Apache Tika + python-docx + openpyxl | Extract text from PDF/DOCX/XLSX while preserving structure metadata |
| **PII Detection Service** | Presidio + custom recognizers + local LLM | Multi-layer PII detection with confidence scores |
| **Review State Manager** | FastAPI + PostgreSQL | Track human review decisions, manage job lifecycle, enforce review completeness |
| **Second Scan Service** | Same pipeline as PII Detection | Defense-in-depth re-scan of pseudonymized output to catch residual PII |
| **LLM Proxy** | — | **Removed from MVP.** User copies pseudonymized text to their own Claude/GPT account manually |
| **De-pseudonymization Service** | Python + FastAPI | Accept pasted LLM response text, replace pseudonyms with originals using stored mapping |
| **Database** | PostgreSQL 16 | Jobs, PII entity mappings, audit trail, user accounts, document classifications |
| **Cache/Queue** | Redis + Celery | Job queue for async document processing, session cache |

---

## 4. Detailed Component Design

### 4.1 Upload & Document Classification

Before document processing begins, the user must select a **document class**. This drives sensitivity settings throughout the pipeline.

```python
DOCUMENT_CLASSES = {
    "contract":       {"label": "Vertrag / Contract",          "sensitivity": "standard", "dpia_trigger": False},
    "invoice":        {"label": "Rechnung / Invoice",          "sensitivity": "standard", "dpia_trigger": False},
    "correspondence": {"label": "Geschäftskorrespondenz",      "sensitivity": "standard", "dpia_trigger": False},
    "hr_document":    {"label": "HR / Personaldokument",       "sensitivity": "high",     "dpia_trigger": True},
    "health_data":    {"label": "Gesundheitsdaten",            "sensitivity": "critical",  "dpia_trigger": True},
    "legal_casefile": {"label": "Rechtsakte / Legal Case File","sensitivity": "critical",  "dpia_trigger": True},
    "financial":      {"label": "Finanzdokument",              "sensitivity": "high",     "dpia_trigger": False},
    "other":          {"label": "Sonstiges / Other",           "sensitivity": "standard", "dpia_trigger": False},
}
```

**Behavior for high/critical sensitivity documents:**
- UI shows prominent warning: *"Dieses Dokument enthält wahrscheinlich besondere Kategorien personenbezogener Daten (Art. 9 DSGVO). Wir empfehlen dringend eine Datenschutz-Folgenabschätzung (DPIA) vor der Verarbeitung."*
- Default confidence threshold for review is lowered (more entities flagged for manual inspection)
- Audit log tags the job as `sensitivity: high/critical`
- "Confirm all" batch action is **disabled** for critical-sensitivity jobs — every entity must be individually reviewed
- For `legal_casefile`: additional warning about § 203 StGB implications

### 4.2 Document Processing Service

**Purpose:** Convert uploaded files into structured text segments that can be analyzed for PII.

**Input:** Raw file bytes + file type + document class
**Output:** List of `TextSegment` objects with positional metadata

```python
@dataclass
class TextSegment:
    id: str                    # unique segment ID
    text: str                  # raw extracted text
    source_type: str           # "paragraph", "cell", "header", "footer"
    source_location: dict      # e.g. {"sheet": "Sheet1", "row": 3, "col": "B"}
                               # or {"page": 2, "paragraph": 5}
    original_formatting: dict  # bold, font size, etc. (for reconstruction)
```

**Implementation details:**

- **PDF:** Use `pdfplumber` (better table extraction than PyPDF2) to extract text with positional data. For MVP, only handle text-based PDFs — reject scanned PDFs with a clear error message ("OCR support coming soon").
- **DOCX:** Use `python-docx` to walk paragraphs, tables, headers, footers. Preserve run-level formatting metadata so the pseudonymized doc can be reconstructed.
- **XLSX:** Use `openpyxl` to iterate sheets → rows → cells. Each non-empty cell becomes a TextSegment. Preserve cell coordinates for reconstruction.

**Critical edge cases to handle from day one:**
- Merged cells in Excel (common in German business docs)
- Headers/footers in Word (often contain company name, address)
- Table of contents and footnotes in PDFs
- Text in Excel comments and cell notes
- Tracked changes in DOCX (both current and deleted text may contain PII)

### 4.3 PII Detection Service — Multi-Layer Pipeline

This is the core of the product. Each layer runs independently, and results are merged with a conflict resolution strategy.

#### Layer 1: Deterministic Pattern Matching (Regex + Checksum)

High-precision, high-recall for structured identifiers. These should approach 99%+ recall because the patterns are well-defined.

```python
DACH_RECOGNIZERS = {
    # ──────────────────────────────
    # Germany
    # ──────────────────────────────
    "DE_STEUER_ID": {
        "pattern": r"\b\d{2}\s?\d{3}\s?\d{5}\b",  # 11 digits, optional spaces
        "validator": steuer_id_checksum,  # mod-11 checksum validation
        "confidence": 0.95
    },
    "DE_SOZIALVERSICHERUNGSNR": {
        "pattern": r"\b\d{2}\s?\d{6}\s?[A-Z]\s?\d{3}\b",
        "validator": sv_nr_checksum,
        "confidence": 0.95
    },
    "DE_HANDELSREGISTER": {
        "pattern": r"\b(HRA|HRB)\s?\d{3,6}\b",
        "validator": None,  # no checksum, but context helps
        "confidence": 0.85
    },

    # Switzerland
    "CH_AHV_NR": {
        "pattern": r"\b756\.\d{4}\.\d{4}\.\d{2}\b",
        "validator": ahv_ean13_checksum,
        "confidence": 0.97
    },
    "CH_UID": {
        "pattern": r"\bCHE-\d{3}\.\d{3}\.\d{3}\b",
        "validator": uid_checksum,
        "confidence": 0.95
    },

    # Austria
    "AT_SOZIALVERSICHERUNGSNR": {
        "pattern": r"\b\d{4}\s?\d{6}\b",  # 10 digits
        "validator": at_sv_checksum,
        "confidence": 0.90
    },

    # Common
    "IBAN": {
        "pattern": r"\b[A-Z]{2}\d{2}\s?(\d{4}\s?){3,7}\d{1,4}\b",
        "validator": iban_mod97_checksum,
        "confidence": 0.98
    },
    "EMAIL": {
        "pattern": r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        "validator": None,
        "confidence": 0.95
    },
    "PHONE_DACH": {
        "pattern": r"\b(\+\s?(?:49|43|41)|0)\s?[\d\s/\-()]{6,15}\b",
        "validator": phone_plausibility_check,  # digit count, area code
        "confidence": 0.80  # lower: many false positives with numbers
    },
    "DATE_OF_BIRTH": {
        "pattern": r"\b(?:geb\.?|geboren|Geburtsdatum|birth|DOB|born)\s*:?\s*\d{1,2}[./\-]\d{1,2}[./\-]\d{2,4}\b",
        "validator": date_plausibility,  # must be valid date, age 0-120
        "confidence": 0.90
    },

    # ──────────────────────────────
    # English-specific patterns
    # (for English documents from DACH businesses, e.g., international contracts)
    # ──────────────────────────────
    "UK_NATIONAL_INSURANCE": {
        "pattern": r"\b[A-CEGHJ-PR-TW-Z]{2}\s?\d{2}\s?\d{2}\s?\d{2}\s?[A-D]\b",
        "validator": ni_number_prefix_check,
        "confidence": 0.90
    },
    "US_SSN": {
        "pattern": r"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b",
        "validator": ssn_area_check,  # exclude known invalid ranges
        "confidence": 0.75  # lower: many false positives with random 9-digit numbers
    },
    "PASSPORT_NUMBER": {
        "pattern": r"\b(?:passport|Reisepass|Pass-?Nr)[:\s]*[A-Z0-9]{6,9}\b",
        "validator": None,
        "confidence": 0.85
    }
}
```

**Implementation:** Extend Microsoft Presidio's `PatternRecognizer` with these DACH-specific and English patterns. Presidio natively supports both `en` and `de` languages — configure the `AnalyzerEngine` to run recognizers for both languages on every document. Language-agnostic patterns (IBAN, email, phone) run regardless of detected language.

#### Layer 2: NER Model (Names, Addresses, Organizations)

This handles unstructured PII that can't be caught by regex.

**Model choice for MVP (bilingual DE+EN):**

Since the MVP must handle both German and English documents (and mixed-language documents are common in DACH businesses), a multilingual model is the pragmatic choice:

- **Primary:** `xlm-roberta-large` fine-tuned for NER — strong on both German and English, handles code-switching within documents (e.g., English email with German address block)
- **Alternative:** Run two language-specific models in parallel: `deepset/gbert-large` for German segments + `en_core_web_trf` (spaCy) for English segments — higher accuracy per language but requires a language detection step first
- **Language detection:** Use `langdetect` or `fasttext` at the segment level (not document level) since many DACH business docs mix languages paragraph by paragraph

**Recommended approach:** Use `xlm-roberta-large` as the single bilingual model for MVP simplicity. Switch to dual-model with per-segment language routing in v1.2 if accuracy demands it.

**Entity types for Layer 2:**
- `PER` — Person names (including common German compound names like "Müller-Schmidt")
- `LOC` — Locations/addresses (including German address format: "Musterstraße 42, 80331 München")
- `ORG` — Organization names
- `MISC` — Other potentially identifying entities

**Training data strategy:**
1. Start with existing NER datasets: GermEval 2014 + CoNLL 2003 German for DE, CoNLL 2003 English + OntoNotes for EN
2. Generate synthetic business documents in both languages using Faker (`de_DE`, `de_AT`, `de_CH`, `en_US`, `en_GB` locales) — include mixed-language documents (e.g., English contract template with German party names and addresses)
3. Manually annotate 500-1000 real documents from beta customers (with their consent) to create a DACH-business-specific gold standard
4. Use Presidio's data generator framework to create additional training examples in both languages
5. Pay special attention to mixed-language edge cases: English text with German PII ("Please contact Herr Dr. Friedrich Müller-Hohenstein at Bahnhofstraße 42, 8001 Zürich")

**Expected performance:** F1 ~0.88-0.92 on German names, ~0.90-0.94 on English names, ~0.85-0.90 on addresses in both languages, ~0.80-0.85 on organization names. This is NOT sufficient on its own — which is exactly why Layer 3 and human review exist.

#### Layer 3: LLM Contextual Review

A small LLM does a second pass specifically looking for PII that the first two layers may have missed.

> **Phased deployment strategy:** Layer 3 is valuable but not required from day one. The infrastructure and deployment approach differ across development phases — see Section 7 for the full phased infrastructure plan.
>
> **Critical design requirement:** Switching between Mistral API (development) and self-hosted Ollama (production) must be a single environment variable change — no code changes, no redeployment, no downtime. The `Layer3LLM` abstraction below enforces this. Both backends expose the same interface; the rest of the pipeline doesn't know or care which one is running.
>
> - **During development (Phases 1-3):** Use Mistral API to validate the prompt strategy, tune the merge logic, and measure how much Layer 3 improves recall over Layers 1+2 alone. Testing with your own documents is fine during development — you are the data controller. Switch to self-hosted before processing **customer** documents. Cost: ~€0-5/month.
> - **Pre-launch testing:** Practice self-hosted deployment on your local machine using Ollama (free). The setup is identical to production — same commands, same API endpoint (`localhost:11434`). Write and test a deployment script so it's repeatable.
> - **Production (first customer onward):** Self-hosted Llama 3.1 8B (quantized) via Ollama on the production GPU server. No PII ever leaves your infrastructure.
> - **Fallback if GPU server isn't ready:** Launch with Layers 1+2 only + human review, or use an EU-hosted LLM API with a DPA (e.g., Mistral API, Paris) as a temporary bridge. If using an external API, disclose as sub-processor in AVV.

**Model:** Llama 3.1 8B (quantized, ~5GB) or Qwen 2.5 7B — runs on a single GPU or even CPU for MVP volumes.

**Prompt strategy:**

```
You are a data privacy expert. Analyze the following text and identify ALL
personally identifiable information (PII) that has NOT already been detected.
The text may be in German, English, or a mix of both.

Du bist ein Datenschutz-Experte. Analysiere den folgenden Text und identifiziere
ALLE personenbezogenen Daten (PII), die noch nicht markiert wurden.
Der Text kann auf Deutsch, Englisch oder gemischt sein.

Already detected entities / Bereits erkannte Entitäten (DO NOT re-report):
{already_detected_entities}

Text:
{text_segment}

Respond ONLY in JSON format / Antworte NUR im JSON-Format:
{
  "additional_pii": [
    {"text": "...", "type": "...", "language": "de|en", "reason": "...", "confidence": 0.0-1.0}
  ]
}

If no additional PII found: {"additional_pii": []}
```

**Why this works:** The LLM can catch contextual identifiers that regex and NER miss — things like "der Geschäftsführer der Bäckerei am Marktplatz" (which could identify someone in a small town) or "the only female partner at the firm" in English text. It also provides a natural-language `reason` field that feeds into the review UI.

**Important:** In production, this LLM runs locally on your infrastructure via Ollama. No customer PII ever leaves your network. During development, the Mistral API is fine for testing with your own documents — just switch to self-hosted before onboarding customers.

**Layer 3 abstraction (the key to easy switching):**

The entire Layer 3 integration is behind a single abstraction. Switching backends is one env var: `LLM_BACKEND=mistral` → `LLM_BACKEND=ollama`. No code changes anywhere else in the pipeline.

```python
class Layer3LLM:
    """
    Abstraction over LLM backend. Same interface whether using
    Mistral API (dev) or local Ollama (production).

    Switch via environment variable — zero code changes:
      LLM_BACKEND=mistral  → Mistral API (development, your own docs)
      LLM_BACKEND=ollama   → Self-hosted via Ollama (production)
      LLM_BACKEND=disabled → Layer 3 skipped entirely (Layers 1+2 only)

    Both backends return identical output format. The merge logic,
    review UI, and everything downstream is completely unaware of
    which backend is running.
    """
    async def detect_additional_pii(self, text: str, already_detected: list) -> list:
        if self.backend == "ollama":
            # POST http://localhost:11434/api/generate
            return await self._call_ollama(text, already_detected)
        elif self.backend == "mistral":
            # POST https://api.mistral.ai/v1/chat/completions
            return await self._call_mistral_api(text, already_detected)
        elif self.backend == "disabled":
            return []  # Layer 3 skipped — Layers 1+2 only
```

#### Merge & Conflict Resolution

```python
def merge_detections(
    regex_results: list[Detection],
    ner_results: list[Detection],
    llm_results: list[Detection]
) -> list[MergedDetection]:
    """
    Merge detections from all three layers.

    Rules:
    1. If regex detected with checksum validation → auto-confirm (confidence ≥ 0.95)
    2. If NER and LLM agree on same span → boost confidence by 0.1
    3. If only one layer detected → keep, but lower confidence tier
    4. Overlapping spans → take the wider span
    5. All detections below confidence 0.5 → discard (likely noise)

    Output: sorted by position, with confidence tier:
      - HIGH (≥ 0.90): auto-redact suggestion, reviewer sees green
      - MEDIUM (0.70-0.89): needs review, reviewer sees yellow
      - LOW (0.50-0.69): likely PII but uncertain, reviewer sees orange

    Note: If Layer 3 is disabled (llm_results=[]), the merge logic
    still works — entities just won't get the NER+LLM agreement boost.
    """
```

### 4.4 Human-in-the-Loop Review Interface

This is the most important UX component of the entire product. The review step is what lets you make the promise of "PII risk reduction" with integrity. **Critically, the UI design directly affects the legal analysis** — under German law (§ 823 BGB Organisationsverschulden), if the vendor designs an interface that encourages rubber-stamping, the vendor shares blame for missed PII. The EU AI Act Art. 14(4)(b) requires designing for awareness of automation bias.

#### Review UI Requirements

**Document view panel (left side):**
- Shows the extracted document text with inline highlighted PII entities
- Color-coded by confidence: green (high), yellow (medium), orange (low)
- Each highlight is clickable to open a detail popover
- Shows original document formatting roughly (paragraphs, tables) so reviewer can see context
- Keyboard navigation: Tab to jump between entities, Enter to confirm, Backspace to reject

**Entity detail popover:**
- Entity text (e.g., "Max Mustermann")
- Detected type (e.g., "PERSON")
- Confidence score (e.g., 0.87)
- Detection source (e.g., "NER + LLM agree")
- Replacement preview (e.g., "Felix Bauer")
- Actions: Confirm / Reject / Change Type / Change Span (drag to expand/shrink)

**Missed PII toolbar (right side):**
- "Add PII" button: reviewer can select text and manually tag it as PII
- Quick-tag buttons for common types: Name, Address, Phone, ID Number
- This is critical — it catches what all three detection layers missed

**Batch actions (with automation bias safeguards):**

> ⚠️ **Legal constraint:** A blanket "Confirm all" button without safeguards creates Organisationsverschulden exposure. The reviewer must demonstrably engage with the content.

- **"Confirm all regex-detected entities"** — available for IBAN, Steuer-ID, AHV-Nr and other checksum-validated entities only. These are deterministic, near-100% precision, and individual review adds no value.
- **"Confirm all HIGH confidence NER entities"** — available ONLY after the reviewer has scrolled through the entire document (UI tracks scroll position and entity visibility). Disabled entirely for `critical` sensitivity jobs.
- **"Review remaining"** — filters to only MEDIUM and LOW items
- **No "Confirm all" without any engagement** — this button simply does not exist

**Zero-detection warning gate:**

If the detection pipeline finds **zero PII entities**, the UI shows a blocking warning:
> *"Es wurden keine personenbezogenen Daten erkannt. Dies kann bedeuten: (a) das Dokument enthält tatsächlich keine PII, (b) die Textextraktion ist fehlgeschlagen, oder (c) die PII-Erkennung hat versagt. Bitte prüfen Sie den extrahierten Text manuell."*

The reviewer must explicitly acknowledge before proceeding to export.

**Reviewer onboarding requirement:**
- First-time reviewers must complete a short training flow (5 min) before gaining review access
- Training covers: what counts as PII, how to add missed entities, why batch-confirm should be used cautiously
- Completion is logged and timestamped for compliance evidence

**Summary panel (bottom):**
- Total entities detected: X
- Confirmed: Y (with breakdown: batch-confirmed vs. individually reviewed)
- Rejected: Z
- Manually added: W
- Estimated review time: ~N minutes (based on historical data)
- Document sensitivity class: [Standard / High / Critical]

#### Review Workflow State Machine

```
┌──────────┐  upload +    ┌────────────┐  detection   ┌──────────────┐
│  CREATED │  doc class   │ PROCESSING │ ──────────► │ READY_REVIEW │
│          │ ───────────► │            │              └──────┬───────┘
└──────────┘              └────────────┘                     │
                                              reviewer opens │
                                                             ▼
                                                      ┌──────────────┐
                                   all entities        │  IN_REVIEW   │
                                   confirmed           │              │
                             ┌───────────────────────── └──────────────┘
                             ▼
                      ┌──────────────┐  2nd scan OK   ┌──────────────┐
                      │ PSEUDONYMIZED│ ─────────────► │  SCAN_PASSED │
                      └──────────────┘                └──────┬───────┘
                             │ 2nd scan finds PII            │
                             ▼                               │ user acknowledges
                      ┌──────────────┐              export gate
                      │  SCAN_FAILED │                       │
                      │  → back to   │                       ▼
                      │  IN_REVIEW   │               ┌──────────────┐
                      └──────────────┘               │  EXPORTED    │
                                                     └──────┬───────┘
                                              user pastes    │
                                              back response  ▼
                                                     ┌────────────────┐
                                                     │ DE-PSEUDONYMIZED│
                                                     └────────────────┘
```

Each state transition is logged with timestamp, user ID, and action details → immutable audit trail.

Note: EXPORTED and DE-PSEUDONYMIZED are soft states — the user can paste back and de-pseudonymize multiple times per job (e.g., asking the LLM follow-up questions with the same pseudonymized text).

### 4.5 Pseudonymization Engine

Once the human reviewer confirms all PII entities, the pseudonymization step replaces each entity with a consistent synthetic replacement.

**Replacement strategy (configurable per customer):**

| Strategy | Example | Pros | Cons |
|---|---|---|---|
| **Placeholder** | `Max Mustermann` → `[PERSON_001]` | Simple, clearly marked | LLM may produce awkward responses |
| **Synthetic replacement** | `Max Mustermann` → `Felix Bauer` | LLM treats as natural text, better results | Must ensure synthetic name isn't a real person in context |
| **Type-preserving hash** | `Max Mustermann` → `PER_a7f3b2` | Consistent across documents, reversible | Looks unnatural to LLM |

**MVP recommendation:** Use **synthetic replacement** via Faker as default, with **placeholder** as fallback for non-name entities. Choose the Faker locale based on the detected language of the surrounding text — a German name in German text gets a German replacement (`de_DE`), an English name in English text gets an English replacement (`en_US`). For mixed-language contexts, default to `de_DE` since the primary market is DACH.

**Consistency rule:** The same real entity must always map to the same synthetic replacement within a session. "Max Mustermann" → "Felix Bauer" everywhere in the document. Store this mapping in a session-scoped lookup table.

```python
@dataclass
class PseudonymizationMapping:
    job_id: str
    original_text: str
    replacement_text: str
    entity_type: str
    positions: list[tuple[int, int]]  # all positions in document
    created_at: datetime
    confirmed_by: str  # reviewer user ID
    confidence: float
    detection_sources: list[str]  # ["regex", "ner", "llm"]
```

This mapping is stored encrypted in PostgreSQL and is the key to de-pseudonymization later.

### 4.6 Second Scan — Defense-in-Depth

After pseudonymization, the entire output text is run through the PII detection pipeline **one more time**. This catches:
- PII that was somehow missed in the first pass and not caught by the reviewer
- PII that was partially replaced (e.g., "Max" was replaced but "Mustermann" in a different paragraph was not)
- New PII-like patterns that appeared through replacement (unlikely but possible with synthetic names)

```python
async def second_scan(job_id: str, pseudonymized_text: str) -> SecondScanResult:
    """
    Run the full detection pipeline on the pseudonymized output.
    Only Layer 1 (regex) and Layer 2 (NER) — skip Layer 3 (LLM) for speed.

    If ANY entity is detected with confidence ≥ 0.70, block export
    and return the job to IN_REVIEW with the new detections highlighted.
    """
    detections = run_detection_pipeline(
        text=pseudonymized_text,
        layers=["regex", "ner"],  # skip LLM for speed
        # Exclude known synthetic replacements from detection
        allowlist=load_replacement_texts(job_id)
    )

    # Filter: ignore detections that match known synthetic replacements
    real_detections = [d for d in detections if d.text not in get_allowlist(job_id)]

    if real_detections:
        await log_audit(job_id, "second_scan_failed", {
            "detections_found": len(real_detections),
            "details": [{"text_hash": sha256(d.text), "type": d.entity_type} for d in real_detections]
        })
        return SecondScanResult(passed=False, detections=real_detections)

    await log_audit(job_id, "second_scan_passed", {})
    return SecondScanResult(passed=True, detections=[])
```

**If second scan fails:** Job returns to `IN_REVIEW` state with the newly found entities highlighted. The reviewer must address them before export is possible again. This creates a hard safety loop.

### 4.7 Export Gate — Mandatory Acknowledgement

The copy-to-clipboard button is **not** freely available. Before any pseudonymized text can leave the system, the user must pass through an export gate.

**Export gate requirements:**
1. **Review completeness check:** All detected entities must be in `confirmed` or `rejected` status. No `pending` entities allowed.
2. **Second scan passed:** The defense-in-depth scan must have completed successfully.
3. **Mandatory acknowledgement dialog:**

```
┌──────────────────────────────────────────────────────────────┐
│  ⚠️  Export-Bestätigung / Export Confirmation                │
│──────────────────────────────────────────────────────────────│
│                                                              │
│  Sie sind dabei, pseudonymisierten Text zu exportieren.      │
│  You are about to export pseudonymized text.                 │
│                                                              │
│  Bitte bestätigen Sie:                                       │
│                                                              │
│  ☐ Ich habe die erkannten Entitäten geprüft und bestätigt.  │
│    I have reviewed and confirmed the detected entities.      │
│                                                              │
│  ☐ Ich werde den Text ausschließlich in einen Enterprise-    │
│    /API-Zugang mit Auftragsverarbeitungsvertrag (DPA)        │
│    einfügen — NICHT in einen Consumer-Chat (z.B. ChatGPT    │
│    Free, Gemini Free).                                       │
│    I will only paste this into an Enterprise/API account     │
│    with a DPA — NOT into a consumer chat.                    │
│                                                              │
│  ☐ Mir ist bewusst, dass pseudonymisierte Daten weiterhin   │
│    personenbezogene Daten im Sinne der DSGVO sein können.    │
│    I understand that pseudonymized data may still constitute │
│    personal data under GDPR.                                 │
│                                                              │
│              [ Abbrechen ]    [ ✅ Bestätigen & Kopieren ]   │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

All three checkboxes must be ticked. The acknowledgement is logged in the audit trail with timestamp, user ID, and IP address.

**For `critical` sensitivity jobs** (health data, legal case files), an additional checkbox:
> ☐ Eine Datenschutz-Folgenabschätzung (DPIA) wurde für diesen Verarbeitungsvorgang durchgeführt oder ist nicht erforderlich.

### 4.8 Pseudonymized Output & De-pseudonymization Paste Tool

Once the export gate is passed, the system shows the pseudonymized text.

**Pseudonymized Output Panel:**

```
┌──────────────────────────────────────────────────────────────┐
│  ✅ Pseudonymized Output                         [📋 Copy]  │
│──────────────────────────────────────────────────────────────│
│                                                              │
│  Vertrag zwischen Felix Bauer, wohnhaft in                   │
│  Ahornweg 12, 80331 Neustadt, und der Firma                  │
│  Sonnental GmbH, vertreten durch Lisa Berger...              │
│                                                              │
│  IBAN: DE02 1234 5678 9012 3456 78                           │
│  Steuer-ID: 92 041 83947                                     │
│                                                              │
└──────────────────────────────────────────────────────────────┘

Replacement table (visible to reviewer):
  Max Mustermann     → Felix Bauer
  Hauptstraße 7      → Ahornweg 12
  München            → Neustadt
  Muster GmbH        → Sonnental GmbH
  Anna Schneider      → Lisa Berger
  DE89 3704 0044...  → DE02 1234 5678...
```

**De-pseudonymization Paste Tool (separate tab/panel):**

After the user gets a response from the LLM, they paste it back into the tool. One click replaces all pseudonyms with the original values.

```
┌──────────────────────────────────────────────────────────────┐
│  📋 Paste LLM Response Here                                  │
│──────────────────────────────────────────────────────────────│
│                                                              │
│  [Textarea: user pastes Claude/GPT response]                 │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                    [ 🔄 De-pseudonymize ]

┌──────────────────────────────────────────────────────────────┐
│  ✅ De-pseudonymized Result                      [📋 Copy]   │
│──────────────────────────────────────────────────────────────│
│                                                              │
│  Result with original names/data restored.                   │
│  Changed tokens highlighted in blue so user can              │
│  verify each replacement.                                    │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**De-pseudonymization implementation:**

```python
async def de_pseudonymize(job_id: str, llm_response_text: str) -> DePseudonymizedResult:
    """
    Replace synthetic pseudonyms back with real values.

    Steps:
    1. Load mapping for this job from DB
    2. Sort replacements by length descending (avoid partial matches)
    3. Also generate common variants (surname-only, title+surname)
    4. Replace all occurrences
    5. Track which replacements were actually made (for highlighting in UI)
    6. Log to audit trail
    """
    mappings = await load_mappings(job_id)

    # Build expanded replacement map including likely LLM-generated variants
    replacement_map = {}
    for m in mappings:
        replacement_map[m.replacement_text] = m.original_text

        if m.entity_type == "PERSON":
            fake_parts = m.replacement_text.split()
            real_parts = m.original_text.split()
            if len(fake_parts) > 1 and len(real_parts) > 1:
                replacement_map[fake_parts[-1]] = real_parts[-1]
                for title in ["Herr", "Frau", "Mr", "Mrs", "Ms", "Dr"]:
                    replacement_map[f"{title} {fake_parts[-1]}"] = f"{title} {real_parts[-1]}"

    sorted_keys = sorted(replacement_map.keys(), key=len, reverse=True)

    result = llm_response_text
    replacements_made = []
    for key in sorted_keys:
        if key in result:
            result = result.replace(key, replacement_map[key])
            replacements_made.append({
                "pseudonym": key,
                "original": replacement_map[key],
                "count": llm_response_text.count(key)
            })

    await log_audit(job_id, "response_depseudonymized", {
        "replacements_made": len(replacements_made),
        "input_length": len(llm_response_text),
        "output_length": len(result)
    })

    return DePseudonymizedResult(
        text=result,
        replacements_made=replacements_made,
        unmapped_pseudonyms=find_unmapped(result, mappings)
    )
```

**Key UX detail:** The de-pseudonymized result highlights every replaced token in blue, so the user can visually scan and verify the replacements make sense. If the LLM rephrased a pseudonym in an unexpected way (e.g., "F. Bauer" instead of "Felix Bauer"), the tool flags this as an **unmapped pseudonym warning** so the user can manually fix it.

**Why manual paste-back is the right MVP choice:**
- No API key management complexity
- No billing/token cost questions
- User keeps full control over which LLM they use
- We learn from real usage how LLMs rephrase pseudonyms before automating de-pseudonymization
- Much smaller attack surface — no external API calls from our infrastructure at all
- Cleaner product positioning: "We help you reduce PII risk" (not "We're an LLM wrapper")
- Legally, the user (not us) performs the data transfer to the LLM — cleaner processor/controller boundary

### 4.9 Audit Log

Every action in the system is logged to an append-only audit table. This is non-negotiable for DACH compliance.

```sql
CREATE TABLE audit_log (
    id              BIGSERIAL PRIMARY KEY,
    job_id          UUID NOT NULL REFERENCES jobs(id),
    timestamp       TIMESTAMPTZ NOT NULL DEFAULT now(),
    actor_id        UUID,            -- NULL for system actions
    action_type     TEXT NOT NULL,    -- 'pii_detected', 'pii_confirmed',
                                     -- 'pii_rejected', 'pii_added_manual',
                                     -- 'document_pseudonymized',
                                     -- 'second_scan_passed', 'second_scan_failed',
                                     -- 'export_acknowledged', 'text_copied',
                                     -- 'response_depseudonymized',
                                     -- 'reviewer_training_completed',
                                     -- 'dpia_trigger_shown', 'dpia_acknowledged'
    entity_type     TEXT,             -- 'PERSON', 'IBAN', etc.
    entity_hash     TEXT,             -- SHA-256 of original PII (not the PII itself!)
    confidence      FLOAT,
    detection_source TEXT,            -- 'regex', 'ner', 'llm', 'human'
    document_class  TEXT,             -- 'contract', 'hr_document', 'health_data', etc.
    sensitivity     TEXT,             -- 'standard', 'high', 'critical'
    metadata        JSONB,           -- additional context (export acknowledgement details, etc.)
    ip_address      INET
);

-- This table is append-only. No UPDATE or DELETE allowed.
-- Enforce via database trigger or application-level policy.

-- Critical: export_acknowledged events store which checkboxes were ticked
-- and the exact text of the acknowledgement shown, for evidentiary purposes.
```

---

## 5. Data Model

### Core Tables

```sql
-- Users and authentication
CREATE TABLE users (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email       TEXT UNIQUE NOT NULL,
    name        TEXT NOT NULL,
    org_id      UUID NOT NULL REFERENCES organizations(id),
    role        TEXT NOT NULL CHECK (role IN ('admin', 'reviewer', 'user')),
    created_at  TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE organizations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT NOT NULL,
    plan            TEXT NOT NULL DEFAULT 'trial',
    llm_provider    TEXT,           -- which LLM the customer prefers (informational only)
    settings        JSONB,  -- pseudonymization preferences, confidence thresholds
    created_at      TIMESTAMPTZ DEFAULT now()
);

-- Document processing jobs
CREATE TABLE jobs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id          UUID NOT NULL REFERENCES organizations(id),
    created_by      UUID NOT NULL REFERENCES users(id),
    status          TEXT NOT NULL DEFAULT 'created',
    file_name       TEXT NOT NULL,
    file_type       TEXT NOT NULL,  -- 'pdf', 'docx', 'xlsx'
    file_hash       TEXT NOT NULL,  -- SHA-256 of uploaded file
    file_size_bytes BIGINT NOT NULL,
    document_class  TEXT NOT NULL,   -- 'contract', 'invoice', 'hr_document', etc.
    sensitivity     TEXT NOT NULL DEFAULT 'standard', -- 'standard', 'high', 'critical'
    dpia_triggered  BOOLEAN DEFAULT FALSE,
    dpia_acknowledged BOOLEAN DEFAULT FALSE,
    export_acknowledged BOOLEAN DEFAULT FALSE,
    second_scan_passed BOOLEAN DEFAULT FALSE,
    created_at      TIMESTAMPTZ DEFAULT now(),
    processing_started_at   TIMESTAMPTZ,
    review_started_at       TIMESTAMPTZ,
    review_completed_at     TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ
);

-- Extracted text segments
CREATE TABLE text_segments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id          UUID NOT NULL REFERENCES jobs(id),
    segment_index   INT NOT NULL,
    text_content    TEXT NOT NULL,
    source_type     TEXT NOT NULL,  -- 'paragraph', 'cell', 'header'
    source_location JSONB NOT NULL,
    created_at      TIMESTAMPTZ DEFAULT now()
);

-- Detected PII entities (mutable during review phase)
CREATE TABLE pii_entities (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL REFERENCES jobs(id),
    segment_id          UUID NOT NULL REFERENCES text_segments(id),
    original_text_enc   TEXT NOT NULL,     -- encrypted original PII
    replacement_text    TEXT NOT NULL,      -- synthetic replacement
    entity_type         TEXT NOT NULL,
    start_offset        INT NOT NULL,
    end_offset          INT NOT NULL,
    confidence          FLOAT NOT NULL,
    detection_sources   TEXT[] NOT NULL,    -- {'regex', 'ner', 'llm'}
    review_status       TEXT NOT NULL DEFAULT 'pending',
                        -- 'pending', 'confirmed', 'rejected', 'added_manual'
    reviewed_by         UUID REFERENCES users(id),
    reviewed_at         TIMESTAMPTZ,
    created_at          TIMESTAMPTZ DEFAULT now()
);

-- Indexes
CREATE INDEX idx_jobs_org_status ON jobs(org_id, status);
CREATE INDEX idx_pii_job_status ON pii_entities(job_id, review_status);
CREATE INDEX idx_audit_job ON audit_log(job_id, timestamp);
```

---

## 6. API Design

### Core Endpoints

```
POST   /api/v1/jobs                    Upload document + select document class, create job
GET    /api/v1/jobs                    List jobs for current org
GET    /api/v1/jobs/{id}               Get job status and metadata
GET    /api/v1/jobs/{id}/review        Get document with PII highlights for review
PATCH  /api/v1/jobs/{id}/entities/{eid}   Update entity (confirm/reject/modify)
POST   /api/v1/jobs/{id}/entities      Add manually detected PII entity
POST   /api/v1/jobs/{id}/confirm       Confirm all reviewed entities, trigger pseudonymization
POST   /api/v1/jobs/{id}/second-scan   Trigger defense-in-depth second scan on pseudonymized output
GET    /api/v1/jobs/{id}/pseudonymized Get pseudonymized text (only after second scan + export gate)
POST   /api/v1/jobs/{id}/export        Submit export acknowledgement (checkboxes), unlock copy
POST   /api/v1/jobs/{id}/deanonymize   Paste LLM response, get de-pseudonymized text back
GET    /api/v1/jobs/{id}/audit         Get audit trail for job
```

### Upload Flow Example

```
POST /api/v1/jobs
Content-Type: multipart/form-data

file: <contract.pdf>

Response 202:
{
  "job_id": "a1b2c3d4-...",
  "status": "processing",
  "estimated_processing_seconds": 15
}
```

### De-pseudonymize Flow Example

```
POST /api/v1/jobs/a1b2c3d4-.../deanonymize
Content-Type: application/json

{
  "llm_response_text": "Der Vertrag mit Felix Bauer sieht vor, dass die
   Sonnental GmbH bis zum 31.03.2026 liefern muss. Herr Bauer hat
   ein Sonderkündigungsrecht..."
}

Response 200:
{
  "depseudonymized_text": "Der Vertrag mit Max Mustermann sieht vor, dass die
   Muster GmbH bis zum 31.03.2026 liefern muss. Herr Mustermann hat
   ein Sonderkündigungsrecht...",
  "replacements_made": [
    {"pseudonym": "Felix Bauer", "original": "Max Mustermann", "count": 1},
    {"pseudonym": "Sonnental GmbH", "original": "Muster GmbH", "count": 1},
    {"pseudonym": "Herr Bauer", "original": "Herr Mustermann", "count": 1}
  ],
  "unmapped_warnings": []
}
```

### Review Data Response Example

```
GET /api/v1/jobs/a1b2c3d4-.../review

Response 200:
{
  "job_id": "a1b2c3d4-...",
  "status": "ready_review",
  "document_text": "Vertrag zwischen Max Mustermann, wohnhaft in ...",
  "entities": [
    {
      "id": "e1f2...",
      "text": "Max Mustermann",
      "type": "PERSON",
      "start": 20,
      "end": 34,
      "confidence": 0.92,
      "sources": ["ner", "llm"],
      "tier": "HIGH",
      "replacement_preview": "Felix Bauer",
      "review_status": "pending"
    },
    {
      "id": "e3f4...",
      "text": "DE89 3704 0044 0532 0130 00",
      "type": "IBAN",
      "start": 245,
      "end": 271,
      "confidence": 0.98,
      "sources": ["regex"],
      "tier": "HIGH",
      "replacement_preview": "DE02 1234 5678 9012 3456 78",
      "review_status": "pending"
    }
  ],
  "summary": {
    "total_entities": 14,
    "high_confidence": 11,
    "medium_confidence": 2,
    "low_confidence": 1,
    "estimated_review_minutes": 2
  }
}
```

---

## 7. Infrastructure & Hosting

### 7.1 Phased Infrastructure Strategy

> **Key principle:** Don't pay for production infrastructure while writing code. The full GPU-powered stack is only needed when real customers with real documents arrive. Until then, a cheap VPS handles everything.

#### Phase A: Development (Weeks 1-9) — ~€8-15/month

A single Hetzner Cloud VPS runs the entire stack except the local LLM:

```
┌─────────────────────────────────────────────┐
│   Hetzner Cloud VPS (CPX21 or CPX31)        │
│   3-4 vCPU / 8-16 GB RAM                    │
│   ~€8-15/month                              │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  All-in-one:                         │   │
│  │  - FastAPI backend                   │   │
│  │  - Celery worker                     │   │
│  │  - React dev build (nginx)           │   │
│  │  - Presidio + spaCy NER (Layer 1+2)  │   │
│  │  - PostgreSQL 16                     │   │
│  │  - Redis 7                           │   │
│  │  - MinIO (or local filesystem)       │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  Layer 3: DISABLED or Mistral API            │
│  (switch to self-hosted before customer data) │
└─────────────────────────────────────────────┘
```

This covers 70%+ of the product: document extraction, regex recognizers, NER model, review UI, pseudonymization engine, de-pseudonymization, audit logging, export gate — everything except GPU-accelerated LLM inference.

**Layer 3 during development:** Use Mistral API (`mistral-small` or `mistral-nemo`) to validate the Layer 3 prompt strategy and measure recall improvement. Set `LLM_BACKEND=mistral` in env. Testing with your own documents is fine — you're the data controller. Switch to self-hosted (Ollama) before processing any customer documents. Cost: effectively free (Mistral free tier) or ~€5/month on pay-as-you-go.

**Local LLM practice:** In parallel, install Ollama on your development laptop/desktop to practice the self-hosted deployment:

```bash
# On your laptop (macOS/Linux) — free, no server needed
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.1:8b-instruct-q4_0
# Test: curl http://localhost:11434/api/generate -d '{"model":"llama3.1:8b-instruct-q4_0","prompt":"test"}'
```

This is identical to the production setup. Same commands, same API endpoint, same model. Practice until it's boring.

#### Phase B: Pre-Launch Preparation (Weeks 10-11) — ~€8-15/month + throwaway server costs

Before the first customer, validate the full deployment end-to-end:

1. **Write a deployment script** (bash script, Ansible playbook, or Docker Compose) that installs everything: OS packages, PostgreSQL, Redis, your app, Ollama + model pull, nginx, SSL certs.
2. **Test the script 2-3 times** on fresh Hetzner Cloud instances. Spin up a CPX31 (€15/month, billed hourly → ~€0.02/hour), run the script, verify everything works, tear it down. Total cost: a few euros.
3. **Benchmark Layer 3 on CPU** to understand performance: On a CPX31 (4 vCPU / 16 GB), Llama 3.1 8B Q4 via Ollama runs at ~5-10 tokens/sec. Slow but functional. Measure actual document processing times.
4. **Decision point:** Is Layer 3 on CPU fast enough for your first customer, or do you need the GPU server from day one? For a customer processing 5-10 documents/day, CPU inference (15-30 sec per document for Layer 3) is likely fine.

#### Phase C: Production — First Customer (Weeks 12+) — €30-184/month

Two options depending on whether you need GPU speed. In all cases, the switch from Mistral API to self-hosted is a single env var change (`LLM_BACKEND=ollama`) — the Layer3LLM abstraction (Section 4.3) ensures zero code changes are needed.

**Option 1: Single GPU server — best experience (€184/month)**

```
┌─────────────────────────────────────────────┐
│         Hetzner GEX44 (Falkenstein, DE)      │
│         i5-13500 / 64 GB / RTX 4000 (20GB)  │
│         €184/month                           │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  Everything on one box:              │   │
│  │  - FastAPI + Celery                  │   │
│  │  - React build (nginx)              │   │
│  │  - Presidio + spaCy (Layer 1+2)     │   │
│  │  - Ollama + Llama 3.1 8B (Layer 3)  │   │
│  │    → GPU inference: ~50-100 tok/sec  │   │
│  │  - PostgreSQL 16 + Redis 7          │   │
│  │  - MinIO / Hetzner Object Storage   │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  No outbound API calls to any LLM provider.  │
│  Pseudonymized text leaves only via clipboard.│
└─────────────────────────────────────────────┘
```

This is the ideal single-box production setup. The RTX 4000 with 20 GB VRAM runs Llama 3.1 8B at high speed, and the i5-13500 (14 cores) plus 64 GB RAM handles everything else alongside it. Good enough for 3-5 beta customers processing documents throughout the day.

**Option 2: CPU-only server — cheapest production path (~€30-50/month)**

```
┌─────────────────────────────────────────────┐
│     Hetzner Dedicated (e.g., AX42 or        │
│     Auction server with 8+ cores / 64GB)    │
│     ~€30-50/month                           │
│                                              │
│  Same as above, but Layer 3 runs on CPU:    │
│  - Ollama + Llama 3.1 8B Q4 on CPU         │
│    → ~10-20 tok/sec (slower but works)      │
│  - Layer 3 adds ~15-30 sec per document     │
│  - Acceptable for low volume (5-10 docs/day)│
│                                              │
│  Upgrade to GEX44 when volume justifies it. │
└─────────────────────────────────────────────┘
```

**Option 3: Launch without Layer 3 initially (~€15-30/month)**

Layers 1 (regex) + Layer 2 (NER) + human review is a solid product on its own. Launch with `LLM_BACKEND=disabled`, measure what reviewers manually add (this is your data on Layer 3's value), and enable Layer 3 when you upgrade to a GPU box. This lets you validate the entire product at minimal cost.

**Option 4: EU-hosted LLM API as temporary bridge (~€15/month server + API costs)**

If you need Layer 3 quality but can't justify the GPU server yet, use Mistral API (Paris, EU entity, DPA available) with real data temporarily. You lose the "no outbound calls" selling point and must disclose Mistral as a sub-processor in your AVV, but it's operationally simple. Plan to migrate to self-hosted as soon as volume justifies the GPU server.

#### Phase D: Growth — Multiple Customers

When concurrent usage increases (5+ simultaneous users, 50+ documents/day), consider splitting:

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  App Server   │     │  GPU Server   │     │  DB Server   │
│  (CPX31)      │────▶│  (GEX44)      │     │  (Managed PG)│
│  FastAPI,     │     │  Ollama only  │     │              │
│  Celery,      │     │              │     │              │
│  React, nginx │     │              │     │              │
│  ~€15/month   │     │  €184/month  │     │  ~€20/month  │
└──────────────┘     └──────────────┘     └──────────────┘
```

But don't split prematurely — a single GEX44 handles a lot of load.

### 7.2 Deployment Script (Write This During Phase A)

The deployment script eliminates the "I need to deploy fast when a customer signs" risk. Write it early, test it often.

```bash
#!/bin/bash
# deploy.sh — Full stack deployment on a fresh Ubuntu 24 server
# Test this 2-3 times on throwaway Hetzner instances before you need it for real

set -euo pipefail

echo "=== Installing system packages ==="
apt update && apt install -y postgresql redis-server nginx certbot python3-pip python3-venv git

echo "=== Setting up PostgreSQL ==="
sudo -u postgres createuser piigateway
sudo -u postgres createdb piigateway -O piigateway
# ... schema migration

echo "=== Installing Ollama + LLM ==="
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.1:8b-instruct-q4_0
# Model download: ~4.7GB, takes 2-5 min depending on bandwidth

echo "=== Deploying application ==="
git clone <your-repo> /opt/piigateway
cd /opt/piigateway
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
python -m spacy download de_core_news_lg  # German NER model
python -m spacy download en_core_web_trf  # English NER model

echo "=== Configuring environment ==="
cat > .env << EOF
LLM_BACKEND=ollama          # Change to "mistral" or "disabled" — that's it.
                             # No other code changes needed. See Layer3LLM in Section 4.3.
OLLAMA_HOST=http://localhost:11434
DATABASE_URL=postgresql://piigateway@localhost/piigateway
REDIS_URL=redis://localhost:6379
# ...
EOF

echo "=== Starting services ==="
systemctl enable --now postgresql redis-server
# Start FastAPI, Celery, nginx (via systemd units or supervisor)

echo "=== Done. Total time: ~10-15 minutes ==="
```

**Key insight:** The entire deployment, including downloading the LLM model, takes ~10-15 minutes on a Hetzner server with good bandwidth. There is no "I need weeks to deploy an LLM" problem — Ollama makes it trivial. Practice it 2-3 times during development so it's boring and routine when you need it for real.

### 7.3 Why Hetzner (not AWS/Azure)

For DACH customers, "hosted on our own Hetzner server in Falkenstein, Germany" is a strong trust signal. Many Mittelstand firms are skeptical of US hyperscalers. For Swiss customers, consider Exoscale (Swiss-owned, Zürich datacenter, GPU instances from ~€0.92/hour) as a future option — but start with Hetzner for cost reasons.

### 7.4 Cost Summary by Phase

| Phase | Duration | Server | Layer 3 Strategy | Monthly Cost |
|---|---|---|---|---|
| **A: Development** | Weeks 1-9 | Hetzner CPX21/31 | Mistral API (your own docs OK) | **€8-15** |
| **B: Pre-launch prep** | Weeks 10-11 | Same + throwaway test instances | Ollama on test box | **€10-20** |
| **C: Production (budget)** | Week 12+ | Hetzner dedicated (CPU) | Ollama on CPU or disabled | **€30-50** |
| **C: Production (ideal)** | Week 12+ | Hetzner GEX44 (GPU) | Ollama on GPU | **€184** |
| **D: Growth** | When needed | App + GPU + DB split | Ollama on GPU | **€220-300** |

> **Rule of thumb:** Don't upgrade to the next tier until the current one is causing actual problems or you have revenue to justify it. A single €15/month VPS with Layers 1+2 only is a better product than no product at all.

### 7.5 Swiss Hosting (Future)

For Swiss customers requiring Swiss data residency, Exoscale (Zürich datacenter) offers GPU instances. Pricing starts at ~€0.92/hour per GPU (~€670/month always-on). This is significantly more expensive than Hetzner, so only add a Swiss deployment when you have Swiss customers willing to pay for it. The application code is identical — only the infrastructure and AVV/Switzerland addendum differ.

---

## 8. Security Considerations

### Encryption
- All data encrypted at rest (PostgreSQL TDE or application-level encryption for PII columns)
- All PII entity original text stored with AES-256-GCM encryption, key managed via env var (MVP) or HashiCorp Vault (v2)
- TLS 1.3 for all connections
- File uploads encrypted at rest in object storage

### Data Retention
- Original uploaded files: deleted after job completion + configurable retention (default 24h)
- PII mappings: deleted after job completion + retention period
- Audit logs: retained for compliance period (configurable, default 90 days)
- Anonymized text sent to LLM: handled by user via copy-paste to their own LLM account — retention is the user's/LLM provider's responsibility, not ours. Note: what the user copies is pseudonymized, not anonymized — GDPR still applies to that data.

### Access Control
- Role-based: Admin, Reviewer, User
- Only Reviewers and Admins can see/confirm PII detections
- Users can upload and see final results but never see raw PII mappings
- All access to PII data logged in audit trail

### Network
- In production (self-hosted LLM): No PII ever leaves the DACH-hosted infrastructure. No outbound API calls to any LLM provider.
- During development: Mistral API may be used with your own test documents. Switch to self-hosted before processing customer data.
- Pseudonymized text only leaves via the user's browser clipboard (user's responsibility from that point)
- Local LLM (Layer 3) runs on same infrastructure via Ollama, no external calls
- Firewall: only port 443 exposed, all internal communication on private network
- This is a massive trust advantage: "Our servers never talk to OpenAI/Anthropic/Google. Period."

> **If using EU-hosted LLM API as temporary bridge (Option 4):** The "no outbound calls" claim does not apply. Update marketing copy accordingly and disclose the LLM provider as sub-processor in AVV. Migrate to self-hosted as soon as possible to restore this selling point.

---

## 9. Evaluation & Testing Strategy

### 9.1 Synthetic Test Dataset

Build a DACH-specific evaluation dataset before writing production code.

```python
# Generate 10,000 synthetic business documents in DE and EN
# with known PII at known positions

from faker import Faker
fake_de = Faker('de_DE')
fake_at = Faker('de_AT')
fake_ch = Faker('de_CH')
fake_en = Faker('en_US')
fake_gb = Faker('en_GB')

TEMPLATES = [
    "contracts_de",        # Kaufvertrag, Mietvertrag, Arbeitsvertrag
    "contracts_en",        # Purchase agreement, lease, employment contract
    "contracts_mixed",     # English template with German party details
    "invoices_de",         # Rechnung with IBAN, Steuer-Nr
    "invoices_en",         # Invoice (common for international DACH business)
    "hr_documents_de",     # Personalakte, Bewerbung
    "hr_documents_en",     # HR files for international employees
    "emails_de",           # Business email with names, addresses
    "emails_en",           # English business correspondence
    "emails_mixed",        # English email body, German signature block
    "financial_de",        # Kontoauszug, Bilanz
]

# For each template:
# 1. Generate document text with Faker PII injected at known positions
# 2. Store ground truth: exact positions, types, and language of all PII
# 3. Include edge cases:
#    - German compound names in English text ("Please contact Herr Müller-Schmidt")
#    - Mixed address formats ("Bahnhofstrasse 42" in an English document)
#    - English names in German documents (common in international teams)
#    - Code-switching mid-paragraph
#    - Swiss addresses with French/Italian city names in German/English text
```

> **Important for phased Layer 3:** This synthetic dataset gives you ground-truth PII positions for automated evaluation. You can (and should) also test with your own real documents during development to catch edge cases that synthetic data misses — Mistral API is fine for this since you're the data controller of your own docs.

### 9.2 Evaluation Metrics

Run evaluation after every model change or recognizer update:

```python
def evaluate(predictions: list, ground_truth: list) -> dict:
    """
    Calculate per-entity-type and overall metrics.
    Target: F2 >= 0.95 overall, Recall >= 0.97 for structured PII (IBAN, IDs)
    """
    return {
        "overall": {"precision": ..., "recall": ..., "f1": ..., "f2": ...},
        "per_type": {
            "PERSON":   {"precision": ..., "recall": ..., "f2": ...},
            "IBAN":     {"precision": ..., "recall": ..., "f2": ...},
            "ADDRESS":  {"precision": ..., "recall": ..., "f2": ...},
            # ... etc
        },
        "per_layer_config": {
            "layers_1_2_only":  {"precision": ..., "recall": ..., "f2": ...},
            "layers_1_2_3":     {"precision": ..., "recall": ..., "f2": ...},
        },
        "error_analysis": {
            "false_negatives": [...],  # missed PII — most dangerous
            "false_positives": [...],  # over-detection — annoying
            "layer3_unique_catches": [...],  # PII found only by Layer 3
        }
    }
```

> **Layer 3 go/no-go decision:** If `layers_1_2_only` F2 is already ≥ 0.93 on your synthetic dataset, you can confidently launch without Layer 3 and add it later. If there's a significant gap (e.g., F2 drops from 0.96 to 0.88 without Layer 3), prioritize the GPU deployment.

### 9.3 Continuous Red-Teaming

Maintain a growing list of adversarial test cases:

- Names that look like common words ("Rose Engel", "Christian Schwarz", "Bill Banks")
- PII split across table cells ("Max" in cell A1, "Mustermann" in B1)
- PII in unusual positions (Excel sheet names, PDF metadata, DOCX comments)
- Abbreviations ("Hr. Müller", "Fr. Dr. Schmidt-Weber", "Mr. Smith-Jones")
- Mixed-language documents: German PII in English text and vice versa
- Code-switching mid-sentence ("Please forward this to Frau Meier at Hauptstraße 7")
- English names that are also German words ("Art Fischer", "Mark Wolf")
- German names that look English ("Thomas Mann", "Peter Gabriel")
- International phone formats mixed in (+49 vs +44 vs +1 in same document)
- DACH addresses written in English format ("42 Bahnhofstrasse, Zurich 8001")
- PII in numbers context ("Konto 12345678, BLZ 37040044", "Account #98765")

---

## 10. MVP Development Timeline

### Phase 1: Foundation (Weeks 1-3)
- [ ] Set up project repo, CI/CD, Hetzner Cloud VPS (CPX21/31, ~€8-15/month)
- [ ] Implement Document Processing Service (PDF, DOCX, XLSX extraction)
- [ ] Build document class selector and sensitivity-based routing
- [ ] Build DACH regex recognizers (Layer 1) with checksum validators
- [ ] Create synthetic test dataset (1,000 documents minimum)
- [ ] Set up PostgreSQL schema (incl. document_class, sensitivity, export fields) and basic API scaffolding
- [ ] Install Ollama on local dev machine, practice LLM deployment

### Phase 2: Detection Pipeline (Weeks 4-6)
- [ ] Integrate Presidio with custom DACH recognizers
- [ ] Set up spaCy German NER model (Layer 2)
- [ ] Integrate Mistral API for Layer 3 testing (`LLM_BACKEND=mistral`) — test with your own documents
- [ ] Build Layer 3 abstraction (`Layer3LLM` class with mistral/ollama/disabled backends) — **switching must be a single env var change, no code changes**
- [ ] Implement merge/conflict resolution logic
- [ ] Build evaluation pipeline, measure baseline metrics for Layers 1+2 only AND Layers 1+2+3
- [ ] Iterate on detection quality until F2 ≥ 0.90
- [ ] **Decision point:** How much does Layer 3 improve recall? Is it worth GPU cost for launch?

### Phase 3: Review UI (Weeks 7-9)
- [ ] Build React review interface with inline PII highlighting
- [ ] Implement entity confirm/reject/modify/add actions
- [ ] Build batch confirmation with automation bias safeguards (scroll tracking, regex-only batch, sensitivity gating)
- [ ] Build zero-detection warning gate
- [ ] Build reviewer onboarding/training flow
- [ ] Implement job state machine and audit logging (incl. document class, export events)
- [ ] User testing with internal team reviewing real-ish documents

### Phase 4: Pseudonymized Output, Second Scan & Export Gate (Weeks 10-11)
- [ ] Build pseudonymization engine with synthetic replacement
- [ ] Build second scan (defense-in-depth) service — rerun Layer 1+2 on output, allowlist synthetic replacements
- [ ] Build export gate UI with mandatory acknowledgement checkboxes (Enterprise/DPA, GDPR awareness)
- [ ] Build DPIA trigger warnings for high/critical sensitivity document classes
- [ ] Build pseudonymized output panel with gated copy-to-clipboard
- [ ] Build paste-back textarea with de-pseudonymization endpoint
- [ ] Implement variant matching (surname-only, title+surname)
- [ ] Build unmapped pseudonym warning system
- [ ] Highlight replaced tokens in de-pseudonymized output
- [ ] **Write and test deployment script** — run it 2-3 times on throwaway Hetzner instances
- [ ] End-to-end testing: upload → classify → detect → review → pseudonymize → 2nd scan → export gate → copy → (manual LLM) → paste → de-pseudonymize

### Phase 5: Legal Kit, Hardening & Beta Launch (Weeks 12-14)
- [ ] **Infrastructure upgrade:** Order Hetzner GEX44 (€184/month) or dedicated CPU server (~€30-50/month) — run deployment script, verify Layer 3 works with Ollama in production
- [ ] **Fallback plan:** If GPU server unavailable or delayed, launch with Layers 1+2 only (`LLM_BACKEND=disabled`) or with Mistral API bridge (`LLM_BACKEND=mistral` + sub-processor disclosure in AVV)
- [ ] Security audit (encryption, access control, network isolation)
- [ ] Load testing (target: 50 concurrent documents)
- [ ] Draft Minimum Legal Kit: AVV template, customer LLM usage policy template, DPIA trigger checklist, Switzerland addendum (Auslandsbekanntgabe/SCC)
- [ ] Draft service description / Leistungsbeschreibung with § 307(3) BGB framing ("probabilistic detection assistant")
- [ ] Documentation: user guide, API docs, privacy policy
- [ ] Verify all UI copy, docs, and marketing use "pseudonymization" — never "anonymization" without qualifier
- [ ] Set up monitoring and alerting (Grafana + Prometheus)
- [ ] Onboard 3-5 beta customers from DACH region
- [ ] Collect feedback, iterate

**Total estimated MVP timeline: 14 weeks with 2 developers**

> ⚠️ Note: The phased infrastructure approach saves ~€150-170/month during development (weeks 1-11) compared to running a GPU server from day one. Total savings: ~€400-500 before the first customer. The deployment script ensures you can go from cheap VPS to full production in under an hour when needed.

---

## 11. Key Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| German NER model performs poorly on business documents | Core value prop broken | Use multilingual xlm-roberta-large; fine-tune on domain-specific data in both languages; Layer 3 LLM as safety net; human review catches the rest |
| Human review creates too much friction, users abandon | Low adoption | Optimize for speed: batch confirm high-confidence regex entities, keyboard shortcuts, target < 2 min review for typical document. But never sacrifice review quality for speed — automation bias safeguards are non-negotiable |
| Local LLM (Layer 3) too slow | Bad UX during processing | Use quantized model + GPU; run async; show Layer 1+2 results first while Layer 3 processes. On CPU: accept 15-30 sec delay per doc or disable Layer 3 for speed |
| **Need to deploy LLM quickly when first customer signs** | Delayed onboarding, lost deal | **Write deployment script during Phase A. Test it 2-3 times on throwaway servers. Ollama makes LLM deployment trivial (~10 min including model download). Fallback: launch with Layers 1+2 only or Mistral API bridge** |
| **GPU server cost too high before revenue** | Cash burn without income | **Phased approach: develop on €8-15/month VPS, only upgrade when customer revenue justifies it. Option to launch with CPU-only inference or without Layer 3 entirely** |
| Customer's LLM API key management | — | **Eliminated in MVP.** User uses their own LLM account via copy-paste. No API keys stored on our side. |
| Competitor (Private AI, Nymiz) enters DACH market aggressively | Market pressure | Differentiate on DACH-specific entity coverage, German-language review UX, local hosting trust story, sector-specific legal compliance (§ 203 StGB, DORA, VAIT) |
| Edge cases in document extraction break pseudonymization | PII leak through extraction bug | Extensive test suite for document edge cases; log extraction confidence; warn user on complex documents; **second scan catches residual PII** |
| Mixed-language documents confuse NER model | Reduced accuracy at language boundaries | Segment-level language detection; bilingual xlm-roberta handles code-switching; Layer 3 LLM excels at mixed text; human review as final safety net |
| **Kardinalpflicht liability — PII detection is a cardinal obligation under German law** | Contractual liability cannot be disclaimed in AGB | Frame service as "probabilistic detection assistant" in Leistungsbeschreibung (§ 307(3) BGB play); limit liability for cardinal duty breach to foreseeable damages; never promise guaranteed detection; build genuinely excellent detection |
| **Customer uses consumer LLM chat (no DPA)** | GDPR breach attributed partly to us, reputational damage | Export gate with mandatory Enterprise/API acknowledgement; in-app warnings; customer LLM usage policy template; audit trail proving we warned |
| **"Anonymization" marketing claim challenged** | Irreführung / misleading claims; undermines entire contract architecture | Use "pseudonymization" and "PII detection" consistently; never claim "DSGVO erledigt" or "100% anonymisiert"; legal review all marketing copy |
| **Systematic detection failure (unknown PII pattern)** | Unmitigable liability — no contract protects against this | Invest in detection quality above all else; red-team aggressively; second scan as safety net; be transparent about known limitations; carry E&O insurance |
| **Law firm client suffers § 203 StGB exposure** | Criminal liability creates extreme pressure to recover from vendor | Sector-specific contract (Berufsgeheimnisträgervereinbarung); stricter review defaults for legal case files; § 43e BRAO-compliant confidentiality terms |
| **Art. 9 DSGVO data (health, HR) processed without DPIA** | Higher fine tier (€20M / 4% turnover) | Document class selector with DPIA trigger; block critical-sensitivity exports without DPIA acknowledgement; AVV template with Art. 9 provisions |

---

## 12. Success Metrics (MVP)

| Metric | Target | How to Measure |
|---|---|---|
| PII recall (post-detection, pre-review) | ≥ 0.92 | Synthetic test suite |
| PII recall (post-human-review) | ≥ 0.99 | Sample audit of completed jobs |
| Second scan catch rate | Measure, no target yet | How often second scan catches residual PII missed by review |
| Median review time per document | < 3 minutes | Timestamp: review_started → review_completed |
| End-to-end processing time | < 5 minutes (excl. review) | Timestamp: upload → pseudonymized output ready |
| De-pseudonymization accuracy | ≥ 95% of pseudonyms auto-replaced correctly | Sample audit of de-pseudonymize requests |
| Export gate completion rate | ≥ 90% (users don't abandon at gate) | Funnel: pseudonymized → export acknowledged → copied |
| Beta customer satisfaction | ≥ 8/10 | Survey after 2 weeks usage |
| Zero PII in pseudonymized output (post-second-scan) | 0 incidents | Audit log review + customer reports |
| Consumer LLM usage rate | Measure, target: < 5% of exports | Export gate acknowledgement data (are users being honest?) |
| Layer 3 value-add | Measure recall with/without | Compare Layers 1+2 vs Layers 1+2+3 on real reviewer additions |

---

## 13. Post-MVP Roadmap

**v1.1 — Expanded file support + certifications**
- OCR for scanned PDFs (Tesseract + German language pack)
- Support for .csv, .txt, .eml (email files)
- Begin BSI C5 Type 2 attestation process (mandatory for health data since July 2024 under § 393 SGB V)
- ISO 27001 certification planning
- DORA-compliant contract templates for financial sector customers
- § 43e BRAO-compliant Berufsgeheimnisträgervereinbarung for law firms
- If still on CPU inference or Mistral API bridge: migrate to self-hosted GPU (GEX44)

**v1.2 — Smarter detection + LLM proxy**
- Quasi-identifier detection (combinatorial re-identification warnings)
- Custom entity types per customer (e.g., internal project codes, patient IDs)
- Active learning: human corrections feed back into NER model retraining
- **Direct LLM API integration:** automatic proxy to Claude/GPT/Mistral with built-in de-pseudonymization (based on learnings from manual paste-back in v1.0)
- API key management with encrypted storage
- Contextual identifier detection (project names, small locations, specific roles that enable re-identification even without classic PII — per EDPB LLM risk guidance)

**v2.0 — Multi-tenant SaaS**
- Shared infrastructure with strict tenant isolation
- Self-service onboarding with Stripe billing
- Team management, role-based access
- Multiple LLM provider support (GPT, Claude, Mistral)
- SOC 2 Type 2 certification
- IT-Haftpflichtversicherung + Cyber-Versicherung in place (Hiscox Net IT or equivalent, covering AI-related claims)

**v2.1 — On-premise edition**
- Docker Compose / Kubernetes Helm chart for customer-hosted deployment
- Bring-your-own-LLM (including local models)
- Air-gapped mode (no external API calls at all)

**v3.0 — AI Gateway (fully automatic)**
- API-first: customers integrate via API, not just upload UI
- Middleware mode: sits between customer's app and LLM API, transparently pseudonymizing
- Streaming support for real-time chat with pseudonymization
- This is the end-state vision — but only possible because v1.0 manual paste-back taught us how LLMs handle pseudonyms in practice

---

## 14. Legal & Liability Architecture

> This section synthesizes the external legal analysis into actionable product and contractual requirements. It is **not legal advice** — engage German/Swiss counsel to draft the actual contracts. But these constraints are load-bearing for the MVP design.

### 14.1 Why This Matters: The Kardinalpflicht Problem

Under German AGB law (§§ 305–310 BGB), PII detection is the tool's core function — making it a **Kardinalpflicht** (cardinal contractual obligation). German courts (BGH) do not allow vendors to exclude liability for breach of cardinal obligations in standard terms. This means:

- We **cannot** disclaim liability for detection failures in our standard AGB
- We **can** limit liability for simple negligent breach of cardinal duties to "typically foreseeable, contract-typical damages" (vorhersehbarer, vertragstypischer Schaden)
- We **cannot** limit liability for intent, gross negligence, bodily harm, or ProdHaftG claims under any circumstances
- If a liability clause overreaches, **the entire clause is void** (no blue-penciling under German law)

**The most important mitigation is product quality.** No contract architecture survives a systematically broken detection engine.

### 14.2 The § 307(3) BGB Play — Service Description as Legal Shield

The single most powerful legal tool available: **§ 307(3) BGB exempts core service descriptions (Leistungsbeschreibung) from AGB content control.** If the service is defined as a probabilistic detection assistant requiring human verification, then a "defect" only exists where the product deviates from *that* specification — not from an imagined guarantee of complete PII removal.

This means the Leistungsbeschreibung must explicitly state:

> *The service provides probabilistic detection support for identifying personal data in uploaded documents. Detection results are suggestions requiring independent human verification by the customer. The service does not guarantee complete identification or removal of all personal data. The customer (as data controller) bears responsibility for final verification of detection results before export.*

This text (or its German equivalent) must appear in: the Master SaaS Agreement (Leistungsbeschreibung annex), the UI (onboarding flow, export gate), the AVV, the API documentation, and the homepage/marketing materials.

### 14.3 HITL as Shared Responsibility, Not Liability Transfer

The human review step **does not** fully transfer liability to the customer under German law. There is no "Abnahme" (acceptance) concept in Mietverträge (SaaS rental classification per BGH XII ZR 120/04). However, **§ 254 BGB (Mitverschulden)** reduces our share if the customer's reviewer was negligent. Courts could assign 50–80% of fault to a negligent reviewer.

This is why the UI design decisions in Section 4.4 are legally critical:

| UI Design Choice | Legal Effect |
|---|---|
| No blanket "Confirm all" without engagement | Reduces our Organisationsverschulden exposure |
| Scroll tracking before batch confirm | Evidence that reviewer saw all entities |
| Reviewer training requirement | We designed for genuine human oversight (EU AI Act Art. 14) |
| Automation bias warnings | We cannot design for trust then blame the human |
| Confidence scores displayed | Reviewer was informed of uncertainty |
| All actions logged with timestamps | Evidentiary basis for § 254 BGB assessment |

### 14.4 Contract Structure (Five-Document Architecture)

| Document | Purpose | AGB Control? |
|---|---|---|
| **Master SaaS Agreement** | Tiered liability framework, term, termination | Yes — must comply with §§ 305-310 BGB |
| **Leistungsbeschreibung (Service Description)** | Defines service as probabilistic detection assistant | **No** — exempt from content control under § 307(3) BGB |
| **SLA** | Uptime, response times — explicitly excludes accuracy guarantees | Yes |
| **AVV / DPA** | Art. 28 GDPR processing agreement with sector-appropriate provisions | Yes (but largely mandatory content) |
| **Order Form** | Pricing, term, customer-specific config | Partially negotiable |

**Additional documents for specific sectors:**
- Law firms: Berufsgeheimnisträgervereinbarung (§ 203 StGB / § 43e BRAO compliant)
- Financial services: DORA Art. 30 contractual provisions
- Insurance: BaFin VAIT-compatible outsourcing terms with audit rights

**Liability cap structure:**

| Tier | Scope | Cap |
|---|---|---|
| 1 — Unlimited | Intent, gross negligence, bodily harm, ProdHaftG, fraudulently concealed defects | None — mandatory under German law |
| 2 — Foreseeable damages | Simple negligent breach of cardinal obligations (= detection failures) | Foreseeable, contract-typical damages |
| 3 — General cap | Simple negligent breach of non-cardinal duties, indirect/consequential damages | 12 months' annual fees (market standard) |
| Data protection supercap | Data protection / GDPR breaches specifically | 24 months' fees or fixed €-amount |

For enterprise customers: **individually negotiate** liability terms to escape AGB control entirely (BGH requires genuine opportunity to influence content — not just passive acceptance).

### 14.5 GDPR Processor Liability

Under Art. 82 GDPR, fault is presumed and the burden of proof is reversed (CJEU C-667/21). If our tool fails to detect PII, we must prove our detection technology represented the state of the art and the failure was objectively unforeseeable. Under Art. 82(4), **joint and several liability** applies — a data subject can sue either party for the full amount.

The AVV must:
- Define the tool as providing detection "suggestions" subject to human review
- Specify known accuracy limitations (with F-scores if possible)
- Assign final verification responsibility to the controller
- Include enhanced protocols for Art. 9 data (health, HR)
- Address data sub-processing (we have none in MVP with self-hosted LLM — massive advantage. If using Mistral API bridge, disclose as sub-processor)
- Include international transfer mechanisms if customer is CH-based (SCC recognition by EDÖB)

### 14.6 Copy-Paste Is Not a Legal Safe Harbor

The manual copy-paste approach reduces our direct integration risk (no API keys, no outbound calls), but it does **not** eliminate the legal complexity:

- Even pseudonymized text may contain **contextual identifiers** (project names, small office locations, specific roles) that enable re-identification — EDPB guidance explicitly flags this
- The user pasting into a consumer LLM chat (no DPA, unclear retention/training) creates a GDPR breach that could be partly attributed to us if we enabled/facilitated it
- Under Swiss law (DSG), cross-border disclosure (Auslandsbekanntgabe) rules apply regardless of copy-paste mechanism

**Risk tiers for customer LLM usage:**

| LLM Access Type | Risk | Our Mitigation |
|---|---|---|
| Consumer chat (ChatGPT Free, Gemini Free) — no DPA, data may be used for training | **HIGH** | Export gate: mandatory acknowledgement that Enterprise/API is used; warn against consumer chat; document in customer LLM usage policy |
| Enterprise/API with DPA (e.g., OpenAI Business/Enterprise, Anthropic API) — no training, DPA in place | **MEDIUM** | Acceptable for MVP; customer still needs own Rechtsgrundlage, DPIA, Informationspflichten |
| EU/CH-hosted LLM with contractual clarity (or on-prem) | **LOW** | Best case; recommend in customer onboarding |

### 14.7 Insurance Requirements

**Essential before entering regulated markets:**

| Coverage | What It Covers | Provider Examples | Budget |
|---|---|---|---|
| IT-Haftpflichtversicherung | Customer financial losses from tool errors | Hiscox Net IT (explicitly covers AI claims), Markel Pro IT, Allianz | €5k–15k/year |
| Cyber-Versicherung | Own breach costs, incident response, forensics | Allianz CyberSchutz, Hiscox CyberClear | €3k–10k/year |

Ensure coverage includes: contractual liability claims, SLA-based strict liability, GDPR Art. 82 claims.

### 14.8 Certification Roadmap

| Certification | Why | When | Mandatory? |
|---|---|---|---|
| BSI C5 Type 2 | Required for health data cloud providers (§ 393 SGB V since July 2024); increasingly expected by all regulated sectors | v1.1 (begin process) | Yes, for insurance/health sector |
| ISO 27001 | Market expectation in DACH B2B | v1.1–v2.0 | Strongly recommended |
| SOC 2 Type 2 | International credibility, some financial customers require it | v2.0 | Nice-to-have for MVP |

### 14.9 Minimum Legal Kit (Ship with MVP)

These documents must be ready before beta customer onboarding:

1. **AVV Template (Auftragsverarbeitungsvertrag):** Art. 28 GDPR compliant, with probabilistic-detection framing, known limitations, Art. 9 provisions, sub-processor status (none if self-hosted LLM; Mistral if using API bridge)
2. **Customer LLM Usage Policy Template:** Which LLM tools are permitted (Enterprise/API only), which are forbidden (consumer chat), data type restrictions, required DPA with LLM provider
3. **DPIA Trigger Checklist:** When is a DPIA required? What to assess? Based on EDPB LLM risk guidance. Provided as PDF/fillable form.
4. **Switzerland Addendum:** Auslandsbekanntgabe / SCC reference, EDÖB recognition, DSG-specific provisions
5. **Service Description / Leistungsbeschreibung:** The § 307(3) BGB text defining the service as probabilistic detection assistant (dual-language DE/EN)

---

## 15. Homepage & Marketing Wording Guide

> **This section is non-negotiable.** Every word on the homepage, in sales decks, and in customer communications directly affects the liability architecture defined in Section 14. Incorrect claims can void the § 307(3) BGB service description defense, create Irreführung (misleading advertising) exposure under UWG, and undermine the Kardinalpflicht limitation strategy.

### 15.1 Terminology Rules

| ✅ USE | ❌ NEVER USE | Why |
|---|---|---|
| Pseudonymisierung | Anonymisierung (without qualifier) | We retain a re-identification mapping → GDPR continues to apply. Calling it "Anonymisierung" implies GDPR no longer applies, which is legally wrong and creates Irreführung risk |
| PII-Erkennung / PII Detection | Garantierte PII-Entfernung / Guaranteed PII removal | Detection is probabilistic. "Guaranteed" creates an absolute Beschaffenheitsvereinbarung we cannot meet |
| Unterstützt Ihre Mitarbeiter bei der Identifikation | Erkennt alle personenbezogenen Daten | "Alle" = absolute promise = Kardinalpflicht breach if even one PII is missed |
| Reduziert das Risiko unbeabsichtigter Datenweitergabe | Eliminiert das Risiko / Zero Risk | Risk reduction ≠ risk elimination. "Zero Risk" is both legally and technically false |
| Datenschutz-Assistent / Privacy Assistant | DSGVO-konform / GDPR-compliant | We are a tool, not a compliance certification. GDPR compliance depends on how the customer uses the tool |
| Probabilistische Erkennung mit menschlicher Verifikation | Vollautomatische Anonymisierung | Our core legal defense is that human review is required. "Vollautomatisch" undermines this completely |
| KI-gestützte PII-Erkennung | 100% akkurat / 100% sicher | No PII detection system achieves 100%. This claim would be demonstrably false |
| Ihre Daten verlassen unsere DACH-Server nicht | Keine Daten verlassen Ihr Unternehmen | The pseudonymized text *does* leave — via the user's clipboard to their LLM. Our servers don't send it, but the user does. The distinction matters |

### 15.2 Recommended Homepage Copy (DE)

**Headline options:**
- "Personenbezogene Daten erkennen, bevor Sie KI nutzen"
- "Vertrauliche Dokumente sicher für KI vorbereiten"
- "PII-Erkennung mit menschlicher Kontrolle"

**Subheadline:**
- "Unsere KI-gestützte Erkennung findet personenbezogene Daten in Ihren Dokumenten. Ihr Team prüft und bestätigt. Erst dann wird der pseudonymisierte Text freigegeben."

**Feature bullets (if needed):**
- Mehrstufige PII-Erkennung (Regex, NER, kontextuelle KI)
- Menschliche Prüfung als letzte Sicherheitsebene
- Doppelte Kontrolle: automatischer Zweitscan vor Export
- Gehostet in Deutschland/Schweiz — keine Daten an US-Cloud-Anbieter
- Volle Audit-Trails für Compliance-Nachweise
- Integrierte Export-Kontrollen (nur Enterprise-LLM-Zugänge)

**What NOT to put on the homepage:**
- ~~"100% DSGVO-konform"~~
- ~~"Vollständig anonymisiert"~~
- ~~"Garantiert keine Datenlecks"~~
- ~~"Rechtssicher"~~
- ~~"Ersetzt Ihren Datenschutzbeauftragten"~~

### 15.3 Recommended Homepage Copy (EN)

**Headline options:**
- "Detect personal data before you use AI"
- "Prepare confidential documents safely for LLM usage"
- "PII detection with human oversight"

**Subheadline:**
- "Our AI-powered detection finds personal data in your documents. Your team reviews and confirms. Only then is the pseudonymized text released for use with your LLM."

### 15.4 Sales Deck / Customer Communication Guidelines

**When a customer asks "Is this GDPR-compliant?":**
> "Our tool helps you reduce the risk of inadvertent personal data disclosure when using LLMs. It supports your GDPR compliance process — but GDPR compliance ultimately depends on your overall data processing setup, including your DPA with the LLM provider, your legal basis for processing, and your DPIA. We provide the technical safeguards and audit trails to support your compliance, not replace it."

**When a customer asks "Is the output anonymous?":**
> "The output is pseudonymized — personal identifiers are replaced with synthetic alternatives. Because we retain a mapping for de-pseudonymization, the data remains personal data under GDPR in the legal sense. However, the pseudonymization significantly reduces the risk of the LLM provider being able to identify individuals."

**When a customer asks "Can we use ChatGPT Free?":**
> "We strongly recommend using only Enterprise or API LLM accounts that have a data processing agreement (DPA) and do not use your data for model training. Our export gate requires acknowledgement of this. Using consumer chat products without DPA creates significant GDPR risk that our pseudonymization alone cannot mitigate."

### 15.5 In-Product Copy Requirements

All user-facing text in the application must be reviewed against these guidelines. Key locations:

| Location | Required Language |
|---|---|
| Upload page | "PII-Erkennung" / "PII Detection" — never "Anonymisierung" |
| Review UI header | "Überprüfen und bestätigen Sie die erkannten personenbezogenen Daten" |
| Export gate | Full acknowledgement text (see Section 4.7) |
| DPIA trigger warning | "Datenschutz-Folgenabschätzung empfohlen" with link to checklist |
| De-pseudonymization page | "Pseudonyme ersetzen" — never "De-anonymisieren" |
| Audit log export | "Pseudonymisierungsprotokoll" / "Pseudonymization audit trail" |
| Error messages / zero-detection warning | Refer to "PII-Erkennung" not "Anonymisierung" |
| Onboarding / training flow | Explain difference between pseudonymization and anonymization |

### 15.6 Domain & Brand Considerations

If the product name or domain contains "anonym" (e.g., *anonym.ai*, *anonymize.de*), consider whether this creates implicit claims that conflict with the legal architecture. A name built around "detect", "shield", "redact", or "protect" is legally safer than one built around "anonymize." This is a genuine commercial risk — the German UWG (Gesetz gegen den unlauteren Wettbewerb) prohibits misleading commercial practices, and a competitor or consumer protection association could challenge a brand name that implies anonymization when the product performs pseudonymization.
