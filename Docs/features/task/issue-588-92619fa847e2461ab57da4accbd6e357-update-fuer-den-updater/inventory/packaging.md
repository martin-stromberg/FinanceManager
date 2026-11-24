# Paketierung & Einbindung von `msTools.Updater`

Bestand der vendored Einbindung zum Zeitpunkt der Bestandsaufnahme.

## Lokale NuGet-Quelle

Datei: `NuGet.config` (Repository-Root)

| Quelle | Pfad | Bemerkung |
|--------|------|-----------|
| `local-msTools` | `external/msTools.Web.Blazor` | andere Bibliothek, nicht Gegenstand dieser Anforderung |
| `local-msTools.Updater` | `external/msTools.Updater` | flache Paketquelle; liefert alle dort abgelegten `.nupkg`-Dateien |

## Vendored Paketdateien

Verzeichnis: `external/msTools.Updater/`

| Datei | Rolle |
|-------|-------|
| `msTools.Updater.0.10.0-rc.1.nupkg` | verwaist — kein `PackageReference` verweist mehr darauf; nur noch dokumentarische Nennungen (siehe Hauptdokument) |
| `msTools.Updater.0.10.1.nupkg` | aktiv referenzierte Version |

Die Zielversion `msTools.Updater.0.11.0-rc.8.nupkg` ist zum Zeitpunkt der Bestandsaufnahme **nicht** vorhanden.

## Nuspec-Metadaten des aktiven Pakets (0.10.1)

Aus `msTools.Updater.nuspec` im Paket:

- Paket-ID `msTools.Updater`, Version `0.10.1`, Zielframework `net10.0`
- Quell-Repository: `https://github.com/martin-stromberg/msTools.Updater`, Commit `0495e4f4655749ba4cfa1c0504da20c192e50dd3`
- Laufzeitabhängigkeiten (alle `net10.0`, `exclude="Build,Analyzers"`): `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` — je `10.0.7`
- Inhalt: `lib/net10.0/msTools.Updater.dll` + `msTools.Updater.xml` (456 dokumentierte Member)

## PackageReference-Einträge

Alle drei Referenzen tragen synchron `Version="0.10.1"`:

| Datei | Zeile | Eintrag |
|-------|-------|---------|
| `FinanceManager.Web/FinanceManager.Web.csproj` | 68 | `<PackageReference Include="msTools.Updater" Version="0.10.1" />` |
| `FinanceManager.Tests/FinanceManager.Tests.csproj` | 27 | `<PackageReference Include="msTools.Updater" Version="0.10.1" />` |
| `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj` | 17 | `<PackageReference Include="msTools.Updater" Version="0.10.1" />` |

`FinanceManager.Tests.E2E` referenziert das Paket **nicht** direkt (eigene Prüfung: kein `PackageReference`, keine `using msTools`-Direktive im E2E-Quellcode).

## Konfiguration (`Updates`-Abschnitt)

`UpdateOptions.SectionName = "Updates"` wird per `cfg.BindConfiguration(UpdateOptions.SectionName)` an `AutoUpdateOptions` gebunden; der Abschnitt existiert in:

- `FinanceManager.Web/appsettings.json` Zeilen 50–67 (`Enabled=false`, `HostedServicesEnabled=true`, `SourceCheckStartTime`/`EndTime` 20:00/06:00, `RepositoryOwner`/`RepositoryName`/`ManifestAssetName`, `WorkingDirectory="updates"`, `HealthTimeoutSeconds=120`, `MaxAssetBytes=536870912`, `SourceType="Github"`, `LocalFolderPath=null`, `EnableAutomaticDownload=true`, `EnableAutomaticInstallation=false`, `StopHostAfterScriptStart=false`)
- `FinanceManager.Web/appsettings.Production.json` Zeilen 36–46 (Teilmenge ohne `SourceType`/`LocalFolderPath`/`EnableAutomatic*`/`StopHostAfterScriptStart`)
- `FinanceManager.Web/appsettings.Development.json` — kein `Updates`-Abschnitt
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` Zeilen 126–129 setzt Test-Overrides (`HostedServicesEnabled=false`, `SourceType=LocalFolder`, `LocalFolderPath=<temp>`, `WorkingDirectory=<temp>`)

## Veraltete Prozess-/Versionsdokumentation

| Datei | Zeile(n) | Inhalt |
|-------|----------|--------|
| `Docs/maintenance/dependency-upgrade-process.md` | 1–23 | Beschreibt noch das alte ZIP/DLL-Vendoring mit Versionsverzeichnissen (`external/<lib>/<version>/`, `SHA256SUMS.txt`, `lib/`); das tatsächliche Verfahren ist `.nupkg` + lokale NuGet-Quelle |
| `Docs/help/updates/installation.md` | 8, 70, 132 | nennt `msTools.Updater 0.10.0-rc.1` und `external/msTools.Updater/msTools.Updater.0.10.0-rc.1.nupkg` |
| `Docs/help/updates/ablauf-technisch.md` | 7 | nennt `msTools.Updater 0.10.0-rc.1` |
| `Docs/help/systemverwaltung-und-setup/installation.md` | 190–191 | nennt `msTools.Updater 0.10.0-rc.1` und die `.nupkg`-Datei |
| `Docs/help/systemverwaltung-und-setup/ablauf-technisch.md` | 291–293 | beschreibt noch das ältere DLL-Vendoring `v0.3.0` unter `external/msTools.Updater/v0.3.0/` |
| `CHANGELOG.md` | 44 | vorhandener `Changed`-Eintrag „msTools.Updater auf 0.10.0-rc.1 aktualisiert" (Vorbild für den neuen Eintrag unter `[Unreleased]`) |

Referenzsuche `0.10.0-rc.1`: außer den gelisteten Dokumentationsdateien und der `requirement.md` keine Treffer in `.csproj`, `NuGet.config`, Skripten oder Workflows — die Paketdatei ist damit verwaist.
