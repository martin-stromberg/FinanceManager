# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

Verifikation der 5 Iteration-2-Befunde (alle korrekt behoben):

1. **No-op `await`** — `E2ESwitchesProductionTests.cs`: `await Task.CompletedTask;` und `CreateBearerTokenAsync` entfernt; Token-Erzeugung läuft synchron über `TestJwtTokens.Create` (Zeile 60).
2. **JWT-Helper-Duplikat** — Gemeinsamer Helfer `FinanceManager.Tests.Integration/TestJwtTokens.cs` (neu, statische Klasse `TestJwtTokens.Create`) ausgelagert; wird von `CookieCsrfProtectionTests` (Zeilen 28, 45, 61), `E2ESwitchesProductionTests` (Zeile 60) und `ApiClientAuthTests` (Zeilen 122, 140, 158, 177, 193–194, 221, 245, 275, 325, 368) verwendet. Keine verbliebenen `JwtSecurityToken`-Builder im Testprojekt.
3. **`.gitignore` `backups/`** — Regel `/FinanceManager.Web/backups/` (`.gitignore:427`) ergänzt, mit Kommentar, dass der Quellordner `FinanceManager.Web/Infrastructure/Backups/` getrackt bleibt. Verifiziert: `git status --porcelain --ignored` zeigt `!! FinanceManager.Web/backups/` und `?? FinanceManager.Web/Infrastructure/Backups/`; `git add --dry-run` erfasst `BackupUploadSizeLimitAttribute.cs` (die `git check-ignore`-Meldung auf die leere Zeile 455 ist ein bekanntes Anzeige-Artefakt, kein Ignore-Match).
4. **AllowedHosts-Regression** — `"AllowedHosts": "*"` in `appsettings.json:15` und `appsettings.Production.json:53` wiederhergestellt; beide Dateien dokumentieren die deployment-spezifische Härtungsoption (Hostnamen/IPs-Liste bzw. `AllowedHosts`-Env-Var). Direct-HTTP-LAN- und Reverse-Proxy-Zugriff funktionieren out-of-the-box.
5. **`RegisterAsync`-Fehlermapping** — `AuthController.cs:113-118` mappt stabil: `Err_RegistrationDisabled` → 403, `Err_Conflict_UserAlreadyExists` → 409, alle übrigen Fehler → `Err_InvalidRegistration` → 400 (generische Meldung, kein Enumeration-Leak). `AuthErrorCodes.cs` um `InvalidRegistration` erweitert; `UserAuthService.RegisterAsync` liefert die Codes (Zeilen 107, 117, 124, 149 — inkl. `DuplicateUserName`-Race-Abbildung auf 409); resx-Schlüssel `API_Auth_Err_InvalidRegistration` in `Controller.de.resx:110` und `Controller.en.resx:112` vorhanden.

## Geprüfte Dateien

Fokus dieser Iteration auf den geänderten Stellen; keine neuen kritischen Befunde gefunden.

- `FinanceManager.Tests.Integration/TestJwtTokens.cs` (neu — zentraler Token-Builder)
- `FinanceManager.Tests.Integration/E2ESwitchesProductionTests.cs` (geändert)
- `FinanceManager.Tests.Integration/CookieCsrfProtectionTests.cs` (geändert)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` (geändert)
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` (geändert — `HostEnvironment`, `ConfigurationOverrides`)
- `.gitignore` (geändert — `/FinanceManager.Web/backups/`)
- `FinanceManager.Web/appsettings.json`, `appsettings.Production.json` (geändert — `AllowedHosts` + Doku-Kommentare)
- `FinanceManager.Web/Controllers/AuthController.cs` (geändert — Fehlermapping `RegisterAsync`)
- `FinanceManager.Infrastructure/Auth/AuthErrorCodes.cs` (geändert — `InvalidRegistration`)
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs` (geändert — stabile Fehlercodes im Registrierungs-Fluss)
- `FinanceManager.Web/Resources/Controllers/Controller.de.resx`, `Controller.en.resx` (geändert — neue Fehlerschlüssel)
- `FinanceManager.Shared/ApiClient.Auth.cs`, `FinanceManager.Web/Components/Pages/Register.razor` (Fehlerpfad gegen neue 400/409/403-Semantik geprüft — unverändert korrekt)

Build-Verifikation: `dotnet build FinanceManager.Tests.Integration` — 0 Fehler (nur vorbestehende NU1510/ASPDEPR005-Warnungen).
