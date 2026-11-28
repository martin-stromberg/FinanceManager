# Bestandsaufnahme: PathBase-Betrieb (Reverse-Proxy-Unterordner) wiederherstellen

Bestandsaufnahme zum Ist-Zustand der Blazor-Server-Anwendung `FinanceManager.Web` bezogen auf Issue #612: Durch das Redesign aus PR #591 wurden erneut root-absolute interne `href`-Pfade eingeführt, die den Betrieb unter einem `UsePathBase`-Präfix brechen. Analysiert wurden das Razor-Markup, die PathBase-Infrastruktur, die `?back=`-Navigation sowie der projektweite Test-Ausgangszustand.

## Zusammenfassung

- **Verifiziert betroffen sind exakt zwei Dateien** mit zusammen 15 root-absoluten internen `href`-Attributen:
  - `FinanceManager.Web/Components/Layout/MainLayout.razor` — 13 `NavLink`-Ziele (`/`, `/list/accounts`, `/list/contacts`, `/list/savings-plans`, `/list/securities`, `/list/statement-drafts`, `/list/budget/purposes`, `/reports`, `/list/users`, `/list/mcp-accesses`, `/card/setup`, `/help`, `/legal`), Zeilen 20–107.
  - `FinanceManager.Web/Components/LoginStatus.razor` — `href="/change-password"` (Zeile 13), `href="/login"` (Zeile 21).
- **Keine weiteren Befunde** im Produktivcode: Repo-weite Suche nach `(href|src)="/` in `*.razor`, `NavigateTo("/`, `fetch("/`/`fetch('/` in `wwwroot`, `url(`-Asset-Referenzen (außer `data:`-URIs) und `window.location`-Zuweisungen ist leer. Alle `NavigateTo`-Aufrufe, alle `wwwroot`-Fetches, `manifest.json` und alle SVG-Icon-Verweise sind relativ.
- **PathBase-Infrastruktur ist vollständig vorhanden und korrekt:** `ProgramExtensions.ConfigureMiddleware` liest den Konfigurationsschlüssel `PathBase` und registriert `app.UsePathBase(...)` als erste Middleware; `App.razor` setzt `<base href>` dynamisch aus `Request.PathBase`; `AuthenticationRedirectMiddleware`, `AuthReturnUrl`, `AuthRedirect.razor` und `financeManager.js` sind PathBase-fähig.
- **Konvention aus dem früheren Fix** (Commit `0890dd16`): `href=""` mit `Match="NavLinkMatch.All"` für die Home-Route, sonst relative Pfade ohne führenden Slash. Der aktuelle Branch-HEAD `ce17bc2b` ist selbst der Merge von PR #591, der den Rückfall eingeführt hat.
- **`?back=`-Validierung:** `IsSameOriginUri` akzeptiert auch root-absolute Ziele — ein `?back=/list/accounts` würde die Prüfung passieren, aber das Präfix verlieren. Erzeugte `back`-Werte stammen intern aus `Nav.Uri` bzw. `ToBaseRelativePath` und enthalten den Pfad korrekt.
- **`AGENT.md`** enthält aktuell keine Regel gegen root-absolute interne Pfade (35 Zeilen, vier Abschnitte).
- **Regressionsschutz fehlt:** Es existiert kein Test, der Razor-Markup statisch auf root-absolute Pfade prüft, und kein E2E-Test, der die App unter einem PathBase-Präfix fährt.

## Test-Ausgangszustand

- **Unit-Tests** (`FinanceManager.Tests`): 2554 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
- **Integrations-Tests** (`FinanceManager.Tests.Integration`): 180 erfolgreich, 0 fehlgeschlagen, 0 übersprungen.
- **E2E-Tests** (`FinanceManager.Tests.E2E`, Playwright): 185 erfolgreich, **3 fehlgeschlagen**, 0 übersprungen — alle drei Fehlschläge in `UpdateSetupPlaywrightTests` (Timeout beim Warten auf `.setup-update-tab`), nachweislich vorhanden im Ausgangslauf und ohne Bezug zu PathBase.
- Nachweis und Details: [inventory/tests.md](inventory/tests.md).

## Details

- [Betroffene Komponenten und weitere Referenz-Stellen](inventory/komponenten.md)
- [PathBase-Infrastruktur, Navigationslogik und Konventionen](inventory/logic.md)
- [Tests und Test-Ausgangszustand](inventory/tests.md)
