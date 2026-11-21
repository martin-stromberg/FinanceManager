# Umsetzungsplan: Server-Installationsdokumentation (Issue #558)

## Übersicht

Es wird eine eigenständige Server-Installationsanleitung unter `Docs/help/systemverwaltung-und-setup/server-installation.md` erstellt, die einen Betreiber ohne Projektvorwissen von „leerer Linux-Server" bis „FinanceManager läuft" führt — in zwei getrennten, vollständigen Pfaden: ohne Reverse-Proxy (HTTP direkt) und mit Reverse-Proxy (TLS-Terminierung, `PathBase`, `X-Forwarded-*`). Verankerung über `index.md`-Eintrag, gegenseitige Verlinkung mit `installation.md`, Querverweis aus `bereitstellung.md` und Aufnahme des Dateinamens in `HelpContentCatalog.TechnicalOnlyDocumentNames`. Betroffen sind ausschließlich Dokumentationsartefakte plus eine einzeilige Katalog-Ergänzung — kein Laufzeitverhalten.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Dokumentationsartefakt (`server-installation.md`) inklusive seiner Verankerungspunkte (`index.md`-Eintrag, Verlinkung mit `installation.md`, Querverweis `bereitstellung.md`, Eintrag in `HelpContentCatalog.TechnicalOnlyDocumentNames`). Kein Laufzeit-Feature, keine Entität. |
| Auslösende Ereignisse | Kein Systemereignis — Anwendungsfall „Betreiber ohne Projektvorwissen installiert FinanceManager auf eigenem Server" (Issue #558). |
| Erwartete Wirkung | Der Leser erreicht in beiden Betriebsarten einen laufenden Dienst, prüfbar über `GET /health` bzw. `/api/health` (anonym, `FinanceManager.Web/Controllers/HealthController.cs:18-20`), und wird zur In-App-Ersteinrichtung (`installation.md`) weitergeleitet. Im Proxy-Pfad sind TLS-Terminierung am Proxy, `PathBase`-Unterpfad und korrekte Client-IP/Schema-Übernahme per `X-Forwarded-For`/`X-Forwarded-Proto` dokumentiert. |
| Umgang mit fehlenden Objekten | `server-installation.md`: existiert nicht → **neu erzeugen** unter `Docs/help/systemverwaltung-und-setup/`. Eintrag in `index.md`-Inhaltsliste: **ergänzen**. Verlinkung neu → `installation.md` (Folgeschritt) und `installation.md` → neu (vorgelagerter Schritt): **ergänzen**, ohne Inhalte zu duplizieren. `HelpContentCatalog.TechnicalOnlyDocumentNames` (`FinanceManager.Web/Services/Help/HelpContentCatalog.cs:93-101`): Dateiname **ergänzen** — nicht in `Topics`/`SetupDocuments`. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Änderung am Anwendungscode oder Laufzeitverhalten | Die Mechanismen (`PathBase`, `ReverseProxy:KnownProxies`/`KnownNetworks`, `UseForwardedHeaders`, `UseHttpsRedirection`, `UseHsts`, `ValidateOnStart`) werden nur dokumentiert, nicht erweitert. | bestätigt (2026-11-21) |
| 2 | Keine inhaltliche Duplikation der In-App-Ersteinrichtung | `installation.md` (JWT-Referenz, Admin-Anlage, Self-Update, Konfigurationstabelle) bleibt Folgeschritt; die neue Datei verlinkt nur und wiederholt keine Parameter-Referenz. | bestätigt (2026-11-21) |
| 3 | Keine Verweise auf andere Repositories — auch keine öffentlichen | Die Anleitung muss für sich allein stehen; die im Issue genannte private Pattern-Sammlung darf weder verlinkt noch benannt werden. Alle systemd-/Proxy-Beispiele werden inline erklärt. | bestätigt (2026-11-21) |
| 4 | Vollständiger Pfad nur für Linux mit systemd | Windows-Dienst- und Container-/Docker-Szenarien sind kein geforderter vollständiger Pfad — höchstens ein kurzer Hinweiskasten, dass das Artefakt auch als `win-x64` existiert. | bestätigt (2026-11-21) |
| 5 | Kein neuer Dokumentationsbereich außerhalb `Docs/help/systemverwaltung-und-setup/`; keine Publikation in der benutzerseitigen `/help`-Navigation | Die Datei enthält zwingend technische Marker (`appsettings`, `Jwt:`, `Endpunkt`, systemd-Direktiven) und würde `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` (`FinanceManager.Tests/Web/Help/HelpContentCatalogTests.cs:144-164`) brechen; daher `TechnicalOnlyDocumentNames`. | bestätigt (2026-11-21) |
| 6 | Kein vollständiger Kestrel-TLS-Pfad im Abschnitt „ohne Reverse-Proxy" | Zertifikatsbeschaffung/-rotation an Kestrel übersteigt den Nutzen gegenüber dem Proxy-Pfad; der eigenständige Pfad beschreibt HTTP/Direktzugriff (LAN/intern) mit Hinweis, dass für öffentliche TLS-Terminierung der Proxy-Pfad gedacht ist. Empfehlung — siehe Offene Punkte #4. | bestätigt (2026-11-21) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Dateiname | `server-installation.md` | Passt zur gemischt de/en-Namenskonvention des Verzeichnisses (`installation.md`, `bereitstellung.md`, `troubleshooting.md`) und grenzt sich als „Server-/OS-Ebene" vom In-App-`installation.md` ab. |
| Dokumentstruktur | Gemeinsamer Basisteil + zwei eigenständige Varianten-Abschnitte (A: ohne Proxy, B: mit Proxy) | „Jeweils vollständig" wird erfüllt, indem jeder Pfad linear aus Basisteil + Varianten-Abschnitt gelesen werden kann; vermeidet Drift durch doppelt gepflegte redundante Kapitel. Jeder Varianten-Abschnitt enthält alle pfadspezifischen Schritte vollständig. |
| Sprache der Datei | Deutsch, Stil und Aufbau der vorhandenen Dateien (Header `← [Zurück zur Übersicht](index.md)`, Titel `# Systemverwaltung und Setup — …`) | Alle Dateien im Verzeichnis sind deutschsprachig; es existieren keine lokalisierten `*.en.md`-Varianten. |
| Secret-Bereitstellung in der systemd-Unit | `EnvironmentFile=` mit restriktiven Rechten (`root:`/`0600`) für `Jwt__Key` & Co., statt inline `Environment=` | Hält Secrets aus `systemctl show`-Ausgaben und Unit-Dateien, die häufig weiter verteilt werden; wird im Dokument begründet und inline erklärt. |
| Referenz-Reverse-Proxy | Caddy als durchgearbeitetes Beispiel (eingebautes automatisches TLS, minimale Konfiguration); nginx als kurzer Alternativ-Hinweisblock | „Automatische TLS-Terminierung" aus dem Issue-Hintergrund wird mit Caddy ohne Zusatzwerkzeuge erfüllt; nginx/cerbot bleibt als kompakter Hinweis erwähnt. Empfehlung — siehe Offene Punkte #1. |
| Katalog-Einordnung | `TechnicalOnlyDocumentNames` (HashSet, OrdinalIgnoreCase) | Konvention für technische, nicht benutzerseitig publizierte Dokumente; ein `Topics`-Eintrag würde zwei Tests brechen (Marker-Test + fixe Topic-Anzahl 13). |

## Programmabläufe

Für diese Dokumentationsaufgabe sind die „Programmabläufe" die in der neuen Datei beschriebenen Installationsabläufe. Sie bilden den inhaltlichen Aufbau von `server-installation.md` ab.

### Basisteil (für beide Pfade identisch)

1. Voraussetzungen: Linux-Server mit systemd, Root-/sudo-Zugriff, keine .NET-Runtime nötig (self-contained Publish, Ziel `net10.0`).
2. Artefakt beschaffen: Release-ZIP `FinanceManager-vX.Y.Z-linux-x64.zip` von den GitHub-Releases des Repositories (verlinkt nur auf die in `bereitstellung.md` dokumentierte Artefakt-Beschreibung) **oder** lokaler `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj -c Release -r linux-x64 --self-contained true` und Transfer auf den Server.
3. Dedizierten unprivilegierten Service-Account anlegen (`useradd --system`, kein Login).
4. Zielverzeichnis `/opt/financemanager` anlegen, ZIP entpacken, Besitz auf den Service-Account setzen; ausführbare Datei `FinanceManager.Web`.
5. Schreibpfade erläutern und Rechte setzen: `financemanager.db` (ContentRoot, `ServiceCollectionExtensions.cs:76`), `backups/`, `updates/`, `init/` (jeweils ContentRoot = `WorkingDirectory` der Unit) sowie `logs/` (unter `AppContext.BaseDirectory` = Publish-Verzeichnis, in Production aktiviert, `appsettings.Production.json:54-62`); optional persistenter `DataProtection:KeysPath`.
6. Environment-Datei mit Pflichtwerten anlegen: `ASPNETCORE_ENVIRONMENT=Production`, `Jwt__Key` (≥ 32 UTF-8-Bytes, Erzeugung per `openssl rand -base64 48` inline gezeigt), `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`; Hinweis auf `ValidateOnStart`-Abbruch bei Fehlkonfiguration (`JwtOptionsValidator`).
7. systemd-Unit `/etc/systemd/system/financemanager.service` vollständig abdrucken: `User=`/`Group=`, `WorkingDirectory=/opt/financemanager`, `ExecStart=/opt/financemanager/FinanceManager.Web`, `EnvironmentFile=`, Hardening (`ProtectSystem=strict`, `ReadWritePaths=` für die Schreibpfade, `PrivateTmp`, `NoNewPrivileges`), `Restart=on-failure`; jede Direktive wird inline kurz erklärt. Hinweis: `Updates__ServiceName=financemanager` setzen, damit das Self-Update den Dienst kontrolliert.
8. `systemctl daemon-reload`, `enable --now`, Status/Log prüfen (`journalctl -u`), Smoke-Test `curl http://localhost:<port>/health` → `{ "status": "ok" }`; Erklärung, dass der Erststart das SQLite-Schema per `ApplyMigrationsAndSeed` anlegt.

Beteiligte Referenzpunkte: `ProgramExtensions.cs` (`ApplyMigrationsAndSeed`, `ConfigureMiddleware`), `JwtOptionsValidator.cs`, `appsettings.Production.json`, `ServiceCollectionExtensions.cs`, `BackupService.cs`, `AutoInitializationService.cs`, `FileLoggerProvider.cs`.

### Pfad A — Betrieb ohne Reverse-Proxy

1. Listener: Production-Default ist `Kestrel:Endpoints:Http:Url = http://*:5003` (`appsettings.Production.json:10-16`); Port anpassen per `Kestrel__Endpoints__Http__Url` oder `ASPNETCORE_URLS` in der Environment-Datei (kein `UseUrls` im Code).
2. Firewall-Öffnung des gewählten Ports (Beispiel `ufw`/`firewalld` als generische Befehle ohne Repo-Bezug).
3. Erreichbarkeit prüfen: `http://<server>:<port>/health` von einem anderen Host; Hinweis auf `AllowedHosts: "*"` und optionale Einschränkung auf Hostnamen/IP.
4. Verhalten ohne TLS erklären: `UseHttpsRedirection` erzeugt ohne HTTPS-Endpunkt nur eine einmalige Startup-Warnung, Requests laufen durch (`ProgramExtensions.cs:687-690`); `UseHsts` setzt auf reinem HTTP keine Header — für produktive TLS-Terminierung wird auf Pfad B verwiesen.
5. Kurzhinweis: Für reine LAN-/Intranet-Nutzung ist dieser Pfad vollständig; für Internet-Exposition Pfad B empfohlen.

Beteiligte Referenzpunkte: `ProgramExtensions.cs` (`ConfigureMiddleware`), `appsettings.json`/`appsettings.Production.json` (`AllowedHosts`, `Kestrel`).

### Pfad B — Betrieb hinter einem Reverse-Proxy

1. Interne Bindung: Dienst auf Loopback lauschen lassen (`Kestrel__Endpoints__Http__Url=http://127.0.0.1:5003`), Port in der Firewall nicht öffnen.
2. Referenz-Proxy-Konfiguration vollständig abdrucken (Caddyfile mit `reverse_proxy`, automatischem TLS und implizitem Setzen der `X-Forwarded-*`-Header; kompakter nginx-Alternativblock mit explizitem `proxy_set_header X-Forwarded-For/Proto`).
3. Vertrauenswürdige Proxies in der Environment-Datei: `ReverseProxy__KnownProxies=<Proxy-IP>` bzw. `ReverseProxy__KnownNetworks=<CIDR>` (semikolongetrennt); Warnhinweis: ohne Einträge werden `X-Forwarded-*`-Header vollständig ignoriert (`ForwardLimit = 1`, `ProgramExtensions.cs:630-672`) — Auswirkung: falsche Client-IP für Rate-Limiting/`IpBlockMiddleware` und falsches Schema für HSTS/Redirects.
4. `PathBase=/finance` in der Environment-Datei setzen (erste Middleware, `ProgramExtensions.cs:618-628`), Proxy-Route unter `/finance` auf den internen Port mappen; Varianten-Hinweis: Domain-Root-Proxying benötigt kein `PathBase` (siehe Offene Punkte #6).
5. `AllowedHosts` auf den öffentlichen Hostnamen einschränken.
6. End-to-End-Prüfung: `https://<host>/finance/health`, Browser-Aufruf der Anwendung, Kontrolle, dass Client-IP/Schema korrekt ankommen (Hinweis auf `troubleshooting.md`-Abschnitt „Hinter einem Reverse-Proxy stimmen Client-IP oder Protokoll nicht", Zeilen 96-105).

Beteiligte Referenzpunkte: `ProgramExtensions.cs` (`PathBase`, `UseForwardedHeaders`), `appsettings.json` (`AllowedHosts`), `troubleshooting.md`.

### Folgeschritt (Ende beider Pfade)

1. Verweis auf `installation.md`: Erstbenutzer/Admin-Registrierung, Setup-Seite, optionale Konfiguration (Self-Update, AlphaVantage) — als „nächster Schritt" ohne Inhaltsduplikation.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `HelpContentCatalog` (`FinanceManager.Web/Services/Help/HelpContentCatalog.cs`, statische Klasse)

- **Geänderte Member:** `TechnicalOnlyDocumentNames` (Zeilen 93-101) — das Literal `"server-installation.md"` zum HashSet-Initialisierer hinzufügen. Alphabetisch konsistent einordnen (vor `"business-rules.md"` nach `"bereitstellung.md"` bzw. den vorhandenen Sortiermuster folgend). Kein Eintrag in `Topics`/`SetupDocuments`.

### Dokumentationsartefakte

- **Neu:** `Docs/help/systemverwaltung-und-setup/server-installation.md` — Inhalt wie unter „Programmabläufe" beschrieben.
- **`Docs/help/systemverwaltung-und-setup/index.md`:** Inhaltsliste (Zeilen 7-16) um `- [Server-Installation](server-installation.md)` ergänzen, sinnvoll direkt **vor** `Installation & Konfiguration` (Zeile 12), da der Server-Schritt dem In-App-Setup vorgelagert ist.
- **`Docs/help/systemverwaltung-und-setup/installation.md`:** Im Abschnitt „Voraussetzungen" (Zeilen 5-8) einen Verweis ergänzen, dass das vorgelagerte Server-/Betriebssystem-Setup in `server-installation.md` beschrieben ist (relativer Markdown-Link).
- **`Docs/help/systemverwaltung-und-setup/bereitstellung.md`:** Kurzer Querverweis im Abschnitt „Build und Veröffentlichungsartefakt" (nach Zeilen 53-58), dass die Installation des ZIPs Schritt für Schritt in `server-installation.md` beschrieben ist.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Die Anleitung beschreibt ausschließlich bestehende Konfigurationsebenen (`PathBase`, `ReverseProxy:KnownProxies`/`KnownNetworks`, `AllowedHosts`, `Jwt:*`, `DataProtection:KeysPath`, `Updates:ServiceName`, `Kestrel`/`ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`, `FileLogging`).

## Seiteneffekte und Risiken

- **Help-Pipeline:** `Docs/help/**` wird per `FinanceManager.Web.csproj:22` (`CopyToPublishDirectory="PreserveNewest"`) mit publiziert; die Build-Targets `GenerateHelpSearchIndexes`/`GenerateHelpAssetManifest`/`IncludeGeneratedHelpAssetInPublish` (Zeilen 116-133) erzeugen Suchindizes und `help-assets.sha256` beim Build automatisch neu — kein manueller Schritt, aber ein Build/Testlauf nach der Änderung ist Pflicht.
- **Test-Risiko bei falscher Einordnung:** Landet die Datei versehentlich in `Topics`/`SetupDocuments`, brechen `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` und `Topics_ContainsReviewedUserHelpTopicsOnly` (fixe Anzahl 13) — der Plan schließt das durch die `TechnicalOnlyDocumentNames`-Festlegung aus.
- **ContentRoot vs. BaseDirectory:** `financemanager.db`/`backups/`/`updates/`/`init/` liegen unter dem ContentRoot (= `WorkingDirectory` der Unit), `logs/` unter `AppContext.BaseDirectory` (Publish-Verzeichnis). Die Anleitung muss diese Unterscheidung in den `ReadWritePaths=` und Rechteschritten sauber abbilden, sonst startet der Dienst zwar, schreibt aber an unerwartete Orte oder scheitert an `ProtectSystem=strict`.
- **Vorbestehende E2E-Fehler:** 9 E2E-Fehler auf dem Basisstand (Playwright-Timeouts, siehe `inventory/tests.md`) sind dokumentiert und nicht Teil dieser Aufgabe; sie dürfen bei der Abnahme nicht dieser Änderung zugerechnet werden.

## Umsetzungsreihenfolge

1. **`server-installation.md` anlegen (Basisteil)**
   - Voraussetzungen: Keine (reine Markdown-Datei; alle zu beschreibenden Code-Fakten sind im Repo verifiziert, siehe `inventory/code-referenzen.md` und `inventory/konfiguration.md`).
   - Beschreibung: Neue Datei mit Header-Konvention (`← [Zurück zur Übersicht](index.md)`, Titel `# Systemverwaltung und Setup — Server-Installation`) und dem Basisteil (Voraussetzungen, Artefakt-Bezug, Service-Account, Verzeichnislayout/Schreibpfade, Environment-Datei mit `Jwt__*`, systemd-Unit inkl. erklärter Hardening-Direktiven, `Updates__ServiceName`, Dienstaktivierung, `/health`-Smoke-Test).

2. **`server-installation.md`: Pfad A „ohne Reverse-Proxy" ergänzen**
   - Voraussetzungen: Schritt 1 (Basisteil muss vorhanden sein, damit der Pfad vollständig lesbar ist).
   - Beschreibung: Abschnitt mit Listener-Konfiguration (Production-Default Port 5003), Firewall-Öffnung, direkter Erreichbarkeit, `AllowedHosts`-Einschränkung, `UseHttpsRedirection`-/`UseHsts`-Hinweisen und Empfehlung zu Pfad B für Internet-Exposition.

3. **`server-installation.md`: Pfad B „mit Reverse-Proxy" ergänzen**
   - Voraussetzungen: Schritt 1; Entscheidung zum Referenz-Proxy (Offener Punkt #1 — Empfehlung Caddy) sollte vor oder beim Schreiben getroffen sein.
   - Beschreibung: Abschnitt mit Loopback-Bindung, vollständiger Referenz-Proxy-Konfiguration inkl. `X-Forwarded-For`/`X-Forwarded-Proto`, `ReverseProxy__KnownProxies`/`KnownNetworks`, `PathBase=/finance` (plus Domain-Root-Hinweis gemäß Offenem Punkt #6), `AllowedHosts`-Einschränkung, End-to-End-Prüfung.

4. **`server-installation.md`: Folgeschritt-Verlinkung abschließen**
   - Voraussetzungen: Schritte 1-3.
   - Beschreibung: Abschlussabschnitt „Nächste Schritte" mit Link auf `installation.md` (In-App-Ersteinrichtung) und optionalem Link auf `troubleshooting.md`.

5. **`index.md`-Eintrag ergänzen**
   - Voraussetzungen: Schritt 1 (Datei muss existieren, damit der Link nicht ins Leere zeigt — gedankliche Abhängigkeit, Reihenfolge technisch beliebig).
   - Beschreibung: Eintrag `- [Server-Installation](server-installation.md)` in die Inhaltsliste vor „Installation & Konfiguration".

6. **Rückverweis aus `installation.md` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Im Abschnitt „Voraussetzungen" Verweis auf `server-installation.md` als vorgelagerten Schritt ergänzen.

7. **Querverweis aus `bereitstellung.md` ergänzen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Ein Satz/Link im Abschnitt „Build und Veröffentlichungsartefakt" auf die Installationsanleitung für das ZIP.

8. **`HelpContentCatalog.TechnicalOnlyDocumentNames` ergänzen**
   - Voraussetzungen: Schritt 1 (Dateiname muss final feststehen — `server-installation.md`).
   - Beschreibung: `"server-installation.md"` im HashSet-Initialisierer (`HelpContentCatalog.cs:93-101`) ergänzen; keine Änderung an `Topics`/`SetupDocuments`.

9. **Build und betroffene Tests ausführen**
   - Voraussetzungen: Schritte 1-8; .NET-SDK (vorhanden: SDK 11.0.100, Ziel `net10.0`).
   - Beschreibung: `dotnet build FinanceManager.Web/FinanceManager.Web.csproj` (regeneriert Help-Indizes/`help-assets.sha256`), danach `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj` mindestens für `HelpContentCatalogTests`; zusätzlich gedanklicher oder realer Durchlauf der Anleitung als fachlicher Prüfschritt (Akzeptanzkriterium).

## Tests

### Neue Tests

Keine. Die Anforderung ist eine Dokumentationsaufgabe; `TechnicalOnlyDocumentNames` wird bereits durch `CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles` (`FinanceManager.Tests/Web/Help/HelpContentCatalogTests.cs:117-136`) als Ausschlussliste mitgeprüft. `TryResolveDocument_RejectsTechnicalOnlyDocuments` (Zeilen 47-67) benötigt keinen neuen `InlineData`-Eintrag, da nicht publizierte Dokumente ohnehin nicht auflösbar sind.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine Anpassung erforderlich | Die Ergänzung von `TechnicalOnlyDocumentNames` löst keinen bestehenden Assert aus (das HashSet wird nur als Ausschlussliste geprüft); `Topics`-Anzahl bleibt 13. Es genügt der Verifikationslauf von `HelpContentCatalogTests` nach der Änderung. |

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — begründet: Die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Anwendungsablauf. Es werden ausschließlich Markdown-Dateien geändert/erstellt sowie eine statische Ausschlussliste (`TechnicalOnlyDocumentNames`) um ein Literal erweitert; die Datei ist bewusst **nicht** über `/help` erreichbar, sodass es keinen Browser-Fluss gibt, den ein E2E-Test nachweisen könnte. Der fachliche Nachweis erfolgt als gedanklicher/realer Durchlauf der Anleitung (Akzeptanzszenarien 1-3) plus Unit-Test-Verifikation des Katalogs. Ein bloßer Verweis auf bestehende Tests ist hier nicht die Begründung — ausschlaggebend ist, dass kein Benutzerfluss existiert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path ohne Reverse-Proxy | Betreiber öffnet `server-installation.md` (über Repo, `index.md` oder Link aus `installation.md`) | Leerer Linux-Server mit systemd; `server-installation.md` existiert; `installation.md` enthält Rückverweis; `index.md` listet die Datei; `TechnicalOnlyDocumentNames` enthält `server-installation.md` | Basisteil Schritte 1-8 + Pfad A Schritte 1-5 | systemd-Dienst `financemanager` läuft (`systemctl status` aktiv), `curl http://<server>:5003/health` liefert `{"status":"ok"}`, `financemanager.db` wurde beim Erststart angelegt; Leser gelangt über den Folgeschritt-Link zu `installation.md` | Gedanklicher/realer Durchlauf der Anleitung (reale Abnahme) |
| 2 | Happy Path mit Reverse-Proxy | Wie #1, Betreiber wählt Pfad B | Wie #1, zusätzlich öffentlicher Hostname und Proxy-Software verfügbar | Basisteil + Pfad B Schritte 1-6 (Loopback-Bindung, Proxy-Config, `ReverseProxy__KnownProxies`, `PathBase=/finance`, `AllowedHosts`) | `https://<host>/finance/health` liefert `{"status":"ok"}` über TLS; Anwendung unter `/finance` bedienbar; Client-IP und `https`-Schema kommen korrekt an (kein Rate-Limit-/HSTS-Fehlverhalten) | Gedanklicher/realer Durchlauf der Anleitung (reale Abnahme) |
| 3 | Fehlerfall: fehlende/ungültige JWT-Konfiguration | Betreiber startet Dienst ohne `Jwt__Key` in der Environment-Datei | Wie #1 vor Schritt 8 | Dienststart → `ValidateOnStart` bricht den Host ab | `journalctl -u financemanager` zeigt den Options-Validierungsfehler; die Anleitung hat die Pflichtwerte und die 32-Byte-Regel vorab benannt, der Fehler ist für den Leser erklärbar und behebbar | Gedanklicher Durchlauf; Fehlerbild deckt sich mit `JwtOptionsValidator` |
| 4 | Fehlerfall: Forwarded-Header ohne Trusted-Proxy-Konfiguration | Betreiber hängt Proxy vor, vergisst `ReverseProxy__KnownProxies`/`__KnownNetworks` | Pfad B eingerichtet, aber Trusted-Proxies leer | Request über Proxy | `X-Forwarded-*` werden ignoriert (`ForwardLimit=1`, geleerte Listen): Client-IP = Proxy-IP, Schema = `http` → Rate-Limiting/HSTS wirken falsch; Anleitung verweist auf den `troubleshooting.md`-Abschnitt, der genau dieses Bild beschreibt | Gedanklicher Durchlauf; Konsistenz mit `ProgramExtensions.cs:630-672` und `troubleshooting.md:96-105` |
| 5 | Katalog-/Navigationsabnahme | Build + `HelpContentCatalogTests` | Alle Änderungen umgesetzt | `dotnet build` + `dotnet test` (Help-Tests) | Build regeneriert Help-Indizes fehlerfrei; `CatalogDocuments_DoNotContainTechnicalImplementationMarkers`, `CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles` und `Topics_ContainsReviewedUserHelpTopicsOnly` (13 Topics) bleiben grün; Datei ist nicht über `/help` erreichbar | Automatisierter Testlauf |

## Offene Punkte

Alle offenen Punkte wurden am 2026-11-21 dem Anwender vorgelegt; dieser hat die empfohlenen Vorschläge vollständig bestätigt. Die Tabelle dokumentiert die damit getroffenen Entscheidungen (keine verbleibenden offenen Punkte).

| # | Entscheidung (ehemals offener Punkt) | Bestätigter Vorschlag |
|---|---------------|----------------------|
| 1 | Referenz-Proxy: Welche Software soll konkret durchgearbeitet werden (Caddy, nginx + certbot/acme.sh, Apache)? | **Caddy** als Hauptbeispiel (eingebautes automatisches TLS deckt den Issue-Hinweis „automatische TLS-Terminierung" ohne Zusatzwerkzeuge ab, minimale Konfigurationsmenge), plus ein kompakter nginx-Hinweisblock mit expliziten `proxy_set_header`-Zeilen als Alternative. |
| 2 | Zielplattform-Umfang: Ist „nur Linux + systemd" als vollständiger Pfad korrekt, oder soll ein Windows-Dienst-Pfad knapp beschrieben werden? | Nur Linux + systemd vollständig beschreiben (gemäß Scope-Ausschluss #4); einen einzigen Hinweissatz einbauen, dass auch ein `win-x64`-Artefakt existiert — kein separater Installationspfad. |
| 3 | Dateiname und Verlinkungsumfang: Dateiname `server-installation.md` und Platzierung in `index.md` direkt vor „Installation & Konfiguration" — zusätzlich aus `bereitstellung.md` (im Plan eingeplant) und/oder `README.md` verlinken? | `server-installation.md` + `index.md`-Eintrag vor „Installation & Konfiguration" + Querverweis aus `bereitstellung.md` (eingeplant). `README.md` bewusst **nicht** ändern, um die Änderungsfläche minimal zu halten — README verweist bereits auf `Docs/` als Dokumentationsquelle. |
| 4 | Eigenständiger TLS-Betrieb: Soll Pfad A optional Kestrel-TLS (`Kestrel:Endpoints:Https` mit eigenem Zertifikat) beschreiben oder bewusst nur HTTP/Direktzugriff? | Bewusst nur HTTP/Direktzugriff (Scope-Ausschluss #6); in Pfad A einen Hinweis einbauen, dass öffentliche TLS-Terminierung über Pfad B (Reverse-Proxy) empfohlen wird — Kestrel-Zertifikatsverwaltung ist außerhalb des geforderten Umfangs. |
| 5 | systemd-Hardening-Tiefe: Volles Profil (Service-Account, `ProtectSystem=strict`, `ReadWritePaths`, `PrivateTmp`, `NoNewPrivileges`) mit inline erklärten Direktiven? | Ja — volles Hardening-Profil in der Beispiel-Unit, jede Direktive mit einem Satz inline erklärt (kein externer Verweis nötig). |
| 6 | PathBase-Varianten: Nur Sub-Path (`PathBase=/finance`) oder zusätzlich Domain-Root hinter dem Proxy (ohne `PathBase`)? | Sub-Path als durchgearbeitetes Hauptbeispiel (deckt den explizit geforderten `PathBase`-Fall ab) plus ein kurzer Hinweisabsatz, dass Domain-Root-Proxying ohne `PathBase` funktioniert (Proxy mappt `/` direkt auf den internen Port). |
