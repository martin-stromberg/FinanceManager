# Tests – Bestand und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-11, Testläufe zwischen ca. 04:49 und 05:35 Uhr +0100 (CET)
- Branch und Commit-ID: `task/issue-502-f67f552efe2144b18241b6cd392952ff-posten-karte-aktionen-der-grup`, HEAD `e2ce0a72d1345800f175a8d6c9fd1795b9a7d59d` („fix: Ribbon-Gruppierung — 'Verknüpfte Informationen' an letzte Position (#498) (#504)")
- Uncommittete Änderungen im getesteten Stand: keine geänderten getrackten Dateien; einzig untracked ist der Lifecycle-Arbeitsordner `Docs/features/task/issue-502-.../` (requirement.md, inventory-Artefakte). Produktiv- und Testcode unverändert.
- Testumgebung und Runtime-/SDK-Versionen: Windows, `dotnet --version` = 10.0.403 (einzige installierte SDK-Version), Node.js v24.15.0; Testrunner Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`), xUnit v3, bUnit, Moq, FluentAssertions; Playwright-Browser lokal installiert (`%LOCALAPPDATA%\ms-playwright`, u. a. chromium-1169…1248).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`) und `staging-ci.yml` definieren drei Suiten: `FinanceManager.Tests` (Unit, mit Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration` (SQLite-In-Memory via `TestWebApplicationFactory`), `FinanceManager.Tests.E2E` (Playwright, in CI `continue-on-error: true`). `CI-CD.md` Zeilen 224–226 dokumentiert die lokalen Befehle. Der Ordner `FinanceManager.Tests.Integration.ApiClient/` enthält nur eine einzelne getrackte `.cs`-Datei ohne `.csproj`, ist nicht Teil der `FinanceManager.sln` und wird daher nicht ausgeführt; die eigentlichen API-Client-Integrationstests liegen im Unterordner `FinanceManager.Tests.Integration/ApiClient/`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug --results-directory <feature>/inventory/test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unit.trx` | Repo-Root | Testlauf „Bestanden!" (Pipeline-Exit 1 durch `tee`-Fehler auf damals fehlendes `unit/`-Verzeichnis; TRX belegt 0 Fehler) | 2155 | 0 | 0 | [test-results-unit.trx](test-results/unit/test-results-unit.trx), [console.log](test-results/unit/console.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug --results-directory <feature>/inventory/test-results/integration -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | Repo-Root | 0 | 165 | 0 | 0 | [test-results-integration.trx](test-results/integration/test-results-integration.trx), [console.log](test-results/integration/console.log) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug --results-directory <feature>/inventory/test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | Repo-Root | 2 | 135 | 1 | 0 | [test-results-e2e.trx](test-results/e2e/test-results-e2e.trx), [console.log](test-results/e2e/console.log) |

Hinweis: Die `dotnet test`-Aufrufe wurden ohne `--no-build` ausgeführt (Build im selben Lauf, Konfiguration Debug); die Builds verliefen mit 0 Fehlern (bestehende NU1510-/xUnit-Analyzer-Warnungen, siehe console.logs).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | `FinanceManager.Tests.E2E/Tests/Auth/ChangePasswordWellKnownPlaywrightTests.cs` (Zeilen 108–110, via `Helpers/AuthGateway.cs:138` `LogoutAsync`) | `Microsoft.Playwright.PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:59611/login` — Navigation zur Login-Seite beim Logout abgebrochen (Timeout-/Umgebungsproblem, keine Beziehung zu Ribbon-Handlern). Identisches Fehlerbild bereits in der Baseline von Issue #498 dokumentiert → preexisting. | E2E-Lauf, [test-results-e2e.trx](test-results/e2e/test-results-e2e.trx) |

Der Fehlschlag steht in keinem fachlichen Zusammenhang zu den Ribbon-`Linked`-Aktionen der Posten-Karte (Login-Navigation beim Passwortwechsel) und gilt als nachgewiesener vorher existierender („preexisting") Fehler dieses Stands.

### Testlücken und Ausführungsprobleme

- Unit- und Integration-Suite: vollständig ausgeführt, keine Ausfälle, keine übersprungenen Tests.
- E2E-Suite: vollständig ausgeführt (136 ausgeführte Tests), 1 oben dokumentierter Fehlschlag, keine übersprungenen Tests. In der CI ist diese Suite `continue-on-error: true`.
- `Category=OsInterface`-Trait: im Unit-Lauf wie in der CI ausgefiltert; betroffener Umfang aus dem TRX nicht ersichtlich (gezählte 2155 Tests sind bereits der gefilterte Umfang).
- `FinanceManager.Tests.Integration.ApiClient/`: einzelne getrackte `.cs`-Datei ohne Projekt/Lösungsbezug → nicht ausführbar, kein eigenes Suite-Ergebnis.
- **Kein bestehender Test assertiert die `UiActionRequested`-Payloads der `Linked`-Gruppe von `PostingsCardViewModel`** — `OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity`/`OpenAttachments` werden derzeit weder auf Action-Id noch auf Payload/`UiOverlaySpec` geprüft. Es gibt auch keinen Test, der den `CardPage.razor`-`OnUiActionRequested`-Dispatch (fehlende `case`s) abdeckt; die Host-Seite ist eine Razor-Datei ohne bUnit-Testabdeckung.
- `OpenAttachments`-Deaktivierungslogik (`Posting == null`) wird nicht getestet; `disabled` ist derzeit hart `false`.

## Testklassen

### `PostingsCardViewModelReversalTests` (`FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs`, 6 Tests)
- `GetRibbonRegisters_ReverseAction_ShouldBeEnabled_ForNormalPosting` — `Reverse` aktiv bei normalem Posting.
- `GetRibbonRegisters_ReverseAction_ShouldBeDisabled_WhenPostingIsReversed` / `..._WhenPostingIsReversal` — `Reverse` deaktiviert bei Storno-Konstellationen.
- `ReverseAsync_ShouldRaiseNavigateToPostingAction_OnSuccess` — fängt `UiActionRequested` ab und assertiert `Action == "NavigateToPosting"` + String-Payload (Muster für die geforderten Payload-Tests der Linked-Actions).
- `ReverseAsync_ShouldSetLastError_WhenApiFails` — `LastError` bei `null`-Rückgabe der API.
- `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` — assertiert Gruppenreihenfolge Navigation → Actions → Linked (aus #498).

### `PostingDetailViewModelTests` (`FinanceManager.Tests/ViewModels/PostingDetailViewModelTests.cs`, 1 Test)
- `Initialize_LoadsDetail_ResolvesLinks` — lädt Posten via `Postings_GetByIdAsync`-Mock, prüft `vm.Posting`/`vm.Loading` (kein Ribbon-Assert).

### `OverlayHostTests` (`FinanceManager.Tests/Components/OverlayHostTests.cs`, bUnit, 2 Tests)
- `OverlayHost_ShouldRenderOverlayTitle_WhenProvidedInParameters` / `..._WhenOverlayTitleIsMissing` — `TestCardViewModel.RaiseOverlay(spec)` feuert `UiOverlaySpec` ab; Host rendert `DynamicComponent` mit Titel-Fallback. Referenzmuster für den `OpenAttachments`-Overlay-Pfad.

### `RibbonTests` (`FinanceManager.Tests/Components/RibbonTests.cs`, bUnit)
- Rendering von Gruppen/Buttons, `Disabled` → `disabled`-Attribut + `aria-disabled`, Callback-Weiterleitung, Mobile-Shortcuts/`Hidden`-Filter. Referenz für Button-Zustands-Assertions.

### `RibbonGroupOrderPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs`, 3 Tests)
- `PostingsCard_LinkedGroup_IsLastRibbonGroup` — u. a. Letzte `.fm-ribbon-group` enthält `#OpenAttachments`; assertiert Reihenfolge, nicht das Klick-Verhalten.

### Weitere Kontext-Tests (nicht betroffen)
- `ApiClientPostingsTests`/`ApiClientPostingsReversalTests` (`FinanceManager.Tests.Integration/ApiClient/`), `PostingsQueryServiceInformalTests`, `PostingsListReversalColumnTests`, `PostingsListMarksColumnTests` — Posten-API/Listen, keine Karten-Handler.

## Hilfsmethoden

### `PostingsCardViewModelReversalTests`
- `CreateVm()` — `ServiceCollection` mit `ICurrentUserService`-Stub + `IApiClient`-Mock → `PostingsCardViewModel`.
- `BuildPostingDto(...)` — parametrisierbares `PostingServiceDto` (`isReversed`/`isReversal`/Ids; `AccountId` belegt, `ContactId`/`SavingsPlanId`/`SecurityId` `null` — für Linked-Tests erweiterbar).
- `FindReverseAction(vm)` — flacht `GetRibbonRegisters(localizer)` zu Aktionen ab und sucht per `Id`; wiederverwendbares Muster (`FindLinkedAction`).
- `DummyLocalizer` — `IStringLocalizer`, gibt Schlüssel als Wert zurück.
- `TestCurrentUserService` — `ICurrentUserService`-Stub.
- Abfangen von `UiActionRequested` via `vm.UiActionRequested += (_, e) => { ... e.Action/e.Payload ... }` (Zeilen 189–191) — Muster zum Assertieren von Action-Id, String-Payload und `PayloadObject` (`UiOverlaySpec` mit `ParentKind`/`ParentId`).

### `OverlayHostTests`
- `TestCardViewModel : BaseCardViewModel<(string,string)>` mit `RaiseOverlay(spec)` → `RaiseUiActionRequested("OpenOverlay", spec)`; `PassthroughLocalizer<T>`.

### `RibbonTests`
- bUnit-Rendering von `Ribbon<TTabEnum>` mit `Mock<IRibbonProvider>`; DOM-Selektoren `.fm-ribbon-group`, `button.fm-ribbon-btn`, `aria-disabled`.
