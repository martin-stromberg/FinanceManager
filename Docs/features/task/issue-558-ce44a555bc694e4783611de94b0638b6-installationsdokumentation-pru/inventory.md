# Bestandsaufnahme: Server-Installationsdokumentation (Issue #558)

Bestandsaufnahme der Dokumentations-, Code- und Konfigurationsfläche für die Anforderung, eine eigenständige Server-Installationsanleitung (mit und ohne Reverse-Proxy) unter `Docs/help/systemverwaltung-und-setup/` zu erstellen.

## Zusammenfassung

- **Server-Installationsdokumentation fehlt vollständig:** Weder `README.md` noch `Docs/` enthalten `dotnet publish`-Bezug für den Betrieb, systemd-Unit, Service-Account, Schreibpfade oder Server-Voraussetzungen. `Docs/help/systemverwaltung-und-setup/` enthält 13 Dateien, davon keine zum OS-/Infrastruktur-Setup.
- **`installation.md` ist In-App-Doku:** Deckt JWT-Setup, Admin-Anlage, Konfigurationsreferenz, Self-Update ab — setzt eine laufende Anwendung voraus und hat keinen Rückverweis auf ein Server-Setup. Geeignet als Verlinkungsziel „Folgeschritt".
- **`bereitstellung.md` beschreibt das Release-Artefakt** (self-contained `win-x64`/`linux-x64`-ZIPs + `update.json`) — die Bezugsquelle für die Anleitung existiert und ist dokumentiert.
- **`index.md` fehlt ein Eintrag** für die neue Datei; Inhaltsliste hat 11 Einträge.
- **`HelpContentCatalog.TechnicalOnlyDocumentNames`** (`FinanceManager.Web/Services/Help/HelpContentCatalog.cs:93-101`) enthält sechs technische Dateinamen — hier kommt der neue Dateiname hin; kein Katalog-Eintrag nötig (und kontraproduktiv, siehe Tests).
- **Alle in der Anforderung genannten Code-Referenzpunkte verifiziert** — mit zwei Korrekturen: `appsettings.Production.json` setzt sehr wohl `Kestrel:Endpoints:Http:Url = http://*:5003` (Production-Default ist Port 5003 an allen Interfaces, nicht `localhost:5000`), und `FileLogging` ist in Production `enabled` (Schreibpfad `logs/` unter `AppContext.BaseDirectory`). `PathBase`, `UseForwardedHeaders` (nur `ReverseProxy:KnownProxies`/`KnownNetworks`, `ForwardLimit=1`), `UseHttpsRedirection`, `UseHsts`, `ValidateOnStart`-JWT-Regeln, `/health` (anonym), Schreibpfade (`financemanager.db`, `backups/`, `updates/`, `init/`, `logs/`, `DataProtection:KeysPath`) und Publish-Mechanik sind wie beschrieben vorhanden.
- **Test-Ausgangszustand:** Unit 2410/2410 grün, Integration 176/176 grün, E2E 167/176 — **9 nachgewiesene Vorbestandsfehler** (überwiegend Playwright-Timeouts auf `a[href='/list/accounts']`-Selektoren; Muster passt zum unmittelbar vorausgehenden PathBase-Commit `44e9202c`/#557). Details: [tests.md](inventory/tests.md).
- **Anforderungskritischer Test:** `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` würde fehlschlagen, falls die neue Datei in `Topics` landet — begründet die `TechnicalOnlyDocumentNames`-Einordnung. Der `Topics_ContainsReviewedUserHelpTopicsOnly`-Assert (fixe 13 Topics) bliebe ebenfalls betroffen.

## Details

- [Dokumentationsartefakte](inventory/dokumentation.md) — vorhandene Dateien in `Docs/help/systemverwaltung-und-setup/`, Inhalte von `index.md`/`installation.md`/`bereitstellung.md`, Katalog-Mechanik, verifizierte Lücke
- [Code-Referenzpunkte](inventory/code-referenzen.md) — `ProgramExtensions`-Middleware-Reihenfolge, `HealthController`, `JwtOptionsValidator`, Persistenz-/Schreibpfade, Deployment-Artefakt, `HelpContentCatalog`
- [Konfiguration](inventory/konfiguration.md) — `appsettings.json`/`appsettings.Production.json`-Oberfläche für Listener, Reverse-Proxy, JWT-Pflichtwerte und Betriebspfade
- [Tests](inventory/tests.md) — Test-Ausgangszustand aller drei Suiten, nachgewiesene E2E-Fehler, anforderungsrelevante Testklassen
