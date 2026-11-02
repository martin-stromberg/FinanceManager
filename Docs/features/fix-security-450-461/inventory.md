# Bestandsaufnahme: Security-Fixes #450–#461

Analyse des bestehenden Codes (Branch `fix/security-450-461`, Stand nach Merge der #446–#449-Fixes via PR #462) bezogen auf die zwölf Security-Findings aus `requirement.md`. Alle Audit-Angaben wurden gegen den realen Codestand verifiziert.

## Zusammenfassung

- **Alle 12 Findings sind im Code bestätigt** — keines ist bereits gelöst. Die #449-Fixes (`UseForwardedHeaders`, `BackupSecurityOptions`, `HelpSecurityPolicy`, IpBlock) sind gemergt und bilden teilweise Referenz-/Wiederverwendungs-Implementierungen.
- **#450 (TOCTOU Erstbenutzer-Admin):** `UserAuthService.RegisterAsync` prüft `isFirst` via `!AnyAsync()` (Zeile 108) vor dem Einfügen; Admin-Rollen-Zuweisung (Zeilen 155–178) erfolgt nachgelagert und nicht-fatal. Zusätzlich synct `ProgramExtensions.EnsureAdminRole` beim Start `IsAdmin` ↔ `Admin`-Rolle in beide Richtungen — ein fälschlich gesetztes `IsAdmin`-Flag wird damit persistiert verstärkt.
- **#451 (DoS Mass-Import):** `StatementDraftsController.ProcessMassImportAsync` trägt `[RequestSizeLimit(long.MaxValue)]` (Zeile 214) bei `Kestrel:Limits:MaxRequestBodySize` = 20 MB. Drei weitere `long.MaxValue`-Stellen existieren (`SavingsPlansController:324`, `SecuritiesController:325` und `:427`); der Upload-Endpunkt hat bereits 10 MB.
- **#452 (Backup-Kollision):** `BackupService.GetRoot()` liefert einen gemeinsamen Ordner `<ContentRoot>/backups` für alle Benutzer. `UploadAsync` schreibt unter dem clientseitigen `Path.GetFileName(fileName)` und `File.Create` überschreibt bestehende Dateien anderer Benutzer; `CreateAsync` nutzt Sekunden-Zeitstempel. `StoragePath` ist ein schlichter Dateiname und wird in `DeleteAsync`/`OpenDownloadAsync`/`ApplyAsync` unter dem gemeinsamen Root aufgelöst.
- **#453 (Security Headers):** Es existiert nur die `/help`-spezifische CSP über `HelpSecurityPolicy` (Middleware in `ProgramExtensions.cs:475–483`). Keine globalen Header (`X-Frame-Options`, `Referrer-Policy`, `X-Content-Type-Options`, `Permissions-Policy`); `UseHsts` nur außerhalb Development.
- **#454 (DetailedErrors):** `"DetailedErrors": true` steht in `appsettings.Production.json` und zusätzlich in `appsettings.Development.json` (dort legitim).
- **#455 (JWT in Response-Headern):** `JwtRefreshMiddleware.SetTokenResponseHeaders` setzt `X-Auth-Token`/`X-Auth-Token-Expires` mit dem rohen JWT — sowohl nach erfolgreicher Erneuerung als auch auf dem Keepalive-Pfad ohne Erneuerung (aktuelles Token wird gespiegelt). Konsumenten: `financeManager.js` → `AuthRedirect.razor` → `JwtCookieAuthTokenProvider.AcceptBrowserToken`; Integrationstest `ApiClientAuthTests` verankert die Header.
- **#456 (Offene Registrierung):** `POST /api/auth/register` ist anonym und legt bedingungslos Konten an; `GET /api/users/exists` trägt `[AllowAnonymous]` und ist per Test `UsersControllerTests_Authorization` verankert. Rate-Limiting existiert nur indirekt über `IpBlockMiddleware`/`IIpBlockService` (Login-Failures).
- **#457 (CSV-Formel-Injection):** `ExportFileHelper.CsvEscape` quotiert alle Werte, maskiert aber keine Formel-Präfixe (`=`, `+`, `-`, `@`, Tab/CR). CSV-Pfade: `StatementDraftExportService`, `PostingExportService` (je 7–8 `CsvEscape`-Aufrufe für Nutzerdaten wie Subject/Description/Namen). XLSX-Pfade schreiben `CellValues.String` bzw. SharedStrings — nicht formel-auswertbar. `BudgetReportExportService` ist entgegen seinem Klassenkommentar rein XLSX-basiert (kein CSV).
- **#458 (AllowedHosts):** `"AllowedHosts": "*"` in `appsettings.json`; `appsettings.Production.json` überschreibt es nicht. `UseForwardedHeaders` (X-Forwarded-For/-Proto) ist seit PR #462 bereits aktiv.
- **#459 (CSRF/Antiforgery):** `UseAntiforgery()` steht in `ConfigureMiddleware` **vor** `UseAuthentication`/`UseAuthorization` (Zeilen 524–527) — entgegen der ASP.NET-Core-Reihenfolge. Kein `ValidateAntiForgeryToken`/`AutoValidateAntiforgeryToken`/`IgnoreAntiforgeryToken`-Attribut in Controllern oder Razor-Komponenten; Schutz stützt sich auf `SameSite=Lax` + Bearer-Auth (Cookie-Fallback in `OnMessageReceived`, ProgramExtensions.cs:262–279).
- **#460 (JWT im Prerender-HTML):** `RegisterPersistentService<JwtCookieAuthTokenProvider>` (ProgramExtensions.cs:91) + `[PersistentState] PersistedToken` (JwtCookieAuthTokenProvider.cs:27–42) persistieren den JWT in den prerendered Component State. .NET 10-Ziel (`net10.0`).
- **#461 (E2E-Fault-Injection):** Inline-Middleware in `ProgramExtensions.cs:457–473` ist produktiv über `E2E:AccountStatisticsFaultInjectionEnabled` + `E2E:AccountStatisticsFaultFile` schaltbar; `E2E:DisableHttpsRedirection` (Zeile 439) ebenfalls produktiv auswertbar. Aktivierung nur via `PlaywrightWebAppFixture` (Env-Variablen `E2E__*`).

## Details

- [Auth & Registrierung (#450, #456)](inventory/auth-registration.md)
- [Request-Limits Mass-Import (#451)](inventory/request-limits.md)
- [Backup-Ablage (#452)](inventory/backups.md)
- [Security-Header & Konfiguration (#453, #454, #458, #459)](inventory/headers-config.md)
- [Token-Handling (#455, #460)](inventory/token-handling.md)
- [CSV-/XLSX-Export (#457)](inventory/export.md)
- [E2E-Fault-Injection (#461)](inventory/e2e.md)
- [Tests](inventory/tests.md)
