# Übersetzte Anforderung – Issue #588 „Update für den Updater"

## Fachliche Zusammenfassung

Die vendored eingebundene Self-Update-Bibliothek `msTools.Updater` wird von `0.10.1` auf `0.11.0-rc.8` angehoben (Zielversion vorbehaltlich der Klärung unter „Offene Fragen"). Damit wird der Upstream-Fix aus martin-stromberg/msTools.Updater#85 wirksam: Der Install-Scheduler holt versäumte geplante Update-Installationen unmittelbar nach, statt bis zum nächsten konfigurierten Zeitfenster zu warten. Die Einbindung erfolgt weiterhin als lokales NuGet-Paket unter `external/msTools.Updater/` mit synchron angehobener `PackageReference` in allen drei referenzierenden Projekten; neben dem Paketaustausch sind kompatibilitätsbedingte Anpassungen im Testcode erforderlich.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die vendored Dependency `msTools.Updater` als Gesamtpaket — bestehend aus der `.nupkg`-Datei unter `external/msTools.Updater/` (lokale NuGet-Quelle gemäß `NuGet.config`) und den drei `PackageReference`-Einträgen, die stets dieselbe Version tragen müssen.
- **Auslösende Ereignisse:** Veröffentlichung des Upstream-Pre-Releases `v0.11.0-rc.8` (2026-11-23, Asset `msTools.Updater.0.11.0-rc.8.nupkg`), das den korrigierten Install-Scheduler enthält. Die Umsetzung selbst ist eine manuelle Wartungsaktion im Repository, kein Laufzeitereignis.
- **Erwartete Wirkung:**
  - Zur Laufzeit: Eine geplante Update-Installation, deren Zeitpunkt verstrichen ist während das Update-Paket bereits bereitstand (oder der Prozess nicht lief), wird unmittelbar nachgeholt statt auf das nächste Zeitfenster zu warten.
  - Im Repository: Alle drei Projektdateien referenzieren dieselbe neue Version; die Anwendung baut fehlerfrei; die Update-Tests (`FinanceManager.Tests/Updates/*`, `UpdateControllerIntegrationTests`) sind grün; `CHANGELOG.md` dokumentiert die Aktualisierung unter `Changed`; die verwaiste Paketdatei `msTools.Updater.0.10.0-rc.1.nupkg` ist entfernt.
- **Umgang mit fehlenden Objekten:** Das `.nupkg` der Zielversion existiert im Repository noch nicht und ist aus dem GitHub-Release von `martin-stromberg/msTools.Updater` herunterzuladen und unter `external/msTools.Updater/` einzuchecken (erzeugen). Die verwaiste Datei `msTools.Updater.0.10.0-rc.1.nupkg` ist zu verwerfen, nachdem per Referenzsuche sichergestellt wurde, dass kein Build-/Projektverweis mehr darauf besteht (derzeit verweisen nur Dokumentationsdateien auf `0.10.0-rc.1`).
- **Scope-Ausschlüsse:**
  - Keine Nutzung neuer Bibliotheks-Features von `0.11.0-rc.8`: konfigurierbarer Dateierhalt/Merge bei der Installation (`AutoUpdateBuilder.PreserveFile`/`MergeFile`/`ProtectFile`, `AutoUpdateOptions.ProtectedFiles`), IIS-Optionen (`WithIisApplicationPool`, `AutoUpdateOptions.AppPoolName`/`SiteName`, `IAutoUpdateIisHostingProbe`) und neue Statusfelder (`AutoUpdateStatusSnapshot.UpdateLogPath`, `ReadyToInstallAt`) werden nicht angebunden — es sei denn, dies ist für die Kompilierbarkeit zwingend erforderlich.
  - Keine Änderung an der öffentlichen REST-API (`/api/setup/update/*`), an `IUpdateOrchestrator`, den DTOs in `FinanceManager.Shared.Dtos.Update`, `ApiClient`, `SetupUpdateViewModel` oder `SetupUpdateTab.razor`.
  - Keine neue Benutzerinteraktion: Das Feature verhält sich aus Anwendersicht wie bisher; die geänderte Nachhollogik wirkt automatisch im Hintergrund.
  - Keine Konfigurierbarkeit der neuen Nachhollogik — das korrigierte Verhalten der Bibliothek gilt unverändert.

## Betroffene Klassen und Komponenten

- **Projektdateien:** `FinanceManager.Web/FinanceManager.Web.csproj` (`PackageReference` Zeile 68), `FinanceManager.Tests/FinanceManager.Tests.csproj` (Zeile 27), `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj` (Zeile 17) — alle `Version="0.10.1"` → Zielversion.
- **Vendored Paket:** `external/msTools.Updater/` — neue Datei `msTools.Updater.0.11.0-rc.8.nupkg` hinzufügen, `msTools.Updater.0.10.0-rc.1.nupkg` entfernen; `NuGet.config` (Quelle `local-msTools.Updater`) bleibt unverändert.
- **Tests mit Anpassungsbedarf:** `AutoUpdateStatusSnapshot` hat in `0.11.0-rc.8` zwei zusätzliche Konstruktorparameter (`UpdateLogPath`, `ReadyToInstallAt`); die direkten Konstruktoraufrufe in `FinanceManager.Tests/Updates/UpdateStatusTestData.cs` (drei Factory-Methoden) und `FinanceManager.Tests/Updates/UpdateStatusMapperTests.cs` (mindestens zwei Aufrufe) müssen angepasst werden. Ebenso hat `AutoUpdateInstallResult` einen neuen Parameter (`UpdateLogPath`) — Verwendung prüfen.
- **Voraussichtlich unverändert:** `ProgramExtensions.cs` (`UseAutoUpdate` + lokale Extension `SetInitialConfiguration`, Zeilen 264–267 und 570–595), `UpdateOrchestratorAdapter`, `UpdateStatusMapper`, `UpdateSettingsStore`, `AutoUpdateOptionsMapper`, `InstalledReleaseMetadataProvider`, `UpdateServiceCatalog` — die von ihnen genutzten Bibliotheks-APIs (`AutoUpdateBuilder.BindConfiguration`/`WithUpdateUnitName`/`WithDownloadPath`/`WithSourceCheck`/`UseGithubSource`/`UseLocalFolderSource`, `IAutoUpdateOrchestrator`, `AutoUpdateStatusService`, `IAutoUpdatePackageStore`, alle verwendeten `AutoUpdateOptions`-Eigenschaften, `AutoUpdateGithubSource.Create`) existieren in `0.11.0-rc.8` unverändert. `AutoUpdateState` und `AutoUpdateOutcome` haben keine neuen Member, sodass der vollständige Switch in `UpdateStatusMapper` weiterhin abdeckt.
- **Dokumentation:** `CHANGELOG.md` (neuer `Changed`-Eintrag unter `[Unreleased]`). Veraltete Versionsverweise existieren in `Docs/help/updates/installation.md` und `Docs/help/updates/ablauf-technisch.md` (nennen `0.10.0-rc.1`) sowie `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md` (beschreibt noch das alte DLL-Vendoring `v0.3.0`) — Aktualisierung im Dokumentationsschritt prüfen.

## Implementierungsansatz

- `.nupkg` aus dem GitHub-Release `v0.11.0-rc.8` herunterladen (`gh release download v0.11.0-rc.8 --repo martin-stromberg/msTools.Updater`) und unter `external/msTools.Updater/` ablegen.
- Die drei `PackageReference`-Versionen synchron anheben; die lokale NuGet-Quelle (`NuGet.config`) liefert das neue Paket ohne weitere Konfiguration.
- Kompilierbrüche beheben: primär die erweiterten Konstruktorsignaturen von `AutoUpdateStatusSnapshot` (und ggf. `AutoUpdateInstallResult`) im Testcode — die neuen Parameter im Testkontext mit `null`/Defaults belegen, sofern keine neue Fachlichkeit abgebildet werden soll.
- Build (`dotnet build`) und Update-Tests ausführen: `FinanceManager.Tests/Updates/*` sowie `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`.
- Verwaiste `msTools.Updater.0.10.0-rc.1.nupkg` entfernen; vorab Referenzsuche auf `0.10.0-rc.1` in `.csproj`, `NuGet.config`, Build-Skripten und Workflows (aktuell nur dokumentarische Nennungen in `Docs/help/`).
- `CHANGELOG.md` unter `[Unreleased]` → `Changed` ergänzen (Vorbild: vorhandener Eintrag „msTools.Updater auf 0.10.0-rc.1 aktualisiert").
- Prozessreferenz: `Docs/maintenance/dependency-upgrade-process.md` beschreibt noch das ältere ZIP/DLL-Verfahren mit Versionsverzeichnissen; das tatsächliche Verfahren ist inzwischen `.nupkg` + lokale NuGet-Quelle — als mögliche Dokumentationsnacharbeit notiert.

## Konfiguration

Keine neue Konfiguration erforderlich. Die Bibliothek bringt in `0.11.0-rc.8` neue `AutoUpdateOptions`-Felder (`ProtectedFiles`, `AppPoolName`, `SiteName`, `WorkspaceUser`, `WorkspaceGroup`, `UpdateUnitName`, `StopHostAfterScriptStart`) mit; für diese gelten die Bibliotheks-Defaults, da `cfg.BindConfiguration(UpdateOptions.SectionName)` den bestehenden `Updates`-Abschnitt weiterhin bindet. Die bestehenden Einstellungen (Zeitfenster, `ScheduledInstallTime`, `AllowPrereleaseUpdates` usw.) bleiben über `UpdateSettingsStore`/`AutoUpdateOptionsMapper` unverändert wirksam.

## Offene Fragen

- **Zielversion verbindlich festlegen:** `v0.11.0-rc.8` (enthält den Scheduler-Fix, Pre-Release) oder stabile `v0.10.4` (enthält den Fix nicht — würde das beschriebene Problem nicht lösen)? Annahme für diese Übersetzung: `v0.11.0-rc.8` gemäß Issue-Analyse.
- **Pre-Release-Einsatz akzeptabel?** Präzedenzfall vorhanden (`0.10.0-rc.1` wurde bereits konsumiert). Alternative: auf finales `0.11.0` warten.
- **Betriebsrisiko der Nachhollogik:** Nach dem Upgrade werden zuvor versäumte Installationen ggf. sofort ausgeführt — Auswirkung auf produktive Umgebungen prüfen (gewünschter Effekt, aber Verhaltensänderung ohne Vorwarnung).
- **Sprungweite 0.10.1 → 0.11.0-rc.8:** Die Version enthält neben dem Fix umfangreiche Änderungen an IIS- und Linux-Installationspfaden (IIS-Update ohne erhöhte Rechte, konfigurierbarer Dateierhalt, Update-Verifikation ohne `release-metadata.json`, robustes systemd-Monitoring, `AutoUpdateErrorCode.InstallationVerificationInconclusive`). Ist das Mitlaufen dieser Änderungen akzeptiert, obwohl sie lokal nur begrenzt testbar sind?
- **Neues `FailedMarkerFileName`/Fehlermarker-Verhalten:** `IAutoUpdatePackageStore` erhält das Feld `FailedMarkerFileName` und `AutoUpdateErrorCode` ein neues Mitglied; prüfen, ob Fehlerbehandlung/`UpdateErrorMessageMapper` den neuen Fehlercode sinnvoll behandeln (Annahme: kein Handlungsbedarf, da `AutoUpdateErrorCode` in FinanceManager nicht ausgewertet wird).
