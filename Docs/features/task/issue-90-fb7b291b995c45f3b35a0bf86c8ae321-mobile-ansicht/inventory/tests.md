# Tests

## Testklassen

### `AuthenticationFlowPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`
- `Register_Login_Logout_Flow_ShouldWork` — prüft Login/Logout-Flow über UI.

### `ListNavigationPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs`
- `ClickAccountRow_ShouldNavigateToDetailPage` — prüft Navigation von Liste zu Detailseite.
- `Create_Edit_AndAliasContact_FromContactsPage_ShouldWork` — prüft Contact-CRUD/Alias-Flow.
- `CreateContact_FromStatementEntryPage_ShouldAssignContactDirectly` — prüft Kontaktanlage aus Entry-Kontext.
- `Create_Edit_Delete_BankAccount_ShouldWork` — prüft Konto-CRUD.
- `Create_Edit_Delete_SavingsPlan_ShouldWork` — prüft Savings-Plan-CRUD.
- `Create_Edit_Delete_Security_AndImportPrices_OnPricesPage_ShouldWork` — prüft Security-CRUD und Preisimport.

### `ReportingFlowPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs`
- `SaveFavorite_ShouldPersistAndReload` — prüft Persistenz/Laden von Report-Favoriten.
- `CreateBackup_EditMasterData_AndRestoreBackup_ShouldRestoreOriginalState` — prüft Backup/Restore-Flow.

### `HomeMassImportPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs`
- `UploadStatementFile_ShouldShowSuccess_WhenImportCompletes` — prüft Statement-Upload-Flow.
- `Booking_WithErrorsWarnings_AndWithOrWithoutSavingsSecurity_ShouldCreateExpectedPostings` — prüft Buchungs-Flow inkl. Fehler/Warnungen.

## Hilfsmethoden

### `PlaywrightWebAppFixture`
Datei: `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `CreateSessionAsync()` — erstellt Browser-Context/Page für E2E-Tests.
- `LaunchBrowserAsync(...)` — startet Chromium/Channel.
- `StartServer(...)` und `WaitForServerAsync()` — startet WebApp und wartet auf Erreichbarkeit.
- `ResolveWebDllPath()` — ermittelt das gebaute Web-DLL-Artefakt.

### Hilfsmethoden in Testklassen

#### `ListNavigationPlaywrightTests`
- `EnsureAuthenticatedAsync(...)` — standardisierte Test-Authentifizierung.
- `UploadDraftWithSingleEntryAsync(...)` — erzeugt Draft mit Einzeleintrag als Testdatenbasis.

#### `ReportingFlowPlaywrightTests`
- `GetGuid(...)` — extrahiert GUID-Werte aus JSON-Antworten.

#### `HomeMassImportPlaywrightTests`
- `CreateBookingStatementCsv(...)` — erstellt reproduzierbare CSV-Testdaten für Buchungsfälle.

## Befund zur mobilen Testabdeckung

- Die vorhandenen E2E-Flows verwenden `_fixture.CreateSessionAsync()` ohne mobile Browser-Konfiguration (`ViewportSize`, `IsMobile`, `HasTouch` o. ä.).
- Eine standardisierte mobile Session-Erzeugung ist im aktuellen Fixture-Bestand nicht vorhanden.
