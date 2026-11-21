# Bestandsaufnahme — Code-Referenzpunkte

Verifizierte Code-Fakten, die die neue Server-Installationsanleitung beschreiben wird. Keine Änderung an diesen Stellen vorgesehen (Ausnahme: `HelpContentCatalog.TechnicalOnlyDocumentNames`, siehe `tests.md`).

## `ProgramExtensions` — `FinanceManager.Web/ProgramExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Main` (`Program.cs:19-39`) | public static | `CreateBuilder` → `ConfigureLogging` → `RegisterAppServices` → `Build` → `ApplyMigrationsAndSeed` → `ApplyPersistedUpdateSettings` → `ConfigureMiddleware` → `app.Run()` |
| `ConfigureLogging` | public static | Console-Logging; `FileLogging:Enabled` (Default `true` bei fehlendem Wert) aktiviert `AddFile` (Zeilen 64–77) |
| `RegisterAppServices` | public static | `AddInfrastructure(builder.Configuration.GetConnectionString("Default"))` (Zeile 100); JWT-Options mit `.ValidateOnStart()` + `JwtOptionsValidator` (Zeilen 293–297); `DataProtection:KeysPath` → `PersistKeysToFileSystem` nur wenn gesetzt (Zeilen 156–161); `UpdateOptions`-Bindung (Zeile 155) |
| `ApplyMigrationsAndSeed` | public static | `db.Database.Migrate()` beim Start — legt beim Erststart das SQLite-Schema an; danach `SchemaPatcher.RunPostMigrationPatches`, `EnsureAdminRole`, `IAutoInitializationService.Run()` (Zeilen 827–861) |
| `ApplyPersistedUpdateSettings` | public static | Wendet persistierte Update-Einstellungen vor Start der Auto-Update-Hosted-Services an (Zeilen 879–891) |
| `ConfigureMiddleware` | public static | Middleware-Reihenfolge siehe unten (Zeilen 616–) |
| `SetInitialConfiguration` | private static | `cfg.WithDownloadPath(updateOptions.WorkingDirectory)` für den Self-Update-Downloadpfad (Zeilen 560–585) |

### Middleware-Reihenfolge in `ConfigureMiddleware` (verifiziert)

1. **`PathBase`** (Zeilen 618–628): `app.Configuration["PathBase"]`; bei gesetztem Wert `app.UsePathBase(pathBase)` als **erste** Middleware. Konfigurationsschlüssel `PathBase` ist **nicht** in `appsettings.json`/`appsettings.Production.json` hinterlegt — setzbar per Environment-Variable `PathBase` (z. B. `/finance`).
2. **`UseForwardedHeaders`** (Zeilen 630–672): `ForwardedHeaders.XForwardedFor | XForwardedProto`, `ForwardLimit = 1`; `KnownProxies`/`KnownIPNetworks` werden zunächst **geleert** und ausschließlich aus `ReverseProxy:KnownProxies` (IPs) bzw. `ReverseProxy:KnownNetworks` (CIDR, `a.b.c.d/n`, jeweils semikolongetrennt) befüllt; ungültige Einträge → `LogWarning`. Ohne Einträge werden keine Forwarded-Headers honoriert (Schutz gegen `X-Forwarded-For`-Spoofing auf Rate-Limit-Partitionen und `IpBlockMiddleware`, Kommentar Zeilen 630–637).
3. `RequestLoggingMiddleware`, `IpBlockMiddleware`, `UseRateLimiter` (Zeilen 673–675) — beide nutzen `RemoteIpAddress` und profitieren damit von korrekt gesetzten Forwarded-Headers.
4. `UseWhen /mcp` → `McpDisabledMiddleware` (Zeilen 679–680).
5. **`UseHttpsRedirection`** (Zeilen 687–690): aktiv, außer `Environment == Development` **und** `E2E:DisableHttpsRedirection == true`. Ohne konfigurierten HTTPS-Endpunkt gibt es nur eine einmalige Startup-Warnung im Log; Requests laufen auf HTTP durch (ASP.NET-Core-Standardverhalten).
6. **`UseHsts` + `UseExceptionHandler("/Error")`** nur außerhalb Development (Zeilen 692–696); in Development `UseDeveloperExceptionPage`. HSTS greift nur auf HTTPS-Requests — hinter einem Proxy nur wirksam, wenn `X-Forwarded-Proto: https` honoriert wird.
7. `PathBase`-Nutzung in Antworten: `App.razor:7` setzt `<base href="{PathBase}/" />`; `AuthReturnUrl.cs:82`, `MainLayout.razor:147-148`, `AuthRedirect.razor:85`, `ProgramExtensions.cs:233` berücksichtigen `Request.PathBase`.

**Listener:** Kein `UseUrls`, kein `Kestrel:Endpoints` im Code. Bindung kommt aus Konfiguration: `appsettings.Production.json:10-16` setzt `Kestrel:Endpoints:Http:Url = http://*:5003` (Korrektur zur Anforderung: dort steht kein `Kestrel`-Block — in Production lauscht die App per Voreinstellung auf Port **5003** an allen Interfaces, nicht auf `localhost:5000`). Ohne `appsettings.Production.json` (z. B. Environment ≠ Production) gilt der ASP.NET-Core-Default `http://localhost:5000`. Überschreibbar per `ASPNETCORE_URLS` oder `Kestrel__Endpoints__Http__Url`.

## `HealthController` — `FinanceManager.Web/Controllers/HealthController.cs`

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `Get` | `GET /health`, `GET /api/health` (Zeilen 18–20) | `[AllowAnonymous]` (Zeile 10), liefert `200 OK` mit `{ status = "ok" }` — Smoke-Test-Endpunkt; wird auch vom Self-Update genutzt |

## `JwtOptionsValidator` — `FinanceManager.Infrastructure/Auth/JwtOptionsValidator.cs`

Produktionsnahe Start-Validierung (alle Umgebungen außer `Development`, `isProductionLike`, Zeile 40):

- `Jwt:Key`: Pflicht; bekannte Platzhalter (`PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64`, `CHANGE_ME`, `REPLACE_ME`, `TODO`) abgelehnt; mindestens **32 UTF-8-Bytes** (`MinimumProductionKeyBytes`, Zeilen 12, 69–83).
- `Jwt:Issuer`, `Jwt:Audience`: Pflicht in allen Umgebungen (Zeilen 42–50).
- `Jwt:LifetimeMinutes`: > 0; produktionsnah maximal **1440** (`MaximumProductionLifetimeMinutes`, Zeilen 13, 62–65).
- `Jwt:MinimumRenewalWindowMinutes`: > 0 (Zeilen 57–60).
- Verdrahtung: `.ValidateOnStart()` in `RegisterAppServices` (`ProgramExtensions.cs:293-297`) → der Host bricht bei ungültiger Produktionskonfiguration beim Start ab.

## Persistenz- und Schreibpfade (für Verzeichnislayout/Service-Account-Rechte)

| Pfad | Auflösung | Quelle |
|------|-----------|--------|
| `financemanager.db` | `Data Source=financemanager.db` relativ zum ContentRoot/Working Directory; überschreibbar über `ConnectionStrings:Default` | `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs:76,80` (2×), `ProgramExtensions.cs:100` |
| `backups/<userId-N>/` | `<ContentRoot>/backups/<userId>` (Guid ohne Bindestriche); Verzeichnis wird bei Bedarf angelegt | `FinanceManager.Infrastructure/Backups/BackupService.cs:82-87`; auch `SchemaPatcher.cs:114` |
| `init/` | `<ContentRoot>/init`, Fallback `AppContext.BaseDirectory/init`; `skip`-Datei überspringt Auto-Init | `FinanceManager.Infrastructure/Setup/AutoInitializationService.cs:99-117`; läuft beim Start am Ende von `ApplyMigrationsAndSeed` |
| `updates/` | `Updates:WorkingDirectory` (Default `updates`, `UpdateOptions.cs:46`) → `cfg.WithDownloadPath` für Pending-Paket, Staging, Lock, Skripte | `ProgramExtensions.cs:564-566`; `appsettings.json:49`; Repo enthält `FinanceManager.Web/updates/{pending,staging}` |
| `logs/` | `FileLogging:PathFormat` `logs/app-{date}.log`; relative Pfade werden gegen `AppContext.BaseDirectory` aufgelöst (Publish-Verzeichnis); in Production `FileLogging:Enabled = true` | `FileLoggerProvider.cs:220-226`; `ProgramExtensions.cs:71-75`; `appsettings.Production.json:54-62` |
| Data-Protection-Key-Ring | nur wenn `DataProtection:KeysPath` gesetzt → `PersistKeysToFileSystem`; sonst ASP.NET-Core-Plattformdefault | `ProgramExtensions.cs:156-161` |
| Anhänge | in der SQLite-Datenbank gespeichert (kein separates Dateiverzeichnis) | `FinanceManager.Infrastructure/Attachments/AttachmentService.cs:11` („storing … in the application database“) |

**ContentRoot vs. BaseDirectory:** Beim systemd-Betrieb ist `ContentRoot` = `WorkingDirectory` der Unit; `AppContext.BaseDirectory` = Publish-Verzeichnis (Ort von `FinanceManager.Web.dll`). `financemanager.db` und `backups/` liegen unter ContentRoot, `logs/` unter BaseDirectory.

## Deployment-Artefakt

- Release-Workflow `.github/workflows/release.yml` → Composite-Action `.github/actions/build-and-package/action.yml:53`: `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj --configuration Release --framework net10.0 --runtime <win-x64|linux-x64> --self-contained true --output publish/<rid>`.
- Assets: `FinanceManager-vX.Y.Z-win-x64.zip`, `FinanceManager-vX.Y.Z-linux-x64.zip` + `update.json`-Manifest (`scripts/generate-update-manifest.mjs`).
- Ziel-Framework `net10.0` (`FinanceManager.Web.csproj:6`); self-contained → keine .NET-Runtime auf dem Server nötig.
- Ausführbare Datei im Publish: `FinanceManager.Web` (Linux) bzw. `FinanceManager.Web.exe` (Windows).
- `appsettings.Production.json` enthält **keinen** `Jwt:Key` (verifiziert, Zeilen 17–21) — der Schlüssel muss per Environment bereitgestellt werden.

## `HelpContentCatalog` — `FinanceManager.Web/Services/Help/HelpContentCatalog.cs`

| Member | Typ | Kurzbeschreibung |
|--------|-----|------------------|
| `Topics` | `IReadOnlyList<HelpTopic>` (public static, Zeilen 72–87) | 13 publizierte Help-Topics; `systemverwaltung-und-setup` → `SetupDocuments` (Zeilen 46–53) |
| `TechnicalOnlyDocumentNames` | `IReadOnlySet<string>` (public static, Zeilen 93–101) | Technische Dateien, die nicht in der Benutzer-Help-Navigation erscheinen: `api.md`, `ablauf-technisch.md`, `bereitstellung.md`, `business-rules.md`, `datenmodell.md`, `index.md` — **hier kommt der neue Dateiname laut Anforderung hin** |
| `TryGetTopic` | public static | Topic-Auflösung per Id (Zeilen 109–114) |
| `TryResolveDocument` | public static | Route → Topic/Document/Markdown-Pfad; nur Katalog-Dokumente lösbar (Zeilen 126–161) |
| `IsCatalogMarkdownFile` | public static | Prüft, ob ein Pfad ein publiziertes Katalog-Dokument ist (Zeilen 169–176) |
| `HelpTopic` / `HelpTopicDocument` | records | Katalog-Einträge (Zeilen 194–209) |
