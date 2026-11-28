# Lokaler Start mit Demodaten & Screenshot-Tooling

## 1. Voraussetzungen

- .NET SDK 10 (installiert: `dotnet --version` → 11.0.100; Projekte targetieren `net10.0`; `global.json` enthält kein SDK-Pinning, nur `"test": { "runner": "Microsoft.Testing.Platform" }`).
- Node.js 26.x laut `package.json`-`engines` (`"node": ">=26.13"`) und README; installiert: `v24.15.0` — Versionsabweichung (kein `engine-strict`, Skripte laufen faktisch auch auf 24).
- Playwright: Node-Paket `playwright 1.62.0` (`scripts/screenshots/package.json`); Browser einmalig via `npx playwright install chromium`.

## 2. Startbefehle

| Zweck | Befehl |
|---|---|
| Restore | `dotnet restore` |
| Build | `dotnet build FinanceManager.sln` |
| Dev-Run | `dotnet run --project FinanceManager.Web` (`launchSettings.json`: `http://localhost:5208`, `https://localhost:7013`; `ASPNETCORE_ENVIRONMENT=Development`) |
| Publish (Screenshot-Basis) | `dotnet publish FinanceManager.Web -c Release` → `FinanceManager.Web/bin/Release/net10.0/publish/` |

## 3. Konfiguration (relevante Schlüssel)

- `ConnectionStrings:Default` — SQLite, Fallback `Data Source=financemanager.db`; per Env `ConnectionStrings__Default` überschreibbar (temporäre DB).
- `Jwt:Key` — Pflicht (≥ 32 Bytes); `appsettings.Development.json` enthält Platzhalter `PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64`; die Screenshot-/E2E-Skripte setzen `Jwt__Key` per Env.
- `Auth:AllowSelfRegistration` — `false` (nur Erstbenutzer registriert sich selbst); E2E setzt `true`.
- `BackgroundTasks:Enabled` — `true` erforderlich für `CreateDemoData`.
- `Workers:SecurityPriceWorker:Enabled`, `Updates:Enabled`, `Updates:HostedServicesEnabled`, `FileLogging:Enabled` — werden für reproduzierbare Offline-Läufe deaktiviert.
- `E2E:DisableHttpsRedirection` — Testschalter (nur Development wirksam).
- `Api:BaseAddress` — wird von den Skripten auf die eigene URL gesetzt.

## 4. Demodaten-Mechanismus

Fluss (Quelllage):

1. `Register.razor` zeigt `#create-demo-data`-Checkbox nur beim ersten Benutzer (`_isFirstUser` via `IUserReadService.HasAnyUsersAsync`, Z. 48–52, 73).
2. `POST /api/auth/register` mit `CreateDemoData`-Flag → `AuthController` → `UserAuthService.RegisterAsync` → `ShouldCreateDemoDataForFirstUser` → `_backgroundTaskManager.Enqueue(BackgroundTaskType.CreateDemoData, ...)` (`UserAuthService.cs` Z. 228–231).
3. `DemoDataTaskExecutor` (`Services/DemoDataTaskExecutor.cs`) führt `IDemoDataService.CreateDemoDataAsync(userId, createPostings: true)` aus und setzt die Benutzerkultur.
4. `Infrastructure/Demo/DemoDataService.cs` (~1225 Zeilen, deterministischer Seed `BuildDeterministicSeed()`) erzeugt über die echten Business-Services: Kontakte + Kategorien, Konten (Giro, Spar-, Bausparkonto), Sparpläne + Kategorien + Konto-Verknüpfungen, Wertpapiere + Kategorien + Kurse, Budget-Kategorien/-Zwecke/-Regeln, Kontoauszugs-Entwürfe + gebuchte Buchungen für **24 Monate**, Home-KPI-Kacheln (`IHomeKpiService`) und Report-Favoriten (`IReportFavoriteService`).
5. Status-Abfrage: `GET /api/background-tasks/active` (`Controllers/BackgroundTasksController.cs`, `api/background-tasks`) — Bearer = JWT aus `FinanceManager.Auth`-Cookie.
6. Alternative manuelle Variante: `POST /api/users/demo/{userId}` (`UsersController` Z. 111, Admin/Dev-Szenario).

Dokumentiert in `Docs/development.md` („Demodaten lokal testen") und `Docs/screenshot-generation.md`.

## 5. Playwright-Skripte unter `scripts/screenshots/`

Package `financemanager-screenshots` (CommonJS): `playwright 1.62.0`, `pngjs 7.0.0`, `gifenc 1.0.3`.

### 5.1 `generate-screenshots.js` (287 Zeilen)

- Erwartet Release-Publish unter `FinanceManager.Web/bin/Release/net10.0/publish/FinanceManager.Web.dll` (Fehler, wenn fehlend).
- Start: freier Port (`getFreePort`), `spawn('dotnet', [appDll])` mit Env `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS`, `Api__BaseAddress`, `E2E__DisableHttpsRedirection=true`, `ConnectionStrings__Default` (temp SQLite in `%TEMP%/financemanager-screenshots-*`), `Jwt__Key` (Hardcode-Testkey), `BackgroundTasks__Enabled=true`, `Workers__SecurityPriceWorker__Enabled=false`, `Updates__Enabled=false`, `Updates__HostedServicesEnabled=false`, `FileLogging__Enabled=false`, `DetailedErrors=true`. App-Log → `app.log` im Temp-Verzeichnis (bei Fehler konserviert).
- Readiness: `GET /register` < 500; Browser: Chromium headless, Viewport **1280×900**, `fullPage:false`, Locale `de-DE`, TZ `Europe/Berlin`.
- Registrierung über echte UI (`#username`, `#password`, `#create-demo-data` mit dispatched input/change-Events, bis zu 3 Submit-Versuche wegen Blazor-Event-Attach).
- Login über `/login` (`#login-user`/`#login-pass`, `fmAuthLogin`-Hook), dann Cookie `FinanceManager.Auth` als Bearer für `GET /api/background-tasks/active` — wartet Task-Start (90 s) und Ende (360 s).
- Aufnahmen (11 PNGs → `Docs/screenshots/`): `register`, `home` (`.kpi-grid .kpi-tile` ≥ 3), `accounts`/`contacts`/`savings-plans`/`securities`/`statement-drafts`/`budget-purposes` (`.fm-table tbody tr` Mindestzeilen), `reports` (`.fav-grid .fav-card` ≥ 1), `budget-report` (`.budget-report-table` + `#PrevMonth`-Klick).
- Warte-Strategie: explizite Selektor-/Row-Count-Polling, **kein** `networkidle` (Blazor-WebSocket).

Abdeckung heute: nur Desktop-Viewport, nur Dark-Mode, nur die 11 Listen-/Startansichten — keine Karten (`/card/*`), Setup, Detailseiten, Overlays, Mobile, Light, HTML-Dumps.

### 5.2 `mcp-granular-shots.js` (229 Zeilen)

- Startet `dotnet run --project FinanceManager.Web --no-build` (Debug-DLL), Viewport 1600×1000, erste User-Registrierung ohne Demodaten, aktiviert MCP per `python3`-sqlite3-Eingriff in die Temp-DB (`McpSettings.Enabled/WriteAccessEnabled=1`).
- Injiziert eine Importmap per `context.route` in HTML-Responses (Workaround für bare specifier `js/*.js`).
- Klickt Wizard/Matrix durch, Screenshots: `mcp-accesses-list`, `mcp-wizard-step1/-level/-expertmode/-token`, `mcp-card-grants`, `mcp-grant-matrix`, `mcp-grants-saved`, `mcp-grants-confirmed`.
- Selektoren: `button:has-text("Neu")`, `.mcp-wizard`, `.mcp-level-card`, `input[name="mcp-approval"]`, `.mcp-token-panel`, `table.fm-table input[type="checkbox"]`, `button.btn-primary:has-text(...)`.

### 5.3 `make-gif.js` (75 Zeilen)

Setzt `Docs/screenshots/*.png` (Liste `frames`) auf 1280×900 zugeschnitten zu `demo.gif` (2 s/Frame) — nur README-Beleg, für Inventar irrelevant.

## 6. Wiederverwendbare Selektoren (UI-Abnahme)

Aus Skripten und E2E-Tests verwendete CSS-Selektoren: `.fm-table` (Tabellen), `.kpi-grid .kpi-tile` (Home), `.fav-grid .fav-card` (Reports), `.budget-report-table`, `#PrevMonth`, `.mcp-wizard`, `.mcp-level-card`, `.mcp-token-panel`, `button.btn-primary`, `.split-dialog`, `.modal-overlay`, `#login-user`, `#login-pass`, `#username`, `#password`, `#create-demo-data`, `#change-password-link`, `#blazor-error-ui`.

## 7. Dokumentierte Startanleitungen

- `Docs/development.md` — „Demodaten lokal testen" (Publish + Env-Variablen, Register → Checkbox → Login → Task `CreateDemoData` ~1 min).
- `Docs/screenshot-generation.md` — vollständige Schritt-für-Schritt-Anleitung des Skript-Ablaufs inkl. aller Env-Variablen und Aufnahme-Seiten.
