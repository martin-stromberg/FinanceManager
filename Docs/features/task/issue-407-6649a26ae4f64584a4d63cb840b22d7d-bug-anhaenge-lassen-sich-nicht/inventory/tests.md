# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-09-26, ca. 05:43–05:52 MESZ (UTC+02:00; UTC 03:43–03:52)
- Branch und Commit-ID: `task/issue-407-6649a26ae4f64584a4d63cb840b22d7d-bug-anhaenge-lassen-sich-nicht` @ `105e88dd9d29b846768c67e462a68d35878de9ba` („chore: Dependabot-Gruppierung einrichten (#401)", 2026-09-19 13:16:33 +0200)
- Uncommittete Änderungen im getesteten Stand: nur untracked `Docs/features/task/issue-407-6649a26ae4f64584a4d63cb840b22d7d-bug-anhaenge-lassen-sich-nicht/` (Anforderungs- und Inventar-Dokumentation); keine Änderungen an Produktivcode, Tests oder Konfiguration.
- Testumgebung und Runtime-/SDK-Versionen: Windows (DESKTOP-CM8OBSG), .NET SDK 10.0.401 / Runtime 10.0.12, Node v24.15.0 / npm 11.10.0, xunit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5, bunit 2.11.3, Moq 4.20.72, coverlet.collector 10.0.1, Microsoft.Playwright 1.62.0 mit Browser-Channel `msedge` (Edge installiert unter `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`, headless). E2E-Fixture `PlaywrightWebAppFixture` startet `FinanceManager.Web` gegen eine Wegwerf-SQLite-DB; Integrationstests nutzen `TestWebApplicationFactory` (in-memory).
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`, Windows) definiert drei `dotnet test`-Läufe (`FinanceManager.Tests` mit Filter `Category!=OsInterface` und XPlat-Coverage, `FinanceManager.Tests.Integration` mit Coverage, `FinanceManager.Tests.E2E` best-effort) sowie `npm ci` + Playwright-Install als Voraussetzung. `package.json` definiert zusätzlich `npm run test:release-version` (Node-`--test` auf `scripts/*.test.mjs`). Vorbereitend ausgeführt: `dotnet restore FinanceManager.sln` und `dotnet build FinanceManager.sln -c Debug --no-restore` — 0 Fehler, 55 Warnungen (u. a. vorab bekannte xUnit-Analysehinweise und NU1510; kein Fehlschlag).
- Hinweis zur Messung: Die Konsolenausgaben wurden per `| tee` in die Nachweisdateien gespiegelt; `EXIT_CODE` in den Logs ist der Exit-Code der Pipeline, für die Läufe 1–2 und 4 entspricht er dem von `dotnet test`/`npm` (Erfolg). Für Lauf 3 ist der Prozess-Exit-Code nicht separat erfasst (siehe Tabelle).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit-/Komponententests | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --filter "Category!=OsInterface" --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results-regular-tests.trx" --logger "console;verbosity=normal" --results-directory <inv>/test-results/unit` | Repo-Root | 0 | 1335 | 0 | 0 | [TRX](test-results/unit/test-results-regular-tests.trx) · [Konsole](test-results/unit/console.log) · [Coverage](test-results/unit/5d2da4e4-2d68-448a-a5c5-d14892b95531/coverage.cobertura.xml) |
| Integrationstests | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results-regular-integration.trx" --logger "console;verbosity=normal" --results-directory <inv>/test-results/integration` | Repo-Root | 0 | 134 | 0 | 0 | [TRX](test-results/integration/test-results-regular-integration.trx) · [Konsole](test-results/integration/console.log) · [Coverage](test-results/integration/30944a71-bf10-4a35-b0e6-f297ecc1d92b/coverage.cobertura.xml) |
| E2E-Tests | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --logger "trx;LogFileName=test-results-e2e-tests.trx" --logger "console;verbosity=normal" --results-directory <inv>/test-results/e2e` | Repo-Root | nicht erfasst (tee-Pipeline); TRX-`ResultSummary outcome="Failed"` | 99 | 1 | 0 | [TRX](test-results/e2e/test-results-e2e-tests.trx) · [Konsole](test-results/e2e/console.log) |
| Node-Skript-Tests | `npm run test:release-version` (= `node --test scripts/resolve-release-version.test.mjs scripts/generate-update-manifest.test.mjs`) | Repo-Root | 0 | 26 | 0 | 0 | [Konsole](test-results/node/console.log) |

(`<inv>` = `docs/features/task/issue-407-6649a26ae4f64584a4d63cb840b22d7d-bug-anhaenge-lassen-sich-nicht/inventory`)

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` | `FinanceManager.Tests.E2E`, `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs:534` | `Expected response.Status to be 204, but found 401 (difference of 197)` — ein Keepalive-/Auth-Request im E2E-Test liefert 401 statt 204. Kein Bezug zum Anhangs-/Dialog-Layering-Bug (der Bestätigungs-E2E-Test `AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` lief erfolgreich). | E2E-Lauf, [TRX](test-results/e2e/test-results-e2e-tests.trx) (StackTrace enthalten) |

In den Unit- und Integrationsläufen wurden keine Testfehler nachgewiesen.

### Testlücken und Ausführungsprobleme

- Der Filter `Category!=OsInterface` ist aktuell wirkungslos: Kein Test in `FinanceManager.Tests` trägt diese Kategorie; alle 1335 entdeckten Tests wurden ausgeführt (`notExecuted="0"` im TRX).
- Keine übersprungenen, deaktivierten oder abgebrochenen Tests in keinem Lauf (`notExecuted="0"`, `aborted="0"` in allen TRX-Dateien).
- Der CI-Workflow enthält zusätzlich nicht-testbezogene Gates (`dotnet format --verify-no-changes`, Build mit `TreatWarningsAsErrors`), die hier nicht als Testlauf betrachtet wurden; der reguläre Solution-Build (`-c Debug`) wurde als Voraussetzung ausgeführt und war erfolgreich.
- Testlücke für die Anforderung: **Kein automatisierter Test prüft die Schichtreihenfolge** (kein Test referenziert `split-center`, `z-index` oder die DOM-Position von `ConfirmationDialogHost` relativ zum Overlay). bUnit rendert ohne CSS-Auswertung; der einzige E2E-Bestätigungstest (`ConfirmationDialogE2ETests`) löscht ein Konto ohne geöffnetes Overlay. Der beschriebene Bug ist somit testseitig unabgedeckt und im bisherigen Stand nicht reproduzierbar.
- `FinanceManager.Tests.Integration.ApiClient` ist kein eigenes Projekt; die Datei `ApiClientBudgetReportUnbudgetedMirrorTests.cs` liegt zusätzlich als lose Kopie im gleichnamigen Verzeichnis des Repo-Roots und ist im Integrationsprojekt enthalten (Namespace `FinanceManager.Tests.Integration.ApiClient`, im Lauf erfolgreich ausgeführt).

## Testklassen

### `ConfirmDialogTests` (`FinanceManager.Tests/Components/ConfirmDialogTests.cs`)
- `ConfirmDialog_ShouldRenderTitleAndMessage` — Rendert Titel- und Message-Resource-Key im Markup.
- `ConfirmDialog_ShouldCallSetResultTrue_OnConfirm` — Klick auf Bestätigen-Button ruft `IConfirmationService.SetResult(true)` auf.
- `ConfirmDialog_ShouldCallCancel_OnCancel` — Klick auf Abbrechen-Button ruft `IConfirmationService.Cancel()` auf.

### `ConfirmationDialogHostTests` (`FinanceManager.Tests/Components/ConfirmationDialogHostTests.cs`)
- `ConfirmAsync_RendersAndDismisseDialog` — `ConfirmAsync` rendert `.confirm-dialog` im Host; `SetResult(true)` entfernt den Dialog wieder (`WaitForState` auf `confirm-dialog`).

### `OverlayHostTests` (`FinanceManager.Tests/Components/OverlayHostTests.cs`)
- `OverlayHost_ShouldRenderOverlayTitle_WhenProvidedInParameters` — `OverlayTitle`-Parameter gewinnt im `<h2>` des Overlay-Headers.
- `OverlayHost_ShouldUseLocalizedFallbackTitle_WhenOverlayTitleIsMissing` — Fallback auf lokalisierten Titel per `ComponentType` (`SecurityPriceImportPanel` → `SecurityPricesImport_Title`).

### `ConfirmationServiceTests` (`FinanceManager.Tests/Services/ConfirmationServiceTests.cs`)
- `ConfirmAsync_ReturnsTrue_WhenShowConfirmationsIsFalse` — Unterdrückung bei deaktivierter Einstellung.
- `ConfirmAsync_RaisesOnShow_AndReturnsTrue_WhenUserConfirms` — `OnShow` wird ausgelöst, Ergebnis `true`.
- `ConfirmAsync_RaisesOnShow_AndReturnsFalse_WhenUserCancels` — Abbruchpfad liefert `false`.
- `ConfirmAsync_DefaultsToEnabled_WhenApiThrows` — API-Fehler → Dialog wird angezeigt (Default `true`).
- `ConfirmAsync_UsesCachedValue_OnSecondCall` — `UserSettings_GetProfileAsync` wird nur einmal aufgerufen.
- `InvalidateCacheAsync_RefetchesProfile_OnNextConfirm` — Cacheinvalidierung erzwingt erneuten API-Aufruf.
- `ConfirmAsync_SetsCurrentRequest_BeforeReturning` — `CurrentRequest` ist während des ausstehenden Dialogs gesetzt.
- `SeverityValues_AreDefined` (Theory) — `ConfirmationSeverity`-Werte sind definiert.

### `CardPageTests` (`FinanceManager.Tests/Components/CardPageTests.cs`)
- `ContactCard_EditFields_And_Save_CallsApiUpdate` — Editieren + Speichern via Ribbon ruft `Contacts_UpdateAsync`.
- `ContactCard_OpenAttachments_RendersAttachmentsPanelOverlay` — Klick auf Ribbon-Button `Attachments` rendert das Attachments-Overlay (`.drop-upload` vorhanden).
- `ContactCard_DisplaysAllFields_WhenContactHasNonDefaultValues` — Vollständige Feldbefüllung inkl. Symbolbild.

### `ListPageTests` (`FinanceManager.Tests/Components/ListPageTests.cs`)
- `ListPage_Accounts_RendersStatisticsAndGenericListOnlyForAccounts` — Statistiken + generische Liste auf der Kontenliste.

### `AttachmentsControllerTests` (`FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`)
- `DeleteAsync_ShouldReturn_NoContent_WhenDeleted` (Zeile 582) — `204` bei erfolgreichem Löschen.
- `DeleteAsync_ShouldReturn_NotFound_WhenMissing` (Zeile 597) — `404`, wenn `DeleteAsync` `false` liefert.
- (u. a. weitere Upload-/Download-/Kategorie-Tests — nicht anforderungsrelevant)

### `AttachmentServiceTests` / `AttachmentCleanupTests` (`FinanceManager.Tests/Attachments/`)
- `AttachmentServiceTests.Download_UpdateCategory_Delete_Reassign_Work` (Zeile 128) — End-to-End-Servicefluss inkl. Löschen.
- `AttachmentServiceTests.DeleteAsync_ShouldDeleteMasterAndAllReferences_WhenDeletingReference` (Zeile 197) — Löschkaskade.
- `AttachmentCleanupTests` — Bereinigungslogik.

### `ConfirmationDialogE2ETests` (`FinanceManager.Tests.E2E/Tests/Confirmation/ConfirmationDialogE2ETests.cs`)
- `AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` — Bestätigungsdialog auf der Kontokarte: Abbrechen erhält das Konto, Bestätigen löscht und navigiert zur Liste. Im Ausgangslauf **bestanden**; deckt kein Overlay-Szenario ab.

## Hilfsmethoden

### `ConfirmDialogTests`
- `RegisterServices(IConfirmationService?)` — Registriert Confirmation-Service-Mock und `PassthroughLocalizer<Pages>` im bUnit-`Services`-Container.
- `PassthroughLocalizer<T>` (private Klasse) — `IStringLocalizer<T>`, der den Resource-Key als Wert zurückgibt.

### `ConfirmationDialogHostTests`
- `PassthroughLocalizer<T>` (private Klasse) — wie oben; registriert zudem `IApiClient`-Mock (`ShowConfirmations = true`), `NullLogger<ConfirmationService>` und `ConfirmationService` als Scoped.

### `OverlayHostTests`
- `TestCardViewModel` (private Klasse, `BaseCardViewModel<(string,string)>`) — `RaiseOverlay(UiOverlaySpec)` löst `RaiseUiActionRequested("OpenOverlay", spec)` aus; abstrakte Symbol-Upload-Member gestubbt.
- `PassthroughLocalizer<T>` (private Klasse) — Key-Echo-Localizer.

### `CardPageTests`
- `TestCurrentUserService` (private Klasse) — `ICurrentUserService`-Stub (`IsAuthenticated`, `IsAdmin`, `UserId`).
- Konstruktor-Setup — `LoadingBarService`, `IConfirmationService`-Mock, JSInterop-Stubs für `financeManager.loadingBar.start/stop`, fixe Kultur `en-US`.

### E2E-Infrastruktur (`FinanceManager.Tests.E2E/Infrastructure`, `Helpers`)
- `PlaywrightWebAppFixture` — startet echte `FinanceManager.Web`-Instanz (Wegwerf-SQLite, seeded Update-Quelle) + Playwright-Browser (`msedge`, headless); `CreateSessionAsync` liefert `IPage`-Sessions.
- `PlaywrightBrowserSession`, `PlaywrightCollection`, `PlaywrightTestOptions` — Session-/Optionskapselung (`PLAYWRIGHT_BROWSER_CHANNEL`, `PLAYWRIGHT_HEADED`, `PLAYWRIGHT_TRACE`, `PLAYWRIGHT_ARTIFACTS`; im Lauf Defaults, keine Env-Variablen gesetzt).
- `AuthGateway` / `TestUserSeeder` — Login über UI bzw. User-Seeding in der SQLite-DB.
- `AccountsApiSeedHelper`, `ListPageGateway`, `HomePageGateway`, `ReportDashboardGateway`, `SetupUpdateGateway`, `BrowserApiHelper`, `TestAuthCookieHelper` — Seeding- und UI-Gateway-Helfer.
