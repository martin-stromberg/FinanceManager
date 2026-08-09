# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added

- **Posting Reversal (Stornierung):** Erroneous postings can now be cancelled (reversed) via the API and UI (feature branch `140-buchung-rückgängig-machen`).
  - New endpoint `POST /api/postings/{id}/reverse` — creates a counter-posting with negated amount, reversing the original posting (and all postings in the same booking group). Returns a `ReversalResultDto` with the IDs of reversed and newly created postings plus the reconciliation import ID.
  - New endpoint `GET /api/postings/{id}/validate-reversal` — validates whether a posting can be reversed without performing the operation. Returns a `ReversalValidationDto` (`isValid`, `errors[]`). Use this to pre-validate before showing a confirmation UI.
  - New database columns on the Posting entity:
    - `ReversedByPostingId` (`Guid?`) — ID of the counter-posting that reversed this posting.
    - `ReversalForPostingId` (`Guid?`) — ID of the original posting this posting reverses.
    - `ReversedByUserId` (`string?`) — User ID of the user who performed the reversal.
    - `ReversedAtUtc` (`DateTime?`) — UTC timestamp of the reversal.
  - Computed properties on the Posting entity:
    - `IsReversed` (`bool`) — `true` when `ReversedByPostingId` is set.
    - `IsReversal` (`bool`) — `true` when `ReversalForPostingId` is set.
  - `PostingServiceDto` extended with `IsReversed`, `IsReversal`, `ReversedByPostingId`, `ReversalForPostingId` fields.
  - Action button "Stornieren" (Cancel/Reverse) added to posting detail pages in the web UI.
  - "Storno" indicator column added to posting list views.

- **Self-update extracted into `SoftwareSchmiede.AutoUpdate` library:** The self-update system previously built into `FinanceManager.Web` has been extracted into a standalone, hosting-independent NuGet-ready library (feature branch `230-programmupdate-als-komponenten`).
  - New project `SoftwareSchmiede.AutoUpdate` — activated via a single `builder.UseAutoUpdate(cfg => ...)` call on any `IHostApplicationBuilder` (web, worker or console host).
  - Fluent configuration via `AutoUpdateBuilder`: `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `UseSource`/`UseGithubSource`/`UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices`.
  - Pluggable update sources via `IAutoUpdateSource`: built-in `AutoUpdateGithubSource` (GitHub Releases) and `AutoUpdateLocalFolderSource` (local directory, the new default).
  - Cancellable lifecycle events via `IAutoUpdateEventAggregator`: `BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript`, `AfterStartUpdateScript`, `ErrorOccurred`.
  - Thread-safe status tracking via `AutoUpdateStatusService`/`IAutoUpdateStatusProvider`, persisted across restarts, and manual control via `AutoUpdateCommandService`/`IAutoUpdateCommandHandler`.
  - Background services `AutoUpdateCheckerService` (periodic source check, honoring configured time windows) and `AutoUpdateSchedulerService` (scheduled installation).
  - New project `SoftwareSchmiede.AutoUpdate.Tests` with unit test coverage for the library.
  - `FinanceManager.Web` now consumes the library through a new `UpdateOrchestratorAdapter`; the public REST API (`/api/setup/update/*`), `ApiClient`, `SetupUpdateViewModel` and `SetupUpdateTab.razor` are unchanged.
  - New `Updates` configuration entries: `SourceType` (`Github`/`LocalFolder`), `LocalFolderPath`, `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `SourceCheck:Interval`/`SourceCheck:TimeRanges`, `StopHostAfterScriptStart`. Existing installations keep their previous behavior unchanged.
  - macOS is not supported (unchanged from before); documented as a known limitation of the library.

### Changed

- **Budgetbericht objektorientiert neu strukturiert:** Die Berechnungslogik des Budgetberichts wurde von einer prozeduralen `BudgetReportService`-Implementierung auf ein objektorientiertes Domänenmodell umgestellt (`Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationPosting` in `FinanceManager.Domain.Budget.ReportCalculation`); `BudgetReportService` bleibt als schlanker Adapter bestehen.
  - Neue Zeilenreihenfolge im Detailbericht: Kategorie-/Zweckzeilen → Zwischensumme → Unbudgetiert → Zwischensumme → Kostenneutral → Endsumme.
  - Buchungen mit `GroupId` (Self-Kontakt, kostenneutrale Transfers) werden jetzt als eigene Zeilenart „Kostenneutral" ausgewiesen statt unter „Unbudgetiert".
  - Neue virtuelle Kategorie „Ohne Kategorie" für Budgetzwecke ohne zugeordnete Kategorie; wird ausgeblendet, wenn sie die einzige Kategoriezeile ist.
  - Mehrere Gesamtbudget-Erwartungen am selben Zweck/derselben Kategorie werden sequenziell nach `BudgetRule.StartDate` (aufsteigend), dann Erstellungsreihenfolge befüllt.
  - Neue DTOs: `BudgetReportEntry` (Detailzeile, `BudgetReportEntryRowKind`), `BudgetReportCumulativeEntry` (Intervall-Zusammenfassung), `MonthlyBudgetRealization` (Eingabe-DTO für Buchungsposten).
- **Self-update library moved to external release artifact:** `FinanceManager.Web` now references the vendored `msTools.Updater` `v0.2.0` release under `external/msTools.Updater/v0.2.0/` instead of the local `SoftwareSchmiede.AutoUpdate` project. The ZIP is kept with a SHA256 checksum and the extracted `msTools.Updater.dll` is copied locally for build and publish.

### Removed

- Removed the local `SoftwareSchmiede.AutoUpdate` and `SoftwareSchmiede.AutoUpdate.Tests` projects from the solution. Updater library tests belong to the external updater repository; this repository keeps only the FinanceManager integration tests.
- Removed obsolete `msTools.Updater` `v0.2.0` (under `external/msTools.Updater/v0.2.0/`) after successful migration to `v0.3.0`, which is now the only vendored version and referenced by `FinanceManager.Web.csproj`.

---

### Known Issues

- **Bug:** `GetRelatedPostingsAsync` with `GroupId == Guid.Empty` returns all ungrouped postings instead of an empty result. Integration test `L21` is skipped as a workaround. This is a pre-existing issue unrelated to the reversal feature.
- **UX:** No confirmation dialog is shown before executing a reversal. The action is irreversible and currently triggers immediately on button click.
- **Tests:** 13 pre-existing test failures exist in the test suite. These failures are not caused by the reversal feature and were present before this branch was created.
- **Tests:** 3 `UpdateSetupPlaywrightTests` E2E tests fail (Admin/Update-Setup UI, timeouts and a strict-mode selector match). Pre-existing, unrelated to the Budgetbericht refactor.
- **Bug:** `BudgetReportEntry.BudgetedAmount` is a net amount per purpose; purposes with both an income and an expense rule active in the same month have the smaller side netted away, skewing `PlannedIncome`/`PlannedExpenseAbs`/`ExpectedExpenseAbs`. Pre-existing, out of scope for the Budgetbericht restructuring — tracked as follow-up.

---

[Unreleased]: https://github.com/Muesli84/FinanceManager/compare/main...140-buchung-rückgängig-machen
