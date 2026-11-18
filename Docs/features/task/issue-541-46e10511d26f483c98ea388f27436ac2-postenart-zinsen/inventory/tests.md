# Tests — Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-18, ca. 16:31 Uhr MEZ (UTC+1, „W. Europe Standard Time")
- **Branch und Commit-ID:** `task/issue-541-46e10511d26f483c98ea388f27436ac2-postenart-zinsen`, Commit `fcd9f2eab22de6f6e75677909be893c232650f27` („test: CancellationToken in SaveChangesAsync-Aufrufen und eindeutigen cref für GetOpenDraftsAsync (Issue #541)")
- **Uncommittete Änderungen im getesteten Stand:** Nur ungetrackte Dateien unter `Docs/features/task/issue-541-.../` (Anforderungs- und Inventory-Dokumentation). **Keine uncommitteten Code-Änderungen** — der getestete Stand ist inhaltsgleich mit Commit `fcd9f2ea`.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash, `dotnet --version` = **10.0.403** (.NET 10). Testprojekte: xUnit v3 + Microsoft.Testing.Platform (Aufruf über `dotnet test <csproj> -- <mtp-args>`).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Laut CI-Workflow `.github/workflows/pr-staging-ci.yml` (und baugleich `staging-ci.yml`) drei Suiten:
  1. `FinanceManager.Tests` (Unit) — `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -- --filter-not-trait "Category=OsInterface"` (CI zusätzlich mit Coverage)
  2. `FinanceManager.Tests.Integration` (API-/Host-Integration)
  3. `FinanceManager.Tests.E2E` (Playwright; Step läuft mit `continue-on-error: true`, Traces via `PLAYWRIGHT_TRACE`/`PLAYWRIGHT_ARTIFACTS`)
  Dazu Gate „static checks" (Format, Security-Scan, TreatWarningsAsErrors-Build) und Coverage-Gate ≥ 70 %.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| CI – Unit (Job „build & test", Step „Test regular tests") | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --coverage ...` | GitHub Actions `windows-latest`, Run 44304457333 | 0 (Step success) | 2382 | 0 | 0 | [gh run view 44304457333](https://github.com/martin-stromberg/FinanceManager/actions/runs/44304457333), Job-Log ~12:39 UTC |
| CI – Integration (Step „Test regular integration") | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug -- --report-xunit-trx --coverage ...` | dto. | 0 (Step success) | 176 | 0 | 0 | dto., Job-Log ~12:50 UTC |
| CI – E2E (Step „Test E2E tests") | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug -- --report-xunit-trx` | dto. | **2** (Testlauf), Step-Ergebnis „success" wegen `continue-on-error: true` | 169 | **1** | 0 | dto., Job-Log ~13:16 UTC; siehe „Nachgewiesene bestehende Testfehler" |
| CI – Coverage-Gate | ReportGenerator + Threshold-Skript | dto. | 0 | Line coverage **72,3 %** (≥ 70 %) | — | — | dto., Job-Log ~13:16 UTC |
| CI – Statische Prüfungen (Job „static checks") | `dotnet format --verify-no-changes --severity error`; Security-Scan; `dotnet build -p:TreatWarningsAsErrors=true` | dto. | 0 (Job success) | — | — | — | dto. |
| Lokal – Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug --results-directory Docs/features/task/issue-541-.../inventory/test-results -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unit-baseline.trx` | Repo-Root | 0 | 2382 | 0 | 0 | [dotnet-test-unit.log](test-results/dotnet-test-unit.log), [test-results-unit-baseline.trx](test-results/test-results-unit-baseline.trx) |

**CI-Lauf 44304457333** (Workflow „PR CI for Staging", Event `pull_request`, Branch = Feature-Branch, `headSha` = `fcd9f2ea` = lokaler HEAD, Attempt 1): Jobs `detect-backmerge`, `build & test`, `static checks`, `no pending backmerge` = **success**; `back-merge-skip` = skipped. Nachweis lokal eingeholt via `gh run view 44304457333 --repo martin-stromberg/FinanceManager` und `gh api .../actions/jobs/134802461586`.

**Begründung für fehlenden lokalen E2E-/Integrationslauf:** Der komplette E2E-Lauf dauert auf CI ~27 Minuten und erfordert Playwright-Browser plus laufende App-Instanz. Der dokumentierte CI-Ausgangslauf bezieht sich **nachweislich auf denselben Commit** (`headSha` `fcd9f2ea` = lokaler `HEAD`); der lokale Stand unterscheidet sich ausschließlich durch ungetrackte Dokumentationsdateien (keine Code-Änderungen). Damit ist der CI-Lauf als Ausgangsnachweis für alle drei Suiten fachlich gleichwertig; der lokale Verifikationslauf der Unit-Suite bestätigt die Reproduzierbarkeit (identische 2382 Tests, alle bestanden). Ein „preexisting"-Verweis bei späteren Vergleichen muss für Integration/E2E auf den CI-Lauf zeigen.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|-------------------|----------------------------|-------------------|
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | `FinanceManager.Tests.E2E/Tests/Auth/ChangePasswordWellKnownPlaywrightTests.cs` (Z. 104) | `System.TimeoutException : Timeout 15000ms exceeded` — Warten auf `Locator("button.setup-section-toggle").First` sichtbar; E2E-Run: total 170, failed 1, succeeded 169, Dauer 26m 37s | CI-Run 44304457333, Job „build & test" (~12:59 UTC); Step `continue-on-error: true` → Job/Run dennoch „success". **Fachlich nicht verwandt** (Auth/Passwort-Änderung), aber ein dokumentierter Preexisting-Failure des Ausgangsstands |

Unit- und Integrationssuite fehlerfrei; keine weiteren nachgewiesenen Testfehler im Ausgangslauf. Übersprungene Tests: 0 (lokaler Lauf und alle CI-Teilläufe).

### Testlücken und Ausführungsprobleme

- **E2E-Toleranz beachten:** Der E2E-Step steht auf `continue-on-error: true`; ein „grüner" Job heißt dort nicht zwingend 0 Fehlschläge — für Vergleiche immer die TRX/den Test-Run-Summary-Abschnitt heranziehen.
- **Lokal nicht ausgeführt:** `FinanceManager.Tests.Integration` (~10,5 min auf CI) und `FinanceManager.Tests.E2E` (~27 min, laufende App + Browser nötig) wurden lokal nicht gestartet — Ausgangsnachweis via CI-Lauf auf identischem Commit (Begründung oben). Keine Aussage über lokale Ausführbarkeit dieser Suiten.
- **Trait-Filter:** Der projektübliche Filter `--filter-not-trait "Category=OsInterface"` schließt ggf. `OsInterface`-getaggte Unit-Tests aus (Anzahl nicht ermittelt/unbekannt) — identisch zur CI-Referenz.

## Testklassen

### `AccountDetailViewModelTests` — `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs`

ViewModel-Tests für `BankAccountCardViewModel` mit gemocktem `IApiClient` und `DummyLocalizer` (liefert Key-Namen als Label).

- `Initialize_LoadsAccount_and_builds_card` — Laden bestehender Konten + Kartenaufbau
- `Initialize_NewAccount_ProducesEmptyCard` — Neuanlage ohne API-Call
- `Save_NewAccount_CreatesAndReturnsId` / `Save_Update_CallsUpdateAndKeepsId` — Create-/Update-Pfad via privatem `SavePendingAsync` (Reflection)
- `DeleteAsync_Success_ReturnsTrue` — Löschfluss
- `GetRibbon_ContainsExpectedGroups` — Ribbon enthält `Ribbon_Group_Navigation`, `Ribbon_Group_Manage`, `Ribbon_Group_Linked` (assertiert **nicht** die Buchungen-Gruppe explizit, aber die Tab-Liste)
- `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` — **assertiert exakte Gruppenreihenfolge** `[Navigation, Manage, Bookings, Linked]`; ein neuer Ribbon-Button in der Buchungen-Gruppe ändert die Reihenfolge nicht (Test bleibt grün), ein Test auf den neuen Button existiert noch nicht

### `AccountServiceTests` — `FinanceManager.Tests/Accounts/AccountServiceTests.cs`

Posting-Rule-Service-Tests (Owner-Isolierung, OrderIndex, Validierung):

- `GetPostingRulesAsync_ShouldReturnNull_WhenAccountBelongsToOtherUser`
- `UpsertPostingRuleAsync_ShouldCreateRules_WithSequentialOrderIndex`
- `UpsertPostingRuleAsync_ShouldFail_WhenAccountBelongsToOtherUser`
- `UpsertPostingRuleAsync_ShouldFail_WhenPostingTypeUnspecified`
- `UpsertPostingRuleAsync_ShouldFail_WhenPatternEmpty`
- `UpsertPostingRuleAsync_ShouldUpdateExistingRule`
- `UpsertPostingRuleAsync_ShouldFail_WhenRuleBelongsToOtherAccount`
- `RemovePostingRuleAsync_ShouldRemoveRule_AndHonorOwnership`
- `UpsertPostingRuleAsync_ShouldUseExplicitOrderIndex_IncludingZero`
- `UpsertPostingRuleAsync_ShouldKeepOrderIndex_WhenNullOnUpdate`

### `ApiClientAccountPostingRulesTests` — `FinanceManager.Tests.Integration/ApiClient/ApiClientAccountPostingRulesTests.cs`

REST-Integrations-Tests (`TestWebApplicationFactory`):

- `PostingRules_Create_Update_List_Remove` — CRUD-Lebenszyklus über API
- `PostingRules_Upsert_RejectsInvalidPayloads` — leeres Muster + `Unspecified` abgelehnt
- `PostingRules_ForeignAccount_IsRejected` — Owner-Isolierung
- `PostingTypes_Reclassify_EnqueuesBackgroundTask` — Re-Evaluation-Endpoint

### `AccountPostingRulesPlaywrightTests` — `FinanceManager.Tests.E2E/Tests/Accounts/AccountPostingRulesPlaywrightTests.cs` ⚠ anforderungsrelevant

E2E-Tests (Playwright, `[Collection(PlaywrightCollection.CollectionName)]`, Fixture `PlaywrightWebAppFixture`):

- `ManagePostingRules_ViaUi` — Regel über Panel anlegen/entfernen; **Selektoren: `.posting-rules-panel`, `input[type='text'].card-input`, `select.card-input` (`.Nth(1)`), `input[type='number'].card-input`, `button.btn-primary`, `button.btn-danger`, page-scope `td`-Filter**
- `ManagePostingRules_ValidationError_ViaUi` — leeres Muster → Fehlerzeile im Panel; `option[value='0']` nicht vorhanden; **Selektor `button.btn-primary`**
- `ClassifiedEntry_CardShowsRuleBasedPostingType` — Regel-basierte Klassifikation (API-seitig, kein Panel-UI)
- `ManualPostingTypeChange_PersistsAfterReclassify` — `IsPostingTypeManual`-Schutz
- `BookDraft_TransfersPostingTypeToPosting` — Übertrag in Buchung + Listenspalte
- `ReevaluatePostingTypes_ViaRibbon` — Ribbon-Button `button#ReevaluatePostingTypes` klicken → Hintergrundtask
- `EntryWithoutRules_ShowsSignBasedPostingType` — vorzeichenbasierte Fallback-Klassifikation

**Wichtig:** `ManagePostingRules_ViaUi` und `ManagePostingRules_ValidationError_ViaUi` bedienen das Panel **direkt über die Button-Klassen `btn-primary`/`btn-danger`** und erwarten die sofortige Sichtbarkeit von `.posting-rules-panel` nach Laden der Karte. Bei Umstellung auf Symbolbuttons (Klassen ändern sich) und Overlay-Öffnung (Panel erst nach Ribbon-Klick sichtbar) schlagen beide Tests in ihrer jetzigen Form fehl und müssen angepasst werden.

### Weitere berührte Testklassen (Kontext)

- `PostingTypeEvaluatorTests` (`FinanceManager.Tests/Postings`) — Domänen-Auswerter
- `StatementDraftClassificationTests`, `StatementDraftServiceTests` (`FinanceManager.Tests/Statements`) — Klassifikation nutzt Regeln
- `EnumCoverageTests` (`FinanceManager.Tests/Common`) — `BankPostingType_Values`, `PostingRuleField_Values`
- `BackupRestoreCoverageTests` (`FinanceManager.Tests/Infrastructure`) — Backup-Abdeckung von `AccountPostingRule`
- `PostingTypeReevaluationTaskExecutorTests` (`FinanceManager.Tests/Web`) — Hintergrundtask
- `PostingTypeMigrationBackfillTests` (`FinanceManager.Tests/Infrastructure`) — Datenmigration

## Hilfsmethoden

### `AccountDetailViewModelTests` (interne Helfer)

- `CreateVm()` — `ServiceCollection` + `Mock<IApiClient>` + `TestCurrentUserService` + `DummyLocalizerGeneric<Pages>`; liefert `(vm, apiMock)`
- `DummyLocalizer` / `DummyLocalizerGeneric<T>` — Localizer-Stubs, liefern Key-Namen als `LocalizedString` zurück

### `StubAccountService` — `FinanceManager.Tests/TestHelpers/StubAccountService.cs`

- Vollständige `IAccountService`-Implementierung, jede Methode → `NotSupportedException` (inkl. `GetPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync`)

### E2E-Helper (`FinanceManager.Tests.E2E/Helpers/`)

- `PlaywrightWebAppFixture` — Browser + Testserver + `DatabasePath` (SQLite)
- `AuthGateway` — `LoginAsync(username, password)`
- `TestUserSeeder` — `EnsureUserAsync` legt Testbenutzer in der SQLite-DB an
- `AccountsApiSeedHelper` — `CreateAccountAsync(name, iban)` über API im Browser-Kontext
- `BrowserApiHelper` — `GetJsonAsync`, `PostJsonAsync`, `PostNoContentAsync`, `PostWithStatusAsync` (API-Calls aus der Browser-Session)
- In `AccountPostingRulesPlaywrightTests`: private Helfer `SeedDraftWithEntryAsync`, `WaitForEntryPostingTypeAsync`, `WaitForPostingTypeAsync`, `CreateDbContext`
