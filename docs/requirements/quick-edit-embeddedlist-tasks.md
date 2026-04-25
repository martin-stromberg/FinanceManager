# Task Board / Issue-Liste — QuickEdit EmbeddedList

Date: 2026-04-01
Scope: Schnellbearbeitungsmodus für EmbeddedList (StatementDraft entries)

Format: Jede Zeile ist ein eigenständiges Issue / Task mit ID, Titel, Kurzbeschreibung, Priorität, Aufwand, Abhängigkeiten und Akzeptanzkriterien.

| ID | Titel | Kurzbeschreibung | Priorität | Aufwand | Abhängigkeiten | Branch (Vorschlag) | Status |
|----|-------|------------------:|-----------|--------:|----------------|--------------------|--------|
| T1 | API‑Spec: Batch Update Endpoint | Spezifikation `POST /api/statement-drafts/{id}/entries/batch-update` inkl. Request/Response DTOs, Fehlerformat, Berechtigungen, Transaktion | MUST HAVE | M | none | `feature/quickedit/api-spec` | ToDo |
| T2 | BaseListViewModel Erweiterung | Implementiere virtuelle APIs: `IReadOnlyList<string> EditableFields`, `bool IsRowEditable(object)`, `Task BeginQuickEditAsync()`, `Task EndQuickEditAsync()`, `IReadOnlyDictionary<Guid, IDictionary<string, object?>> CollectChangedRows()`, `IEnumerable<(string Field,string Message)> ValidateRow(object)` + XML Doc & Default-Impl | MUST HAVE | M | T1 | `feature/quickedit/base-listvm` | ToDo |
| T3 | Entries VM: StatementDraftEntriesListViewModel | Implementiere `EditableFields`, `IsRowEditable`, row‑change tracking (original vs edited), client-side row validation hooks, Reset per row | MUST HAVE | M | T2 | `feature/quickedit/entries-vm` | ToDo |
| T4 | UI: Editable Table & Cells (Blazor) | Komponenten: `QuickEditTable`, `EditableListCell` (text/date/currency/lookup), per-row Reset button, inline error display, keyboard navigation | MUST HAVE | L | T3 | `feature/quickedit/ui` | ToDo |
| T5 | Ribbon Actions | Ribbon Toggle `QuickEdit` (on/off), `SaveQuickEdit`, `CancelQuickEdit`; wire to VM methods and enable/disable state | HIGH | S | T4 | `feature/quickedit/ribbon` | ToDo |
| T6 | Backend: Implement Batch Update | Controller + Service + Validation + DB Transaction + logging; return per-row errors on validation failure; 403 on permission failure | MUST HAVE | M | T1 | `feature/quickedit/backend` | ToDo |
| T7 | Integration: Frontend ↔ Backend | API client DTOs, call Batch endpoint, handle per-row errors, update UI on success, optimistic UI refresh | MUST HAVE | M | T4,T6 | `feature/quickedit/integration` | ToDo |
| T8 | Tests: Unit / Integration / E2E | Unit tests for BaseListViewModel & Entries VM; integration tests for API (InMemory DB); E2E test for full flow (Toggle → edit → save → error handling) | MUST HAVE | M | T2,T3,T6,T7 | `feature/quickedit/tests` | ToDo |
| T9 | Accessibility & Keyboard | Accessibility audit; ensure ARIA, focus handling, keyboard navigation, screenreader labels | HIGH | S | T4 | `feature/quickedit/a11y` | ToDo |
| T10 | Perf & UX polish | Client responsiveness checks, spinner/disabled states, large-draft behavior (1000 entries) — include pagination fallback | MEDIUM | S | T4 | `feature/quickedit/perf` | ToDo |
| T11 | Docs & Release Notes | Update `docs/requirements/quick-edit-embeddedlist.md`, `docs/Anforderungsstatus.md`, add developer how-to and API spec | LOW | S | All | `chore/docs/quickedit` | ToDo |


## Acceptance Criteria (per epic)
- API Spec (T1) must include sample request/response JSON and per-row error format; security checks documented.
- BaseListViewModel (T2) compiles, has XML docs, unit tests for defaults; no breaking changes for existing lists.
- Entries VM (T3) correctly reports editable fields and `IsRowEditable` returns false for `AlreadyBooked` entries; track changes per row and allow reset.
- UI (T4) renders table in QuickEdit mode, supports inline validation and Reset per row; Save disabled while client validation fails.
- Backend (T6) validates all rows; if any row invalid: return 400 with per-row errors and do NOT commit any changes; on success commit transactionally and return updated draft snapshot.
- Tests (T8): Unit tests cover validation and change tracking; integration tests demonstrate atomic commit and error propagation; E2E demonstrates user flow.

## Suggested Issue Templates (copy into GitHub issues)

Title: `feat(quickedit): <short description>`
Labels: `feature`, `quick-edit`, `frontend`/`backend`/`docs`/`tests`
Assignee: TBD

Body template:
- Summary
- Related Requirements: `FR-1, FR-2, FR-6` (as applicable)
- Acceptance Criteria (copy from board)
- Implementation notes / links to docs

## Next immediate actions (recommended order)
1. Create API Spec (T1) and store under `docs/requirements/quick-edit-embeddedlist-api.md`.
2. Implement `BaseListViewModel` virtual members (T2) with minimal unit tests.
3. Implement `StatementDraftEntriesListViewModel` changes & change-tracking (T3).
4. Build UI prototype for QuickEdit Table (T4) and wire Toggle (T5).
5. Implement backend batch endpoint (T6) and run integration tests (T8).


---
File created by automation. Assign owners and create GitHub issues from the above tasks as desired.