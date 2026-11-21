# Übersetzte Anforderung

## Fachliche Zusammenfassung

Es fehlt eine Server-Installationsdokumentation auf Betriebssystem-/Infrastrukturebene: Weder `README.md` noch `Docs/` beschreiben `dotnet publish`, eine systemd-Unit oder Server-Voraussetzungen. Zu erstellen ist eine eigenständige, schrittweise Anleitung unter `Docs/help/systemverwaltung-und-setup/`, die einen Leser ohne Vorwissen von „leerer Server" bis „FinanceManager läuft" führt — in zwei jeweils vollständigen Pfaden: eigenständig (HTTP auf eigenem Port, direkt erreichbar) und hinter einem Reverse-Proxy (externe TLS-Terminierung, `PathBase`-Routing, Weiterleitung der `X-Forwarded-*`-Header). Die neue Datei verlinkt auf die bestehende `installation.md` (In-App-Ersteinrichtung als Folgeschritt) und muss ohne Verweise auf andere — insbesondere private — Repositories auskommen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Dokumentationsartefakt — die neue Server-Installationsanleitung als Markdown-Datei inklusive ihrer Verankerungspunkte (`index.md`-Eintrag, gegenseitige Verlinkung mit `installation.md`, technische Einordnung im Help-Katalog). Kein Laufzeit-Feature, keine Entität.
- **Auslösende Ereignisse:** Kein Systemereignis — adressiert wird der Anwendungsfall „Betreiber ohne Projektvorwissen installiert FinanceManager auf einem eigenen Server" (getrieben durch Issue #558).
- **Erwartete Wirkung:** Der Leser kann beide Betriebsarten vollständig und ohne externe Quellen einrichten; Ergebnis ist jeweils ein laufender Dienst, der über `GET /health` bzw. `/api/health` (anonym, `HealthController.cs:18-20`) prüfbar ist und zur Erstregistrierung weiterleitet. Beim Proxy-Pfad sind TLS-Terminierung, `PathBase`-Unterpfad und korrekte Client-IP/Schema-Übernahme per `X-Forwarded-For`/`X-Forwarded-Proto` erreicht.
- **Umgang mit fehlenden Objekten:**
  - Server-Installationsanleitung: existiert nicht → **neu erzeugen** unter `Docs/help/systemverwaltung-und-setup/` (Dateiname in Planung, z. B. `server-installation.md`).
  - Eintrag in `Docs/help/systemverwaltung-und-setup/index.md` (Inhaltsliste): **ergänzen**.
  - Verlinkung: neue Datei → `installation.md` (Fortsetzung nach erfolgreichem Server-Setup); `installation.md` → neue Datei (vorgelagerter Schritt vor der In-App-Ersteinrichtung): **ergänzen**, keine inhaltliche Vermischung.
  - `HelpContentCatalog.TechnicalOnlyDocumentNames` (`FinanceManager.Web/Services/Help/HelpContentCatalog.cs:93-101`): Dateiname der neuen technischen Doku **ergänzen** — Konvention für technische, nicht benutzerseitig publizierte Dokumente.
- **Scope-Ausschlüsse:**
  - Keine Änderung am Anwendungscode oder an Laufzeitverhalten — die vorhandenen Mechanismen (`PathBase`, `ReverseProxy:KnownProxies`/`KnownNetworks`, `UseForwardedHeaders`) werden nur dokumentiert, nicht erweitert.
  - Keine Inhalte der In-App-Ersteinrichtung (`installation.md`: JWT-Konfiguration, Admin-Benutzer, Self-Update) duplizieren — nur verlinken.
  - Keine Verweise auf andere Repositories — weder auf private (die im Issue genannte Pattern-Sammlung darf weder verlinkt noch benannt werden) noch auf weitere öffentliche Repos; die Anleitung muss für sich allein stehen.
  - Zielplattform der vollständigen Anleitung ist Linux mit systemd (aus dem Hintergrund: gehärteter Service-Account, systemd-Unit ableitbar). Windows-Dienst- und Container-/Docker-Szenarien sind kein geforderter vollständiger Pfad — höchstens als kurzer Hinweis, sonst bewusst ausgeschlossen.
  - Kein neuer Dokumentationsbereich außerhalb `Docs/help/systemverwaltung-und-setup/`; keine Erweiterung der benutzerseitigen `/help`-Navigation (die Datei enthält zwingend technische Details und darf nicht in den publizierten Katalog).

## Betroffene Klassen und Komponenten

### Dokumentationsartefakte (neu/zu ändern)

- `Docs/help/systemverwaltung-und-setup/<neue-datei>.md` — neu; zwei vollständige Installationspfade (mit/ohne Reverse-Proxy) von „leerer Server" bis „Anwendung läuft".
- `Docs/help/systemverwaltung-und-setup/index.md` — Inhaltseintrag für die neue Datei.
- `Docs/help/systemverwaltung-und-setup/installation.md` — ergänzender Verweis auf die neue Server-Setup-Anleitung als vorgelagerten Schritt.
- `Docs/help/systemverwaltung-und-setup/bereitstellung.md` — optionaler Querverweis (beschreibt bereits das Release-Artefakt; Entscheidung in Planung).

### Code (minimal)

- `FinanceManager.Web/Services/Help/HelpContentCatalog.cs` — `TechnicalOnlyDocumentNames` um den neuen Dateinamen ergänzen. Kein Eintrag in `Topics`/`SetupDocuments`.

### Zu dokumentierende Bestandsfakten (Referenzpunkte, nicht zu ändern)

- `FinanceManager.Web/ProgramExtensions.cs` (`ConfigureMiddleware`, ca. Zeilen 616-696):
  - `PathBase`: Konfigurationsschlüssel `PathBase` (z. B. `/finance`) → `app.UsePathBase(...)` als erste Middleware; nicht in `appsettings.json` hinterlegt, also per Environment-Variable `PathBase` setzbar.
  - `UseForwardedHeaders` mit `ForwardedHeaders.XForwardedFor | XForwardedProto`, `ForwardLimit = 1`; vertrauenswürdige Proxies ausschließlich aus `ReverseProxy:KnownProxies` (IPs) / `ReverseProxy:KnownNetworks` (CIDR, semikolongetrennt); ohne Einträge werden keine Forwarded-Headers honoriert.
  - `UseHttpsRedirection` aktiv außerhalb Development-Opt-out; ohne konfigurierten HTTPS-Endpunkt nur einmalige Warnung im Log, Requests laufen durch (für den eigenständigen HTTP-Pfad zu erläutern).
  - `UseHsts` nur außerhalb Development; HSTS-Header wird nur auf HTTPS-Requests gesetzt (hinter Proxy mit `X-Forwarded-Proto: https` wirksam).
- `FinanceManager.Web/appsettings.json`: `AllowedHosts: "*"` (einschränkbar, semikolongetrennte Hostliste), `Kestrel:Limits:MaxRequestBodySize` = 20 MB, `Jwt:*` (produktionsnah per `ValidateOnStart` geprüfte Pflichtwerte: `Jwt__Key` ≥ 32 UTF-8-Bytes, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`), `Updates:ServiceName` (Linux = systemd-Service-Name für Self-Update), `Updates:WorkingDirectory` = `updates`, `FileLogging` (optional `logs/`).
- Persistenz und Schreibpfade (für Verzeichnislayout/Service-Account-Rechte der Anleitung):
  - SQLite-Datenbank `financemanager.db` relativ zum ContentRoot (`FinanceManager.Infrastructure/ServiceCollectionExtensions.cs:76`); EF-Migrationen laufen automatisch beim Start (`ApplyMigrationsAndSeed`, `ProgramExtensions.cs:827-848`) — der Erststart legt das Schema an.
  - Benutzer-Backups unter `<ContentRoot>/backups/<userId>/` (`FinanceManager.Infrastructure/Backups/BackupService.cs:82-85`).
  - Init-Verzeichnis `<ContentRoot>/init` (`FinanceManager.Infrastructure/Setup/AutoInitializationService.cs:100`).
  - Update-Arbeitsverzeichnis `<ContentRoot>/updates`; optional `DataProtection:KeysPath` für den Data-Protection-Key-Ring auf persistentem Pfad.
- Deployment-Artefakt: self-contained Publish `FinanceManager-vX.Y.Z-linux-x64.zip` (plus `win-x64`) aus GitHub-Releases mit `update.json`-Manifest (`bereitstellung.md`, `.github/workflows/release.yml`); alternativ `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj -c Release -r linux-x64 --self-contained true`. Ausführbare Datei: `FinanceManager.Web`. Ziel-Framework `net10.0`; für self-contained kein .NET-Runtime auf dem Server nötig.
- Listener-Konfiguration: kein `UseUrls`/festes `Kestrel:Endpoints` im Code — Bindung über `ASPNETCORE_URLS` bzw. `Kestrel__Endpoints__Http__Url` (Default `http://localhost:5000`).
- Environment: `ASPNETCORE_ENVIRONMENT=Production` für produktive Starts (steuert `ValidateOnStart`, `UseHsts`, Fehlerseiten).

### Tests

- `FinanceManager.Tests/Web/Help/HelpContentCatalogTests.cs` — `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` (Zeilen 144-164) würde fehlschlagen, wenn die neue Datei in `HelpContentCatalog.Topics` landet (Marker wie `appsettings`, `Jwt:`, `Endpunkt`); daher bewusst kein Katalog-Eintrag, sondern `TechnicalOnlyDocumentNames`. Bestehende Asserts (`Topics_ContainsReviewedUserHelpTopicsOnly` mit fixer Topic-Anzahl 13) bleiben unverändert.
- Keine neuen Tests erforderlich (Dokumentationsaufgabe); manueller/gedanklicher Durchlauf der Anleitung ist Akzeptanzkriterium.

## Implementierungsansatz

- Reine Dokumentationsarbeit plus einer minimalen Code-Ergänzung (`TechnicalOnlyDocumentNames`). Keine neuen Klassen, Services, Interfaces oder Enums; keine EF-Migration; keine Ribbon-/UI-Änderung.
- Neue Markdown-Datei in zwei klar getrennten, jeweils in sich vollständigen Abschnitten aufbauen:
  - **Gemeinsamer Vorspann:** Voraussetzungen (Linux-Server, kein .NET-Runtime wegen self-contained), Bezug des Artefakts (Release-ZIP oder `dotnet publish`-Kommando), Zielverzeichnis (z. B. `/opt/financemanager`), dedizierter unprivilegierter Service-Account, benötigte Schreibpfade (`financemanager.db`, `backups/`, `updates/`, `init/`, ggf. `logs/`, `DataProtection:KeysPath`), Pflicht-Umgebungsvariablen (`ASPNETCORE_ENVIRONMENT=Production`, `Jwt__Key`/`Jwt__Issuer`/`Jwt__Audience`/`Jwt__LifetimeMinutes`), systemd-Unit inkl. Hardening (`User=`, `ProtectSystem`, `PrivateTmp`, `NoNewPrivileges`, `ReadWritePaths` für die Schreibpfade) und `Updates:ServiceName`-Hinweis für das Self-Update, Smoke-Test über `/health`, Abschlussverweis auf `installation.md` für Erstbenutzer/In-App-Setup.
  - **Pfad „ohne Reverse-Proxy":** Listener auf gewünschtem Port (`ASPNETCORE_URLS`/`Kestrel__Endpoints__Http__Url`), Firewall-/Portfreigabe, direkte Erreichbarkeit per IP/Hostname, Hinweis zum `UseHttpsRedirection`-Verhalten ohne HTTPS-Endpunkt und zum HSTS-Verhalten auf reinem HTTP, optionale `AllowedHosts`-Einschränkung.
  - **Pfad „mit Reverse-Proxy":** Bindung auf Loopback oder internen Port, TLS-Terminierung am Proxy (Beispiel-Proxy-Konfiguration inkl. Weiterleitung `X-Forwarded-For`/`X-Forwarded-Proto`), `PathBase`-Konfiguration für pfadbasiertes Routing (z. B. `PathBase=/finance`), `ReverseProxy__KnownProxies`/`ReverseProxy__KnownNetworks` mit den Proxy-Adressen, `AllowedHosts` auf den öffentlichen Hostnamen.
- Verankerung: Eintrag in `Docs/help/systemverwaltung-und-setup/index.md`, Rückverweis aus `installation.md` (neue Anleitung als vorgelagerter Schritt; `installation.md` bleibt die Fortsetzung für die In-App-Ersteinrichtung).
- Nebenbedingungen: keine Verweise auf andere/private Repositories; Schritt-für-Schritt ohne Vorwissen zum Infrastruktur-Projekt; gedanklicher oder realer Durchlauf als Prüfung (Akzeptanzkriterium).
- Build-Hinweis: `Docs/help/**` wird mit publiziert (`FinanceManager.Web.csproj`, `CopyToPublishDirectory="PreserveNewest"`); der Build erzeugt Help-Suchindizes und `help-assets.sha256` automatisch neu — kein manueller Zusatzschritt, aber ein Build/Testlauf nach der Änderung ist sinnvoll.

## Konfiguration

Kein neues konfigurierbares Verhalten. Die Anleitung beschreibt ausschließlich bestehende Konfigurationsebenen: Anwendungseinstellungen per Environment-Variablen bzw. `appsettings`-Überschreibungen (`PathBase`, `ReverseProxy:KnownProxies`/`KnownNetworks`, `AllowedHosts`, `Jwt:*`, `DataProtection:KeysPath`, `Updates:ServiceName`, `Kestrel`/`ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`).

## Offene Fragen

1. **Referenz-Proxy:** Welche Reverse-Proxy-Software soll die Anleitung konkret beschreiben (z. B. Caddy mit automatischem TLS, nginx plus certbot/acme.sh oder Apache)? „Automatisches TLS" im Hintergrund deutet auf eine Lösung mit eingebauter Zertifikatsverwaltung hin; eine Referenz-Implementierung ist zu wählen, Alternativen ggf. als Kurzhinweis.
2. **Zielplattform-Umfang:** Ist Linux mit systemd als einziger vollständiger Pfad korrekt, oder soll zusätzlich ein Windows-Dienst-Pfad knapp beschrieben werden (Self-Update unterstützt beide Plattformen)?
3. **Dateiname und Einordnung:** Konkreter Dateiname (Vorschlag `server-installation.md`) und Platzierung im `index.md`-Inhaltsverzeichnis; soll die Datei zusätzlich aus `bereitstellung.md` und/oder `README.md` verlinkt werden?
4. **Eigenständiger TLS-Betrieb:** Soll der Pfad „ohne Reverse-Proxy" optional auch Kestrel-TLS (eigenes Zertifikat, `Kestrel:Endpoints:Https`) beschreiben, oder bewusst nur HTTP/Direktzugriff (z. B. LAN)?
5. **systemd-Hardening-Tiefe:** Soll die Beispiel-Unit das volle Hardening-Profil (Service-Account, `ProtectSystem=strict`, `ReadWritePaths`, `PrivateTmp`, `NoNewPrivileges`) widerspiegeln — Annahme: ja, ohne externe Referenz, die Direktiven werden im Repo selbst erklärt?
6. **PathBase-Varianten:** Soll nur der Sub-Path-Fall (`PathBase=/finance`) oder zusätzlich der Domain-Root-Fall hinter dem Proxy (kein `PathBase` nötig) dokumentiert werden?
