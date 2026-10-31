# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `FinanceManager.Web/Infrastructure/Auth/CircuitAuthTokenStore.cs` — angelegt (`StoreForHandoff`/`TryTakeHandoff` single-use mit 60 s TTL + `SetCurrent`/`TryGetCurrent` Latest-Token-Registry auf `IMemoryCache`), als Singleton in `RegisterAppServices` registriert (`ProgramExtensions.cs:113`)
- [x] `FinanceManager.Web/Infrastructure/MassImport/MassImportOptions.cs` — angelegt (`MaxRequestBytes` 150 M, `MaxFiles` 50, `MaxBytesPerFile` 10 MB, `MaxTotalContentBytes` 100 MB)
- [x] `FinanceManager.Web/Infrastructure/MassImport/MassImportRequestSizeLimitAttribute.cs` (+ interner `MassImportRequestSizeLimitFilter`) — angelegt, setzt `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` in `OnResourceExecuting`
- [x] `FinanceManager.Infrastructure/Auth/AuthOptions.cs` — angelegt (`AllowSelfRegistration`, Default `false`, Section `Auth`)
- [x] `FinanceManager.Web/Services/SecurityHeadersPolicy.cs` — angelegt (`ContentSecurityPolicy`, `PermissionsPolicy`, `Apply(HttpResponse)` mit `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, `Permissions-Policy`, CSP)
- [x] `FinanceManager.Web/Infrastructure/CookieCsrfProtectionMiddleware.cs` — angelegt (unsafe Methode + `IAuthorizeData`-Metadaten + kein `Bearer`-Header → `IAntiforgery.ValidateRequestAsync`, sonst 403)
- [x] `FinanceManager.Web/Infrastructure/RateLimitPolicies.cs` — angelegt (Konstante `AuthSensitive = "auth-sensitive"`; Ergänzung zum Plan)

### Geänderte Klassen / Methoden

- [x] `UserAuthService.RegisterAsync` — statisches `SemaphoreSlim s_registrationGate` (Z. 34), Duplikat-Prüfung → `isFirst` → Gate `!isFirst && !_authOptions.AllowSelfRegistration` (Z. 120–125) → Create → Re-Check mit `SetAdmin(false)` (Z. 174–184) → Rolle/Demo-Data an korrigiertes `isFirst` gebunden (Z. 187–223); optionaler `IOptions<AuthOptions>`-Ctor-Parameter mit Default
- [x] `AuthController.RegisterAsync` — `[EnableRateLimiting(RateLimitPolicies.AuthSensitive)]` hinzugefügt; Fail-Mapping weiterhin 409 (`Err_Conflict_UserAlreadyExists`)
- [x] `UsersController.HasAnyUsersAsync` — `[EnableRateLimiting]` hinzugefügt, `[AllowAnonymous]` unverändert
- [x] `Register.razor.OnInitializedAsync` — `IOptions<AuthOptions>` injiziert, Redirect auf `/login` bei `!_isFirstUser && !AllowSelfRegistration`
- [x] `StatementDraftsController.ProcessMassImportAsync` — `[MassImportRequestSizeLimit]` statt `[RequestSizeLimit(long.MaxValue)]`; applikative Checks `MaxFiles`/`MaxBytesPerFile`/`MaxTotalContentBytes` → 400 `Err_MassImport_TooManyFiles`/`Err_MassImport_FileTooLarge`/`Err_MassImport_BatchTooLarge`; optionaler `IOptions<MassImportOptions>`-Ctor-Parameter
- [x] `SavingsPlansController.UploadSymbolAsync` — `[AttachmentUploadSizeLimit]` statt `[RequestSizeLimit(long.MaxValue)]`
- [x] `SecuritiesController.UploadSymbolAsync` + `ImportPricesAsync` — `[AttachmentUploadSizeLimit]` statt `[RequestSizeLimit(long.MaxValue)]`
- [x] `BackupService` — `GetRoot(Guid userId)` (`backups/{userId:N}`), `ResolveStoragePath` mit Traversal-Guard (nur Plain-Filename, kein `..`), `CreateAsync` Dateiname `backup-yyyyMMddHHmmss-{8hex}.zip`, `UploadAsync` servergenerierter `StoragePath` `upload-{Guid:N}.zip` mit Client-`FileName`, `DeleteAsync`/`OpenDownloadAsync`/`ApplyAsync` über `ResolveStoragePath`
- [x] `SchemaPatcher.MigrateBackupStorageAsync` — angelegt und in `RunPostMigrationPatches` nach `EnsureAspNetUsersColumnsAsync` aufgerufen; verschiebt flache Alt-Dateien in `backups/{OwnerUserId:N}/`, Kollision → Rename + `StoragePath`-Update, idempotent + non-fatal
- [x] `ExportFileHelper.CsvEscape` — Formel-Präfix-Entschärfung (`=`, `+`, `-`, `@`, `\t` → führendes `'`) nach Newline-Neutralisierung
- [x] `BudgetReportExportService` — veralteter Klassenkommentar korrigiert (XLSX statt „small CSV stream")
- [x] `JwtRefreshMiddleware` — `SetTokenResponseHeaders` + Konstanten `RefreshHeaderName`/`RefreshExpiresHeaderName` entfernt; `Cache-Control: no-store` auf Keepalive- und Refresh-Pfad gesetzt
- [x] `JwtCookieAuthTokenProvider` — `PersistedToken` → `[PersistentState] PersistedTokenKey` (Getter `StoreForHandoff`, Setter `TryTakeHandoff` → Validierung → `Cache` + `SetCurrent`); `AcceptBrowserToken` entfernt; `GetAccessTokenAsync` adoptiert Registry-Token bei auslaufendem/fehlendem Cache; `PrimeCache` publiziert via `SetCurrent`; Ctor mit `CircuitAuthTokenStore`
- [x] `financeManager.js` — `X-Auth-Token`-Lesen, `tokenReceiver`, `invokeMethodAsync("ReceiveTokenAsync")` und `keepalive.setTokenReceiver` entfernt; Ping-Logik unverändert
- [x] `AuthRedirect.razor` — `_tokenReceiver`, `DotNetObjectReference`, `ReceiveTokenAsync`, `JwtCookieAuthTokenProvider`-Injection entfernt
- [x] `App.razor` — `<meta name="fm-xsrf-token">` für Nicht-Help-Seiten via `IAntiforgery.GetAndStoreTokens(HttpContext)` (`[CascadingParameter] HttpContext`)
- [x] `attachments.js` — Drop-Upload sendet `RequestVerificationToken`-Header aus dem Meta-Tag
- [x] `ProgramExtensions.RegisterAppServices` — `Configure<MassImportOptions>`, `Configure<AuthOptions>`, `AddSingleton<CircuitAuthTokenStore>`, `AddRateLimiter` mit Policy `auth-sensitive` (Fixed-Window, Partition `RemoteIpAddress`, konfigurierbar über `RateLimiting:AuthSensitive:*`)
- [x] `ProgramExtensions.ConfigureMiddleware` — `app.UseRateLimiter()` nach `IpBlockMiddleware`; `E2E:DisableHttpsRedirection` + Fault-Injection-Block jeweils in `IsDevelopment()`-Guard; globaler Security-Header-Block ersetzt `/help`-CSP-Block (Help-Pfade behalten `HelpSecurityPolicy.ContentSecurityPolicy`); `UseAntiforgery()` hinter `UseAuthorization()` verschoben; `CookieCsrfProtectionMiddleware` danach registriert; `EnsureAdminRole` unverändert
- [x] `appsettings.json` — `AllowedHosts` → `localhost;127.0.0.1;[::1]`; neue Sections `MassImport` und `Auth`
- [x] `appsettings.Production.json` — `"DetailedErrors"` entfernt; `AllowedHosts` = `localhost` (Platzhalter gemäß Offenem Punkt 2)

### Testinfrastruktur und Tests

- [x] `TestWebApplicationFactory` — Override `Auth:AllowSelfRegistration=true` + `RateLimiting:AuthSensitive:PermitLimit=100000`; neue Mechanismen `HostEnvironment` und `ConfigurationOverrides`
- [x] `PlaywrightWebAppFixture` — Server-Env `Auth__AllowSelfRegistration=true` + `RateLimiting__AuthSensitive__PermitLimit=100000`
- [x] `UserAuthServiceTests` — `RegisterAsync_ConcurrentCalls_OnlyOneBecomesAdmin`, `RegisterAsync_RevokesAdmin_WhenOtherUserAppearsBeforeRecheck`, drei Gate-Tests; Bestandstests über `AuthOptions { AllowSelfRegistration = true }`-Default abgesichert
- [x] `ExportFileHelperTests` (neu) — Formel-Präfix-Theory, Quoting-/Newline-Neutralisierung
- [x] `StatementDraftsControllerTests` — drei Limit-Negativtests + Ctor-Parameter; `UploadLimitAttributeTests` (neu) mit Filter-Test und Attribut-Reflexion für alle umgestellten Endpunkte
- [x] `BackupServiceTests` — `UploadAsync_SameFileNameTwoUsers_BothSucceed`, `OpenDownloadAsync_StoragePathTraversal_IsRejected`; `SchemaPatcherBackupMigrationTests` (neu) für Altbestand-Migration inkl. Kollision
- [x] `SecurityHeadersTests` (Integration, neu) — globale Header, Help-CSP-Vorrang, kein `upgrade-insecure-requests`
- [x] `CookieCsrfProtectionTests` (Integration, neu) — 403 ohne Token, Bearer unberührt, mit Token erlaubt, anonymer Endpunkt unberührt
- [x] `AuthRateLimitingTests` (Integration, neu) — `register`/`users/exists` → 429
- [x] `E2ESwitchesProductionTests` (Integration, neu) — `E2E:*`-Schlüssel unter `Production` wirkungslos
- [x] `JwtCookieAuthTokenProviderTests_Circuit` — auf `PersistedTokenKey`/Registry umgeschrieben (RoundTrip, Single-Use, Unknown-Key, Expired, Registry-Adoption, Fremd-Subject-Ignoranz); `JwtCookieAuthTokenProviderTests` inkl. `PrimeCache_PublishesTokenToRegistry` und neuem Ctor-Parameter
- [x] `ApiClientAuthTests` — Keepalive-Tests assertieren Abwesenheit der `X-Auth-Token*`-Header; `AcceptBrowserToken`-Aufruf durch Registry-basierten Sync ersetzt
- [x] `JwtRenewalTimingTests`, `BackupServiceFullExportTests` — angepasst
- [x] E2E-Keepalive/Circuit-Session-Abdeckung — durch bestehenden `SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` (Circuit-API-Aufrufe nach Cookie-Rotation; Token-Lesung via Cookie, nicht via Header)

## Offene Aufgaben

- [ ] `RegistrationGatePlaywrightTests` (Plan: `FinanceManager.Tests.E2E/Tests/Auth/RegistrationGatePlaywrightTests.cs`, #456) — fehlt vollständig: kein E2E-Test für „`/register` leitet zu `/login` um / API lehnt Zweitregistrierung ab" bei `AllowSelfRegistration=false`; Service-Gate ist nur unit- und integrationsseitig abgedeckt
- [ ] E2E-Regressionslauf der Bestandssuiten (`HomeMassImportPlaywrightTests`, `CollectionAccountImportPlaywrightTests`, `BudgetReportDemoDataE2ETests`, `StatementDraftExportE2ETests`, `AccountsOverviewStatisticsPlaywrightTests`) — im Working Tree nicht nachweisbar; Ausführung steht noch aus
- [ ] `FinanceManager.Web/Infrastructure/Backups/BackupUploadSizeLimitAttribute.cs` — existiert im Working Tree, wird aber von `.gitignore`-Regel `Backup*/` (Zeile ~274) verschluckt und ist nicht committbar → `[BackupUploadSizeLimit]` auf `BackupsController.UploadAsync` würde auf frischem Checkout den Build brechen; `.gitignore` anpassen oder Datei per `git add -f` aufnehmen (betrifft die über den Plan hinausgehende Umsetzung von Offenem Punkt 6)

## Hinweise

- `CsvEscape`: Ein führendes `\r` kann nach der Newline-Neutralisierung (`.Replace("\r", "")`) nicht mehr auftreten — die OWASP-CR-Prävention ist implizit abgedeckt; die Theory führt `\r` nicht als eigenen `InlineData`-Fall (funktional ausreichend).
- `AllowedHosts` in `appsettings.Production.json` steht auf `localhost` — bewusster Platzhalter gemäß Offenem Punkt 2 des Plans; deployment-spezifische Hostnamen (inkl. LAN-Hostname/-IP für Direct-HTTP) beim Deployment per Umgebungsvariable setzen.
- `BackupsController.UploadAsync` wurde über den Plan hinaus auf `[BackupUploadSizeLimit]` umgestellt (setzt Offenen Punkt 6 des Plans um: `Backups:Security:MaxUploadBytes` statt 1-GB-Limit) — inhaltlich sinnvoll, aber siehe offene Aufgabe zur git-ignorierten Attributklasse.
- `TestWebApplicationFactory` erhielt zusätzlich `HostEnvironment` + `ConfigurationOverrides` (nicht im Plan, erforderlich für die neuen Rate-Limit- und Production-Tests).
- `FinanceManager.Infrastructure.csproj` ergänzte `InternalsVisibleTo` für `FinanceManager.Tests` (für `ExportFileHelperTests` auf interne `ExportFileHelper` — plangemäß notwendig).
