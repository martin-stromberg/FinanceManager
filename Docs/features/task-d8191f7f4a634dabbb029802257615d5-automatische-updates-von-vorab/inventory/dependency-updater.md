# Detail: Dependency und Updater-API

## Bestand

- `FinanceManager.Web/FinanceManager.Web.csproj` referenziert `msTools.Updater` als lokale DLL:
  - `Reference Include="msTools.Updater"`
  - `HintPath` auf `..\external\msTools.Updater\v0.2.0\lib\msTools.Updater.dll`
  - `Private` ist `true`.
- `external/msTools.Updater/v0.2.0/README.md` beschreibt das Artefakt als temporaer vendorte Updater-Release-Version `v0.2.0`.
- Die Assembly meldet per Reflection `msTools.Updater, Version=0.2.0.0`.
- Die lokale Updater-Doku liegt in `external/msTools.Updater/v0.2.0/lib/msTools.Updater.xml`.

## Gefundene Updater-Oberflaechen

- `AutoUpdateBuilder`
  - `BindConfiguration(string)`
  - `UseGithubSource(string repositoryOwner, string repositoryName, string manifestAssetName)`
  - `UseLocalFolderSource(string sourceDirectory, string manifestFileName)`
  - `WithSourceCheck(int interval, IEnumerable<SourceCheckTimeRange>?)`
  - `WithDownloadPath(string)`
  - `WithUpdateUnitName(string)`
- `AutoUpdateOptions` oeffentliche Properties:
  - `Enabled`
  - `EnableAutomaticDownload`
  - `EnableAutomaticInstallation`
  - `SourceCheck`
  - `Source`
  - `DownloadPath`
  - `ServiceName`
  - `ExecutablePath`
  - `ScheduledInstallTime`
  - `HealthTimeoutSeconds`
  - `MaxAssetBytes`
  - `HostedServicesEnabled`
  - `StopHostAfterScriptStart`
  - `UpdateUnitName`
- `AutoUpdateGithubSource`
  - Konstruktor: `(HttpClient, string, string, IAutoUpdatePlatformResolver, string)`
  - Factory: `Create(string, string, string)`

## Vorabversions-API

Im lokalen `v0.2.0`-Artefakt wurde keine explizite API fuer Vorabversionen gefunden:

- Keine Treffer in der XML-Doku fuer `PreRelease`, `Prerelease`, `pre-release` oder `Preview`.
- Keine Property in `AutoUpdateOptions`, die nach Vorabversionen klingt.
- `AutoUpdateGithubSource.Create(...)` akzeptiert nur Owner, Repository und Manifest-Asset-Name.

Damit ist `v0.2.0` zwar die aktuell referenzierte/vendorte Version, aber nicht ausreichend belegt als Zielversion fuer diese Anforderung. Die Planung sollte vor der Implementierung eine neuere Updater-Version oder Paketquelle verifizieren.

## Auswirkungen einer Dependency-Aktualisierung

- Falls weiterhin DLL-vendoring genutzt wird: `external/msTools.Updater/<version>/` ergaenzen und `FinanceManager.Web.csproj`-HintPath umstellen.
- Falls ein NuGet-Paket verfuegbar ist: lokale `Reference` durch `PackageReference` ersetzen und ggf. `external/msTools.Updater` entfernen oder dokumentiert belassen.
- Nach API-Aenderung muessen `ProgramExtensions.SetInitialConfiguration` und `AutoUpdateOptionsMapper.ApplySettings` die neue Option anwenden.
