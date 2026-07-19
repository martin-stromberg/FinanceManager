# Umsetzungsplan - Automatisches Update

## Ziel

Die Anwendung erhaelt einen administrativen Self-Update-Mechanismus fuer produktive Installationen auf Windows und Linux. Administratoren koennen Updatepruefung und Zeitplan konfigurieren, ein verfuegbares Release einsehen, ein Update manuell starten oder zu einer geplanten Uhrzeit ohne weitere Bestaetigung ausfuehren lassen. Die eigentliche Dateiersetzung erfolgt ausserhalb des laufenden ASP.NET-Core-Prozesses ueber ein generiertes Plattformskript.

Verbindliche Updatequelle ist das GitHub-Repository `martin-stromberg/FinanceManager`. Alte Verweise auf `Muesli84/FinanceManager` duerfen fuer den Self-Updater nicht mehr verwendet werden.

## Testbarer MVP-Umfang

Der MVP umfasst die vollstaendige produktive Unterstuetzung fuer Windows und Linux inklusive Release-Artefakten.

Im MVP enthalten:

- GitHub-Release-Manifest `update.json` wird in der Release-Pipeline erzeugt und als Release-Asset veroeffentlicht.
- `update.json` enthaelt Release Notes aus GitHub, Version, PublishedAt, Repository, plattformspezifische Assets, Dateigroessen und SHA-256-Hashes.
- Die Release-Pipeline erzeugt direkt installierbare ZIP-Artefakte fuer Windows und Linux, z. B. `FinanceManager-v{version}-win-x64.zip` und `FinanceManager-v{version}-linux-x64.zip`.
- Die Webanwendung kann `update.json` aus `martin-stromberg/FinanceManager` laden, die passende Plattform auswaehlen, Metadaten anzeigen und das passende ZIP-Asset herunterladen.
- Die aktuell installierte Version wird aus Release-Metadaten bestimmt, die mit dem Release ausgeliefert und im Installationsverzeichnis abgelegt werden.
- Die Update-Domaene enthaelt Status, Lock-Datei, Downloadverzeichnis, Hash-Pruefung, Dateigroessenlimit, Plattformvalidierung und Fehlerstatus.
- Dienstnamen sind variabel. Die Anwendung versucht eine Best-Effort-Ermittlung, verlaesst sich fuer produktive Installationen aber auf konfigurierbare Werte fuer Windows-Service und Linux-systemd, falls keine robuste Ermittlung moeglich ist.
- Admin-only API unter `api/setup/update` fuer Status, Einstellungen, Sofortpruefung, geplante Installation, Installationsstart und Lock-Reset.
- Ein gesicherter Admin-Reset-Endpunkt fuer haengende Update-Locks ist Teil des MVP.
- Neue Setup-Sektion `update` mit Anzeige von Metadaten, Status, Aktivierung der Pruefung, Pruefintervall, geplanter Uhrzeit, Dienstkonfiguration und Installationsaktion.
- Geplante Installation darf nach Erreichen der Uhrzeit automatisch ohne erneute Benutzerbestaetigung starten.
- Externe Skripterzeugung fuer Windows und Linux wird implementiert und unit-getestet.
- Ein anonymer, einfacher Health-Endpunkt dient der Warteseite zum Polling nach Neustart.
- Die Warteseite pollt alle zwei Sekunden und zeigt nach maximal zwei Minuten eine Fehlermeldung mit weiterer Admin-Handlung an.
- Periodische Updatepruefung und Scheduler laufen als eigene Hosted Services, in Tests deaktivierbar wie bestehende Worker.

Nicht im MVP enthalten:

- Backup des alten Anwendungsverzeichnisses vor Dateiersetzung.
- Vollautomatisches Rollback nach fehlgeschlagener Installation.
- Mehrere parallele Releasekanaele oder Architekturvarianten jenseits der festgelegten Windows-/Linux-x64-Artefakte.
- Echte End-to-End-Tests, die den laufenden Testprozess beenden und Dateien ersetzen.

## Architektur

### Shared DTOs

Neue DTOs werden in `FinanceManager.Shared` abgelegt, vorzugsweise unter `Dtos/Update`:

- `UpdateMetadataDto`: Version, ReleaseNotes, PublishedAt, RepositoryOwner, RepositoryName, Assets.
- `UpdateAssetDto`: Platform, RuntimeIdentifier, AssetName, AssetUrl, Sha256, SizeBytes.
- `UpdateStatusDto`: Status, InstalledVersion, InstalledReleasePublishedAt, AvailableVersion, CurrentPlatform, LastCheckedAt, LastError, DownloadedAssetName, IsLocked, LockCreatedAt, ScheduledInstallAt.
- `UpdateSettingsDto`: Enabled, CheckIntervalMinutes, RepositoryOwner, RepositoryName, ManifestAssetName, ScheduledInstallTime, WindowsServiceName, LinuxServiceName, ExecutablePath, WorkingDirectory, HealthTimeoutSeconds.
- `UpdateSettingsUpdateRequest`, `UpdateScheduleRequest`, `UpdateStartRequest`, `UpdateLockResetRequest`.
- `UpdateCheckResultDto` fuer manuelle Prueflaeufe.

Statuswerte werden als Enum modelliert: `NoUpdate`, `Checking`, `Available`, `Downloading`, `Ready`, `Installing`, `Failed`.

### Persistenz und Optionen

Statische Defaults kommen in eine neue `Updates`-Sektion in `FinanceManager.Web/appsettings.json` und `appsettings.Production.json`.

Admin-aenderbare Einstellungen werden nicht in `appsettings` zurueckgeschrieben. Fuer den MVP wird eine kleine persistierte JSON-Datei im Update-Arbeitsverzeichnis verwendet, z. B. `updates/settings.json`, mit atomarem Schreiben ueber Temp-Datei und Rename. Das vermeidet kurzfristige Datenbankmigrationen fuer Betriebsparameter und passt zum prozessweiten Charakter des Features.

Die aktuell installierte Version wird ueber einen neuen `IInstalledReleaseMetadataProvider` aus einer mit dem Release ausgelieferten Metadatendatei gelesen, z. B. `release-metadata.json` im ContentRoot. Diese Datei wird durch die Release-Pipeline erzeugt und enthaelt mindestens Version, PublishedAt, CommitSha, Repository und RuntimeIdentifier. Falls die Datei in lokalen Entwicklungsumgebungen fehlt, liefert der Provider `Unknown`; produktive Updatevergleiche duerfen dann kein Update automatisch installieren.

### Update-Services

Neue Services in `FinanceManager.Web/Services/Updates`:

- `IUpdateSettingsStore`: Lesen und Schreiben der globalen Updateeinstellungen.
- `IInstalledReleaseMetadataProvider`: Lesen der lokal installierten Release-Metadaten.
- `IUpdateManifestClient`: Abruf von `update.json` und Download des ZIP-Assets ueber konfigurierten `HttpClient`.
- `IUpdatePlatformResolver`: Ermittlung von Betriebssystem und RuntimeIdentifier, Auswahl des passenden Assets.
- `IUpdateServiceResolver`: Best-Effort-Ermittlung von Windows-Service oder Linux-systemd-Service und Zusammenfuehrung mit konfigurierten Namen.
- `IUpdateFileStore`: Verwaltung von `updates/pending`, `updates/staging`, Manifest, ZIP, Lock-Datei und Temp-Dateien.
- `IUpdateStatusStore`: prozessweiter Status, synchronisiert mit Dateien und Lock.
- `IUpdateValidator`: Versionvergleich, Assetauswahl, Dateigroessenlimit, SHA-256-Pruefung, ZIP-Grundvalidierung.
- `IUpdateScriptGenerator`: erzeugt `update.ps1` oder `update.sh` aus validierten absoluten Pfaden und Service-/EXE-Konfiguration.
- `IUpdateExecutor`: erstellt Lock, generiert Skript, startet externen Prozess und triggert kontrolliertes Herunterfahren der Webanwendung.
- `IUpdateOrchestrator`: gemeinsamer Einstieg fuer Controller, Checker und Scheduler.

Der vorhandene `IBackgroundTaskManager` wird nicht fuer die Installation verwendet, weil Updateinstallation prozessweit ist und den Host beendet.

### Dienst- und Prozessmodell

Die Diensterkennung wird bewusst defensiv umgesetzt:

- Windows: Wenn ein ServiceName konfiguriert ist, wird dieser verwendet. Ohne Konfiguration kann best-effort anhand des aktuellen Prozesspfads gegen installierte Windows-Services gesucht werden. Ist kein eindeutiger Treffer moeglich, muss der Admin den Dienstnamen oder den EXE-Modus konfigurieren.
- Linux: Wenn ein systemd-ServiceName konfiguriert ist, wird dieser verwendet. Eine robuste automatische Ermittlung ist nicht garantiert; Hinweise aus Prozessumgebung oder cgroup duerfen nur als Vorschlag dienen. Ohne eindeutige Konfiguration wird keine Installation gestartet.
- EXE-Modus: Fuer nicht als Service betriebene Windows-Installationen kann ein konfigurierter ExecutablePath mit Startargumenten genutzt werden.

Vor dem Installationsstart validiert der Orchestrator, dass fuer die aktuelle Plattform alle notwendigen Betriebsparameter vorhanden sind. Fehlende oder mehrdeutige Dienstinformationen fuehren zu `400 BadRequest` mit konkreter Admin-Handlung.

### Hosted Services

`UpdateChecker`:

- erbt von `BackgroundService`;
- laeuft nur bei aktivierter Updatepruefung;
- loest pro Lauf einen Scope auf;
- prueft Manifest und laedt bei neuer Version das passende ZIP in `updates/pending`;
- loggt Fehler und setzt Status `Failed`, beendet aber nicht den Host.

`UpdateScheduler`:

- prueft minuetlich, ob ein Update `Ready` ist, eine geplante Uhrzeit erreicht wurde und kein Lock existiert;
- verwendet denselben `IUpdateOrchestrator.StartInstallAsync` wie der manuelle Start;
- startet die Installation nach Erreichen der geplanten Uhrzeit automatisch ohne erneute Bestaetigung;
- fuehrt keine Installation aus, wenn die Scheduler-Konfiguration unvollstaendig ist.

Beide Hosted Services werden in `ProgramExtensions.RegisterAppServices` registriert und in `TestWebApplicationFactory` fuer Integrationstests deaktiviert.

### API

Neuer Controller `FinanceManager.Web/Controllers/UpdateController.cs`:

- Route: `api/setup/update`
- Attribute: `[ApiController]`, `[Produces(MediaTypeNames.Application.Json)]`, `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`
- Endpunkte:
  - `GET status`
  - `GET settings`
  - `PUT settings`
  - `POST check`
  - `POST schedule`
  - `POST install/start`
  - `POST lock/reset`

Fehler folgen vorhandenen Mustern mit `ApiErrorDto`/`ApiErrorFactory`. Bei aktivem Lock oder laufender Installation wird `409 Conflict` geliefert. Bei fehlendem heruntergeladenem Update oder unvollstaendiger Dienstkonfiguration wird `400 BadRequest` oder `404 NotFound` mit lokalisierbarer Fehlermeldung geliefert.

`POST lock/reset` ist Admin-only, schreibt ein Audit-Log, loescht nur eine als haengend bewertete Lock-Datei und verweigert den Reset, wenn der aktuelle Prozess noch eine laufende Installation kennt. Eine optionale Request-Begruendung kann fuer Logzwecke angenommen werden.

Der Shared API-Client wird in `IApiClient` und einer neuen Datei `ApiClient.Update.cs` erweitert.

### Health und Warteseite

Ein neuer anonymer Endpunkt `GET /health` oder `GET /api/health` liefert ohne Datenbankzugriff `200 OK`, sobald der Prozess wieder laeuft.

Nach erfolgreichem `POST install/start` navigiert die UI auf eine Warteseite oder zeigt eine dedizierte Installationsansicht, die clientseitig alle zwei Sekunden den Health-Endpunkt abfragt und bei Erfolg die Anwendung neu laedt. Die Polling-Logik darf nicht von Blazor-Server-State abhaengen, da die SignalR-Verbindung beim Update abbricht. Nach 120 Sekunden bricht die Warteseite ab und zeigt eine Fehlermeldung mit Hinweis auf Statuspruefung und Admin-Lock-Reset.

### Setup-UI

Neue Setup-Komponenten:

- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- Eintrag `update` in `SetupCardViewModel.SectionDefinitions`
- Ressourcenkeys analog zu bestehenden Setup-Tabs

Die UI enthaelt:

- Toggle fuer automatische Updatepruefung.
- Eingaben fuer Pruefintervall und geplante Uhrzeit.
- Anzeige der Updatequelle `martin-stromberg/FinanceManager`.
- Anzeige und Bearbeitung von Windows-Service-Name, Linux-systemd-Service-Name und optionalem EXE-Modus.
- Statusanzeige mit letzter Pruefung, aktuellem Lock und letztem Fehler.
- Metadatentabelle fuer Version, Veroeffentlichungsdatum, Release Notes, Plattform, Dateigroesse und SHA-256.
- Aktionen `Jetzt pruefen`, `Einstellungen speichern`, `Update installieren`, `Update-Lock zuruecksetzen`.
- Bestaetigungsdialog vor manuellem Installationsstart mit Hinweis auf Downtime.
- Hinweis bei geplanter Installation, dass diese ohne erneute Bestaetigung automatisch startet.

Admin-only Anzeige orientiert sich an `SetupSecurityTab.razor`. Nicht-Admins sehen die Sektion nicht oder erhalten eine knappe nicht berechtigte Ansicht, passend zum bestehenden Setup-Verhalten.

### Release-Pipeline

Die vorhandene Pipeline wird auf eine plattformuebergreifende Matrix erweitert:

- Windows-Publish: `dotnet publish ... --runtime win-x64 --self-contained true`
- Linux-Publish: `dotnet publish ... --runtime linux-x64 --self-contained true`
- ZIP-Artefakte:
  - `FinanceManager-v{version}-win-x64.zip`
  - `FinanceManager-v{version}-linux-x64.zip`
- Pro Publish-Output wird `release-metadata.json` erzeugt und in das ZIP aufgenommen.
- SHA-256-Berechnung je ZIP-Asset.
- Dateigroesse je ZIP-Asset in Bytes.
- GitHub Release Notes werden aus dem aktuellen Release-Kontext in `update.json` uebernommen.
- `update.json` enthaelt alle Plattformassets in strukturierter Form.
- Upload von `update.json` als weiteres Release-Asset.
- Tests in `scripts/resolve-release-version.test.mjs` oder einer neuen Skript-Testdatei fuer Manifeststruktur, Release-Notes-Uebernahme, plattformspezifische Assetnamen und Hash-Felder.

Die Anwendung muss bei Manifesten mit mehreren Assets exakt das Asset fuer die aktuelle Plattform auswaehlen und unbekannte Plattformen ablehnen.

## Umsetzungsschritte

1. Shared DTOs, Enums und ApiClient-Erweiterungen anlegen.
2. `Updates`-Optionsklasse und Default-Konfiguration ergaenzen.
3. Release-Metadatenmodell und `IInstalledReleaseMetadataProvider` implementieren.
4. Update-Dateispeicher, Settings-Store, Status-Store, Manifest-Client, PlatformResolver und Validator implementieren.
5. ServiceResolver fuer Windows/Linux mit Best-Effort-Ermittlung und konfigurierbaren Overrides implementieren.
6. Scriptgenerator fuer Windows und Linux mit Plattformabstraktion implementieren.
7. Orchestrator und Executor implementieren; echte Prozess-/Host-Beendigung hinter testbarer Schnittstelle kapseln.
8. `UpdateChecker` und `UpdateScheduler` als Hosted Services implementieren und registrieren.
9. `UpdateController` mit Admin-only Endpunkten inklusive `POST lock/reset` implementieren.
10. Anonymen Health-Endpunkt ergaenzen.
11. Setup-ViewModel und `SetupUpdateTab.razor` implementieren, inklusive Warteseiten-/Polling-Flow mit 120-Sekunden-Timeout.
12. Release-Pipeline um Windows-/Linux-Artefakte, `release-metadata.json` und `update.json` erweitern.
13. Tests ergaenzen und bestehende Test-Infrastruktur fuer neue Hosted Services anpassen.
14. README-/Hilfedokumentation fuer administratives Updateverhalten aktualisieren.

## Tests

Unit-Tests:

- `update.json`-Parsing mit gueltigen und ungueltigen Feldern.
- Release-Notes-Feld aus Manifest wird korrekt uebernommen.
- Plattform- und Assetauswahl fuer `win-x64` und `linux-x64`.
- Installierte Version aus `release-metadata.json` inklusive fehlender Datei in Entwicklungsumgebungen.
- Versionvergleich inklusive gleicher Version, neuer Version und unbekannter installierter Version.
- SHA-256-Pruefung und Dateigroessenlimit.
- Lock-Datei-Verhalten bei freiem, aktivem und verwaistem Lock.
- Admin-Lock-Reset-Regeln fuer haengende und aktive Locks.
- Statusuebergaenge fuer Check, Download, Ready, Installing und Failed.
- ServiceResolver fuer konfigurierte Dienstnamen und mehrdeutige Best-Effort-Ermittlung.
- Windows- und Linux-Skripterzeugung mit erwarteten Kernbefehlen und gequoteten Pfaden.
- Scheduler-Entscheidungen fuer geplante Uhrzeit, fehlendes Ready-Update, aktiven Lock und automatische Installation ohne erneute Bestaetigung.

Integrationstests:

- Admin-only Zugriff auf alle `api/setup/update`-Endpunkte inklusive `lock/reset`.
- Status-, Settings-, Check- und Schedule-Flows ueber `ApiClient`.
- `POST install/start` liefert Conflict bei aktivem Lock.
- `POST install/start` liefert BadRequest bei fehlender Dienstkonfiguration.
- Health-Endpunkt ist anonym erreichbar.

ViewModel-/bUnit-Tests:

- Laden und Speichern von Updateeinstellungen.
- Anzeige eines verfuegbaren Updates inklusive Release Notes.
- Anzeige und Validierung von Windows-/Linux-Dienstkonfiguration.
- Installationsaktion navigiert in den Wartestatus und behandelt API-Fehler.
- Lock-Reset-Aktion ruft den Admin-Endpunkt auf und aktualisiert den Status.

Release-Skript-Tests:

- Windows- und Linux-ZIP-Artefakte werden mit erwarteten Namen erzeugt.
- `release-metadata.json` wird in beide Publish-Outputs geschrieben.
- `update.json` wird mit erwarteter Version, Repository, Release Notes, Assetnamen, Dateigroessen und SHA-256 erzeugt.
- Fehlende ZIP-Datei, fehlende Release Notes oder leerer Hash bricht den Release-Schritt ab.

Manuelle Verifikation:

- Lokaler Start der Anwendung, Setup-Sektion oeffnen, Einstellungen speichern, Status abrufen.
- Mit lokalem Testmanifest ein Update als `Ready` markieren und Installationsstart bis zur Skriptausfuehrung mit gemocktem Executor pruefen.
- Warteseite gegen Health-Endpunkt pruefen: erfolgreicher Reload und Timeout nach 120 Sekunden.

## Risiken und Gegenmassnahmen

- Prozess ersetzt eigene Dateien: Dateiersetzung bleibt vollstaendig im externen Skript; Webprozess startet nur das Skript und beendet sich kontrolliert.
- Dienstname nicht robust ermittelbar: Dienstname und EXE-Modus sind konfigurierbar; Best-Effort-Ermittlung ist nur Komfort und blockiert bei Mehrdeutigkeit.
- Lock bleibt nach Fehler liegen: Admin-only Reset-Endpunkt ist im MVP enthalten, inklusive Konfliktpruefung und Logging.
- Falsches Asset oder manipuliertes ZIP: Manifestfelder, Plattform, Dateiname, Groesse, SHA-256 und ZIP-Grundstruktur werden vor Installation validiert.
- Hosted Services stoeren Tests: Registrierung wird konfigurierbar gemacht und in `TestWebApplicationFactory` deaktiviert.
- Linux-Pipeline erhoeht Release-Komplexitaet: Release-Skript-Tests validieren beide Plattformartefakte und das mehrplattformfaehige Manifest.
- Kein Backup im MVP: Vor Installation werden Validierung und Staging strikt ausgefuehrt; Rollback bleibt bewusst ausserhalb des MVP.

## Offene Punkte

Keine.
