# Übersetzte Anforderung

## Fachliche Zusammenfassung

Die zwölf verbleibenden Security-Findings des FinanceManager-Audits (GitHub Issues #450–#461, Repo `martin-stromberg/FinanceManager`) sind zu beheben. Es handelt sich um einen Härtungskatalog über die Bereiche Authentifizierung/Registrierung, Middleware-Pipeline, Datei- und Exportverarbeitung sowie Anwendungskonfiguration: atomare Erstbenutzer-Admin-Zuweisung, Request-Body-Limits, benutzerisolierte Backup-Ablage, globale Security-Header, produktionssichere Fehlerausgabe, Entzug der JWT-Exposition über Response-Header und Prerender-HTML, Einschränkung der offenen Registrierung, CSV-Formel-Escaping, `AllowedHosts`-Einschränkung, CSRF-/Antiforgery-Härtung und Entfernung produktiv schaltbarer E2E-Fault-Injection. Die legitimen Benutzerflüsse (Erstbenutzer-Registrierung, Massenimport, Backup/Restore, CSV-/XLSX-Export, Token-Erneuerung, Keepalive) müssen funktional erhalten bleiben.

## Betroffene Klassen und Komponenten

### Logikklassen / Services / Middleware (Bestand, zu ändern)

- `FinanceManager.Infrastructure/Auth/UserAuthService.cs` — `RegisterAsync`: nicht-atomare `isFirst`-Prüfung (`!await _db.Users.AsNoTracking().AnyAsync(ct)`) und nachgelagerte Admin-Rollen-Zuweisung (`_userManager.AddToRoleAsync(user, "Admin")`) (#450).
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs` — `SetTokenResponseHeaders` setzt `X-Auth-Token`/`X-Auth-Token-Expires` mit dem rohen JWT auf Responses (inkl. `/api/auth/keepalive`-Pfad) (#455).
- `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs` — `[PersistentState]`-Eigenschaft `PersistedToken` überträgt den JWT über den persistenten Komponentenzustand ins Prerender-HTML; `PrimeCache`/`AcceptBrowserToken` sind Teil des Tokenflusses (#460).
- `FinanceManager.Infrastructure/Backups/BackupService.cs` — `GetRoot()` liefert den gemeinsamen Ordner `<ContentRoot>/backups` für alle Benutzer; `UploadAsync` schreibt unter dem clientseitig gelieferten `Path.GetFileName(fileName)`, `CreateAsync` unter dem Sekunden-Zeitstempel `backup-yyyyMMddHHmmss.zip`; `OpenDownloadAsync`, `ApplyAsync`, `DeleteAsync` lösen `StoragePath` im gemeinsamen Root auf → Cross-User-Kollision/Überschreiben (#452).
- `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` — `CsvEscape` maskiert Zeilenumbrüche/Anführungszeichen, aber keine Formel-Präfixe (`=`, `+`, `-`, `@`) (#457).
- CSV-/XLSX-Export-Verbraucher von `CsvEscape` bzw. eigene Zellwert-Erzeugung: `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs`, `FinanceManager.Infrastructure/Reports/PostingExportService.cs`, `FinanceManager.Web/Services/BudgetReportExportService.cs` (#457).
- `FinanceManager.Web/ProgramExtensions.cs` — `RegisterAppServices`: `RegisterPersistentService<JwtCookieAuthTokenProvider>(InteractiveServer)` (#460), `AddAntiforgery`-Cookie-Optionen `FinanceManager.Antiforgery` (#459); `ConfigureMiddleware`: `UseForwardedHeaders` (vorhanden), `UseAntiforgery()`-Reihenfolge vor `UseAuthentication`/`UseAuthorization` (#459), Inline-Fault-Injection-Middleware unter `E2E:AccountStatisticsFaultInjectionEnabled`/`E2E:AccountStatisticsFaultFile` (#461), bisher nur `/help`-spezifische CSP über `HelpSecurityPolicy` (#453).
- `FinanceManager.Web/Services/Help/HelpSecurityPolicy.cs` — bestehende CSP (`frame-ancestors 'self'` u. a.) als Referenz für die globale Header-Politik (#453).
- `FinanceManager.Web/Infrastructure/IpBlockMiddleware.cs` + `FinanceManager.Infrastructure`-`IIpBlockService`-Implementierung — vorhandener Blockier-/Zählmechanismus, ggf. Wiederverwendung für Rate-Limiting sensibler anonymer Endpunkte (#456).

### Datenmodellklassen / DTOs

- `FinanceManager.Infrastructure/Backups/BackupRecord.cs` — `StoragePath`, `FileName` (Ablagepfad-Schema ggf. je Benutzer isolieren; Altdateien beachten) (#452).
- `FinanceManager.Domain/Users/User` — `IsAdmin`-Flag; Ziel einer atomaren Erstbenutzer-Entscheidung (Unique-Constraint-/DB-Invariante als mögliche Umsetzung, Annahme) (#450).
- `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs` — `MassImportBatchRequestDto.Files` mit `MassImportFileUploadDto.Content` (`byte[]` im JSON-Body) → unbegrenzte Request-Bodies (#451).
- `FinanceManager.Shared/Dtos/Users/RegisterRequest.cs` — Registrierungspayload (offene Registrierung) (#456).

### UI-Komponenten / Controller / Client-Skripte

- `FinanceManager.Web/Controllers/StatementDraftsController.cs` — `ProcessMassImportAsync` mit `[RequestSizeLimit(long.MaxValue)]` (#451). Hinweis/Annahme: gleiche Muster `[RequestSizeLimit(long.MaxValue)]` existieren in `SecuritiesController.UploadSymbolAsync` und `SavingsPlansController.UploadSymbolAsync`; Zuordnung zum Finding im Plan klären.
- `FinanceManager.Web/Controllers/AuthController.cs` — `RegisterAsync` (`POST /api/auth/register`, anonym erreichbar) (#456).
- `FinanceManager.Web/Controllers/UsersController.cs` — `HasAnyUsersAsync` (`GET /api/users/exists`, `[AllowAnonymous]`, liefert `AnyUsersResponse(bool)`) (#456).
- `FinanceManager.Web/Controllers/BackupsController.cs` — `UploadAsync`/`DownloadAsync` (Storage-Isolation wirkt hier durch) (#452).
- `FinanceManager.Web/Components/Pages/Register.razor` — Erstbenutzer-Logik `_isFirstUser`, `CreateDemoData` (#450/#456).
- `FinanceManager.Web/Components/AuthRedirect.razor` — `setTokenReceiver`/`ReceiveTokenAsync`-Keepalive-Integration (#455).
- `FinanceManager.Web/wwwroot/js/financeManager.js` — Keepalive-Fetch liest `X-Auth-Token`-Response-Header (#455).
- `FinanceManager.Web/Controllers/AdminController.cs` — `CreateUserAsync` (Admin-Rolle erforderlich) als alternativer User-Anlagepfad bei eingeschränkter Selbstregistrierung (#456).

### Konfiguration

- `FinanceManager.Web/appsettings.json` — `"AllowedHosts": "*"` (#458); vorhandenes `Kestrel:Limits:MaxRequestBodySize` = 20 MB (wird durch `[RequestSizeLimit(long.MaxValue)]` ausgehebelt) (#451).
- `FinanceManager.Web/appsettings.Production.json` — `"DetailedErrors": true` (Blazor `CircuitOptions.DetailedErrors` in Production) (#454).
- E2E-Konfigurationsschlüssel `E2E:AccountStatisticsFaultInjectionEnabled`, `E2E:AccountStatisticsFaultFile` (sowie benachbart `E2E:DisableHttpsRedirection`) (#461).

### Tests (Bestand, anzupassen/erweitern)

- `FinanceManager.Tests/Auth/UserAuthServiceTests.cs` (#450, #456)
- `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests*.cs`, `JwtRenewalTimingTests.cs` (#455, #460)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` — Keepalive-Test erwartet `X-Auth-Token`-Header (#455)
- `FinanceManager.Tests/Controllers/UsersControllerTests_Authorization.cs` — verankert `[AllowAnonymous]` auf `HasAnyUsersAsync` (#456)
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs` (#451)
- `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs` (#453)
- Backup-/Export-Tests (`ApiClientBackupsTests`, Export-Service-Tests) (#452, #457)
- Pro Finding neue Negativ-/Regressionstests (Race-Simulation, Limit-Überschreitung, Kollision, Header-Abwesenheit, Formel-Escaping, CSRF).

## Implementierungsansatz

- **#450 (TOCTOU Erstbenutzer-Admin):** Die Sequenz „existiert kein Benutzer → `IsAdmin` setzen → `Admin`-Rolle zuweisen" in `UserAuthService.RegisterAsync` atomar machen. Ansatzoptionen (in der Planung festzulegen, DB-Fähigkeit SQLite/EF Core beachten): serialisierbare Transaktion, DB-seitige Invariante (z. B. eindeutiger Index, der maximal einen `IsAdmin`-Erstbenutzer bzw. genau eine Admin-Rollenzuweisung zulässt) oder ein anwendungsweiter Sperrmechanismus; in jedem Fall verbindlicher Re-Check nach dem Einfügen, sodass ein Race-Verlierer keine Admin-Rolle erhält. Demo-Daten-Pfad (`ShouldCreateDemoDataForFirstUser`, `IBackgroundTaskManager`) an dieselbe atomare Entscheidung binden.
- **#451 (Request-Body-Limit Mass-Import):** `[RequestSizeLimit(long.MaxValue)]` auf `ProcessMassImportAsync` durch ein fachlich begründetes Limit ersetzen (Vorschlag: Konfiguration `MassImport:MaxRequestBytes` bzw. Ableitung aus Dateianzahl × MaxFileBytes) und zusätzlich `Files.Count`/Summe `Content.Length` applikativ begrenzen.
- **#452 (Backup-Isolation):** Ablage pro Benutzer isolieren — z. B. benutzerbezogenes Unterverzeichnis unter `GetRoot()` oder servergenerierte, kollisionsfreie Dateinamen; `StoragePath`-Auflösung in `OpenDownloadAsync`/`ApplyAsync`/`DeleteAsync` auf den isolierten Pfad beschränken (zusätzlich Path-Traversal-Sicherung des gespeicherten Namens). Bestandsdateien: Migrations- oder Koexistenzstrategie klären.
- **#453 (Security-Header):** Globale Header-Middleware in `ConfigureMiddleware` ergänzen: `X-Frame-Options`/`frame-ancestors`, `Content-Security-Policy`, `Referrer-Policy`, `X-Content-Type-Options`, ggf. `Permissions-Policy`. Kompatibilität mit Blazor Server/SignalR (`connect-src` inkl. `ws:`/`wss:`) und mit der spezifischeren `HelpSecurityPolicy.ContentSecurityPolicy` für `/help` sicherstellen (Reihenfolge/Überschreibverhalten festlegen).
- **#454 (`DetailedErrors` in Production):** `"DetailedErrors": true` aus `appsettings.Production.json` entfernen bzw. auf `false` setzen; sicherstellen, dass detaillierte Circuit-Fehler nur in Development aktiv sind.
- **#455 (`X-Auth-Token`-Header):** JWT nicht mehr über Response-Header ausliefern. `JwtRefreshMiddleware.SetTokenResponseHeaders` entfernen/umstellen; die Keepalive-Übertragung an den Browser (`financeManager.js`, `AuthRedirect.razor`, `JwtCookieAuthTokenProvider.AcceptBrowserToken`) auf einen Mechanismus umstellen, der Tokenwerte nicht in Headern/Logs platziert (Alternativen: ausschließlich HttpOnly-Cookie, bewusstes Minimal-Format ohne langfristige Token-Ablegung — konkrete Lösung in Planung, siehe Offene Fragen).
- **#456 (Offene Registrierung / Enumeration):** `POST /api/auth/register` darf nicht mehr bedingungslos für jeden anonymen Aufrufer neue Konten anlegen (z. B. nur solange kein Benutzer existiert, oder per Konfigflag/Admin-Einladung — Zielbild zu klären). `GET /api/users/exists` in seiner Aussagekraft bzw. Erreichbarkeit einschränken (Fehlerverhalten vereinheitlichen, ggf. Rate-Limiting über vorhandene `IpBlockMiddleware`/`IIpBlockService`-Infrastruktur). Hinweis zur fachlichen Einordnung: Der Endpoint legt aktuell nur den Systemzustand „es existiert mindestens ein Benutzer" offen (`AnyUsersResponse`); das Finding bezeichnet dies als User-Enumeration.
- **#457 (CSV-Formel-Injection):** `ExportFileHelper.CsvEscape` so erweitern, dass Zellwerte mit führendem `=`, `+`, `-`, `@` (sowie führende Steuerzeichen wie Tab/CR) entschärft werden (übliche Praxis: vorangestelltes `'`). Alle CSV-Ausgabepfade erfassen (`StatementDraftExportService`, `PostingExportService` sowie ggf. weitere CSV-Exporte; XLSX-Zelltypen separat bewerten).
- **#458 (`AllowedHosts`):** `appsettings.json`/`appsettings.Production.json`: `AllowedHosts` von `"*"` auf die tatsächlich verwendeten Hostnamen einschränken; Zusammenspiel mit `UseForwardedHeaders` (seit #449 aktiv) beachten.
- **#459 (CSRF/Antiforgery):** `UseAntiforgery()` in `ConfigureMiddleware` an die von ASP.NET Core vorgesehene Position verschieben (nach `UseAuthentication`/`UseAuthorization`) und den Schutz nicht allein auf `SameSite=Lax` stützen: Antiforgery-Validierung für zustandsändernde, cookie-authentifizierte Endpunkte sowie Blazor-Formposts prüfen (Cookie `FinanceManager.Antiforgery` bereits registriert; Cookie-Fallback für Bearer-Auth in `OnMessageReceived` beachten).
- **#460 (JWT im Prerender-HTML):** `RegisterPersistentService<JwtCookieAuthTokenProvider>` entfernen und `[PersistentState] PersistedToken` aus `JwtCookieAuthTokenProvider` streichen, sodass kein Token im initialen HTML (persisted component state) landet; Tokenversorgung des Circuits auf nicht persistierte Weise sicherstellen (z. B. Cookie-/Request-bezogene Auflösung beim Circuit-Start).
- **#461 (E2E-Fault-Injection):** Die Inline-Middleware unter `E2E:AccountStatisticsFaultInjectionEnabled` aus dem produktiven Pipeline-Code entfernen bzw. strikt auf nicht-produktive Umgebungen beschränken (Annahme: Entfernung aus `ConfigureMiddleware` und Verlagerung in die Test-Infrastruktur; `E2E:DisableHttpsRedirection` analog prüfen).

## Konfiguration

- `AllowedHosts`: Anwendungseinstellung (appsettings je Umgebung) — konkrete Hostnamen deploy­ment­spezifisch.
- Mass-Import-Limits: Vorschlag neuer Abschnitt `MassImport` (`MaxRequestBytes`/`MaxFiles`/`MaxBytesPerFile`) als Anwendungseinstellung (Annahme).
- Registrierungsverhalten: Vorschlag Anwendungseinstellung (z. B. `Auth:AllowSelfRegistration`) oder feste Regel „nur Erstbenutzer" — Zielbild offen (siehe Offene Fragen).
- `DetailedErrors`: nur in `appsettings.Development.json`; aus Production entfernen.
- Security-Header: feste Default-Policy im Code; CSP-Inhalt ggf. als Anwendungseinstellung übersteuerbar (optional, Annahme).
- `E2E:*`-Schlüssel: aus produktiven Konfigurationspfaden entfernen bzw. nur in Test-Hosting-Kontext zulassen.
- `Backups`-Ablage: Pfad-Schema intern; keine neue Benutzereinstellung vorgesehen.

## Offene Fragen

1. **#456 Registrierung:** Soll die Selbstregistrierung (a) vollständig deaktiviert werden (Konten nur via `AdminController`/`IUserAdminService` anlegbar), (b) nur für den Erstbenutzer offen bleiben, oder (c) per Konfigflag steuerbar sein? Wie verhalten sich `Register.razor` und `AuthRedirect.razor` bei gesperrter Registrierung?
2. **#456 `users/exists`:** Darf der Endpunkt weiterhin anonym bleiben (für Login-/Register-Umleitung benötigt, siehe `UsersControllerTests_Authorization`), jedoch mit abgeschwächter Aussage/Rate-Limit — oder soll er ganz entfallen bzw. authentifiziert werden?
3. **#458 AllowedHosts:** Welche Hostnamen sind in der Zielumgebung zulässig (Reverse-Proxy/Domain)? Erfolgt die Einschränkung nur per Konfiguration oder zusätzlich mit Beispielwerten in `appsettings.Production.json`?
4. **#455/#460 Tokenfluss:** Nach Wegfall der `X-Auth-Token`-Header und der `[PersistentState]`-Übergabe — wie erhält der Blazor-Circuit den (erneuerten) Token: reine HttpOnly-Cookie-Lösung, Server-seitige Auflösung ohne JS-Roundtrip, oder bewusstes neues Übergabeformat? Auswirkung auf Keepalive/`AcceptBrowserToken` klären.
5. **#453 CSP-Inhalt:** Mindestumfang der globalen CSP für Blazor Server (SignalR-WebSocket, Inline-Styles/Skripte, Help-Inhalte) — welche `*src`-Direktiven sind fachlich tolerierbar?
6. **#451 Limit-Werte:** Konkrete Obergrenzen für den Mass-Import (Gesamt-Body, pro Datei, Dateianzahl) — fachliche Vorgabe oder technische Ableitung?
7. **#452 Altbestand:** Bestehende Backup-Dateien im gemeinsamen `backups`-Ordner beim Umstieg auf user-isolierte Pfade migrieren/verschieben oder Altpfade weiter auflösbar halten?
8. **#450 Mechanismus:** Bevorzugte Umsetzung der atomaren Erstbenutzer-Entscheidung (Unique-Index/DB-Invariante vs. Transaktion vs. anwendungsweite Sperre) unter SQLite — Entscheidung in der Planung.
