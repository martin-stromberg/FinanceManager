# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### BackupUploadSizeLimitAttribute.cs (BackupUploadSizeLimitAttribute)

- **Bereitstellung/Build** — Die Datei `FinanceManager.Web/Infrastructure/Backups/BackupUploadSizeLimitAttribute.cs` existiert im Working Tree, wird aber von der `.gitignore`-Regel `Backup*/` (`.gitignore:274`) verschluckt und ist nicht committbar. `BackupsController.UploadAsync` (`BackupsController.cs:84`) und `UploadLimitAttributeTests` referenzieren die Klasse — auf frischem Checkout/CI bricht der Build.

  Empfehlung: `.gitignore` um eine Negativ-Regel ergänzen (z. B. `!FinanceManager.Web/Infrastructure/Backups/`) oder die Datei per `git add -f` versionieren.

### ProgramExtensions.cs (Rate-Limiter-Partitionierung)

- **Security-Umgehbarkeit** — Die `auth-sensitive`-Policy partitioniert über `httpContext.Connection.RemoteIpAddress`, während `UseForwardedHeaders` (Zeile ~450) ohne `KnownProxies`/`KnownNetworks`/`ForwardLimit` konfiguriert ist. Ein direkt erreichbarer Client kann `X-Forwarded-For` pro Request beliebig variieren und so die #456-Rate-Limit-Partition (sowie die IP-Blockliste in `IpBlockMiddleware`) umgehen.

  Empfehlung: Vertrauenswürdige Proxies per `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks` (konfigurierbar) hinterlegen bzw. `ForwardLimit` setzen, damit Client-IP nur von vertrauten Proxies übernommen wird.

### UserAuthService.cs (UserAuthService.RegisterAsync)

- **Fehlerbehandlung/Konsistenz** — Nach dem nicht abbrechbaren `_userManager.CreateAsync` werden die Folge-Schreibvorgänge (`_db.Users.Add` + `SaveChangesAsync`, Self-Contact) mit `ct` (RequestAborted) ausgeführt (Zeilen 155–217). Bricht der Client die Registrierung ab, bleibt ein Identity-User ohne Domain-Datensatz/Self-Contact zurück — dieselbe Fehlerklasse, die #463/#465 für Settings behoben hat.

  Empfehlung: Die Schreibvorgänge nach erfolgreichem `CreateAsync` analog zum Admin-Strip (`CancellationToken.None`) unabhängig vom Request-Abbruch committen.

### CookieCsrfProtectionMiddleware.cs (CookieCsrfProtectionMiddleware.RequiresValidation)

- **Fehlende Validierung** — `RequiresValidation` prüft nur `IAuthorizeData`, nicht `IAllowAnonymous` (Zeile 66). Ein anonymer Unsafe-Endpoint unter einem `[Authorize]`-Controller oder einer Fallback-Policy würde fälschlich ein Antiforgery-Token verlangen (403). Aktuell existiert kein solcher Endpoint — latente Falle bei künftigen Endpoints.

  Empfehlung: `context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null` vorab auf `false` auswerten.

### AuthController.cs (AuthController.RegisterAsync)

- **Fehlermeldung ohne Kontext** — Jeder `!result.Success`-Fall wird auf `Err_Conflict_UserAlreadyExists` mit HTTP 409 abgebildet (Zeilen 106–112). Die neue Ablehnung „Registration is not available" (Self-Registration deaktiviert, `UserAuthService.cs:124`) wird dadurch fälschlich als „Benutzer existiert bereits" mit 409 gemeldet.

  Empfehlung: Für den deaktivierten Registrierungsfall einen eigenen stabilen Code (z. B. `Err_RegistrationDisabled`) mit HTTP 403 zurückgeben.

### CircuitAuthTokenStore.cs (CircuitAuthTokenStore.TryTakeHandoff)

- **Concurrency** — `TryGetValue` + `Remove` (Zeilen 53–63) ist nicht atomar: Zwei gleichzeitige Consumer desselben Handoff-Keys können beide den Token auslesen; die dokumentierte Single-Use-Garantie (XML-Doc) gilt nicht strikt. Praktisch geringes Risiko (opaker Guid-Key), aber die Semantik ist falsch.

  Empfehlung: Zugriff synchronisieren (z. B. `lock` um Lookup+Remove oder `ConcurrentDictionary.TryRemove` statt `IMemoryCache`).

### JwtRefreshMiddleware.cs (JwtRefreshMiddleware.InvokeAsync)

- **XML-Doc** — Die `<remarks>` von `InvokeAsync` (Zeile ~35) behaupten weiterhin, der erneuerte Token werde „also exposed via response headers". Genau diese Exposition (`X-Auth-Token`/`X-Auth-Token-Expires`) wurde mit #455 entfernt; die Dokumentation widerspricht der Implementierung.

  Empfehlung: Remarks aktualisieren (Cookie + serverseitige Registry-Veröffentlichung beschreiben).

### CookieCsrfProtectionTests.cs (CookieCsrfProtectionTests.CreateBearerTokenAsync)

- **Toter Code** — `await Task.CompletedTask;` (Zeile 123) ist ein No-op, der die Methode künstlich `async` macht.

  Empfehlung: Statement entfernen und Methode synchron als `Task.FromResult`/`string` zurückgeben oder gar nicht `async` deklarieren.

## Geprüfte Dateien

- `Docs/features/fix-security-450-461/todo.md` (geändert)
- `Docs/features/fix-security-450-461/review.md`, `Docs/features/fix-security-450-461-tasks.md` (neu, Dokumentation)
- `FinanceManager.Infrastructure/Auth/AuthOptions.cs` (neu)
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs` (geändert)
- `FinanceManager.Infrastructure/Backups/BackupService.cs` (geändert)
- `FinanceManager.Infrastructure/FinanceManager.Infrastructure.csproj` (geändert)
- `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` (geändert)
- `FinanceManager.Infrastructure/Setup/SchemaPatcher.cs` (geändert)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` (geändert)
- `FinanceManager.Tests.Integration/AuthRateLimitingTests.cs` (neu)
- `FinanceManager.Tests.Integration/CookieCsrfProtectionTests.cs` (neu)
- `FinanceManager.Tests.Integration/E2ESwitchesProductionTests.cs` (neu)
- `FinanceManager.Tests.Integration/SecurityHeadersTests.cs` (neu)
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` (geändert)
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs` (geändert)
- `FinanceManager.Tests/Auth/UserAuthServiceTests.cs` (geändert)
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs` (geändert)
- `FinanceManager.Tests/Controllers/UploadLimitAttributeTests.cs` (neu)
- `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests.cs` (geändert)
- `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests_Circuit.cs` (geändert)
- `FinanceManager.Tests/Infrastructure/Auth/JwtRenewalTimingTests.cs` (geändert)
- `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs` (geändert)
- `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs` (geändert)
- `FinanceManager.Tests/Infrastructure/SchemaPatcherBackupMigrationTests.cs` (neu)
- `FinanceManager.Tests/Reports/ExportFileHelperTests.cs` (neu)
- `FinanceManager.Web/Components/App.razor` (geändert)
- `FinanceManager.Web/Components/AuthRedirect.razor` (geändert)
- `FinanceManager.Web/Components/Pages/Register.razor` (geändert)
- `FinanceManager.Web/Controllers/AuthController.cs` (geändert)
- `FinanceManager.Web/Controllers/BackupsController.cs` (geändert)
- `FinanceManager.Web/Controllers/SavingsPlansController.cs` (geändert)
- `FinanceManager.Web/Controllers/SecuritiesController.cs` (geändert)
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` (geändert)
- `FinanceManager.Web/Controllers/UsersController.cs` (geändert)
- `FinanceManager.Web/Infrastructure/Auth/CircuitAuthTokenStore.cs` (neu)
- `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs` (geändert)
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs` (geändert)
- `FinanceManager.Web/Infrastructure/Backups/BackupUploadSizeLimitAttribute.cs` (neu, git-ignoriert!)
- `FinanceManager.Web/Infrastructure/CookieCsrfProtectionMiddleware.cs` (neu)
- `FinanceManager.Web/Infrastructure/MassImport/MassImportOptions.cs` (neu)
- `FinanceManager.Web/Infrastructure/MassImport/MassImportRequestSizeLimitAttribute.cs` (neu)
- `FinanceManager.Web/Infrastructure/RateLimitPolicies.cs` (neu)
- `FinanceManager.Web/ProgramExtensions.cs` (geändert)
- `FinanceManager.Web/Services/BudgetReportExportService.cs` (geändert)
- `FinanceManager.Web/Services/SecurityHeadersPolicy.cs` (neu)
- `FinanceManager.Web/appsettings.json`, `appsettings.Production.json` (geändert)
- `FinanceManager.Web/wwwroot/js/attachments.js`, `financeManager.js` (geändert)
