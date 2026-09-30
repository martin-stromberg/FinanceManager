# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-09-30, lokale Zeit (UTC+2, Mitteleuropa)
- Branch und Commit-ID: `task/8fe0b9cee72c42b7a287a1b7d0f81835-reparatur-des-kontoauszugimpor` @ `1070dcf0f059e042c722606c8b568ccc91d80152`
- Uncommittete Änderungen im getesteten Stand: nur Lifecycle-Artefakte unter `Docs/features/task/8fe0b9cee72c42b7a287a1b7d0f81835-reparatur-des-kontoauszugimpor/` (kein Produktivcode)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.401, Windows, Konfiguration `Release` (`Debug`-Ausgabe von `FinanceManager.Web.exe` durch laufende App-Instanz gesperrt — Release-Build als Ausweichkonfiguration)
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/staging-ci.yml` — `FinanceManager.Tests` (Unit, Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration` (WebApplicationFactory + In-Memory-SQLite), `FinanceManager.Tests.E2E` (Playwright)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit-Baseline | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Release -- --filter-not-trait "Category=OsInterface"` | Repo-Root | 0 | 1336 | 0 | 0 | [Log](test-results/baseline-unit-tests.log) |
| Integrations-Baseline | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Release` | Repo-Root | 0 | 134 | 0 | 0 | [Log](test-results/baseline-integration-tests.log) |

### Nachgewiesene bestehende Testfehler

Keine — beide ausgeführte Suiten sind vollständig grün.

### Testlücken und Ausführungsprobleme

- `FinanceManager.Tests.E2E` wurde in der Bestandsaufnahme nicht ausgeführt (Playwright-Browser-Suite, zeitaufwändig; Ausführung ist für Schritt 10 vorgesehen, da UI-Änderungen geplant sind).
- `dotnet build` in `Debug` scheitert, solange eine laufende `FinanceManager.Web.exe`-Instanz (PID 13972) die Debug-Ausgabe sperrt; daher Release-Konfiguration verwendet.

## Testklassen

### `StatementParserAdapterTests` (`FinanceManager.Tests/Statements/StatementParserAdapterTests.cs`)
- `Parse_ShouldReturnNull_WhenFileNotRecognized` — ING-CSV-Parser liefert `null` bei fremdem Inhalt
- `Parse_IngCsv_ShouldReturnSingleElementList_WhenValidSingleBlockContent` — Baseline: altes 9-Spalten-ING-CSV → 1 `StatementParseResult`
- `Parse_ShouldReturnMultipleResults_ForCollectionAccountCSV` — Sammelkonto (2 IBAN-Blöcke, altes Format) → mehrere Ergebnisse
- `Parse_BackupJson_*`, `Parse_IngPdf*`, `Parse_ShouldReturnNull_ForTemplateParsers_WhenFileTypeDoesNotMatch` — übrige Parser
- Hilfmethoden: `CreateIngCsvBytes`, Fake-`IStatementFile` (`FakeLineStatementFile`), `ING_Csv_StatementFile.Load`

### `MassImportOrchestratorTests` (`FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs`)
- Deckt `ProcessAsync` ab: Erkennung Kontoauszug/Wertpapierkurse/Unknown, `RequiresConfirmation` je `DialogPolicy`, Excluded/Skipped, Security-Guess, Ausführung
- Hilfsmethoden: Orchestrator-Fakes (`IStatementFileFactory`, `IStatementFileParser`, `ISecurityService`, `ISecurityPriceImportServiceFactory`)

### `HomeViewModelTests` (`FinanceManager.Tests/ViewModels/HomeViewModelTests.cs`)
- `ProcessMassImportSelectionAsync_ShouldOpenPendingDialog_WhenConfirmationIsRequired` — `AlwaysConfirm` → `PendingMassImport` gesetzt
- `ConfirmMassImportAsync_ShouldSubmitDecisionsAndApplyExecutionResult` — Confirm-Roundtrip (`NullConfirmationService` bestätigt automatisch)
- `ProcessMassImportSelectionAsync_ShouldForceExcludeUnknownType_AndIgnoreManualSelection` — `Unknown` → `Excluded=true`
- Hilfsmethoden: `CreateVm` (ServiceCollection + `IApiClient`-Mock), `InvokeProcessMassImportSelectionAsync` (Reflection), `FakeBrowserFile`

### `AppDbContextMassImportDialogPolicyTests` (`FinanceManager.Tests/Infrastructure/`)
- Persistenz der `MassImportDialogPolicy`-Benutzereinstellung

### E2E `HomeMassImportPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Import/`)
- `UploadStatementFile_ShouldShowSuccess_WhenImportCompletes` (+ mobile Variante) — Upload via `/api/statement-drafts/upload` (Legacy-Endpoint, altes 9-Spalten-Format)
- `Booking_WithErrorsWarnings_AndWithOrWithoutSavingsSecurity_ShouldCreateExpectedPostings`

### E2E `CollectionAccountImportPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Import/`)
- `UploadCollectionAccountCsv_ViaUi_ShouldShowMultipleDraftsInList` — Multi-IBAN-CSV über das Home-Import-Widget (Ribbon `#Import`), klickt ggf. den Review-Dialog durch
- `UploadWithLinkedIban_ViaUi_*`, `ManualAccountAssignment_ViaUi_*`, `BookCollectionAccountDraft_*` — allesamt altes 9-Spalten-Format

## Hilfsmethoden (E2E)

- `AuthGateway.LoginAsync`, `TestUserSeeder.EnsureUserAsync`/`EnsureSelfContactAsync`, `AccountsApiSeedHelper.CreateAccountAsync`, `BrowserApiHelper.PostMultipartAsync`/`PostJsonAsync`/`GetJsonAsync`, `PlaywrightWebAppFixture.CreateSessionAsync`, `HomePageGateway`
