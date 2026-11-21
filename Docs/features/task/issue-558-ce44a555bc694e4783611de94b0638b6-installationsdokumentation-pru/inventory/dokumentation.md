# Bestandsaufnahme — Dokumentationsartefakte

Analysierte Dokumentationsflächen bezogen auf die Anforderung „Server-Installationsanleitung unter `Docs/help/systemverwaltung-und-setup/`“.

## Verzeichnis `Docs/help/systemverwaltung-und-setup/`

Vorhandene Dateien (Stand Commit `44e9202c`):

| Datei | Inhalt | Katalog-Status |
|-------|--------|----------------|
| `index.md` | Bereichsübersicht + Inhaltsverzeichnis (11 Einträge) | in `TechnicalOnlyDocumentNames` |
| `beschreibung.md` | Bereichsbeschreibung | publiziert (Primärdokument des Topics `systemverwaltung-und-setup`) |
| `ablauf-anwender.md` | Bedienablauf für Anwender | publiziert |
| `einrichtung-anwender.md` | Einrichtung für Anwender | publiziert (RouteSegment `installation`) |
| `troubleshooting.md` | Fehlerbehebung inkl. Abschnitt „Hinter einem Reverse-Proxy stimmen Client-IP oder Protokoll nicht“ (Zeilen 96–105) | publiziert |
| `sicherheit-help.md` | Sicherheit der Help-Doku | publiziert |
| `ablauf-technisch.md` | Technischer Ablauf | in `TechnicalOnlyDocumentNames` |
| `api.md` | API-Referenz des Bereichs | in `TechnicalOnlyDocumentNames` |
| `bereitstellung.md` | Release-Pipeline, Publish-Artefakte, Self-Update, JWT-Produktionskonfiguration, Key-Ring-Betrieb | in `TechnicalOnlyDocumentNames` |
| `business-rules.md` | Business Rules | in `TechnicalOnlyDocumentNames` |
| `datenmodell.md` | Datenmodell | in `TechnicalOnlyDocumentNames` |
| `demo-daten.md` | Demo-Daten | technisch (nicht im Katalog, nicht in `TechnicalOnlyDocumentNames` aufgeführt) |
| `installation.md` | In-App-Ersteinrichtung und Konfigurationsreferenz (Details unten) | technisch (nicht publiziert, nicht in `TechnicalOnlyDocumentNames` aufgeführt) |

**Fehlend:** Jede Datei, die das Server-/Betriebssystem-Setup beschreibt (`dotnet publish`-Bezug, Dateiablage, Service-Account, systemd-Unit, Firewall, Reverse-Proxy-Konfiguration, `PathBase`). Keine `server-installation.md` o. ä. vorhanden.

## `Docs/help/systemverwaltung-und-setup/installation.md` (280 Zeilen)

Deckt ausschließlich In-App-/Konfigurationsthemen ab:

- „Voraussetzungen“: „Laufende Anwendung mit Datenbankmigrationen“ — setzt die Anwendung als bereits laufend voraus, erklärt nicht den Weg dorthin (Zeilen 5–8).
- „Installationsschritte“: Anwendung starten, Admin anlegen, `setup`-Seite (Zeilen 10–14).
- Konfigurationstabelle (Zeilen 18–59): `Jwt:*`, `DataProtection:KeysPath`, `Api:BaseAddress`, `Updates:*` (u. a. `Updates:ServiceName` = systemd-Service unter Linux, Zeile 39), Import-/MassImport-/Attachments-/RateLimiting-Parameter, `ReverseProxy:KnownProxies`/`KnownNetworks` (Zeilen 57–58), `AllowedHosts` (Zeile 59).
- Abschnitte: „JWT-Konfiguration“ (Pflichtwerte `Jwt__Key`/`Jwt__Issuer`/`Jwt__Audience`/`Jwt__LifetimeMinutes`, Platzhalter-Ablehnung), „Registrierung und Benutzeranlage“, „Reverse-Proxy und Host-Header“ (Konfigurationsoptionen, aber keine Installationsanleitung), „Data Protection und AlphaVantage API Keys“, „Self-Update-Betrieb“, „Überprüfung“ (u. a. „`/health` ist anonym erreichbar“, Zeile 275).
- Kein Rückverweis auf eine vorgelagerte Server-Installation (es gibt keine).

## `Docs/help/systemverwaltung-und-setup/index.md` (17 Zeilen)

- Einleitung nennt Benutzerverwaltung, Sicherheit, Benachrichtigungen, Sicherungen, security.txt — kein Server-Setup.
- Inhaltsliste (Zeilen 7–16) ohne Server-Installations-Eintrag; `installation.md` ist als „Installation & Konfiguration“ gelistet (Zeile 12), `bereitstellung.md` als „Release und Bereitstellung“ (Zeile 13).

## `Docs/help/systemverwaltung-und-setup/bereitstellung.md` (218 Zeilen)

- Beschreibt das Release-Artefakt: `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj` mit `--framework net10.0`, `--runtime win-x64`/`linux-x64`, `--self-contained true` (Zeilen 43–46); ZIP-Namen `FinanceManager-vX.Y.Z-<rid>.zip` inkl. `release-metadata.json` (Zeilen 54–56); `update.json`-Manifest (Zeilen 67–72).
- „Produktive JWT-Konfiguration“ (Zeilen 146–175) und „AlphaVantage-Key-Ring-Betrieb“ (Zeilen 193–218) decken Teile der Betriebskonfiguration ab — aber kein Schritt-für-Schritt-Server-Setup.
- Beispiel-Befehle in `installation.md`/`bereitstellung.md` sind überwiegend PowerShell (`$env:...`), keine Linux-Shell.

## Weitere berührte Dokumente

- `README.md` (Repo-Root): Projektbeschreibung, Architekturübersicht; **kein** `dotnet publish`, kein systemd, keine Server-Voraussetzungen — verifiziert per Volltextsuche.
- `CI-CD.md`: Pipeline-Doku; nennt den self-contained Publish für `win-x64`/`linux-x64` (Zeile 103), ist aber CI-Doku, keine Installationsanleitung.
- `Docs/development.md`: Entwickler-Doku; nennt `dotnet publish FinanceManager.Web -c Release` nur im Kontext der Screenshot-Generierung (Zeile 49) und dokumentiert `ReverseProxy:*` als Konfigurationstabelle (Zeilen 90–113) — keine Installationsanleitung.
- `Docs/help/updates/installation.md`: Enthält systemd-nahe Inhalte (Polkit-Regel für `systemd-run`, `Updates:ServiceName`), aber ausschließlich im Kontext des Self-Updates — keine vollständige Dienst-Einrichtung.

## Verankerungspunkte (Mechanik)

- `HelpContentCatalog.TechnicalOnlyDocumentNames` (`FinanceManager.Web/Services/Help/HelpContentCatalog.cs:93-101`): HashSet mit `api.md`, `ablauf-technisch.md`, `bereitstellung.md`, `business-rules.md`, `datenmodell.md`, `index.md` — Dateien in dieser Liste sind bewusst von der Benutzer-Help-Navigation ausgeschlossen.
- `HelpContentCatalog.Topics` (`HelpContentCatalog.cs:72-87`): 13 Topics; `systemverwaltung-und-setup` publiziert `SetupDocuments` = `beschreibung.md`, `ablauf-anwender.md`, `einrichtung-anwender.md` (RouteSegment `installation`), `troubleshooting.md`, `sicherheit-help.md` (Zeilen 46–53).
- `FinanceManager.Web.csproj:22`: `<Content Include="..\Docs\help\**\*" … CopyToPublishDirectory="PreserveNewest" />` — alle `Docs/help`-Dateien werden mit publiziert; die Build-Targets `GenerateHelpSearchIndexes`/`GenerateHelpAssetManifest`/`IncludeGeneratedHelpAssetsInPublish` (Zeilen 116–133) erzeugen Suchindizes und `help-assets.sha256` automatisch neu.
