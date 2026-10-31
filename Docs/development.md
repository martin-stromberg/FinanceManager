# Entwicklung

Diese Dokumentation ergänzt die [README](../README.md) um Details für Entwickler und Betreiber.

## Inhalt

- [Lokale Ausführung](#lokale-ausf%C3%BChrung)
- [Demodaten lokal testen](#demodaten-lokal-testen)
- [Konfiguration](#konfiguration)
- [Authentifizierung und Sitzungserhaltung](#authentifizierung-und-sitzungserhaltung)
- [Relevante Endpunkte](#relevante-endpunkte)
- [Tests](#tests)
- [Git-Hooks und Codequalität](#git-hooks-und-codequalit%C3%A4t)

## Lokale Ausführung

### Voraussetzungen

- [.NET SDK 10.0](https://dotnet.microsoft.com)

Für release-nahe Aufgaben zusätzlich:

- [Node.js 22.x](https://nodejs.org/)

### Starten

```bash
dotnet restore
dotnet build FinanceManager.sln
dotnet run --project FinanceManager.Web
```

Entwicklungsprofile aus `FinanceManager.Web/Properties/launchSettings.json`:

- `http://localhost:5208`
- `https://localhost:7013`

Beim Start der Webanwendung werden in `Program.cs` und `ProgramExtensions.cs` unter anderem:

- Services und Logging registriert,
- EF-Core-Migrationen ausgeführt (`ApplyMigrationsAndSeed()`),
- gespeicherte Update-Einstellungen angewendet,
- Middleware, Authentifizierung und Routing konfiguriert.

## Demodaten lokal testen

Der Demodaten-Bestand lässt sich lokal ohne externe Dienste erzeugen:

1. `dotnet publish FinanceManager.Web -c Release` aus dem Repository-Root ausführen.
2. Die App aus dem Publish-Verzeichnis (`FinanceManager.Web/bin/Release/net10.0/publish/`) starten. Die benötigten Umgebungsvariablen stehen vollständig in [`scripts/screenshots/generate-screenshots.js`](../scripts/screenshots/generate-screenshots.js): freier Port für `ASPNETCORE_URLS` und `Api__BaseAddress`, frische SQLite-Datei über `ConnectionStrings__Default`, `BackgroundTasks__Enabled=true` sowie deaktivierte externe Worker (`Workers__SecurityPriceWorker__Enabled=false`, `Updates__HostedServicesEnabled=false`).
3. Auf `/register` den ersten Benutzer mit aktivierter Checkbox `Demodaten anlegen` registrieren; die Registrierung meldet den Browser nicht an, anschließend über `/login` einloggen.
4. Der Hintergrundtask `CreateDemoData` legt danach Stammdaten, Konten, Wertpapiere, Budgets und gebuchte Kontoauszüge für 24 Monate an (~1 Minute). Danach zeigen Startseite, Listen und Berichte den Demo-Datenbestand.

Für die automatisierte Screenshot-Erzeugung siehe [Docs/screenshot-generation.md](screenshot-generation.md).

## Konfiguration

Die wichtigsten Standardwerte stammen aus `FinanceManager.Web/appsettings.json`, `appsettings.Development.json` und `appsettings.Production.json`.

| Schlüssel | Standardwert | Bedeutung |
|---|---|---|
| `ConnectionStrings:Default` | Fallback auf `Data Source=financemanager.db` | Standarddatenbank für die Infrastruktur |
| `Jwt:Issuer` | `financemanager` | JWT-Issuer |
| `Jwt:Audience` | `financemanager` | JWT-Audience |
| `Jwt:LifetimeMinutes` | `30` | Gültigkeitsdauer der JWT-/Auth-Sitzung |
| `Jwt:MinimumRenewalWindowMinutes` | `5` | Positive Untergrenze des Erneuerungsfensters; effektiv gilt das Maximum aus Untergrenze und halber Tokenlaufzeit |
| `DataProtection:KeysPath` | leer | Optionaler persistenter Speicherort für Data-Protection-Keys |
| `Api:BaseAddress` | leer | Basisadresse für API-/Security.txt-bezogene Fallbacks |
| `BackgroundTasks:Enabled` | `true` | Aktiviert den Background-Task-Runner |
| `Workers:SecurityPriceWorker:Enabled` | `true` | Aktiviert den Kurs-Worker |
| `Updates:Enabled` | `false` | Aktiviert die Update-Funktionen |
| `Updates:SourceType` | `Github` | Update-Quelle (`Github` oder `LocalFolder`) |
| `Updates:RepositoryOwner` | `martin-stromberg` | Eigentümer des Release-Repositories |
| `Updates:RepositoryName` | `FinanceManager` | Name des Release-Repositories |
| `Updates:ManifestAssetName` | `update.json` | Manifest-Datei für Updates |
| `Updates:WorkingDirectory` | `updates` | Arbeitsverzeichnis des Update-Systems |
| `Updates:HealthTimeoutSeconds` | `120` | Timeout für Health-basierte Update-Prüfungen |
| `Backups:Security:MaxUploadBytes` | `104857600` | Maximale Backup-Uploadgröße |
| `FileLogging:Enabled` | `false` in `appsettings.json`, `true` in `appsettings.Production.json` | Schaltet Dateilogging ein/aus |
| `Identity:Lockout:MaxFailedAccessAttempts` | `3` | Maximale Fehlversuche bis zum Lockout |
| `Identity:Password:RequiredLength` | `8` | Minimale Passwortlänge |
| `Auth:AllowSelfRegistration` | `false` | Öffnet die Selbstregistrierung für weitere Benutzer; Standard: nur der erste Benutzer registriert sich selbst |
| `MassImport:MaxRequestBytes` | `150000000` | Request-Body-Limit des Massenimport-Endpunkts (deckt die Base64-Inflation von `MaxTotalContentBytes` ab) |
| `MassImport:MaxFiles` | `50` | Maximale Dateianzahl pro Massenimport-Batch |
| `MassImport:MaxBytesPerFile` | `10485760` | Maximale Rohbytes pro Datei im Massenimport (10 MB) |
| `MassImport:MaxTotalContentBytes` | `104857600` | Maximale Summe aller Dateiinhalte pro Batch (100 MB) |
| `Attachments:MaxSizeBytes` | `10485760` | Upload-Größenlimit für Anhänge, Symbol- und Kursimport-Uploads (10 MB) |
| `RateLimiting:AuthSensitive:PermitLimit` | `20` | Zulässige Aufrufe je Zeitfenster für die `auth-sensitive`-Endpunkte (Registrierung, `users/exists`) |
| `RateLimiting:AuthSensitive:WindowSeconds` | `10` | Fenstergröße des Rate-Limits in Sekunden |
| `ReverseProxy:KnownProxies` | leer | Semikolongetrennte IPs vertrauenswürdiger Reverse-Proxies, deren Forwarded-Headers berücksichtigt werden |
| `ReverseProxy:KnownNetworks` | leer | Semikolongetrennte CIDR-Netze vertrauenswürdiger Proxies |
| `AllowedHosts` | `*` | Host-Header-Filterung; `*` lässt LAN-Zugriff per IP/Hostname zu — deployment-spezifisch einschränkbar |

### Wichtige Hinweise zur Produktionskonfiguration

- In produktionsnahen Umgebungen validiert `JwtOptionsValidator` die JWT-Konfiguration bereits beim Start.
- `Jwt:Key` darf dort nicht leer sein, kein Platzhalterwert sein und muss mindestens **32 UTF-8-Bytes** enthalten.
- Wenn `security.txt` keinen expliziten `Canonical`-Wert hat, erwartet der Fallback `Api:BaseAddress` eine gültige absolute URI.
- Für geschützte persistierte Secrets, z. B. AlphaVantage-Zugangsdaten, sollte `DataProtection:KeysPath` auf einen persistenten Speicher zeigen.
- Läuft die Anwendung hinter einem Reverse-Proxy, müssen dessen Adressen über `ReverseProxy:KnownProxies`/`ReverseProxy:KnownNetworks` eingetragen werden — sonst werden Forwarded-Headers ignoriert und Client-IP sowie Schema fallen auf die direkte Verbindung zurück. Direkter HTTP-Zugriff im LAN funktioniert ohne diese Konfiguration.
- `DetailedErrors` und die `E2E:*`-Testschalter werden nur noch unter `Development` ausgewertet und sind in produktiven Konfigurationen wirkungslos.

Typische Environment-Variablen sind beispielsweise:

- `ConnectionStrings__Default`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__LifetimeMinutes`
- `DataProtection__KeysPath`
- `Api__BaseAddress`
- `Auth__AllowSelfRegistration`
- `ReverseProxy__KnownProxies` / `ReverseProxy__KnownNetworks`

## Authentifizierung und Sitzungserhaltung

Die Anwendung verwendet JWT-basierte Authentifizierung mit Cookie-Transport:

- Login: `POST /api/auth/login`
- Registrierung: `POST /api/auth/register`
- Logout: `POST /api/auth/logout`
- Keepalive: `GET /api/auth/keepalive`

Wichtige Punkte aus dem aktuellen Code:

- Das Auth-Cookie heißt **`FinanceManager.Auth`**.
- Die konfigurierte Standardlaufzeit beträgt **30 Minuten** (`Jwt:LifetimeMinutes`).
- Beide Refreshpfade verwenden dasselbe Fenster: `max(Jwt:MinimumRenewalWindowMinutes, Jwt:LifetimeMinutes / 2.0)`, standardmäßig 15 Minuten bei einer Untergrenze von 5 Minuten.
- `JwtRefreshMiddleware` erneuert Tokens automatisch, sobald sie in ihr Renewal-Fenster kommen; erneuerte Tokens werden ausschließlich über das HttpOnly-Cookie und serverseitig an den Circuit weitergegeben (`CircuitAuthTokenStore`) — es werden keine Token in Response-Headern oder im gerenderten HTML ausgeliefert.
- `JwtRefreshService` validiert vor einem Refresh den Benutzerzustand, den `security_stamp` und die aktuelle Admin-Rolle erneut gegen die Datenbank.
- `MainLayout.razor` und `wwwroot/js/financeManager.js` triggern Keepalive-Aufrufe bei Navigation sowie bei Benutzerinteraktionen wie `pointerdown`, `keydown`, `focusin`, `input` und Quick-Edit-`blur`.
- Ein fehlgeschlagener Keepalive-Aufruf führt nicht selbst direkt zu einer Umleitung; die Umleitung auf geschützten Routen erfolgt über die reguläre Authentifizierungsprüfung.
- `POST /api/auth/register` akzeptiert standardmäßig nur den ersten Benutzer (`Auth:AllowSelfRegistration`); Registrierung und `GET /api/users/exists` unterliegen der Rate-Limit-Policy `auth-sensitive`.
- Zustandsändernde Requests, die über das Auth-Cookie authentifiziert sind, benötigen ein Antiforgery-Token (`RequestVerificationToken`-Header); Bearer-authentifizierte Aufrufe sind davon ausgenommen.

Damit ist die in den Feature-Unterlagen beschriebene Sitzungserhaltung für aktive Benutzer explizit im aktuellen Codepfad abgebildet.

## Relevante Endpunkte

Eine Auswahl konkreter, im Repository vorhandener Einstiegspunkte:

- `GET /health`
- `GET /api/health`
- `POST /api/auth/login`
- `POST /api/auth/register`
- `POST /api/auth/logout`
- `GET /api/auth/keepalive`
- `POST /api/statement-drafts/upload`
- `POST /api/statement-drafts/mass-import`
- `POST /api/statement-drafts/preliminary`
- `GET /api/portfolio/analysis-report`
- `GET /api/portfolio/kpi-configuration`
- `POST /api/portfolio/kpi-configuration`
- `GET /api/setup/update/status`
- `GET /api/setup/update/settings`
- `PUT /api/setup/update/settings`
- `GET /security.txt`
- `GET /.well-known/security.txt`
- `GET /.well-known/security.md`
- `GET /.well-known/security.html`

Die Controller liegen unter `FinanceManager.Web/Controllers/`.

## Tests

Die Session-Ablauftests laufen mit echtem Formularlogin und unveränderter Uhr standardmäßig mit **3 Minuten** Tokenlaufzeit. Nur ihr isolierter Webhost setzt `Jwt:MinimumRenewalWindowMinutes=1`; dadurch beträgt das effektive Fenster 1,5 Minuten. Der unveränderte Browser-Throttle von 60 Sekunden lässt innerhalb dieses Fensters eine Erneuerung zu. Navigation, Idle und beide Quick-Edit-Fälle überschreiten weiterhin das ursprüngliche `exp` samt zehn Sekunden ClockSkew. Die vier Pflichtfälle benötigen zusammen ungefähr 14 Minuten statt über 40 Minuten; weitere Tests und Hoststarts kommen hinzu.

`E2E_SESSION_LIFETIME_MINUTES=30` wählt weiterhin den regulären 30-Minuten-Referenzlauf. Ab zehn Minuten Laufzeit verwendet die Fixture die produktive Untergrenze von fünf Minuten; bei drei bis neun Minuten beträgt sie eine Minute. Laufzeiten unter drei Minuten werden abgelehnt. Ohne explizite Konfiguration bleiben sämtliche produktiven JWT-Standardwerte unverändert.

Die Testprojekte in der Solution sind:

- `FinanceManager.Tests`
- `FinanceManager.Tests.Integration`
- `FinanceManager.Tests.E2E`

Frameworks laut Projektdateien:

- **xUnit v3**
- **FluentAssertions**
- **bUnit**
- **Microsoft.AspNetCore.Mvc.Testing**
- **Microsoft.Playwright**

Alle Tests der Solution starten:

```bash
dotnet test FinanceManager.sln
```

Die aktuellen Testdateien enthalten unter anderem Abdeckung für:

- Login, Registrierung, Logout
- JWT-Validierung und Refresh-Verhalten
- Keepalive bei aktiver Navigation und Interaktion
- Quick-Edit-Verhalten in Kontoauszugsentwürfen
- Summen, Filterung und Fehlerzustände der Bankübersicht

## Git-Hooks und Codequalität

Das Repository verwendet Git-Hooks aus dem Verzeichnis **`.githooks/`**. Aktivierung nach dem Klonen:

```cmd
.githooks\install-hooks.cmd
```

bzw. unter Linux/macOS:

```bash
./.githooks/install-hooks.sh
```

Die Skripte setzen `core.hooksPath` auf `.githooks`. Der **pre-commit**-Hook blockiert Commits auf `main`/`staging` und prüft Übersetzungskonsistenz, XML-Dokumentation, hartkodierte UI-Texte in Razor-Komponenten, unreferenzierte Komponenten, Stub-Implementierungen (`NotImplementedException`) und die Enum-Testabdeckung. Der **pre-push**-Hook führt die Stub-, Komponenten- und Enum-Prüfungen strikt für das gesamte Repository aus.
