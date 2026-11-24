# Umsetzungsplan: Update für den Updater — `msTools.Updater` 0.10.1 → 0.11.0-rc.8 (Issue #588)

## Übersicht

Die vendored Self-Update-Bibliothek `msTools.Updater` wird von `0.10.1` auf `0.11.0-rc.8` angehoben, um den Upstream-Fix aus `martin-stromberg/msTools.Updater#85` wirksam zu machen: Der Install-Scheduler holt versäumte geplante Update-Installationen unmittelbar nach. Die Umsetzung ist ein reiner Paket- und Versionsaustausch plus Dokumentation — am tatsächlichen Release-Paket verifiziert ist das API-Delta für alle von FinanceManager konsumierten Member **vollständig quellkompatibel** (neue Konstruktorparameter sind optional), sodass kein Produktiv- oder Testcode angepasst werden muss.

## Scope-Klärung

Verbindliche fachliche Abgrenzung — übernommen und am Release-Paket `v0.11.0-rc.8` verifiziert:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die vendored Dependency `msTools.Updater` als Gesamtpaket: `msTools.Updater.0.11.0-rc.8.nupkg` unter `external/msTools.Updater/` (flache lokale NuGet-Quelle `local-msTools.Updater` in `NuGet.config`) plus die drei synchronen `PackageReference`-Einträge in `FinanceManager.Web.csproj` (Z. 68), `FinanceManager.Tests.csproj` (Z. 27), `FinanceManager.Tests.Integration.csproj` (Z. 17). Zielversion ist verbindlich `0.11.0-rc.8` — die stabile `0.10.4` enthält den Scheduler-Fix nicht und würde die Anforderung nicht erfüllen. |
| Auslösende Ereignisse | Veröffentlichung des Upstream-Pre-Releases `v0.11.0-rc.8` (2026-11-23, Asset `msTools.Updater.0.11.0-rc.8.nupkg`, 132.679 Bytes, SHA-256 `3040739920ea20d383564d274174243eebb663fc866bf5b5497132b3877207bc` — Existenz und Digest verifiziert). Die Umsetzung selbst ist eine manuelle Wartungsaktion im Repository, kein Laufzeitereignis. |
| Erwartete Wirkung | Laufzeit: Eine geplante Update-Installation, deren Zeitpunkt verstrichen ist, während das Paket bereitstand oder der Prozess nicht lief, wird unmittelbar nachgeholt statt auf das nächste Zeitfenster zu warten. Repository: Alle drei Projektdateien referenzieren `0.11.0-rc.8`; Build fehlerfrei; Update-Tests grün; `CHANGELOG.md` dokumentiert die Aktualisierung unter `Changed`; die verwaiste Datei `msTools.Updater.0.10.0-rc.1.nupkg` ist entfernt; Versionsverweise in `Docs/help/` sind aktualisiert. |
| Umgang mit fehlenden Objekten | `msTools.Updater.0.11.0-rc.8.nupkg` — **erzeugen**: per `gh release download` aus dem GitHub-Release beziehen, SHA-256 gegen den Release-Digest verifizieren, unter `external/msTools.Updater/` einchecken. `msTools.Updater.0.10.0-rc.1.nupkg` — **verwerfen**: nach abschließender Referenzsuche löschen (verifiziert: nur dokumentarische Nennungen in `Docs/help/`). `msTools.Updater.0.10.1.nupkg` — **verwerfen**: nach abschließender Referenzsuche löschen (Anwender-Entscheidung 2026-11-24; Quelle enthält danach nur noch `0.11.0-rc.8`). Persistierte Laufzeitdateien (`status.json` im 0.10.1-Schema, `settings.json`, `update.lock`, persistierter Paket-Descriptor) — **wiederverwenden**: müssen von 0.11.0-rc.8 weiterhin lesbar sein (durch neuen Kompatibilitätstest abgesichert). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Nutzung neuer Bibliotheks-Features von `0.11.0-rc.8`: konfigurierbarer Dateierhalt (`AutoUpdateBuilder.PreserveFile`/`MergeFile`/`ProtectFile`, `AutoUpdateOptions.ProtectedFiles`, `AutoUpdateProtectedFile`/`...Strategy`), IIS-Optionen (`WithIisApplicationPool`, `AutoUpdateOptions.AppPoolName`/`SiteName`, `IAutoUpdateIisHostingProbe`), neue Statusfelder (`AutoUpdateStatusSnapshot.UpdateLogPath`/`ReadyToInstallAt`) werden nicht angebunden. | Anforderung verlangt ausschließlich den Scheduler-Fix; die Features sind für Kompilierbarkeit nicht erforderlich (verifiziert) und wären eigene Features. | bestätigt (Anwender, 2026-11-24) |
| 2 | Keine Änderung an der öffentlichen REST-API (`/api/setup/update/*`), `IUpdateOrchestrator`, den DTOs in `FinanceManager.Shared.Dtos.Update`, `ApiClient`, `SetupUpdateViewModel`, `SetupUpdateTab.razor`. | Öffentliche Oberfläche bleibt bewusst stabil; Anforderung schließt dies explizit aus. | bestätigt (Anwender, 2026-11-24) |
| 3 | Keine neue Benutzerinteraktion: Die geänderte Nachhollogik wirkt automatisch im Hintergrund (`AutoUpdateSchedulerService` der Bibliothek). | Anwendersicht unverändert; kein neuer UI-Fluss. | bestätigt (Anwender, 2026-11-24) |
| 4 | Keine Konfigurierbarkeit der Nachhollogik und keine Anbindung neuer `AutoUpdateOptions`-Felder (`ProtectedFiles`, `AppPoolName`, `SiteName`, `WorkspaceUser`, `WorkspaceGroup`, `StopHostAfterScriptStart`, `UpdateUnitName`) an FinanceManager-Einstellungen. | Bibliotheks-Defaults gelten; `BindConfiguration(UpdateOptions.SectionName)` bindet den bestehenden `Updates`-Abschnitt unverändert weiter. | bestätigt (Anwender, 2026-11-24) |
| 5 | ~~`msTools.Updater.0.10.1.nupkg` bleibt in `external/msTools.Updater/` liegen~~ — **zurückgenommen (Anwender, 2026-11-24):** `msTools.Updater.0.10.1.nupkg` wird zusammen mit `0.10.0-rc.1` entfernt; die Quelle enthält danach nur noch `0.11.0-rc.8`. | Anwender-Entscheidung: kein Rollback-Bedarf in der Quelle; Download bei Bedarf jederzeit über GitHub-Release möglich. | bestätigt (Anwender, 2026-11-24) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Testcode-Anpassungen | **Keine** — das verifizierte API-Delta ist vollständig quellkompatibel. | Entgegen der Erwartung in `requirement.md`/`inventory.md` sind alle neuen Konstruktorparameter optional: `AutoUpdateStatusSnapshot` erhielt `UpdateLogPath = null` und `ReadyToInstallAt = null` (Positionsparameter 12/13, am Ende angehängt), `AutoUpdateLocalFolderSource.#ctor` erhielt `platformResolver = null` und `manifestFileName = null`. Alle 5 Snapshot-Konstruktoraufrufe, der `AutoUpdateLocalFolderSource`-Aufruf (Z. 148 `AutoUpdateOptionsMapperTests.cs`) und alle übrigen konsumierten Signaturen kompilieren unverändert. |
| Schema-Kompatibilität `status.json` | Ein neuer Integrationstest schreibt ein Statusdokument im 0.10.1-Schema (ohne `updateLogPath`/`readyToInstallAt`) und liest es über den produktiven Pfad ein. | Der Upgrade-Pfad einer produktiven Installation liest eine von 0.10.1 geschriebene `status.json`; der Test belegt, dass fehlende neue Felder auf die optionalen Ctor-Defaults abgebildet werden. |

## Programmabläufe

### Paketbereitstellung und Versionsanhebung

1. `gh release download v0.11.0-rc.8 --repo martin-stromberg/msTools.Updater --pattern "msTools.Updater.0.11.0-rc.8.nupkg"` ausführen; SHA-256 der Datei gegen den Release-Digest `3040739920ea20d383564d274174243eebb663fc866bf5b5497132b3877207bc` prüfen (`certutil -hashfile` oder `Get-FileHash`/`sha256sum`).
2. Datei unter `external/msTools.Updater/` ablegen und einchecken.
3. `Version="0.10.1"` → `Version="0.11.0-rc.8"` in den drei `PackageReference`-Einträgen anheben (`FinanceManager.Web.csproj` Z. 68, `FinanceManager.Tests.csproj` Z. 27, `FinanceManager.Tests.Integration.csproj` Z. 17).
4. `dotnet restore` — die flache Quelle `local-msTools.Updater` (`NuGet.config` Z. 5) liefert das neue Paket ohne Konfigurationsänderung; transitiv kommt neu `System.Management` 10.0.11 hinzu (rest der Laufzeitabhängigkeiten unverändert `10.0.7`).
5. `dotnet build FinanceManager.sln -c Debug` — erwartet 0 Fehler ohne Quellcode-Änderung (Delta verifiziert quellkompatibel); eventuelle nicht erfasste Deltas werden an dieser Stelle iterativ behoben.

Beteiligte Artefakte: `external/msTools.Updater/*.nupkg`, `NuGet.config`, drei `.csproj`-Dateien.

### Schema-Kompatibilitätstest und Testverifikation

1. Neuen Integrationstest in `UpdateControllerIntegrationTests` ergänzen: `status.json` im alten 11-Feld-Schema in den isolierten Update-Workspace schreiben (Literal-JSON ohne `updateLogPath`/`readyToInstallAt`, camelCase-Feldnamen und numerischer `state`-Enum-Wert wie von `JsonSerializerDefaults.Web` erzeugt — analog `WriteStatusAsync`), dann `GET /api/setup/update/status` als Admin aufrufen und das korrekt gemappte `UpdateStatusDto` assertieren.
2. `dotnet test FinanceManager.Tests` (mit `--filter-not-trait "Category=OsInterface"` wie CI) und `dotnet test FinanceManager.Tests.Integration` vollständig ausführen — Fokus auf `FinanceManager.Tests/Updates/*` und `UpdateControllerIntegrationTests`.
3. `UpdateSetupPlaywrightTests` aus `FinanceManager.Tests.E2E` als Regression laufen lassen (Update-Setup-UI unverändert, muss grün bleiben).

Beteiligte Klassen: `UpdateControllerIntegrationTests`, `UpdateStatusTestData`, `FileSystemAutoUpdateStateStore` (Bibliothek, indirekt).

### Bereinigung und Dokumentation

1. Abschließende Referenzsuche auf `0.10.0-rc.1` in `.csproj`, `NuGet.config`, Build-Skripten, Workflows und `Docs/`; verwaiste Datei `external/msTools.Updater/msTools.Updater.0.10.0-rc.1.nupkg` löschen.
2. `CHANGELOG.md` unter `[Unreleased]` → `Changed` ergänzen (Vorbild Z. 44: „msTools.Updater auf 0.10.0-rc.1 aktualisiert"); Erwähnung des Nachhol-Verhaltens als Nutzer-relevante Änderung.
3. Versionsverweise in `Docs/help/updates/installation.md` (Z. 8, 70, 132), `Docs/help/updates/ablauf-technisch.md` (Z. 7), `Docs/help/systemverwaltung-und-setup/installation.md` (Z. 190–191) auf `0.11.0-rc.8` aktualisieren; `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md` (Z. 291–293, beschreibt noch DLL-Vendoring `v0.3.0`) auf das `.nupkg`-Verfahren korrigieren.
4. `Docs/maintenance/dependency-upgrade-process.md` modernisieren: beschreibt noch das veraltete ZIP/DLL-Verfahren mit Versionsverzeichnissen; auf das tatsächliche `.nupkg` + lokale-NuGet-Quelle-Verfahren umstellen (in der Anforderung als Nacharbeit notiert).

### Laufzeitablauf: Nachholen versäumter Installationen (Bibliotheksverhalten, kein FM-Code)

Rein informativ — die Änderung liegt vollständig in `AutoUpdateSchedulerService` (interner `HostedService` der Bibliothek, über `UseAutoUpdate` registriert):

1. `AutoUpdateSchedulerService` prüft periodisch, ob `AutoUpdateOptions.ScheduledInstallTime` erreicht/überschritten ist und ein Paket bereitliegt (`AutoUpdateState.ReadyToInstall`).
2. Neu in 0.11.0-rc.8: Ist der geplante Zeitpunkt verstrichen (Prozess war down oder Paket erst später bereit), wird die Installation unmittelbar nachgeholt statt auf das nächste Zeitfenster zu warten.
3. In FinanceManager-Integrationstests ist der Dienst über `Updates:HostedServicesEnabled=false` deaktiviert (`TestWebApplicationFactory` Z. 126–129); das Verhalten ist FM-seitig nicht automatisierbar testbar und wird durch die Upstream-Tests der Bibliothek abgedeckt.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

Keine — weder Produktiv- noch Testcode müssen geändert werden. Verifiziert am Release-Paket `v0.11.0-rc.8` (XML-Dokumentation + Quellstand am Tag):

- `AutoUpdateStatusSnapshot`: 13 Positionsparameter, `UpdateLogPath`/`ReadyToInstallAt` mit `= null`-Defaults am Ende — alle 5 Aufrufstellen (`UpdateStatusTestData.cs` Z. 18–29/38–49/57–69, `UpdateStatusMapperTests.cs` Z. 37–48/75–86) kompilieren unverändert.
- `AutoUpdateLocalFolderSource.#ctor(string, IAutoUpdatePlatformResolver? = null, string? = null)` — der Aufruf `new AutoUpdateLocalFolderSource(dir.FullName)` in `AutoUpdateOptionsMapperTests.cs` Z. 148 kompiliert unverändert.
- `AutoUpdateCheckResult` (`IsPrerelease = false` optional), `AutoUpdateResult` (Legacy-4-Param-Ctor erhalten), `AutoUpdateInstallResult` (neuer Parameter — kein Konstruktionsaufruf im Repo), `AutoUpdatePackageDescriptor`, `AutoUpdateDownloadResult`, `InstalledReleaseInfo`, `AutoUpdateGithubSource.Create` (optionale Parameter erhalten), `FileSystemAutoUpdatePackageStore`, `AutoUpdateStatusService`, `SourceCheckTimeRange`, `SourceCheckWindowEvaluator`, `JsonFileStore` — alle konsumierten Signaturen unverändert.
- Von FinanceManager real implementierte Bibliotheks-Interfaces (`IAutoUpdateOrchestrator` → `SucceedingAutoUpdateOrchestrator`, `IAutoUpdateEnvironment` → `AutoUpdateEnvironmentAdapter`, `IInstalledVersionProvider` → `FixedInstalledVersionProvider` ×2) haben keine neuen Member; `IAutoUpdatePackageStore` (neu: `FailedMarkerFileName`, `FailedMarkerPath`) wird nur per Moq gemockt.
- `UseAutoUpdate` registriert weiterhin `AutoUpdateOptions` via `AddSingleton(options)` — der Testhelfer `SetDownloadPath` (`UpdateControllerIntegrationTests` Z. 520–533) bleibt funktionsfähig.
- `AutoUpdateGithubSource._includePrereleases` existiert weiterhin (Reflexionstest `ReadGithubIncludePrereleases` unverändert gültig).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine neuen oder geänderten Eingaben eingeführt.

## Konfigurationsänderungen

Keine. `cfg.BindConfiguration(UpdateOptions.SectionName)` bindet den bestehenden `Updates`-Abschnitt unverändert; neue `AutoUpdateOptions`-Felder (`ProtectedFiles` u. a.) laufen mit Bibliotheks-Defaults. `NuGet.config` bleibt unverändert (flache Quelle liefert jede abgelegte `.nupkg`).

## Seiteneffekte und Risiken

- **Verhaltensänderung Nachhollogik (gewünscht, aber ohne Vorwarnung wirksam):** Nach dem Upgrade werden zuvor versäumte geplante Installationen ggf. sofort ausgeführt. Das ist der Zweck des Issues, aber eine spürbare Betriebsänderung — im CHANGELOG-Eintrag explizit benennen.
- **`AutoUpdateOutcome` hat zwei neue Member (`Canceled`, `Skipped`)** — entgegen der Annahme in `requirement.md` (am Paket verifiziert). Kein Kompilierbruch: `UpdateOrchestratorAdapter` nutzt ausschließlich `==`/`!=`-Vergleiche. Semantik: In `CheckAsync` (Z. 119–134) landen beide im Nicht-`Success`-Pfad → `UpdateCheckResultDto(false, ..., result.Message)`; in `StartInstallAsync` (Z. 164–172) im Nicht-`Failed`-Pfad → keine Exception, Lock-Cleanup-Validierung, Status aus dem Snapshot gemappt. Akzeptables Default-Verhalten, kein Handlungsbedarf.
- **`AutoUpdateErrorCode.InstallationVerificationInconclusive` und `AutoUpdateResult.Code`/`AutoUpdateError.ProcessOutput` neu** — werden in FinanceManager nirgends ausgewertet (`UpdateErrorMessageMapper` arbeitet nur mit Nachrichtentexten). Eine unentschiedene Installationsverifikation würde als `Failed`-Outcome mit Meldungstext bis in die UI durchgereicht — generische, aber funktionierende Anzeige.
- **Neue transitive Abhängigkeit `System.Management` 10.0.11** (Windows-spezifische WMI/IIS-API) — wird beim Restore automatisch mitgezogen; auf Linux-Deployments vermutlich ungenutzt (Bibliothek intern, nicht FM-kontrollierbar). Publish-/Runtime-Build auf dem Zielsystem sollte einmal verifiziert werden.
- **`status.json`-Schema-Erweiterung:** Von 0.10.1 geschriebene Dateien ohne `updateLogPath`/`readyToInstallAt` deserialisieren dank optionaler Ctor-Parameter auf `null`-Defaults; neuer Integrationstest sichert dies ab. In Integrationsrichtung serialisiert `WriteStatusAsync` (Z. 501–506) bereits das neue Schema — Rückwärtslesen durch 0.10.1 ist nicht erforderlich (kein Downgrade-Szenario im Scope).
- **E2E-Bestandsfehler:** 8 dokumentierte browser-/timeoutbedingte Fehlschläge außerhalb des Update-Subsystems (siehe `inventory/tests.md`) — kein Blocker, Ausgangszustand bleibt Referenz.
- **`System.Management`-Transitivität bei `dotnet publish`:** Trimmen/AOT nicht im Einsatz; kein weiterer Handlungsbedarf erwartet.

## Umsetzungsreihenfolge

1. **Paket bereitstellen**
   - Voraussetzungen: `gh` CLI mit Zugriff auf `martin-stromberg/msTools.Updater` (verifiziert); Release `v0.11.0-rc.8` mit Asset existiert (verifiziert).
   - Beschreibung: `.nupkg` herunterladen, SHA-256 gegen `3040739920ea20d383564d274174243eebb663fc866bf5b5497132b3877207bc` verifizieren, unter `external/msTools.Updater/` ablegen und einchecken.

2. **Versionen synchron anheben und bauen**
   - Voraussetzungen: Schritt 1 (Paket liegt in der lokalen Quelle).
   - Beschreibung: `Version="0.11.0-rc.8"` in den drei `PackageReference`-Einträgen setzen; `dotnet restore`; `dotnet build FinanceManager.sln -c Debug` → 0 Fehler. Erwartete Bruchstellen existieren nicht (verifiziert); etwaige Restdeltas iterativ beheben.

3. **Schema-Kompatibilitätstest ergänzen**
   - Voraussetzungen: Schritt 2 (Projekt baut gegen 0.11.0-rc.8).
   - Beschreibung: Neuen Test `Status_ReadsStatusJsonWrittenByOlderSchema_ReportsMappedStatus` (Arbeitstitel) in `UpdateControllerIntegrationTests` hinzufügen — literal `status.json` im 0.10.1-Schema (11 Felder, ohne `updateLogPath`/`readyToInstallAt`) in den isolierten Workspace schreiben, `GET /api/setup/update/status` als Admin aufrufen, gemapptes `UpdateStatusDto` assertieren. Hilfsmethoden `SetDownloadPath`/`AuthenticateAdminAsync` wiederverwenden.

4. **Testsuiten ausführen**
   - Voraussetzungen: Schritte 2–3.
   - Beschreibung: `dotnet test FinanceManager.Tests` (CI-Filter `--filter-not-trait "Category=OsInterface"`) und `dotnet test FinanceManager.Tests.Integration` vollständig grün; anschließend `UpdateSetupPlaywrightTests` (`FinanceManager.Tests.E2E`) als Regression.

5. **Verwaistes Paket entfernen**
   - Voraussetzungen: Schritt 2 abgeschlossen (kein Verweis mehr auf 0.10.x außer dem beliebigen Vorhandensein von 0.10.1 in der Quelle).
   - Beschreibung: Referenzsuche `0.10.0-rc.1` und `0.10.1` (`.csproj`, `NuGet.config`, Skripte, Workflows — verifiziert nur noch Docs); `external/msTools.Updater/msTools.Updater.0.10.0-rc.1.nupkg` **und** `external/msTools.Updater/msTools.Updater.0.10.1.nupkg` löschen, sodass nur `0.11.0-rc.8` verbleibt (Anwender-Entscheidung, hebt ursprünglichen Scope-Ausschluss 5 auf).

6. **Dokumentation aktualisieren**
   - Voraussetzungen: Schritt 4 (grüner Stand wird dokumentiert).
   - Beschreibung: `CHANGELOG.md` `Changed`-Eintrag (inkl. Hinweis auf Nachhol-Verhaltensänderung); Versionsverweise in den drei `Docs/help/`-Dateien auf `0.11.0-rc.8`; `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md` von `v0.3.0`-DLL-Vendoring auf `.nupkg`-Verfahren korrigieren; `Docs/maintenance/dependency-upgrade-process.md` auf das `.nupkg`-Verfahren modernisieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Status_ReadsStatusJsonWrittenByOlderSchema_ReportsMappedStatus` (Arbeitstitel) | `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs` | Schreibt literal eine `status.json` im 0.10.1-Serialisierungsschema (11 Felder, ohne `updateLogPath`/`readyToInstallAt`) in den isolierten Update-Workspace und assertiert, dass `GET /api/setup/update/status` den Snapshot fehlerfrei deserialisiert und korrekt auf `UpdateStatusDto` mappt — der produktive Upgrade-Pfad liest Bestandsdateien der Vorversion. |

### Betroffene bestehende Tests

Keine — das verifizierte API-Delta ist quellkompatibel; sämtliche als Bruchkandidaten identifizierten Stellen (`UpdateStatusTestData`, `UpdateStatusMapperTests`, `AutoUpdateOptionsMapperTests` Z. 148, `SucceedingAutoUpdateOrchestrator`) kompilieren unverändert.

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich: Die Anforderung enthält **keine neue oder geänderte Benutzerinteraktion** — REST-API, DTOs, ViewModel und `SetupUpdateTab.razor` bleiben unverändert, und die geänderte Nachhollogik läuft vollständig im internen `AutoUpdateSchedulerService` der Bibliothek, der in Tests über `Updates:HostedServicesEnabled=false` deaktiviert ist und nicht über die UI ansteuerbar ist. Es existiert kein über UI oder Nutzeraktion erreichbarer geänderter Ablauf, den ein E2E-Test nachweisen könnte.

Als Regression ist der Lauf der bestehenden `UpdateSetupPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`) eingeplant — sie decken die unveränderte Update-Setup-UI ab und müssen grün bleiben.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Regression | Bestehende `UpdateSetupPlaywrightTests` laufen unverändert grün | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs` | Update-Setup-UI funktioniert nach Bibliotheks-Upgrade weiter | Nachweis, dass die UI-Schicht durch das Paket-Upgrade nicht beschädigt wurde |

Welche bestehenden E2E-Tests müssen angepasst werden: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Upgrade durchführen | Manuelle Wartungsaktion (Entwickler) | Repo auf `0.10.1`; Ziel-`.nupkg` fehlt unter `external/msTools.Updater/`; `0.10.0-rc.1`-Paket verwaist vorhanden | Paket downloaden + verifizieren + ablegen; drei `PackageReference` auf `0.11.0-rc.8`; `dotnet restore` + `dotnet build`; Testanpassung entfällt (quellkompatibel); Tests laufen; verwaiste Datei löschen; CHANGELOG + Docs | Alle drei Projekte referenzieren `0.11.0-rc.8`; Build 0 Fehler; Unit-/Integrationstests grün; `0.10.0-rc.1.nupkg` entfernt; `CHANGELOG.md` dokumentiert die Änderung | `dotnet build`, `dotnet test`-Läufe, `git status`/Diff |
| 2 | Laufzeitwirkung: versäumte Installation wird nachgeholt | `AutoUpdateSchedulerService`-Zeitprüfung (Bibliothek, Hintergrund) | `ScheduledInstallTime` konfiguriert und verstrichen, während der Prozess nicht lief oder das Paket noch fehlte; Paket liegt bereit (`ReadyToInstall`) | Anwendung mit 0.11.0-rc.8 starten; Scheduler erkennt überfälligen Zeitpunkt | Installation startet unmittelbar statt am nächsten Zeitfenster — sichtbar über Update-Status/Neustart der Anwendung | Upstream-Fix (msTools.Updater#85); FM-seitig nicht automatisierbar (HostedService in Tests deaktiviert) — reale Abnahme im Betrieb; indirekt abgesichert durch grüne Adapter-/Integrationstests |
| 3 | Bestandsschutz: `status.json` der Vorversion bleibt lesbar | `GET /api/setup/update/status` nach Upgrade | Produktive Installation mit von 0.10.1 geschriebener `status.json` (11-Feld-Schema, ohne neue Felder) | Test schreibt Alt-Schema-Datei in isolierten Workspace; API-Status abrufen | Status wird korrekt angezeigt (kein Deserialisierungsfehler, kein Status-Verlust) | Neuer Integrationstest in `UpdateControllerIntegrationTests` |
| 4 | Orphan-Bereinigung ohne Seiteneffekte | Löschung `msTools.Updater.0.10.0-rc.1.nupkg` | Datei verwaist; nur dokumentarische Nennungen in `Docs/help/` | Referenzsuche bestätigt keinen Build-/Projekt-/Workflow-Verweis; Datei löschen; Docs aktualisieren | `dotnet restore`/`dotnet build` weiterhin fehlerfrei; keine verwaiste Datei mehr im Repo | Build + Referenzsuche (0 Treffer außer Changelog-Historie) |

## Offene Punkte

Keine.
