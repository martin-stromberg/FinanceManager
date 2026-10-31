# Tests — Bestand pro Finding

## Testinfrastruktur
- `FinanceManager.Tests` — Unit-/Komponententests (EF Core In-Memory/SQLite, Moq, bunit, FluentAssertions).
- `FinanceManager.Tests.Integration` — `TestWebApplicationFactory.cs` (`WebApplicationFactory`, Environment Development → DeveloperExceptionPage-Pfad); `FinanceManager.Tests.Integration.ApiClient` — API-Client-Tests gegen gehostete App.
- `FinanceManager.Tests.E2E` — Playwright; `Infrastructure/PlaywrightWebAppFixture.cs` startet den Web-Server als separaten Prozess mit `E2E__*`-Env-Variablen.

## Testklassen

### `UserAuthServiceTests` (#450, #456)
Datei: `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`
- `RegisterAsync_ShouldCreateFirstUserAsAdmin_WhenNoUsersExist` — Erstbenutzer wird Admin.
- `RegisterAsync_ShouldFail_WhenDuplicateUsername`, `..._ShouldFail_WhenUsernameOrPasswordMissing`, `..._ShouldSetPreferredLanguage_WhenProvided`.
- `RegisterAsync_ShouldQueueDemoData_WhenFirstUserAndCreateDemoDataEnabled`, `..._ShouldNotQueueDemoData_WhenNotFirstUser` — Demo-Data-Bindung an `isFirst`.
- Login-/Lockout-/ChangePassword-Tests (Z. 278–518). **Kein Race-/Concurrency-Test.**

### `UsersControllerTests_Authorization` (#456)
Datei: `FinanceManager.Tests/Controllers/UsersControllerTests_Authorization.cs`
- `HasAnyUsersAsync_AllowAnonymous_RemainsPubliclyReachable` — reflektionsbasiert verankert `[AllowAnonymous]` auf `GET /api/users/exists` (Begründung: Login-Page benötigt anonyme Abfrage).
- Weitere Authorization-Attribut-Prüfungen auf Controller-Ebene.

### `StatementDraftsControllerTests` (#451, #457)
Datei: `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs`
- `ProcessMassImportAsync_ShouldReturnBadRequest_WhenRequestHasNoFiles` / `..._ShouldReturnInternalServerError_WhenOrchestratorIsMissing` / `..._ShouldDelegateToOrchestrator_WithCurrentUserAndTraceId` — Controller-Verhalten; `RequestSizeLimit`-Attribut wird nicht getestet.
- `ExportAsync_ShouldReturnCsvFile_WithEntries` / `..._XlsxFile` / `..._NotFound` / `..._BadRequest_ForInvalidFormat`.
- `MassImportOrchestratorTests` (`FinanceManager.Tests/Statements/`) — Orchestrierer-Logik.

### `JwtCookieAuthTokenProviderTests` + `_Circuit` (#455, #460)
Dateien: `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests*.cs`
- `GetAccessTokenAsync_*` (Cookie-Präferenz, Circuit-Fallback, ungültiger Issuer/Audience, Refresh, Reject).
- `AcceptBrowserToken_*` (4 Tests: anderer User, älterer Response, nach Invalidation, Update bei späterem Expiry).
- `PersistedToken_ExpiredToken_IsRejected` — verankert `[PersistentState]`-Setter-Verhalten (#460).
- `JwtRenewalTimingTests.cs`, `JwtRefreshServiceTests.cs`, `JwtTokenServiceTests.cs`, `JwtOptionsValidatorTests.cs`.

### `ApiClientAuthTests` (#455)
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`
- Keepalive-Test (Z. ~172–213): erwartet `X-Auth-Token` + `X-Auth-Token-Expires` im Header und erneuertes Cookie — **muss bei #455 angepasst werden**.
- Negativtest (Z. 241–244): Header fehlt wenn kein Refresh nötig.
- Security-Stamp-Invalidierungstest (Z. 248ff).

### `BackupServiceTests` / `BackupServiceFullExportTests` / `ApiClientBackupsTests` (#452)
Dateien: `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`, `BackupServiceFullExportTests.cs`, `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`, `ApiClientBackupsWithDemoDataTests.cs`
- Create/Upload (Zip-Validierung, Entry-Name, Version, Größenlimit)/List/Delete/OpenDownload — alle mit gemeinsamem `backups`-Root; kein Cross-User-Test.

### `HelpSecurityMiddlewareTests` (#453)
Datei: `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs`
- CSP-Header-Assertions für `/help`-Routen; Integritäts-Blocking für Help-Assets — Referenzmuster für Header-Assertions.

### Export-Tests (#457)
- `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` — CSV-Streaming-Inhalt; kein Formel-Escaping-Test.
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs`.

### E2E (#451, #461, #456)
- `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs`, `CollectionAccountImportPlaywrightTests.cs` — legitime Mass-Import-Flüsse.
- `FinanceManager.Tests.E2E/Tests/Accounts/AccountsOverviewStatisticsPlaywrightTests.cs` — einziger Verbraucher der Fault-Injection.
- `FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs`, `PlaywrightWebAppFixture.cs` — Seeding/Server-Start; abhängig von offener Registrierung.

## Hilfsmethoden
### `TestWebApplicationFactory`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` — konfiguriert In-Process-Host inkl. Test-JWT-Optionen.
### `PlaywrightWebAppFixture`
- Server-Start mit `E2E__DisableHttpsRedirection`, `E2E__AccountStatisticsFaultInjectionEnabled`, `E2E__AccountStatisticsFaultFile`; `EnableAccountStatisticsFault`/`ResetAccountStatisticsFault` als Datei-Toggle.
### `UserAuthService.NoopIpBlockService`
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs` (Z. 313–336) — no-op `IIpBlockService` für Tests/Legacy-Konstruktor.
