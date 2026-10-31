# Umsetzungsplan: Security-Fixes #450–#461

## Übersicht

Behebung der zwölf verbleibenden Security-Findings des Audits in einem Durchgang: atomare Erstbenutzer-Admin-Zuweisung (#450), Request-Body-Limits für Mass-Import und drei weitere `long.MaxValue`-Stellen (#451), benutzerisolierte Backup-Ablage inkl. Altbestand-Migration (#452), globale Security-Header (#453), `DetailedErrors` aus Production (#454), Entfernung der JWT-Exposition über `X-Auth-Token`-Header (#455) und Prerender-HTML (#460), Einschränkung der Selbstregistrierung + Rate-Limiting (#456), CSV-Formel-Escaping (#457), `AllowedHosts`-Einschränkung (#458), CSRF-/Antiforgery-Härtung (#459) und Entfernung produktiv schaltbarer E2E-Schalter (#461). Legitime Flüsse (Erstbenutzer-Registrierung, Massenimport, Backup/Restore, Export, Token-Erneuerung, Keepalive, Drag-and-Drop-Uploads) sowie Direct-HTTP im LAN und HTTPS hinter Reverse-Proxy müssen erhalten bleiben.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Atomare Erstbenutzer-Entscheidung (#450) | Statisches `SemaphoreSlim` in `UserAuthService` um Prüfung → `CreateAsync` → `IsAdmin`-Persistenz → `AddToRoleAsync` → Demo-Data-Entscheidung, plus verbindlichem Re-Check nach dem Einfügen (kein anderer Benutzer/Admin vorhanden; sonst `SetAdmin(false)` + keine Rolle) | Eindeutiger Index auf `IsAdmin` ist ungeeignet: `UserAdminService.CreateAsync`/`AdminController.CreateUserAsync` dürfen legitim weitere Admins anlegen. Serializable-Transaktion bringt unter SQLite keinen Zusatznutzen (single-process DB) und erschwert InMemory-Tests. `EnsureAdminRole`-Startsync bleibt als Drift-Korrektur bestehen, kann aber durch Re-Check+Strip kein Race-`IsAdmin` mehr verstärken. |
| Registrierungs-Gate (#456) | Konfigflag `Auth:AllowSelfRegistration` (Default `false`), Regel `isFirst \|\| AllowSelfRegistration`, geprüft in `UserAuthService.RegisterAsync` unter demselben Semaphore wie die Erstbenutzer-Entscheidung | Erstbenutzer-Bootstrap muss immer funktionieren; Admin-Anlagepfad (`AdminController`) bleibt für weitere Konten. Gate im Service (nicht Controller) verhindert Umgehung und ist atomar mit `isFirst`. Fehlersemantik bleibt vereinheitlicht (409). |
| Rate-Limiting anonymer Endpunkte (#456) | `AddRateLimiter` (Framework-Boardmittel) mit benannter Policy `auth-sensitive` (Fixed-Window, Partition `RemoteIpAddress`), `[EnableRateLimiting]` auf `AuthController.RegisterAsync` und `UsersController.HasAnyUsersAsync`, `app.UseRateLimiter()` früh in der Pipeline | `IIpBlockService` zählt nur Login-Failures, kein generelles Rate-Limiting; Boardmittel ist minimal-invasiv, liefert 429 (bereits als Response-Deklaration dokumentiert) und hält `users/exists` anonym erreichbar (Login-/Register-Umleitung). |
| Mass-Import-Limit (#451) | Neue `MassImportOptions` + `MassImportRequestSizeLimitAttribute` (IFilterFactory-Ressourcenfilter nach Muster `AttachmentUploadSizeLimitAttribute`) setzt `IHttpMaxRequestBodySizeFeature` zur Laufzeit; applikative Prüfung `MaxFiles`/`MaxBytesPerFile`/`MaxTotalContentBytes` im Controller → 400 | `[RequestSizeLimit]` akzeptiert nur Konstanten; das bestehende Filter-Muster erlaubt konfigurierbare Limits. Applikative Checks fangen übergroße JSON-Payloads unterhalb des Body-Limits ab. |
| Übrige drei `long.MaxValue`-Stellen (#451) | `[RequestSizeLimit(long.MaxValue)]` durch `[AttachmentUploadSizeLimit]` ersetzen (`SavingsPlansController.UploadSymbolAsync`, `SecuritiesController.UploadSymbolAsync`, `SecuritiesController.ImportPricesAsync`) | Alle drei sind Multipart-IFormFile-Uploads; zwei gehen in `_attachments.UploadAsync` (identische Grenze wie `AttachmentsController.UploadAsync`), der Preisimport-CSV ist dieselbe Upload-Klasse. `AttachmentUploadOptions.NormalizedMaxSizeBytes` (Default 10 MB) ist fachlich ausreichend. |
| Backup-Isolation (#452) | Benutzer-Unterverzeichnis `backups/{userId:N}` unter `GetRoot()`; `StoragePath` bleibt schlichter Dateiname (kein Schema-Change); Uploads erhalten servergenerierten `StoragePath` (`upload-{guid}.zip`), `FileName` bleibt Anzeige-Original; `CreateAsync`-Name `backup-yyyyMMddHHmmss-{8hex}.zip`; zentrale `ResolveStoragePath` mit Traversal-Guard (nur Plain-Filename) | DB-Record trägt `OwnerUserId` — Auflösung über den Record ist nicht client-manipulierbar; servergenerierte Namen eliminieren Kollision und Traversal vollständig. |
| Backup-Altbestand (#452) | Einmalige Startup-Migration als neuer Schritt in `SchemaPatcher.RunPostMigrationPatches`: Dateien flach unter `backups/` werden anhand der `BackupRecord.OwnerUserId` in die User-Verzeichnisse verschoben; Namenskollisionen → Umbenennung + `StoragePath`-Update; Fehler werden geloggt und blockieren den Start nicht | `SchemaPatcher` ist der etablierte Hook für nicht-migrationsfähige Laufzeit-Korrekturen; keine Lazy-Fallback-Auflösung (würde den unsicheren Alt-Pfad dauerhaft offen halten). |
| Globale Security-Header (#453) | Neue statische `SecurityHeadersPolicy` (analog `HelpSecurityPolicy`) + ein Header-Middleware-Block in `ConfigureMiddleware`: globale Header immer; auf `/help`-Pfaden gewinnt die strengere `HelpSecurityPolicy.ContentSecurityPolicy` | Ein Setzpunkt vermeidet Header-Konflikte; Help-Referenz-CSP bleibt unverändert streng. Kein `upgrade-insecure-requests` (würde Direct-HTTP im LAN brechen), kein HSTS-Change. `script-src`/`style-src 'unsafe-inline'` nötig wegen `<ImportMap />`-Inline-Skript und Inline-`style`-Attributen in Razor-Komponenten. |
| Tokenfluss Circuit (#455/#460) | Neuer Singleton `CircuitAuthTokenStore` (`IMemoryCache`): (a) Einmal-Handoff Prerender→Circuit über Zufalls-Key statt JWT (`[PersistentState] PersistedTokenKey`, single-use, kurze TTL); (b) benutzerbezogene Latest-Token-Registry (`SetCurrent`/`TryGetCurrent`), beschrieben aus `JwtCookieAuthTokenProvider.PrimeCache` (alle bestehenden Aufrufstellen: `AuthController`, `JwtRefreshMiddleware`), gelesen in `GetAccessTokenAsync` im Circuit-Zweig | Der Token verlässt den Server nie (kein Header, kein HTML, kein JS-Argument). Handoff-Key ist ein unbrauchbarer Zufalls-GUID; Registry-Sync ersetzt `AcceptBrowserToken` semantisch äquivalent (gleicher Subject, nur neuere Tokens). Reine Cookie-Lösung scheidet aus: Circuit hat keinen `HttpContext`. |
| Keepalive (#455) | `SetTokenResponseHeaders` und Header-Konstanten entfallen komplett; `financeManager.js` pingt weiter `/api/auth/keepalive` (löst Cookie-Rotation aus), liest aber keine Header mehr; `ReceiveTokenAsync`/`setTokenReceiver`/`AcceptBrowserToken` werden entfernt; `Cache-Control: no-store` bleibt auf der Keepalive-Response | Keepalive dient dann nur noch der serverseitigen Cookie-Rotation + Registry-Befüllung; Circuit-Sync läuft serverseitig über die Registry. |
| CSRF-Schutz (#459) | `UseAntiforgery()` hinter `UseAuthorization()` verschieben; neue `CookieCsrfProtectionMiddleware` (nach `UseAntiforgery`): unsafe Methoden auf Endpunkten mit `IAuthorizeData`-Metadaten und cookie-authentifizierter Anfrage (kein `Authorization: Bearer`-Header) → `IAntiforgery.ValidateRequestAsync`, sonst 403; Token-Ausgabe via `<meta name="fm-xsrf-token">` in `App.razor` (`IAntiforgery.GetAndStoreTokens(HttpContext)`); `attachments.js` sendet `RequestVerificationToken`-Header | Echter Antiforgery-Nachweis statt nur SameSite/Header-Heuristik; Cookie `FinanceManager.Antiforgery` ist bereits konfiguriert (`SecurePolicy = SameAsRequest` → funktioniert unter LAN-HTTP und Proxy-HTTPS). Bearer-Clients (Circuit-HttpClient, API-/E2E-Tests) sind unberührt; Login/Logout/Keepalive sind anonyme bzw. sichere Methoden. |
| E2E-Schalter (#461) | Beide `E2E:*`-Auswertungen (`AccountStatisticsFaultInjectionEnabled`/`FaultFile`, `DisableHttpsRedirection`) bleiben in `ConfigureMiddleware`, werden aber in einen `app.Environment.IsDevelopment()`-Guard gehüllt | Minimal-invasive Umkehr des Findings: produktiv sind die Schlüssel wirkungslos; `PlaywrightWebAppFixture` läuft ohnehin mit `ASPNETCORE_ENVIRONMENT=Development` → Testinfrastruktur unverändert. |

## Programmabläufe

### Atomare Erstbenutzer-Registrierung (#450)

1. `AuthController.RegisterAsync` ruft `IUserAuthService.RegisterAsync` auf.
2. `UserAuthService.RegisterAsync` betritt das statische `SemaphoreSlim` (gesamte Erstbenutzer-Sequenz unter Sperre).
3. Duplikat-Prüfung `AnyAsync(u => u.UserName == ...)` wie bisher.
4. `isFirst = !await _db.Users.AnyAsync(ct)` — nun unter Sperre atomar.
5. Registrierungs-Gate (#456): `if (!isFirst && !_authOptions.AllowSelfRegistration)` → `Result.Fail` → Controller mappt auf 409.
6. `new User(..., isFirst)` → `_userManager.CreateAsync` → manuelles Tracking in `_db.Users` wie bisher.
7. **Re-Check:** `await _db.Users.AnyAsync(u => u.Id != user.Id)` — falls inzwischen ein anderer Benutzer existiert (bypassender Schreiber), `user.SetAdmin(false)` persistieren und `isFirst` auf `false` setzen; keine Admin-Rolle, kein Demo-Data.
8. Bei `isFirst`: `Admin`-Rolle sicherstellen + `AddToRoleAsync` (weiterhin non-fatal, `EnsureAdminRole` korrigiert Restrisiko beim nächsten Start).
9. Self-Contact + Demo-Data-Enqueue über `ShouldCreateDemoDataForFirstUser` an die **nach Re-Check gültige** `isFirst`-Entscheidung gebunden.
10. Token via `IsInRoleAsync` + `_jwt.CreateToken`; Semaphore verlassen.

Beteiligte Klassen/Komponenten: `UserAuthService`, `User`, `RoleManager`/`UserManager`, `IBackgroundTaskManager`, `ProgramExtensions.EnsureAdminRole` (unverändert), `AuthController`

### Registrierungs-Gate & Rate-Limiting (#456)

1. `POST /api/auth/register` → Rate-Limiter-Policy `auth-sensitive` (Partition `RemoteIpAddress`, Fixed-Window); Überschreitung → 429.
2. Ansonsten wie oben: Gate in `UserAuthService.RegisterAsync` Schritt 5.
3. `GET /api/users/exists` bleibt `[AllowAnonymous]`, trägt zusätzlich `[EnableRateLimiting("auth-sensitive")]` → 429 bei Missbrauch, `AnyUsersResponse` unverändert.
4. `Register.razor.OnInitializedAsync`: `_isFirstUser` wie bisher; zusätzlich `IOptions<AuthOptions>` lesen — ist `!_isFirstUser && !AllowSelfRegistration`, Redirect auf `/login`.
5. `AuthRedirect.CheckRedirectAsync` leitet weiterhin nur zu `/register`, wenn **keine** Benutzer existieren (unverändert konsistent zum Gate).
6. Admin-Anlagepfad `AdminController.CreateUserAsync`/`IUserAdminService.CreateAsync` bleibt der Weg für Folgekonten.

Beteiligte Klassen/Komponenten: `AuthOptions`, `UserAuthService`, `AuthController`, `UsersController`, `Register.razor`, `AuthRedirect.razor`, `AddRateLimiter`/`UseRateLimiter`

### Mass-Import mit Request-Limits (#451)

1. `POST api/statementdrafts/mass-import` → `MassImportRequestSizeLimitFilter.OnResourceExecuting` setzt `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize = MassImport:MaxRequestBytes` vor dem Body-Parsing (Überschreitung → 413).
2. `ProcessMassImportAsync`: `request.Files == null || Count == 0` → 400 (bestehend).
3. Neu: `Files.Count > MaxFiles` → 400 `Err_MassImport_TooManyFiles`; `Files.Any(f => f.Content.Length > MaxBytesPerFile)` → 400 `Err_MassImport_FileTooLarge`; `Σ Content.Length > MaxTotalContentBytes` → 400 `Err_MassImport_BatchTooLarge`.
4. Delegation an `IMassImportOrchestrator.ProcessAsync` unverändert.

Beteiligte Klassen/Komponenten: `StatementDraftsController`, `MassImportRequestSizeLimitAttribute`/`-Filter`, `MassImportOptions`, `MassImportBatchRequestDto`, `IMassImportOrchestrator`

### Backup-Ablage pro Benutzer (#452)

1. `GetRoot(userId)` liefert `<ContentRoot>/backups/{userId:N}` und legt es an.
2. `CreateAsync`: Dateiname `backup-yyyyMMddHHmmss-{8hex}.zip` im User-Verzeichnis; `StoragePath` = Dateiname.
3. `UploadAsync`: `FileName` = `Path.GetFileName(fileName)` (Anzeige + Duplikat-Prüfung + Confirmation-Match); `StoragePath` = servergeneriert `upload-{Guid:N}.zip` im User-Verzeichnis.
4. `DeleteAsync`/`OpenDownloadAsync`/`ApplyAsync`: `ResolveStoragePath(rec)` — validiert `StoragePath` als Plain-Filename (kein Separator, kein `..`, sonst verworfen) und kombiniert `GetRoot(rec.OwnerUserId)` + `StoragePath`.
5. `ListAsync`/`GetAsync` unverändert (Metadaten bereits owner-isoliert).
6. `BackupsController` unverändert (ruft weiter `IBackupService`).

Beteiligte Klassen/Komponenten: `BackupService`, `BackupRecord`, `BackupsController`

### Backup-Altbestand-Migration (#452)

1. `ApplyMigrationsAndSeed` → `SchemaPatcher.RunPostMigrationPatches` → neuer Schritt `MigrateBackupStorageAsync`.
2. Für jeden `BackupRecord`: existiert `<ContentRoot>/backups/{StoragePath}` flach → Ziel `backups/{OwnerUserId:N}/{StoragePath}` anlegen.
3. Ziel-Kollision (Datei existiert bereits): neuen eindeutigen `StoragePath` erzeugen, physische Datei umbenennen, Record aktualisieren.
4. Fehlende Dateien und Fehler pro Record loggen; Schritt ist idempotent und non-fatal.

Beteiligte Klassen/Komponenten: `SchemaPatcher`, `AppDbContext` (`Backups`), `IHostEnvironment`

### Globale Security-Header (#453)

1. Neuer Middleware-Block in `ConfigureMiddleware` (vor `UseStaticFiles`, an der Stelle des bisherigen `/help`-CSP-Blocks, den er ersetzt).
2. Jede Response: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: SAMEORIGIN`, `Permissions-Policy` (restriktiv), `Content-Security-Policy` aus `SecurityHeadersPolicy.ContentSecurityPolicy`.
3. `HelpSecurityPolicy.IsHelpPath(path)` → stattdessen `HelpSecurityPolicy.ContentSecurityPolicy` (strengere Policy gewinnt).

Beteiligte Klassen/Komponenten: `SecurityHeadersPolicy`, `HelpSecurityPolicy`, `ProgramExtensions.ConfigureMiddleware`

### Tokenfluss Prerender→Circuit & Keepalive (#455/#460)

1. **Prerender:** `JwtCookieAuthTokenProvider.GetAccessTokenAsync` läuft mit `HttpContext` (Cookie-Zweig) → `_cachedToken` gefüllt. Beim Persistieren liefert `PersistedTokenKey`-Getter `CircuitAuthTokenStore.StoreForHandoff(token, expiry)` → Zufalls-Key wird ins HTML serialisiert (kein JWT).
2. **Circuit-Start:** `PersistedTokenKey`-Setter → `TryTakeHandoff(key)` → Token serverseitig validieren + `Cache` (+ `SetCurrent` in Registry).
3. **Request mit Auth:** `JwtRefreshMiddleware` validiert Token → `PrimeCache` (schreibt lokal + `CircuitAuthTokenStore.SetCurrent(userId, …)`); bei Renewal: `IJwtRefreshService.RefreshAsync` → neues HttpOnly-Cookie + `PrimeCache` → Registry aktualisiert. **Keine** `X-Auth-Token*`-Header mehr.
4. **Keepalive (`GET /api/auth/keepalive`):** `financeManager.js` fetch mit `credentials:"include"` → Middleware rotiert Cookie bei Bedarf + Registry-Eintrag → Response `204` mit `Cache-Control: no-store`, ohne Token-Header; JS ruft keinen `tokenReceiver` mehr auf.
5. **Circuit API-Aufruf:** `GetAccessTokenAsync` ohne `HttpContext` → `_cachedToken` gültig → zurückgeben; abgelaufen/innerhalb Renewal-Window → `TryGetCurrent(subject)` → neueres Token adoptieren (`Cache`) → zurückgeben; nichts gefunden → `null` (→ 401-Flow via `AuthenticationRequired` wie bisher).
6. `AuthRedirect.razor` registriert keinen `DotNetObjectReference`-Receiver mehr; `AcceptBrowserToken` entfällt.

Beteiligte Klassen/Komponenten: `CircuitAuthTokenStore`, `JwtCookieAuthTokenProvider`, `JwtRefreshMiddleware`, `AuthController` (`PrimeTokenCache`), `financeManager.js`, `AuthRedirect.razor`, `AuthKeepaliveController` (unverändert), `AuthenticatedHttpClientHandler` (unverändert)

### CSRF-/Antiforgery-Schutz (#459)

1. `AddAntiforgery` bleibt (Cookie `FinanceManager.Antiforgery`, `SecurePolicy = SameAsRequest` → LAN-HTTP und Proxy-HTTPS funktionieren); `HeaderName` auf `RequestVerificationToken` belassen (Default).
2. `App.razor` rendert beim Prerender `<meta name="fm-xsrf-token" content="{token}">` via `IAntiforgery.GetAndStoreTokens(HttpContext)`.
3. Pipeline: `UseAuthentication` → `UseAuthorization` → `UseAntiforgery()` (neue, korrekte Position) → `CookieCsrfProtectionMiddleware`.
4. Middleware: Request ist unsafe (POST/PUT/DELETE/PATCH), Endpoint trägt `IAuthorizeData`, kein `Authorization: Bearer`-Header → `IAntiforgery.ValidateRequestAsync`; Fehler → 403.
5. `attachments.js` liest das Meta-Token und sendet `RequestVerificationToken`-Header beim Drag-and-Drop-Upload.
6. Bearer-authentifizierte Clients (`AuthenticatedHttpClientHandler`, API-Tests), anonyme Endpunkte (`/api/auth/login`, `/api/auth/logout`, `/api/auth/register`) und GET (`keepalive`, `fmAuthIsAuthenticated`) unberührt. Blazor-SSR-Formposts erhalten Antiforgery bereits framework-seitig.

Beteiligte Klassen/Komponenten: `CookieCsrfProtectionMiddleware`, `App.razor`, `attachments.js`, `ProgramExtensions.ConfigureMiddleware`, `JwtBearerEvents.OnMessageReceived` (unverändert)

### CSV-Export mit Formel-Escaping (#457)

1. `StatementDraftExportService`/`PostingExportService` erzeugen Spalten wie bisher; String-Spalten laufen durch `ExportFileHelper.CsvEscape`.
2. `CsvEscape`: Newline-Neutralisierung wie bisher; beginnt der bereinigte Wert mit `=`, `+`, `-`, `@`, Tab oder CR → `'` voranstellen; danach Quoting wie bisher.
3. Numerische/Datums-Spalten (ungequotet, aus `ToString(InvariantCulture)`/`decimal`/`int`) können kein `=`/`@` erzeugen → unverändert; ein führendes `-` in Text-Spalten wird ebenfalls entschärft (OWASP-konform, Anzeigewert in Excel bleibt als Text lesbar).
4. XLSX-Pfade (`WriteXlsx`/`CreateRow`, `CellValues.String`/SharedString) bleiben unverändert — literale Zellen, nicht formel-auswertbar.

Beteiligte Klassen/Komponenten: `ExportFileHelper`, `StatementDraftExportService`, `PostingExportService`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `FinanceManager.Web/Infrastructure/Auth/CircuitAuthTokenStore.cs` | Singleton-Service | Einmal-Handoff-Store (`StoreForHandoff`/`TryTakeHandoff`, single-use, kurze TTL) + benutzerbezogene Latest-Token-Registry (`SetCurrent`/`TryGetCurrent`) auf `IMemoryCache`-Basis (#455/#460) |
| `FinanceManager.Web/Infrastructure/MassImport/MassImportOptions.cs` | Options-Klasse | `MaxRequestBytes`, `MaxFiles`, `MaxBytesPerFile`, `MaxTotalContentBytes` (Section `MassImport`) (#451) |
| `FinanceManager.Web/Infrastructure/MassImport/MassImportRequestSizeLimitAttribute.cs` (+ interner `MassImportRequestSizeLimitFilter`) | MVC-Filter (`IFilterFactory`, `IResourceFilter`) | Setzt `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` aus `MassImportOptions` vor Body-Parsing; Muster von `AttachmentUploadSizeLimitAttribute` (#451) |
| `FinanceManager.Infrastructure/Auth/AuthOptions.cs` | Options-Klasse | `AllowSelfRegistration` (`bool`, Default `false`), Section `Auth` (#456) |
| `FinanceManager.Web/Services/SecurityHeadersPolicy.cs` | statische Klasse | `ContentSecurityPolicy`-Konstante + `Apply(HttpResponse)` für globale Header (#453) |
| `FinanceManager.Web/Infrastructure/CookieCsrfProtectionMiddleware.cs` | Middleware | Antiforgery-Validierung unsafe, cookie-authentifizierter API-Requests (#459) |

## Änderungen an bestehenden Klassen

### `UserAuthService` (Service)

- **Neue Member:** `private static readonly SemaphoreSlim s_registrationGate = new(1,1)` — prozessweite Sperre für Erstbenutzer-Entscheidung; `IOptions<AuthOptions>? _authOptions` (optionaler Ctor-Parameter, Legacy-Ctor → `AuthOptions`-Default).
- **Geänderte Methoden:** `RegisterAsync` — Sequenz Duplikat-Prüfung → `isFirst` → Gate → Create → Re-Check → Rollen/Demo-Data innerhalb des Semaphore; `isFirst` nach Re-Check korrigierbar (`SetAdmin(false)`); `ShouldCreateDemoDataForFirstUser` wird mit der korrigierten `isFirst` aufgerufen.

### `AuthOptions` (Options) — siehe Neue Klassen

### `AuthController` (Controller)

- **Geänderte Methoden:** `RegisterAsync` — `[EnableRateLimiting("auth-sensitive")]` hinzu; Fehler-Mapping unverändert (Gate-Fail → 409).

### `UsersController` (Controller)

- **Geänderte Methoden:** `HasAnyUsersAsync` — `[EnableRateLimiting("auth-sensitive")]` hinzu; `[AllowAnonymous]` bleibt.

### `Register.razor` (Komponente)

- **Änderung:** `OnInitializedAsync` — zusätzlich `IOptions<AuthOptions>` injizieren; bei `!_isFirstUser && !AllowSelfRegistration` → `Nav.NavigateTo("/login")`.

### `StatementDraftsController` (Controller)

- **Ctor:** neuer optionaler Parameter `IOptions<MassImportOptions>?` (Bestandstests-Kompatibilität).
- **Geänderte Methoden:** `ProcessMassImportAsync` — `[RequestSizeLimit(long.MaxValue)]` durch `[MassImportRequestSizeLimit]` ersetzen; applikative Prüfungen `MaxFiles`/`MaxBytesPerFile`/`MaxTotalContentBytes` → 400 `ApiErrorDto` (`Err_MassImport_*`).

### `SavingsPlansController` (Controller)

- **Geänderte Methoden:** `UploadSymbolAsync` — `[RequestSizeLimit(long.MaxValue)]` → `[AttachmentUploadSizeLimit]` (gleiche `AttachmentUploadOptions`-Grenze wie `AttachmentsController.UploadAsync`).

### `SecuritiesController` (Controller)

- **Geänderte Methoden:** `UploadSymbolAsync` — `[RequestSizeLimit(long.MaxValue)]` → `[AttachmentUploadSizeLimit]`; `ImportPricesAsync` — `[RequestSizeLimit(long.MaxValue)]` → `[AttachmentUploadSizeLimit]` (Multipart-Upload derselben Grenzklasse).

### `BackupService` (Service)

- **Geänderte Methoden:** `GetRoot()` → `GetRoot(Guid userId)` (User-Unterverzeichnis `backups/{userId:N}`); `CreateAsync` — Dateiname mit `{8hex}`-Suffix; `UploadAsync` — `StoragePath` servergeneriert `upload-{Guid:N}.zip`, `FileName` bleibt Client-Name; `DeleteAsync`/`OpenDownloadAsync`/`ApplyAsync` — `StoragePath`-Auflösung über neue `ResolveStoragePath(BackupRecord)`.
- **Neue Methoden:** `ResolveStoragePath(BackupRecord rec)` (privat) — Traversal-Guard + Pfadkombination `GetRoot(rec.OwnerUserId)` + `StoragePath`.

### `SchemaPatcher` (Hilfsklasse)

- **Neue Methoden:** `MigrateBackupStorageAsync(IServiceProvider, AppDbContext, ILogger)` (privat) — verschiebt flache Alt-Dateien nach `backups/{OwnerUserId:N}/`, behandelt Kollisionen via Umbenennung + `StoragePath`-Update; Aufruf in `RunPostMigrationPatchesAsync` nach `EnsureAspNetUsersColumnsAsync`.

### `ExportFileHelper` (interne Hilfsklasse)

- **Geänderte Methoden:** `CsvEscape` — Formel-Präfix-Entschärfung (`=`, `+`, `-`, `@`, `\t`, `\r` → führendes `'`).

### `JwtRefreshMiddleware` (Middleware)

- **Geänderte Methoden:** `InvokeAsync` — Keepalive-Pfad ohne Renewal: `SetTokenResponseHeaders` durch `Cache-Control: no-store` auf der Response ersetzen; Refresh-Pfad: Cookie + `PrimeCache` bleiben, `SetTokenResponseHeaders`-Aufruf entfällt.
- **Entfernt:** `SetTokenResponseHeaders`, Konstanten `RefreshHeaderName`/`RefreshExpiresHeaderName`.

### `JwtCookieAuthTokenProvider` (Service)

- **Geänderte Member:** `[PersistentState] PersistedToken` → `[PersistentState] PersistedTokenKey` (Getter: `StoreForHandoff` bei vorhandenem `_cachedToken`; Setter: `TryTakeHandoff` → Validierung wie bisher → `Cache` + `SetCurrent`).
- **Geänderte Methoden:** `GetAccessTokenAsync` — Circuit-Zweig (`ctx == null`/`HasStarted`) konsultiert bei auslaufendem/fehlendem Cache-Token `TryGetCurrent(subject)` und adoptiert neuere Tokens; `PrimeCache` — schreibt zusätzlich `CircuitAuthTokenStore.SetCurrent(userId, token, expiry)`.
- **Entfernt:** `AcceptBrowserToken`.
- **Ctor:** zusätzlich `CircuitAuthTokenStore` (Singleton) injizieren.

### `financeManager.js` (Client-Skript)

- **Änderung:** Keepalive-`then`-Zweig liest keinen `X-Auth-Token`-Header mehr; `tokenReceiver`-Variable, `invokeMethodAsync("ReceiveTokenAsync")`-Aufruf und `fm.keepalive.setTokenReceiver` entfallen. Keepalive-Ping (Intervall, forcierte Pings, Timeout, `X-Requested-With`-Header) bleibt.

### `AuthRedirect.razor` (Komponente)

- **Entfernt:** `_tokenReceiver`, `DotNetObjectReference`-Erzeugung, `setTokenReceiver`-JS-Aufruf, `[JSInvokable] ReceiveTokenAsync`, `JwtCookieAuthTokenProvider`-Injection.

### `App.razor` (Komponente)

- **Änderung:** `<meta name="fm-xsrf-token">` im `<head>` für Nicht-Help-Seiten via `IAntiforgery.GetAndStoreTokens(HttpContext)` (`[CascadingParameter] HttpContext`).

### `attachments.js` (Client-Skript)

- **Änderung:** Drop-Upload-`fetch` sendet `RequestVerificationToken`-Header aus dem `fm-xsrf-token`-Meta-Tag.

### `ProgramExtensions` (Startup)

- **`RegisterAppServices`:** `Configure<MassImportOptions>(GetSection("MassImport"))`, `Configure<AuthOptions>(GetSection("Auth"))`, `AddRateLimiter` mit Policy `auth-sensitive`; `AddAntiforgery` unverändert; `RegisterPersistentService<JwtCookieAuthTokenProvider>` bleibt (persistiert jetzt den Handoff-Key).
- **`ConfigureMiddleware`:** `app.UseRateLimiter()` nach `IpBlockMiddleware`; `E2E:DisableHttpsRedirection` + Fault-Injection-Block in `app.Environment.IsDevelopment()`-Guard; `/help`-CSP-Block durch globalen Security-Header-Block ersetzen; `UseAntiforgery()` hinter `UseAuthorization()` verschieben; `CookieCsrfProtectionMiddleware` danach registrieren.
- **`EnsureAdminRole`:** unverändert (bleibt als Drift-Korrektur; #450 verhindert, dass ein Race-`IsAdmin` entsteht).

### `appsettings.json` / `appsettings.Production.json` (Konfiguration)

- `appsettings.json`: `AllowedHosts` `"*"` → `"localhost;127.0.0.1;[::1]"`; neue Sections `MassImport`, `Auth`.
- `appsettings.Production.json`: `"DetailedErrors": true` entfernen; `AllowedHosts` deployment-spezifisch setzen (siehe Offene Punkte).
- `appsettings.Development.json`: `DetailedErrors` bleibt.

### Test-Infrastruktur (bestehend)

- `TestWebApplicationFactory`: In-Memory-Override `Auth:AllowSelfRegistration = "true"` (hält ~25 Integrationstest-Registrierungen am Laufen); Rate-Limiter-Policy ggf. per Override erhöhen, damit Serientests nicht in 429 laufen.
- `PlaywrightWebAppFixture`: Server-Env `Auth__AllowSelfRegistration = "true"` (E2E-Registrierungen via `AuthGateway` bleiben möglich).

## Datenbankmigrationen

Keine. (Backup-Umstellung ist eine Dateisystem-Migration via `SchemaPatcher`; `BackupRecord.StoragePath` bleibt ein Dateiname; keine Schema-Änderung.)

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `MassImportBatchRequestDto.Files` | `Count >= 1` (bestehend) und `<= MassImport:MaxFiles` | 400 `Err_Invalid_File` bzw. `Err_MassImport_TooManyFiles` |
| `MassImportFileUploadDto.Content` | `Length <= MassImport:MaxBytesPerFile` | 400 `Err_MassImport_FileTooLarge` |
| Σ `Files[].Content.Length` | `<= MassImport:MaxTotalContentBytes` | 400 `Err_MassImport_BatchTooLarge` |
| JSON-Body `mass-import` | `<= MassImport:MaxRequestBytes` via `IHttpMaxRequestBodySizeFeature` | 413 |
| `BackupRecord.StoragePath` | Plain-Filename (kein Separator, kein `..`) | Record nicht auflösbar → NotFound/`false` |
| `RegisterRequest` (Service-Gate) | `isFirst \|\| Auth:AllowSelfRegistration` | `Result.Fail` → 409 (bestehende vereinheitlichte Semantik) |
| `register`/`users/exists` (Rate-Limit) | Fixed-Window pro `RemoteIpAddress` | 429 |
| Unsafe, cookie-authentifizierte API-Requests | `IAntiforgery.ValidateRequestAsync` (Header `RequestVerificationToken` + Cookie) | 403 |
| `Host`-Header | `AllowedHosts`-Whitelist (Kestrel) | 400 |
| CSV-Zellwert | führendes `=`/`+`/`-`/`@`/Tab/CR → `'` voranstellen | — (Entschärfung, kein Fehler) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AllowedHosts` (`appsettings.json`) | string | `localhost;127.0.0.1;[::1]` | Host-Header-Whitelist; lokale Direktaufrufe weiter möglich (#458) |
| `AllowedHosts` (`appsettings.Production.json`) | string | siehe Offene Punkte | Produktiv-Hostnamen (Domain + ggf. LAN-Hostname) (#458) |
| `DetailedErrors` (`appsettings.Production.json`) | — | entfernt (Default `false`) | Keine detaillierten Circuit-Fehler in Production (#454) |
| `MassImport:MaxRequestBytes` | long | `150000000` (≈143 MB) | JSON-Body-Limit mass-import, deckt Base64-Inflation von `MaxTotalContentBytes` ab (#451) |
| `MassImport:MaxFiles` | int | `50` | Max. Dateianzahl pro Batch (#451) |
| `MassImport:MaxBytesPerFile` | long | `10485760` (10 MB) | Max. Rohbytes pro Datei (#451) |
| `MassImport:MaxTotalContentBytes` | long | `104857600` (100 MB) | Max. Summe aller `Content`-Bytes (#451) |
| `Auth:AllowSelfRegistration` | bool | `false` | Selbstregistrierung nur für Erstbenutzer, sofern nicht aktiviert (#456) |
| `E2E:*` | — | — | Unverändert benannt; werden nur noch unter `IsDevelopment()` ausgewertet (#461) |

## Seiteneffekte und Risiken

- **Direct-HTTP im LAN / HTTPS hinter Reverse-Proxy (#449-Erkenntnis):** Globale CSP enthält bewusst kein `upgrade-insecure-requests`; `Cookie.SecurePolicy = SameAsRequest` und `Secure = Request.IsHttps` bleiben unverändert; `UseForwardedHeaders` bleibt erste Middleware. `AllowedHosts` darf produktiv den LAN-Hostnamen/IP-Zugriff nicht versehentlich sperren — deploymentspezifisch konfigurieren (Offener Punkt).
- **`EnsureAdminRole`-Wechselwirkung (#450):** Bleibt als legitimer Sync bestehen; ohne atomare Entscheidung + Re-Check würde sie Race-`IsAdmin`-Flags in echte Rollenmitgliedschaft überführen — der Re-Check muss `IsAdmin` des Verlierers zurücksetzen, **bevor** er persistiert wirkt.
- **Keepalive-Verhalten (#455):** Circuit-Token-Sync läuft nun serverseitig über `CircuitAuthTokenStore`; zwei Browser-Tabs/Circuits desselben Benutzers teilen den Registry-Eintrag (semantisch äquivalent zum bisherigen `AcceptBrowserToken`, das ebenfalls jedes neuere Same-Subject-Token akzeptierte). Ohne Browser-Aktivität keine Keepalive-Rotation → Token läuft ab → 401 → Login-Redirect wie bisher.
- **Registry ist prozessintern (#455/#460):** Bei App-Restart ist der Store leer — Circuit muss sich ohnehin neu verbinden; kein Funktionsverlust.
- **`Register.razor` bei gesperrter Registrierung:** Direktaufruf leitet auf `/login` um; `RegisterRequest` per API direkt → 409. `AuthRedirect` ändert sich nicht (führt nur bei leerer Usertabelle zu `/register`).
- **Integrationstests (#456):** ~25 `ApiClient*Tests`-Dateien registrieren Nicht-Erstbenutzer — `TestWebApplicationFactory` braucht `Auth:AllowSelfRegistration=true`-Override; E2E-Fixture `Auth__AllowSelfRegistration=true` (`AuthGateway.RegisterAsync`, `BudgetReportDemoDataE2ETests`).
- **Rate-Limiter:** Parallel-Testläufe teilen die In-Memory-Partition pro Host — Testfabrik sollte Limits per Override großzügig setzen, um 429-Flakes zu vermeiden.
- **`script-src 'unsafe-inline'` (#453):** Durch `<ImportMap />`-Inline-Skript und Inline-`style`-Attribute erforderlich; schwächt CSP gegen eingeschleuste Inline-Skripte — `script-src 'self'` würde Blazor-Start brechen. Alternative (Nonces/Hashes für dynamisch gerenderte Importmap) ist nicht stabil umsetzbar.
- **Mass-Import-Limits:** `MaxRequestBytes` muss Base64-Inflation (~4/3) berücksichtigen; E2E `HomeMassImportPlaywrightTests` nutzen reale Kontoauszug-Dateien (klein) — bleiben unter allen Defaults.
- **`[AttachmentUploadSizeLimit]` an drei Endpunkten:** Setzt auch `FormOptions.MultipartBodyLengthLimit` — konsistent zur globalen Normalisierung; Preis-CSVs >10 MB werden künftig abgelehnt (bisher unbegrenzt) — fachlich vertretbar.
- **Backup-Migration beim Start (#452):** Einmaliger Dateisystem-Umzug; bei Fehlern (fehlende Datei, IO-Fehler) loggt der Patcher und fährt fort — betroffener Record zeigt dann auf fehlende Datei (Download → NotFound, wie heute bei verlorener Datei).
- **Antiforgery-Meta-Tag (#459):** Wird nur für authentifizierte Prerenders sinnvoll befüllt; `GetAndStoreTokens` setzt/rotiert das `FinanceManager.Antiforgery`-Cookie — unter HTTP-Requests nicht `Secure`-markiert (`SameAsRequest`, beabsichtigt).
- **`BudgetReportExportService`:** Rein XLSX — keine Änderung nötig; veralteter Klassenkommentar („small CSV stream") kann im Zuge von #457 korrigiert werden.

## Umsetzungsreihenfolge

1. **Konfig-Fixes: `DetailedErrors` (#454) + `AllowedHosts` (#458)**
   - Voraussetzungen: Keine (offener Punkt zu Produktiv-Hostnamen klärt den `appsettings.Production.json`-Wert — als Platzhalter `localhost` dokumentieren, wenn ungeklärt).
   - Beschreibung: `"DetailedErrors"` aus `appsettings.Production.json` entfernen; `AllowedHosts` in `appsettings.json` auf `localhost;127.0.0.1;[::1]` und in `appsettings.Production.json` deployment-spezifisch setzen.

2. **CSV-Formel-Escaping (#457)**
   - Voraussetzungen: Keine.
   - Beschreibung: `ExportFileHelper.CsvEscape` um Formel-Präfix-Entschärfung erweitern; Unit-Tests für alle Präfixe; Kommentar in `BudgetReportExportService` korrigieren.

3. **Mass-Import- und Upload-Limits (#451)**
   - Voraussetzungen: `AttachmentUploadSizeLimitAttribute` (vorhanden).
   - Beschreibung: `MassImportOptions` + `MassImportRequestSizeLimitAttribute`/`-Filter` anlegen; `Configure<MassImportOptions>` + `appsettings`-Section; `ProcessMassImportAsync` umbauen (Filter + applikative Checks + Ctor-Parameter); drei Multipart-Stellen auf `[AttachmentUploadSizeLimit]` umstellen; Controller-Tests.

4. **Atomare Erstbenutzer-Entscheidung (#450)**
   - Voraussetzungen: Keine.
   - Beschreibung: `SemaphoreSlim` + Re-Check in `UserAuthService.RegisterAsync`; Race-/Regressionstests; `EnsureAdminRole` unverändert lassen.

5. **Registrierungs-Gate & Rate-Limiting (#456)**
   - Voraussetzungen: Schritt 4 (Gate nutzt denselben Semaphore-Block); `AddRateLimiter` (Framework).
   - Beschreibung: `AuthOptions` + Gate in `RegisterAsync`; `Configure<AuthOptions>`; `AddRateLimiter`+Policy+`UseRateLimiter`; `[EnableRateLimiting]` auf `RegisterAsync`/`HasAnyUsersAsync`; `Register.razor`-Redirect; `TestWebApplicationFactory`- und `PlaywrightWebAppFixture`-Overrides; Tests.

6. **Benutzerisolierte Backup-Ablage + Altbestand-Migration (#452)**
   - Voraussetzungen: `SchemaPatcher`-Hook (vorhanden).
   - Beschreibung: `GetRoot(userId)`, `ResolveStoragePath`, Dateinamen-Anpassungen in `BackupService`; `MigrateBackupStorageAsync` im `SchemaPatcher`; Cross-User- und Traversal-Tests.

7. **Globale Security-Header (#453)**
   - Voraussetzungen: `HelpSecurityPolicy` (vorhanden).
   - Beschreibung: `SecurityHeadersPolicy` anlegen; gemeinsamen Header-Middleware-Block in `ConfigureMiddleware` etablieren (ersetzt `/help`-Block); Integrationstests für Header + Help-CSP-Überschreibverhalten.

8. **E2E-Schalter auf Development beschränken (#461)**
   - Voraussetzungen: Keine.
   - Beschreibung: Beide `E2E:*`-Blöcke in `IsDevelopment()`-Guard hüllen; `PlaywrightWebAppFixture` unverändert (läuft Development); Verifikation, dass Produktiv-Config die Schlüssel ignoriert.

9. **Tokenfluss-Umbau (#455 + #460, gemeinsamer Block)**
   - Voraussetzungen: Keine (größter Eingriff, isoliert umsetzbar).
   - Beschreibung: `CircuitAuthTokenStore` anlegen + registrieren; `JwtCookieAuthTokenProvider` (`PersistedTokenKey`, Registry-Zugriff in `GetAccessTokenAsync`/`PrimeCache`, `AcceptBrowserToken` entfernen); `JwtRefreshMiddleware` (Header entfernen, `no-store` behalten); `financeManager.js` + `AuthRedirect.razor` bereinigen; Circuit-/Integrationstests umschreiben.

10. **CSRF-/Antiforgery-Härtung (#459)**
    - Voraussetzungen: Schritt 9 abgeschlossen (gleiche Dateiregionen: `ConfigureMiddleware`, `financeManager.js`/`App.razor` — Konfliktvermeidung).
    - Beschreibung: `UseAntiforgery()` hinter `UseAuthorization()`; `CookieCsrfProtectionMiddleware` anlegen + registrieren; `App.razor`-Meta-Tag; `attachments.js`-Header; Tests (403 ohne Token, Bearer unberührt).

11. **Gesamttest-Lauf und Regressionsprüfung**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: `dotnet build` + alle Testsuiten (`FinanceManager.Tests`, `Tests.Integration`, `Tests.Integration.ApiClient`, `Tests.E2E`); besondere Aufmerksamkeit: Mass-Import-E2E, Backup-Integrationstests, Keepalive-Tests, Help-CSP-Tests.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RegisterAsync_ConcurrentCalls_OnlyOneBecomesAdmin` | `UserAuthServiceTests` | Zwei parallele Registrierungen auf leerer DB → genau ein Admin (`IsAdmin` + Rolle), Verlierer ist normaler User |
| `RegisterAsync_RevokesAdmin_WhenOtherUserAppearsBeforeRecheck` | `UserAuthServiceTests` | Simulierter Re-Check-Fall → `SetAdmin(false)`, keine Rolle, kein Demo-Data |
| `RegisterAsync_Blocked_WhenNotFirstAndSelfRegistrationDisabled` / `..._Allowed_WhenFlagTrue` / `..._FirstUser_AlwaysAllowed` | `UserAuthServiceTests` | Gate-Semantik in Abhängigkeit von `AuthOptions` |
| `CsvEscape_FormulaPrefix_IsPrefixed` (Theory: `=`, `+`, `-`, `@`, `\t`, `\r`) | neue `ExportFileHelperTests` | Präfix `'` vor gefährlichen Zellwerten; unverändertes Quoting; negative Zahlen im Textkontext |
| `ProcessMassImportAsync_ShouldReturnBadRequest_WhenTooManyFiles/..._FileTooLarge/..._BatchTooLarge` | `StatementDraftsControllerTests` | Applikative Limit-Durchsetzung mit `IOptions<MassImportOptions>` |
| `MassImportRequestSizeLimit_SetsBodyLimitFromOptions` | neue `MassImportRequestSizeLimitTests` | Filter setzt `IHttpMaxRequestBodySizeFeature` aus Options (Muster `AttachmentsControllerTests`) |
| `UploadSymbolAsync`/`ImportPricesAsync` nutzen `AttachmentUploadSizeLimit`-Attribut | `SavingsPlansControllerTests`/`SecuritiesControllerTests` (Reflexion) | Kein `RequestSizeLimit(long.MaxValue)` mehr |
| `UploadAsync_SameFileNameTwoUsers_BothSucceed` | `BackupServiceTests` | Cross-User-Upload gleichen Namens überschreibt nicht |
| `OpenDownloadAsync`/`DeleteAsync`/`ApplyAsync` lösen nur User-Verzeichnis auf | `BackupServiceTests` | Isolation + `StoragePath`-Traversal-Reject |
| `MigrateBackupStorage_MovesLegacyFiles` | `SchemaPatcher`-Tests oder `BackupServiceTests` | Flache Alt-Datei landet im User-Verzeichnis; Kollision → Rename + Record-Update |
| `Responses_IncludeGlobalSecurityHeaders` / `HelpPaths_KeepStricterCsp` / `Csp_HasNoUpgradeInsecureRequests` | neue `SecurityHeadersTests` (Integration, `TestWebApplicationFactory`) | `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, CSP global; Help-Pfade behalten `HelpSecurityPolicy`-CSP |
| `CookieAuth_UnsafeRequest_WithoutToken_IsForbidden` / `Bearer_UnsafeRequest_WithoutToken_IsAllowed` / `CookieAuth_WithToken_IsAllowed` | neue `CookieCsrfProtectionTests` (Integration) | CSRF-Enforcement nur bei Cookie-Auth + `[Authorize]`-Endpunkt |
| `PersistedTokenKey_RoundTrip_PrimesCircuit` / `PersistedTokenKey_UnknownOrExpiredKey_YieldsNoToken` | `JwtCookieAuthTokenProviderTests_Circuit` | Handoff-Key-Flow (kein JWT im persistierten Wert) |
| `GetAccessTokenAsync_ExpiringCache_PullsNewerTokenFromRegistry` / `..._RegistryTokenOfOtherSubject_IsIgnored` | `JwtCookieAuthTokenProviderTests_Circuit` | Registry-Sync ersetzt `AcceptBrowserToken`-Semantik |
| `PrimeCache_PublishesTokenToRegistry` | `JwtCookieAuthTokenProviderTests` | `SetCurrent`-Seiteneffekt |
| `Register_RateLimited_Returns429` / `UsersExists_RateLimited_Returns429` | neue Integrationstests (`TestWebApplicationFactory` mit kleiner Window-Override) | `auth-sensitive`-Policy |
| `E2ESwitches_IgnoredInProduction` | neue Integrationstests (Factory mit `UseEnvironment("Production")`) | `E2E:AccountStatisticsFaultInjectionEnabled`/`DisableHttpsRedirection` produktiv wirkungslos |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UserAuthServiceTests` (Register-Tests, v. a. `..._ShouldNotQueueDemoData_WhenNotFirstUser`) | Gate Default `false` → Tests, die Zweitbenutzer registrieren, brauchen `IOptions<AuthOptions>` mit `AllowSelfRegistration = true` |
| `JwtCookieAuthTokenProviderTests_Circuit` (4× `AcceptBrowserToken_*`, `PersistedToken_*`, `GetAccessTokenAsync_CachedTokenInsideRenewalWindow_*`) | `AcceptBrowserToken`/`PersistedToken` entfallen → auf `PersistedTokenKey`/Registry-Tests umschreiben |
| `JwtCookieAuthTokenProviderTests` | Provider-Ctor erhält `CircuitAuthTokenStore`-Parameter |
| `ApiClientAuthTests` (`Keepalive_ActiveUserInteraction_*`, `Keepalive_CurrentCookieWithoutRotation_*`, `Keepalive_RevokedCurrentCookie_*`, `JwtRefreshMiddleware_ShouldNotLoopOnRejectedRefresh`) | `X-Auth-Token*`-Header-Assertions → assertiert Abwesenheit + Cookie-Rotation; `AcceptBrowserToken`-Aufruf → Registry-basierter Sync via `GetAccessTokenAsync` |
| `StatementDraftsControllerTests` (`Create`-Hilfsmethoden) | Ctor-Parameter `IOptions<MassImportOptions>` ergänzen |
| `UsersControllerTests_Authorization` | `[AllowAnonymous]` bleibt — Test unverändert grün; ggf. Assertion auf `[EnableRateLimiting]` ergänzen |
| `ApiClientBackupsTests`, `ApiClientBackupsWithDemoDataTests`, `BackupServiceTests`, `BackupServiceFullExportTests` | Verhalten transparent gleich; ggf. Hilfs-Pfadannahmen (flaches `backups/`) an User-Verzeichnis anpassen |
| `HelpSecurityMiddlewareTests` | Help-CSP bleibt bestehen; Assertions müssen mit neuem globalem Header-Block konsistent bleiben (keine Doppelsetzung) |
| `ApiClientUsersTests` (`Users_HasAnyAsync`-Aufrufe) | `users/exists` bleibt anonym erreichbar; bei 429-Policy-Override in Factory unverändert |
| ~25 `ApiClient*Tests`-Dateien mit `Auth_RegisterAsync` | Indirekt betroffen — über `TestWebApplicationFactory`-Override `Auth:AllowSelfRegistration=true` abgefangen; nur anpassen, wenn Override nicht reicht |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Gesperrte Selbstregistrierung: `/register` leitet zu `/login` um, API lehnt ab | neu: `FinanceManager.Tests.E2E/Tests/Auth/RegistrationGatePlaywrightTests.cs` | Bei vorhandenen Benutzern und `AllowSelfRegistration=false` keine Kontoanlage über UI/API |
| Erstbenutzer-Registrierung inkl. Demo-Daten bleibt funktional | `BudgetReportDemoDataE2ETests` (bestehend) | Happy Path Erstbenutzer-Registrierung (#450/#456) |
| Massenimport legitimer Dateien unter Limits | `HomeMassImportPlaywrightTests`, `CollectionAccountImportPlaywrightTests` (bestehend) | Happy Path Mass-Import (#451) |
| Statement-Export CSV/XLSX | `StatementDraftExportE2ETests` (bestehend) | Export-Flow mit neuem Escaping (#457) |
| Fehleranzeige bei Statistik-Ausfall via Fault-Injection | `AccountsOverviewStatisticsPlaywrightTests` (bestehend) | E2E-Fault-Injection weiter unter Development nutzbar (#461) |
| Eingeloggte Session bleibt über Keepalive aktiv (Circuit + Cookie) | neu oder Erweiterung eines bestehenden Session-Playwright-Tests | Circuit-API-Aufrufe nach Cookie-Rotation erfolgreich ohne `X-Auth-Token`-Header (#455/#460) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaywrightWebAppFixture` | Server-Env `Auth__AllowSelfRegistration=true` ergänzen (E2E-Registrierungen via `AuthGateway`/`/register` bleiben möglich) |
| `AuthGateway` | Keine Änderung am Ablauf; abhängig vom Fixture-Override |
| `AccountsOverviewStatisticsPlaywrightTests` | Keine Codeänderung nötig (Fixture läuft Development); als Regression mitlaufen lassen |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | #456 Registrierungsmodell: (a) komplett zu, (b) nur Erstbenutzer, (c) Konfigflag? | Variante (c) `Auth:AllowSelfRegistration`, Default `false` (effektiv nur Erstbenutzer; Folgekonten via `AdminController`/`IUserAdminService`). Test-Infrastruktur setzt Flag explizit `true`. |
| 2 | #458 Konkrete `AllowedHosts`-Werte in Production (Domain/LAN-Hostname/IP — Direct-HTTP im LAN muss erreichbar bleiben). | `appsettings.Production.json`: echter Deploy-Hostname + `localhost`; LAN-IP-Zugriff entweder über Hostname abbilden oder IP-Eintrag ergänzen; notfalls per Env `AllowedHosts` beim Deployment setzen und im Repo `localhost` als Minimum stehen lassen. |
| 3 | #451 Fachliche Freigabe der Limit-Werte (`MaxRequestBytes` ≈143 MB, `MaxFiles` 50, `MaxBytesPerFile` 10 MB, `MaxTotalContentBytes` 100 MB). | Werte wie in „Konfigurationsänderungen" vorgeschlagen übernehmen; real typische Kontoauszüge/PDFs liegen weit darunter. |
| 4 | #453 Endgültiger CSP-Inhalt (Inline-Toleranzen). | `default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'self'; form-action 'self'` — `'unsafe-inline'` ist wegen `<ImportMap />`-Inline-Skript und Inline-`style`-Attributen erforderlich; bewusst **kein** `upgrade-insecure-requests` (LAN-HTTP). |
| 5 | #452 Umgang mit Altbestand: migrieren oder Altpfade weiter auflösbar halten? | Startup-Migration via `SchemaPatcher` (im Plan so vorgesehen); kein Lazy-Fallback, damit der gemeinsame Pfad nicht dauerhaft auflösbar bleibt. |
| 6 | #452 `[RequestSizeLimit(1_024_000_000)]` + `RequestFormLimits` auf `BackupsController.UploadAsync` (nicht Teil von #451): auf applikatives `Backups:Security:MaxUploadBytes` (100 MB) angleichen? | Auf `RequestSizeLimit`/`RequestFormLimits` ≈ `MaxUploadBytes` + Multipart-Overhead reduzieren oder auf `[AttachmentUploadSizeLimit]`-Muster umstellen — sonst bleibt 1-GB-Body-DoS-Fenster trotz applikativem 100-MB-Limit. |
