# E2E-Fault-Injection (#461)

## Inline-Middleware in `ConfigureMiddleware`
Datei: `FinanceManager.Web/ProgramExtensions.cs` (Z. 457–473)

```csharp
if (app.Configuration.GetValue<bool>("E2E:AccountStatisticsFaultInjectionEnabled"))
{
    var accountStatisticsFaultFile = app.Configuration["E2E:AccountStatisticsFaultFile"];
    app.Use(async (context, next) => {
        if (context.Request.Path.Equals("/api/accounts/statistics", ...)
            && File.Exists(accountStatisticsFaultFile))
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "E2E_AccountStatisticsFault", ... });
            return;
        }
        await next();
    });
}
```

- **Produktiv schaltbar**: Jeder, der Konfiguration/Umgebungsvariablen setzt, aktiviert einen Schalter, der `GET /api/accounts/statistics` per Datei-Existenz (`File.Exists` pro Request) auf 503 schaltet. DoS-/Fault-Injection-Oberfläche im Produktivcode; `File.Exists` auf Request-Pfad.
- Zweiter E2E-Key: `E2E:DisableHttpsRedirection` (Z. 439–442) — deaktiviert `UseHttpsRedirection` ebenfalls produktiv.
- Middleware sitzt früh in der Pipeline (vor Exception-Handler/StaticFiles).

## Aktivierung (nur Test-Infrastruktur)
Datei: `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`

| Stelle | Inhalt |
|--------|--------|
| Z. 329–333 | Server-Prozess-Env: `E2E__DisableHttpsRedirection = "true"`, `E2E__AccountStatisticsFaultInjectionEnabled = "true"`, `E2E__AccountStatisticsFaultFile = _accountStatisticsFaultFile` |
| `EnableAccountStatisticsFault()` (Z. 251) | Schreibt `"fail"` in die Fault-Datei |
| `ResetAccountStatisticsFault()` (Z. 264) | Löscht die Fault-Datei; wird in `InitializeAsync`/Session-Dispose aufgerufen (Z. 194, 211, 243) |
| `_accountStatisticsFaultFile` | Temp-Datei pro Fixture |

## Verbraucher
- `FinanceManager.Tests.E2E/Tests/Accounts/AccountsOverviewStatisticsPlaywrightTests.cs` Z. 384/394: `_fixture.EnableAccountStatisticsFault()` / `ResetAccountStatisticsFault()` — Fehleranzeige im UI wird getestet.
- Keine `E2E:*`-Schlüssel in `appsettings*.json` — Konfiguration kommt ausschließlich aus Env-Variablen des Test-Hosts. Kein `E2E:...`-Wert in Produktions- oder Development-Config.

## Bestehende Schutzmechanismen
- Keine Environment-Beschränkung: `GetValue<bool>` läuft in jeder Umgebung; einzige „Sicherung" ist, dass die Keys per Default fehlen.

## Relevante Tests
- `FinanceManager.Tests.E2E/Tests/Accounts/AccountsOverviewStatisticsPlaywrightTests.cs` (Fault-Injection-Verbraucher).
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` — Integration-Factory (nutzt die E2E-Keys nicht; Environment=Development).
