# Tests – Bestand und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-09, Testläufe zwischen ca. 17:57 und 18:23 Uhr +0100 (CET)
- Branch und Commit-ID: `task/issue-498-dd574583f8fe40a79651dc73562ca20f-ribbon-gruppierung-verknuepfte`, HEAD `caca94da0ae51a44816db6cc6d9cd226c1fed360` („feat: Posting-Referenzinvariante erzwingen, globale Pools explizit dokumentieren (#497) (#501)")
- Uncommittete Änderungen im getesteten Stand: keine geänderten getrackten Dateien; einzig untracked ist der Lifecycle-Arbeitsordner `Docs/features/task/issue-498-.../` (requirement.md, inventory-Artefakte). Produktiv- und Testcode unverändert.
- Testumgebung und Runtime-/SDK-Versionen: Windows, `dotnet --version` = 10.0.403; Testrunner Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`), xUnit v3, bUnit; Playwright-Browser lokal installiert (`%LOCALAPPDATA%\ms-playwright`, u. a. chromium-1169…1248).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/staging-ci.yml` (Job `build-and-test`, Zeilen 126–150) definiert drei Suiten: `FinanceManager.Tests` (Unit, mit Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E` (Playwright). `FinanceManager.Tests.Integration.ApiClient` ist kein eigenes Projekt, sondern ein Unterordner von `FinanceManager.Tests.Integration`. Der lokale E2E-Lauf wurde wie in der CI ohne Trace-/Artifacts-Env-Variablen gestartet; in der CI ist der E2E-Schritt `continue-on-error: true`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build Unit | `dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug` | Repo-Root | 0 | — | — | — | Konsolenausgabe: 64 Warnungen (xUnit-Analyzer), 0 Fehler |
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <…>/inventory/test-results -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename baseline-unit-tests.trx` | Repo-Root | 0 | 2148 | 0 | 0 | [baseline-unit-tests.trx](test-results/baseline-unit-tests.trx) |
| Build Integration | `dotnet build FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug` | Repo-Root | 0 | — | — | — | Konsolenausgabe: 11 Warnungen, 0 Fehler (erster Build-Versuch während laufendem Unit-Lauf schlug mit MSB3021-Dateisperre fehl; nach Abschluss des Unit-Laufs erfolgreich) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <…>/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename baseline-integration-tests.trx` | Repo-Root | 0 | 165 | 0 | 0 | [baseline-integration-tests.trx](test-results/baseline-integration-tests.trx) |
| Build E2E | `dotnet build FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug` | Repo-Root | 0 | — | — | — | Konsolenausgabe: 10 Warnungen, 0 Fehler |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <…>/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename baseline-e2e-tests.trx` | Repo-Root | 2 | 131 | 2 | 0 | [baseline-e2e-tests.trx](test-results/baseline-e2e-tests.trx) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | `FinanceManager.Tests.E2E/Tests/Auth/ChangePasswordWellKnownPlaywrightTests.cs` (Zeile 108–110, via `Helpers/AuthGateway.cs:138` `LogoutAsync`) | `Microsoft.Playwright.PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:57880/login` — Navigation zur Login-Seite beim Logout abgebrochen (Timeout-/Umgebungsproblem, keine Beziehung zur Ribbon-Reihenfolge). | E2E-Lauf, [baseline-e2e-tests.trx](test-results/baseline-e2e-tests.trx) |
| `FinanceManager.Tests.E2E.SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` | `FinanceManager.Tests.E2E/Tests/Securities/SecurityTrendReversalE2ETests.cs` (Zeile 114) | `Locator expected not to be checked` — `Expect("ToBeCheckedAsync").Not` auf `#notif-trend-reversal` blieb `checked` (23 Versuche / 10 s Timeout); Persistenz-/Timing-Problem der Benachrichtigungs-Einstellung, keine Beziehung zur Ribbon-Reihenfolge. | E2E-Lauf, [baseline-e2e-tests.trx](test-results/baseline-e2e-tests.trx) |

Beide Fehlschläge stehen in keinem fachlichen Zusammenhang zur Ribbon-Gruppenreihenfolge (Login-Navigation bzw. Notification-Checkbox). Sie gelten als nachgewiesene vorher existierende („preexisting") Fehler dieses Stands.

### Testlücken und Ausführungsprobleme

- Unit- und Integration-Suite: vollständig ausgeführt, keine Ausfälle, keine übersprungenen Tests.
- E2E-Suite: vollständig ausgeführt (133 ausgeführte Tests von 119 `[Fact]`/`[Theory]`-Deklarationen — Differenz durch Theory-Datenzeilen), 2 oben dokumentierte Fehlschläge, keine übersprungenen Tests.
- `Category=OsInterface`-Trait: im Unit-Lauf wie in der CI ausgefiltert; betroffener Umfang aus dem TRX nicht ersichtlich (Filter greift vor Ausführung, gezählte 2148 Tests sind bereits der gefilterte Umfang).
- Erster Build-Versuch der Integration-Suite während des laufenden Unit-Testlaufs schlug mit MSB3021-Dateisperren auf `FinanceManager.Tests/bin/Debug/...` fehl (Infrastrukturproblem durch parallele Builds, kein Testfehler); Wiederholung nach Abschluss erfolgreich.
- Kein Test in den Suiten assertiert derzeit die Reihenfolge der Ribbon-Gruppen — die betroffenen `GetRibbonRegisters`-Rückgaben werden nur auf Vorhandensein von Gruppen/Aktionen geprüft (siehe Testklassen unten). Eine Reihenfolge-Absicherung existiert weder als Unit-Test noch als E2E-Assertion.

## Testklassen

### `PostingsCardViewModelReversalTests` (`FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs`)
- `GetRibbonRegisters_ReverseAction_ShouldBeEnabled_ForNormalPosting` — `Reverse`-Aktion aktiv bei normalem Posting.
- `GetRibbonRegisters_ReverseAction_ShouldBeDisabled_WhenPostingIsReversed` — `Reverse` deaktiviert bei bereits storniertem Posting.
- `GetRibbonRegisters_ReverseAction_ShouldBeDisabled_WhenPostingIsReversal` — `Reverse` deaktiviert bei Storno-Buchung.
- `ReverseAsync_ShouldRaiseNavigateToPostingAction_OnSuccess` — Erfolgreiche Stornierung löst `NavigateToPosting`-UiAction aus.
- `ReverseAsync_ShouldSetLastError_WhenApiFails` — API-Fehler setzt `LastError`, keine Navigation.

### `SavingsPlanCardLinkTests` (`FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs`)
- `LoadAsync_ResolvesLinkedAndLinkableAccounts`, `LoadAsync_NonSavingsAccounts_NotLinkable` — Verknüpfungslogik.
- `OpenLinkedAccount_RaisesOpenAccountDetails` — UiAction beim Öffnen verknüpfter Konten.
- `AddAccountLinkAsync_CallsApi_AndRefreshesState`, `RemoveAccountLinkAsync_CallsApi_AndRestoresReportEligibility`, `RemoveAccountLinkAsync_ConfirmedViaService_CallsApi`, `RemoveAccountLinkAsync_Cancelled_DoesNotCallApi` — Link-/Unlink-Abläufe.
- `Ribbon_ActualReport_EnabledOnlyWithoutLinks` — prüft über `GetRibbonRegisters` Enabled/Tooltip der `ActualReport`-Aktion (Gruppe `Ribbon_Group_Bookings`); keine Reihenfolge-Assertion.
- `ReportActualBalanceAsync_PostsReport_AndReloads`, `ReportActualBalanceAsync_LinkedPlan_RejectedLocally` — Istmeldung.

### `AccountDetailViewModelTests` (`FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs`)
- `GetRibbon_ContainsExpectedGroups` — prüft für `BankAccountCardViewModel` nur das **Vorhandensein** der Gruppen `Ribbon_Group_Navigation`, `Ribbon_Group_Manage`, `Ribbon_Group_Linked` (Zeilen 185–202); `Ribbon_Group_Bookings` und die Reihenfolge werden nicht geprüft.
- Weitere Tests zu `SavePendingAsync`/`DeleteAsync` des `BankAccountCardViewModel`.

### `RibbonTests` (`FinanceManager.Tests/Components/RibbonTests.cs`, bUnit)
- `SingleTab_RendersGroupsAndButtons`, `ClickCallback_IsInvoked` — Basis-Rendering der `Ribbon<TTabEnum>`-Komponente.
- `MobileGroupPanel_*`, `MobileGroupMenu_*`, `MobileShortcut_*` (12 Tests) — Mobile-Layout, Hamburger-Menü, Shortcut-Regeln, `Hidden`-Filter.
- Kein Test zur Gruppensortierung/First-Seen-Reihenfolge.

### Weitere Ribbon-bezogene Tests (nicht betroffen, nur Kontext)
- `SetupCardViewModelTests`, `ReportDashboardViewModelTests`, `SecurityCardViewModelTests` (`Web/ViewModels/Securities`), `SecurityCategoryDetailViewModelTests`, `StatementDraftCardViewModelTests`, `SavingsPlansViewModelTests`, `SecurityPricesViewModelTests` u. a. — prüfen jeweils Vorhandensein/Inhalt einzelner Gruppen oder Aktionen über `GetRibbonRegisters`, keine Reihenfolge.

## Hilfsmethoden

### `PostingsCardViewModelReversalTests`
- `CreateVm()` — ServiceProvider mit `ICurrentUserService`-Stub + `IApiClient`-Mock; liefert `PostingsCardViewModel`.
- `BuildPostingDto(...)` — parametrisierbares `PostingServiceDto` (IsReversed/IsReversal u. a.).
- `FindReverseAction(vm)` — flacht `GetRibbonRegisters` zu Aktionen ab, sucht `Id == "Reverse"`; wiederverwendbares Muster für Reihenfolge-Tests.
- `DummyLocalizer` — `IStringLocalizer`, gibt Schlüssel als Wert zurück (`Ribbon_Group_Linked` bleibt so als Titel erkennbar).

### `SavingsPlanCardLinkTests`
- `CreateVm()` / `SetupLoad(apiMock, planId, linkedIds, accounts)` — VM-Factory und API-Setup für `SavingsPlanCardViewModel`.
- `NewPlan(id)` / `NewAccount(id, name)` — DTO-Fabriken.
- `FindActualReportAction(vm)` — sucht `ActualReport`-Aktion über `GetRibbonRegisters`.
- `DummyLocalizer` / `PassthroughLocalizer<T>` — wie oben.

### `RibbonTests`
- `RenderRibbon(registers)` / `CreateRegisters(actions)` — bUnit-Render-Helfer mit `IRibbonProvider`-Mock; für Gruppenreihenfolge-Assertions auf Komponentenebene geeignet (DOM-Reihenfolge der `.fm-ribbon-group`-Elemente).
