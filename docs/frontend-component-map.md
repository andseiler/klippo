# Klippo Frontend Component Map

> **Purpose:** A visual field guide for frontend developers. Look at the running app, find the screen/element here, and know exactly which `.vue` file to open.
>
> **Stack:** Vue 3 (Composition API, `<script setup>`), Pinia, Vue Router, vue-i18n (DE/EN), Radix Vue, Tailwind CSS
>
> **All components live under:** `src/frontend/src/`

---

## Table of Contents

1. [Route Map](#1-route-map)
2. [App Shell & Layout System](#2-app-shell--layout-system)
3. [Screen: Login (`/login`)](#3-screen-login-login)
4. [Screen: Register (`/register`)](#4-screen-register-register)
5. [Screen: Dashboard (`/dashboard`)](#5-screen-dashboard-dashboard)
6. [Screen: Upload (`/upload`)](#6-screen-upload-upload)
7. [Screen: Review Workbench (`/jobs/:id/review`)](#7-screen-review-workbench-jobsidreview)
8. [Screen: Playground (`/playground`)](#8-screen-playground-playground)
9. [Screen: 404 Not Found](#9-screen-404-not-found)
10. [Shared UI Component Reference](#10-shared-ui-component-reference)
11. [Stores (State Management)](#11-stores-state-management)
12. [Composables](#12-composables)
13. [Complete File Index](#13-complete-file-index)

---

## 1. Route Map

| URL Path              | Route Name    | View Component        | Layout    | Auth? |
|-----------------------|---------------|-----------------------|-----------|-------|
| `/`                   | —             | *(redirect)*          | —         | —     |
| `/login`              | `login`       | `LoginView.vue`       | bare      | No    |
| `/register`           | `register`    | `RegisterView.vue`    | bare      | No    |
| `/dashboard`          | `dashboard`   | `DashboardView.vue`   | AppLayout | Yes   |
| `/upload`             | `upload`      | `UploadView.vue`      | AppLayout | Yes   |
| `/jobs/:id/review`    | `job-review`  | `ReviewView.vue`      | bare      | Yes   |
| `/playground`         | `playground`  | `PlaygroundView.vue`  | bare      | No    |
| `/:pathMatch(.*)*`    | `not-found`   | `NotFoundView.vue`    | bare      | No    |

**Router file:** `router/index.ts`

**Layout logic:** `App.vue` checks `route.meta.layout`. If `'bare'` → view renders alone. Otherwise → view is wrapped in `AppLayout` (sidebar + topbar).

---

## 2. App Shell & Layout System

### What you see

```
┌─────────────────────────────────────────────────────┐
│                      App.vue                        │
│  ┌──────────────────────────────────────────────────┤
│  │         IF layout !== 'bare':                    │
│  │  ┌───────────────────────────────────────────┐   │
│  │  │            AppLayout.vue                  │   │
│  │  │  ┌─────┬──────────────────────────────┐   │   │
│  │  │  │     │        AppTopBar.vue          │   │   │
│  │  │  │  A  │  [☰] [🌙] [LanguageToggle]   │   │   │
│  │  │  │  p  │              [UserMenu ▾]     │   │   │
│  │  │  │  p  ├──────────────────────────────┤   │   │
│  │  │  │  S  │                              │   │   │
│  │  │  │  i  │     <slot /> = RouterView    │   │   │
│  │  │  │  d  │     (DashboardView or        │   │   │
│  │  │  │  e  │      UploadView goes here)   │   │   │
│  │  │  │  b  │                              │   │   │
│  │  │  │  a  │                              │   │   │
│  │  │  │  r  │                              │   │   │
│  │  │  └─────┴──────────────────────────────┘   │   │
│  │  └───────────────────────────────────────────┘   │
│  ├──────────────────────────────────────────────────┤
│  │         IF layout === 'bare':                    │
│  │         RouterView renders alone (no shell)      │
│  └──────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────┘
```

### Component tree

```
App.vue                                          → views/App.vue
├── AppLayout.vue                                → components/layout/AppLayout.vue
│   ├── AppSidebar.vue                           → components/layout/AppSidebar.vue
│   │   └── SidebarNavItem.vue  (×2, v-for)      → components/layout/SidebarNavItem.vue
│   │       ├── item: /dashboard (grid icon)
│   │       └── item: /upload   (upload icon)
│   ├── AppTopBar.vue                            → components/layout/AppTopBar.vue
│   │   ├── LanguageToggle.vue                   → components/layout/LanguageToggle.vue
│   │   └── UserMenu.vue                         → components/layout/UserMenu.vue
│   │       └── AppBadge.vue (role badge)         → components/ui/AppBadge.vue
│   └── <slot /> ← view content injected here
└── RouterView (bare, no wrapper)
```

### File details

| Component | File | What it does |
|-----------|------|--------------|
| **App.vue** | `App.vue` | Root. Decides bare vs AppLayout based on `route.meta.layout` |
| **AppLayout** | `components/layout/AppLayout.vue` | Shell: sidebar + topbar + `<slot>` for view content |
| **AppSidebar** | `components/layout/AppSidebar.vue` | Left sidebar, collapsible (desktop) / slide-out (mobile). Shows "Klippo" branding + 2 nav items |
| **SidebarNavItem** | `components/layout/SidebarNavItem.vue` | Single nav link. Props: `to`, `icon` (`'dashboard'`\|`'upload'`), `labelKey`. Label hidden when collapsed |
| **AppTopBar** | `components/layout/AppTopBar.vue` | Top bar: hamburger (mobile), dark mode toggle, language toggle, user menu |
| **LanguageToggle** | `components/layout/LanguageToggle.vue` | Button toggling between DE↔EN. Persists to `localStorage.locale` |
| **UserMenu** | `components/layout/UserMenu.vue` | Radix Vue dropdown: shows email, name, role badge, logout action |

---

## 3. Screen: Login (`/login`)

### What you see

```
┌─────────────────────────────────────────┐
│                          [LanguageToggle]│  ← top-right corner
│                                         │
│              🔒 Klippo                  │
│                                         │
│   ┌─────────────────────────────────┐   │
│   │  [AppAlert] (v-if error)        │   │  ← only if login fails
│   │                                 │   │
│   │  Email:    [____________]       │   │
│   │  Password: [____________]       │   │
│   │                                 │   │
│   │  [AppButton: Login]             │   │
│   │                                 │   │
│   │  → Register link                │   │
│   │  → Try Playground link          │   │
│   └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

### Component tree

```
LoginView.vue                                → views/LoginView.vue
├── LanguageToggle.vue                       → components/layout/LanguageToggle.vue
├── AppAlert.vue          (v-if="error")     → components/ui/AppAlert.vue
└── AppButton.vue         (submit)           → components/ui/AppButton.vue
```

**Layout:** bare (no sidebar/topbar)

---

## 4. Screen: Register (`/register`)

### What you see

```
┌─────────────────────────────────────────┐
│                          [LanguageToggle]│
│                                         │
│              📝 Register                │
│                                         │
│   ┌─────────────────────────────────┐   │
│   │  [AppAlert] (v-if error)        │   │
│   │                                 │   │
│   │  Email:         [____________]  │   │
│   │  Name:          [____________]  │   │
│   │  Organization:  [____________]  │   │
│   │  Password:      [____________]  │   │
│   │  Confirm:       [____________]  │   │
│   │                                 │   │
│   │  [AppButton: Register]          │   │
│   │                                 │   │
│   │  → Login link                   │   │
│   └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

### Component tree

```
RegisterView.vue                             → views/RegisterView.vue
├── LanguageToggle.vue                       → components/layout/LanguageToggle.vue
├── AppAlert.vue          (v-if="error")     → components/ui/AppAlert.vue
└── AppButton.vue         (submit)           → components/ui/AppButton.vue
```

**Layout:** bare

---

## 5. Screen: Dashboard (`/dashboard`)

### What you see

```
┌─────┬──────────────────────────────────────────┐
│     │  AppTopBar  [☰] [🌙] [Lang] [UserMenu▾] │
│  S  ├──────────────────────────────────────────┤
│  i  │                                          │
│  d  │  Dashboard                               │
│  e  │  ┌──────────────────────────┐            │
│  b  │  │ Search: [____________]   │ [Upload ➜] │  ← AppButton links to /upload
│  a  │  └──────────────────────────┘            │
│  r  │                                          │
│     │  ┌── JobTable ──────────────────────────┐│
│     │  │ [AppSpinner]        (if loading)     ││
│     │  │ [AppAlert]          (if error)       ││
│     │  │ "No jobs yet"       (if empty)       ││
│     │  │                     (else: table)    ││
│     │  │ ┌────────────────────────────────┐   ││
│     │  │ │ JobRow                         │   ││
│     │  │ │  fileName | StatusBadge | date │   ││
│     │  │ │           |  └AppBadge |      │   ││
│     │  │ │  [AppSpinner if processing]    │   ││
│     │  │ │            [InlineDeleteButton]│   ││
│     │  │ ├────────────────────────────────┤   ││
│     │  │ │ JobRow (repeated...)           │   ││
│     │  │ └────────────────────────────────┘   ││
│     │  └──────────────────────────────────────┘│
│     │                                          │
│     │  [AppPagination] (if totalCount > 20)    │
└─────┴──────────────────────────────────────────┘
```

### Component tree

```
DashboardView.vue                            → views/DashboardView.vue
├── AppButton.vue         (upload link)      → components/ui/AppButton.vue
├── JobTable.vue                             → components/dashboard/JobTable.vue
│   ├── AppSpinner.vue    (v-if loading)     → components/ui/AppSpinner.vue
│   ├── AppAlert.vue      (v-if error)       → components/ui/AppAlert.vue
│   └── JobRow.vue        (v-for jobs)       → components/dashboard/JobRow.vue
│       ├── StatusBadge.vue                  → components/dashboard/StatusBadge.vue
│       │   └── AppBadge.vue                 → components/ui/AppBadge.vue
│       ├── AppSpinner.vue (v-if processing) → components/ui/AppSpinner.vue
│       └── InlineDeleteButton.vue           → components/ui/InlineDeleteButton.vue
└── AppPagination.vue     (v-if multi-page)  → components/ui/AppPagination.vue
```

**Layout:** AppLayout (sidebar + topbar visible)

**Key behavior:**
- Polls every 5s via `jobsStore.silentFetchJobs()` when any job is `created` or `processing`
- Clicking a `JobRow` navigates to `/jobs/:id/review` (if job is clickable/ready)
- Search input filters `jobsStore.filteredJobs` client-side by fileName or status

---

## 6. Screen: Upload (`/upload`)

### What you see

```
┌─────┬──────────────────────────────────────────┐
│     │  AppTopBar                               │
│  S  ├──────────────────────────────────────────┤
│  i  │                                          │
│  d  │  Upload Document                         │
│  e  │                                          │
│  b  │  [Tab: File] [Tab: Text]                 │
│  a  │  ┌──────────────────────────────────────┐│
│  r  │  │ FILE TAB:                            ││
│     │  │  ┌── FileDropZone ────────────────┐  ││
│     │  │  │   Drag & drop or click here    │  ││  ← v-if="!file"
│     │  │  │   to select a file             │  ││
│     │  │  └────────────────────────────────┘  ││
│     │  │  [AppAlert] (v-if validationError)   ││
│     │  │  ─── or after selection: ───         ││
│     │  │  Selected: myfile.pdf [✕ remove]     ││
│     │  │                                      ││
│     │  │ TEXT TAB:                             ││
│     │  │  Document name: [__________]         ││
│     │  │  Text: [textarea__________]          ││
│     │  ├──────────────────────────────────────┤│
│     │  │ [AppAlert] (v-if submitError)        ││
│     │  │ [AppProgress] (v-if submitting)      ││
│     │  │ [AppButton: Upload]                  ││
│     │  └──────────────────────────────────────┘│
└─────┴──────────────────────────────────────────┘
```

### Component tree

```
UploadView.vue                               → views/UploadView.vue
├── FileDropZone.vue     (v-if="!file")      → components/upload/FileDropZone.vue
├── AppAlert.vue         (validation/error)  → components/ui/AppAlert.vue
├── AppProgress.vue      (v-if submitting)   → components/ui/AppProgress.vue
└── AppButton.vue        (submit)            → components/ui/AppButton.vue
```

**Layout:** AppLayout (sidebar + topbar visible)

**Key behavior:**
- Two tabs: "file" (drag-drop) and "text" (paste)
- Text input is converted to a `.txt` File blob before submission
- Simulated progress bar (random increments every 200ms)
- Redirects to `/dashboard` on success

---

## 7. Screen: Review Workbench (`/jobs/:id/review`)

This is the most complex screen — the core of the app. It has **two layout modes** controlled by `reviewStore.viewMode`.

### Mode A: Pseudonymized View (3-column)

```
┌──────────────────────────────────────────────────────────────────────┐
│  WorkbenchHeader                                                     │
│  [← Back] [editable filename]  [WorkbenchModeToggle: ■Pseud │ Klar] │
│  Entity count | Status chip | [Rescan] [LLM Scan]                    │
├──────────────────────┬─────────────────────┬─────────────────────────┤
│  WorkbenchDocument   │ PseudonymizedText   │  WorkbenchRightRail     │
│  Viewer (col 1-4)    │ Panel (col 5-8)     │  (col 9-12)            │
│                      │                     │                         │
│  ┌─DocumentSearch──┐ │ ┌─toolbar────────┐  │  ┌─EntitiesTabContent─┐│
│  │ Bar (Ctrl+F)    │ │ │ [Copy/Complete]│  │  │                    ││
│  │ [search___] 1/5 │ │ └────────────────┘  │  │ ┌AddPiiByText────┐││
│  │ [AppDropdown]   │ │                     │  │ │Panel           │││
│  │ [Add All btn]   │ │ SegmentRenderer     │  │ │[search match]  │││
│  └─────────────────┘ │ (v-for segments)    │  │ │[AppDropdown]   │││
│                      │ in "pseudonymized"  │  │ │[AppButton:Add] │││
│  SegmentRenderer     │ display mode        │  │ └────────────────┘││
│  (v-for segments)    │   └EntityHighlight  │  │                    ││
│  in "pseudonymized"  │    (per entity)     │  │ ┌TokenMapping────┐││
│  display mode        │                     │  │ │Table           │││
│    └EntityHighlight  │                     │  │ │ TOKEN → ORIG   │││
│     (per entity,     │                     │  │ │ [edit] [delete]│││
│      colored marks)  │                     │  │ │ InlineDelete   │││
│                      │                     │  │ │ Button         │││
│                      │                     │  │ └────────────────┘││
│                      │                     │  └────────────────────┘│
├──────────────────────┴─────────────────────┴─────────────────────────┤
│  MODALS (conditional, floating above the workbench):                 │
│  ┌─ReviewerTrainingModal──┐                                         │
│  │ (if training needed)   │                                         │
│  │ AppModal (persistent)  │                                         │
│  │ AppProgress            │                                         │
│  │ <TrainingStep*> (×4)   │                                         │
│  │ AppButton (back/next)  │                                         │
│  ┌─CompleteReviewModal─┐  └────────────────────────┘                │
│  │ 3 checkboxes        │  ┌─LlmScanModal──────────┐                │
│  │ [Continue][End]     │  │ (configure→scan→result)│                │
│  └─────────────────────┘  │ AppProgress, AppAlert  │                │
│                           └────────────────────────┘                │
│  ┌─AppModal (inline)───┐                                            │
│  │ "LLM not configured"│                                            │
│  └─────────────────────┘                                            │
└──────────────────────────────────────────────────────────────────────┘
```

### Mode B: Depseudonymized / Klartext View (2-column)

```
┌──────────────────────────────────────────────────────────────────────┐
│  WorkbenchHeader                                                     │
│  [← Back] [editable filename]  [WorkbenchModeToggle: Pseud │ ■Klar] │
│  Entity count | Status chip | [Rescan] [LLM Scan]                    │
├──────────────────────────────────────────┬───────────────────────────┤
│  WorkbenchDocumentViewer (col 1-8)       │  WorkbenchRightRail       │
│                                          │  (col 9-12)              │
│  ┌─textarea─────────────────────────┐    │                           │
│  │ Paste AI output here...          │    │  (same as Mode A)        │
│  │ (depseudo input)                 │    │                           │
│  └──────────────────────────────────┘    │                           │
│                                          │                           │
│  ┌─output preview───────────────────┐    │                           │
│  │ Tokens are highlighted inline    │    │                           │
│  │ showing replacement →  original  │    │                           │
│  │ (depseudoOutputFragments)        │    │                           │
│  └──────────────────────────────────┘    │                           │
│                                          │                           │
│  (No SegmentRenderer in this mode)       │                           │
│  (No DocumentSearchBar in this mode)     │                           │
├──────────────────────────────────────────┴───────────────────────────┤
│  (Same modals as Mode A)                                             │
└──────────────────────────────────────────────────────────────────────┘
```

### Full component tree

```
ReviewView.vue                                   → views/ReviewView.vue
│
├── AppSpinner.vue            (v-if loading)      → components/ui/AppSpinner.vue
├── AppAlert.vue              (v-if error)        → components/ui/AppAlert.vue
│
├── WorkbenchHeader.vue                           → components/workbench/WorkbenchHeader.vue
│   ├── AppButton.vue         (back, scan btns)   → components/ui/AppButton.vue
│   └── WorkbenchModeToggle.vue                   → components/workbench/WorkbenchModeToggle.vue
│
├── WorkbenchDocumentViewer.vue                   → components/workbench/WorkbenchDocumentViewer.vue
│   │  (IF viewMode === 'pseudonymized'):
│   ├── DocumentSearchBar.vue                     → components/workbench/DocumentSearchBar.vue
│   │   ├── AppButton.vue                         → components/ui/AppButton.vue
│   │   └── AppDropdown.vue                       → components/ui/AppDropdown.vue
│   └── SegmentRenderer.vue   (v-for segments)    → components/review/SegmentRenderer.vue
│       └── EntityHighlight.vue (v-for entities)  → components/review/EntityHighlight.vue
│   │  (IF viewMode === 'depseudonymized'):
│   └── (textarea + output preview, no sub-components)
│
├── PseudonymizedTextPanel.vue                    → components/workbench/PseudonymizedTextPanel.vue
│   │  (ONLY in pseudonymized mode)
│   ├── AppButton.vue         (copy/complete)     → components/ui/AppButton.vue
│   └── SegmentRenderer.vue   (v-for segments)    → components/review/SegmentRenderer.vue
│       └── EntityHighlight.vue                   → components/review/EntityHighlight.vue
│
├── WorkbenchRightRail.vue                        → components/workbench/WorkbenchRightRail.vue
│   │  (header: "Zuordnung" + "Alle löschen" inline-confirm delete-all button)
│   └── EntitiesTabContent.vue                    → components/workbench/EntitiesTabContent.vue
│       ├── AddPiiByTextPanel.vue                 → components/workbench/AddPiiByTextPanel.vue
│       │   │  (two-column: Klartext search + Verfremdet replacement, auto-fill via fakerGenerator)
│       │   ├── AppButton.vue                     → components/ui/AppButton.vue
│       │   └── AppDropdown.vue                   → components/ui/AppDropdown.vue
│       └── TokenMappingTable.vue                 → components/workbench/TokenMappingTable.vue
│           └── InlineDeleteButton.vue            → components/ui/InlineDeleteButton.vue
│
├── ReviewerTrainingModal.vue (v-if needs train)  → components/review/ReviewerTrainingModal.vue
│   ├── AppModal.vue          (persistent)        → components/ui/AppModal.vue
│   ├── AppProgress.vue                           → components/ui/AppProgress.vue
│   ├── AppButton.vue         (back/next/done)    → components/ui/AppButton.vue
│   └── <component :is> (dynamic, one of):
│       ├── TrainingStepIntro.vue                 → components/review/training/TrainingStepIntro.vue
│       ├── TrainingStepPiiTypes.vue              → components/review/training/TrainingStepPiiTypes.vue
│       ├── TrainingStepManualAdd.vue             → components/review/training/TrainingStepManualAdd.vue
│       └── TrainingStepAutomationBias.vue        → components/review/training/TrainingStepAutomationBias.vue
│           └── AppAlert.vue  (warning)           → components/ui/AppAlert.vue
│
├── CompleteReviewModal.vue                       → components/review/CompleteReviewModal.vue
│   ├── AppModal.vue          (persistent)        → components/ui/AppModal.vue
│   └── AppButton.vue         (×2)                → components/ui/AppButton.vue
│
├── LlmScanModal.vue          (mode="llm")        → components/workbench/LlmScanModal.vue
│   ├── AppModal.vue                              → components/ui/AppModal.vue
│   ├── AppButton.vue                             → components/ui/AppButton.vue
│   ├── AppProgress.vue                           → components/ui/AppProgress.vue
│   └── AppAlert.vue                              → components/ui/AppAlert.vue
│
├── LlmScanModal.vue          (mode="rescan")     → (same component, second instance)
│
└── AppModal.vue (inline, "LLM not configured")   → components/ui/AppModal.vue
    └── AppButton.vue                              → components/ui/AppButton.vue
```

**Layout:** bare (no sidebar/topbar — full-screen workbench)

---

## 8. Screen: Playground (`/playground`)

The Playground is a **3-phase state machine** that reuses workbench components. No auth required.

### Phase 1: Upload

```
┌──────────────────────────────────────────┐
│                           [LanguageToggle]│
│                                          │
│           🧪 Playground                  │
│                                          │
│   [Tab: File] [Tab: Text]                │
│   ┌──────────────────────────────────┐   │
│   │ FileDropZone (or text input)     │   │
│   │ [AppAlert] (validation errors)   │   │
│   │ [AppButton: Try It]              │   │
│   └──────────────────────────────────┘   │
└──────────────────────────────────────────┘
```

### Phase 2: Processing

```
┌──────────────────────────────────────────┐
│                                          │
│        Processing your document...       │
│             [AppSpinner]                 │
│          [AppProgress ████░░░]           │
│          Elapsed: 12s                    │
│          [AppButton: Cancel]             │
│                                          │
└──────────────────────────────────────────┘
```

### Phase 3: Workbench (identical to ReviewView layout)

```
Same as ReviewView Mode A / Mode B above, except:
- WorkbenchHeader has playground=true and back-to="/playground"
- "Start Over" button replaces normal back navigation
- No ReviewerTrainingModal
```

### Component tree

```
PlaygroundView.vue                               → views/PlaygroundView.vue
│
│  PHASE: upload
├── LanguageToggle.vue                           → components/layout/LanguageToggle.vue
├── FileDropZone.vue      (v-if="!file")         → components/upload/FileDropZone.vue
├── AppAlert.vue          (validation/error)     → components/ui/AppAlert.vue
├── AppButton.vue         (submit/try it)        → components/ui/AppButton.vue
│
│  PHASE: processing
├── AppSpinner.vue                               → components/ui/AppSpinner.vue
├── AppProgress.vue                              → components/ui/AppProgress.vue
├── AppButton.vue         (cancel)               → components/ui/AppButton.vue
│
│  PHASE: workbench
├── WorkbenchHeader.vue   (playground=true)      → components/workbench/WorkbenchHeader.vue
│   ├── AppButton.vue                            → components/ui/AppButton.vue
│   └── WorkbenchModeToggle.vue                  → components/workbench/WorkbenchModeToggle.vue
├── WorkbenchDocumentViewer.vue                  → components/workbench/WorkbenchDocumentViewer.vue
│   ├── DocumentSearchBar.vue                    → components/workbench/DocumentSearchBar.vue
│   └── SegmentRenderer.vue                      → components/review/SegmentRenderer.vue
│       └── EntityHighlight.vue                  → components/review/EntityHighlight.vue
├── PseudonymizedTextPanel.vue (pseud. mode)     → components/workbench/PseudonymizedTextPanel.vue
│   ├── AppButton.vue                            → components/ui/AppButton.vue
│   └── SegmentRenderer.vue                      → components/review/SegmentRenderer.vue
│       └── EntityHighlight.vue                  → components/review/EntityHighlight.vue
├── WorkbenchRightRail.vue                       → components/workbench/WorkbenchRightRail.vue
│   └── EntitiesTabContent.vue                   → components/workbench/EntitiesTabContent.vue
│       ├── AddPiiByTextPanel.vue                → components/workbench/AddPiiByTextPanel.vue
│       └── TokenMappingTable.vue                → components/workbench/TokenMappingTable.vue
├── CompleteReviewModal.vue                      → components/review/CompleteReviewModal.vue
├── LlmScanModal.vue      (×2: llm + rescan)    → components/workbench/LlmScanModal.vue
└── AppModal.vue           (LLM not configured)  → components/ui/AppModal.vue
```

**Layout:** bare

---

## 9. Screen: 404 Not Found

```
┌──────────────────────────────────────────┐
│                                          │
│              404                         │
│         Page not found                   │
│                                          │
│         → Back to Dashboard              │
│                                          │
└──────────────────────────────────────────┘
```

### Component tree

```
NotFoundView.vue                             → views/NotFoundView.vue
└── (no child components — RouterLink + plain HTML only)
```

**Layout:** bare

---

## 10. Shared UI Component Reference

These are the reusable building blocks used across all screens. All live under `components/ui/`.

| Component | File | Props | Slots | External Lib | Used By |
|-----------|------|-------|-------|-------------|---------|
| **AppButton** | `ui/AppButton.vue` | `variant` (`primary`\|`secondary`\|`danger`\|`ghost`), `size` (`sm`\|`md`\|`lg`), `loading`, `disabled`, `type` | `default` | — | Everywhere |
| **AppModal** | `ui/AppModal.vue` | `open`, `title`, `persistent` (blocks close), `size` (`default`\|`wide`) | `trigger` (optional), `default` | Radix Vue Dialog | Modals across workbench |
| **AppAlert** | `ui/AppAlert.vue` | `variant` (`info`\|`warning`\|`error`\|`success`), `dismissible` | `default` | — | Error/warning displays |
| **AppBadge** | `ui/AppBadge.vue` | `variant` (`default`\|`success`\|`warning`\|`error`\|`info`), `colorClass` (overrides variant) | `default` | — | StatusBadge, UserMenu |
| **AppSpinner** | `ui/AppSpinner.vue` | `size` (`sm`\|`md`\|`lg`) | — | — | Loading states |
| **AppProgress** | `ui/AppProgress.vue` | `value` (0–100) | — | Radix Vue Progress | Upload, LLM scan, training |
| **AppDropdown** | `ui/AppDropdown.vue` | `modelValue`, `options` (`{value,label}[]`), `placeholder` | — | Radix Vue Select | Entity type selection |
| **AppPagination** | `ui/AppPagination.vue` | `page`, `totalPages`, `totalCount`, `pageSize` | — | — | Dashboard |
| **InlineDeleteButton** | `ui/InlineDeleteButton.vue` | — | — | — | TokenMappingTable, JobRow |

### Component quick-find: "I see X on screen, what component is it?"

| What you see | Component |
|---|---|
| Colored button (blue, grey, red, transparent) | `AppButton` |
| Popup dialog / overlay | `AppModal` (wraps Radix Vue Dialog) |
| Colored banner message (info/warning/error/success) | `AppAlert` |
| Small colored tag/chip (e.g., job status, user role) | `AppBadge` |
| Spinning circle animation | `AppSpinner` |
| Horizontal progress bar | `AppProgress` |
| Select dropdown (opens below, checkmark on selected) | `AppDropdown` |
| Page numbers with « » arrows | `AppPagination` |
| Trash icon → confirm/cancel pair | `InlineDeleteButton` |

---

## 11. Stores (State Management)

All stores live under `stores/` and use Pinia.

| Store | File | Purpose | Key State |
|-------|------|---------|-----------|
| **useAuthStore** | `stores/auth.ts` | Authentication | `accessToken`, `email`, `name`, `role`, `isAuthenticated` |
| **useReviewStore** | `stores/review.ts` | Review workbench state | `segments`, `entities`, `activeEntityId`, `viewMode` (`'pseudonymized'`\|`'depseudonymized'`), `depseudoInputText` |
| **useJobsStore** | `stores/jobs.ts` | Job list & CRUD | `jobs`, `currentJob`, `page`, `searchQuery`, `filteredJobs` |
| **useUiStore** | `stores/ui.ts` | UI state | `sidebarCollapsed`, `sidebarMobileOpen` |

---

## 12. Composables

All live under `composables/`.

| Composable | File | Used By | Purpose |
|------------|------|---------|---------|
| `useTextSelection` | `composables/useTextSelection.ts` | ReviewView, PlaygroundView | Tracks mouse text selection on document for adding PII |
| `useDocumentSearch` | `composables/useDocumentSearch.ts` | ReviewView, PlaygroundView | Ctrl+F search across segments; match navigation |
| `useReviewNavigation` | `composables/useReviewNavigation.ts` | ReviewView | Keyboard nav between entities (↑/↓ arrows) |
| `useReviewerTraining` | `composables/useReviewerTraining.ts` | ReviewView | Multi-step training flow state machine |
| `useFileUpload` | `composables/useFileUpload.ts` | UploadView, PlaygroundView | File selection and validation |
| `useEstimatedProgress` | `composables/useEstimatedProgress.ts` | PlaygroundView | Animated progress bar during processing |
| `useClipboardCopy` | `composables/useClipboardCopy.ts` | PseudonymizedTextPanel, MappingTabContent | Copy-to-clipboard with "copied!" flash |
| `useScrollSync` | `composables/useScrollSync.ts` | ReviewView, PlaygroundView | Proportional scroll sync between original and pseudonymized text panels |
| `useDarkMode` | `composables/useDarkMode.ts` | AppTopBar | Dark/light mode toggle |

### Utilities

| Utility | File | Used By | Purpose |
|---------|------|---------|---------|
| `generateFakeValue` | `utils/fakerGenerator.ts` | AddPiiByTextPanel | Client-side faker (`@faker-js/faker/locale/de`) for auto-generating pseudonymized replacement values by entity type |

---

## 13. Complete File Index

### Views (7 files)
```
views/LoginView.vue
views/RegisterView.vue
views/DashboardView.vue
views/UploadView.vue
views/ReviewView.vue
views/PlaygroundView.vue
views/NotFoundView.vue
```

### Layout Components (6 files)
```
components/layout/AppLayout.vue
components/layout/AppSidebar.vue
components/layout/AppTopBar.vue
components/layout/SidebarNavItem.vue
components/layout/LanguageToggle.vue
components/layout/UserMenu.vue
```

### UI Components (9 files)
```
components/ui/AppButton.vue
components/ui/AppModal.vue
components/ui/AppAlert.vue
components/ui/AppBadge.vue
components/ui/AppSpinner.vue
components/ui/AppProgress.vue
components/ui/AppDropdown.vue
components/ui/AppPagination.vue
components/ui/InlineDeleteButton.vue
```

### Dashboard Components (3 files)
```
components/dashboard/JobTable.vue
components/dashboard/JobRow.vue
components/dashboard/StatusBadge.vue
```

### Upload Components (1 file)
```
components/upload/FileDropZone.vue
```

### Workbench Components (13 files)
```
components/workbench/WorkbenchHeader.vue
components/workbench/WorkbenchModeToggle.vue
components/workbench/WorkbenchDocumentViewer.vue
components/workbench/PseudonymizedTextPanel.vue
components/workbench/WorkbenchRightRail.vue
components/workbench/EntitiesTabContent.vue
components/workbench/TokenMappingTable.vue
components/workbench/AddPiiByTextPanel.vue
components/workbench/DocumentSearchBar.vue
components/workbench/LlmScanModal.vue
components/workbench/ExportTabContent.vue        ← exists but not currently imported
components/workbench/RulesTabContent.vue          ← exists but not currently imported
components/workbench/MappingTabContent.vue        ← exists but not currently imported
```

### Review Components (4 files)
```
components/review/SegmentRenderer.vue
components/review/EntityHighlight.vue
components/review/ReviewerTrainingModal.vue
components/review/CompleteReviewModal.vue
```

### Training Sub-Components (4 files)
```
components/review/training/TrainingStepIntro.vue
components/review/training/TrainingStepPiiTypes.vue
components/review/training/TrainingStepManualAdd.vue
components/review/training/TrainingStepAutomationBias.vue
```

### Other
```
App.vue                  ← root component
router/index.ts          ← route definitions + navigation guards
```

**Total: 48 `.vue` files** (1 root + 7 views + 40 components)

---

## Notes

- **3 workbench components exist but are not actively imported:** `ExportTabContent.vue`, `RulesTabContent.vue`, and `MappingTabContent.vue`. They may be from a previous iteration or reserved for future use.
- **Radix Vue** is used for: `AppModal` (Dialog), `AppProgress` (Progress), `AppDropdown` (Select), `UserMenu` (DropdownMenu).
- **`@vueuse/core`** is used by `FileDropZone` (`useDropZone`).
- **i18n** uses two locales (`de`, `en`) with 7 namespace files each: `common`, `auth`, `upload`, `dashboard`, `job`, `review`, `playground`. Files are at `i18n/de/*.json` and `i18n/en/*.json`.
