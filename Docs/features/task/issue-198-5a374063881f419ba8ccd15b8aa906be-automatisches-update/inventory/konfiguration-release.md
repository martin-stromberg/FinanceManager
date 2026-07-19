# Konfiguration und Release-Pipeline

## Fundstellen

- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/appsettings.Production.json`
- `release.config.js`
- `package.json`
- `.github/workflows/release.yml`
- `scripts/resolve-release-version.mjs`
- `scripts/resolve-release-version.test.mjs`
- `README.md`

## Bestehende Konfiguration

`appsettings.json` enthaelt u. a.:

- `Jwt`
- `AlphaVantage:Quota`
- `BackgroundTasks:Enabled`
- `Workers:SecurityPriceWorker:Enabled`
- `Backups:Security`
- `FileLogging`
- `Identity`

`appsettings.Production.json` ueberschreibt Logging, Kestrel, JWT-Lebensdauer, Worker-Flags und FileLogging.

Es gibt noch keine `Updates`-Sektion.

## Release-Pipeline

Die GitHub-Actions-Pipeline `.github/workflows/release.yml` laeuft auf `windows-latest`, richtet Node 22 und .NET 10 ein, testet Unit- und Integration-Projekte, baut die Solution und published:

`dotnet publish FinanceManager.Web/FinanceManager.Web.csproj --configuration Release --framework net10.0 --runtime win-x64 --self-contained true --output publish`

Danach wird das Publish-Verzeichnis als ZIP verpackt:

`FinanceManager-v$RELEASE_VERSION-win-x64.zip`

Das Asset wird je nach Release-Art per `semantic-release`, `gh release create` oder `gh release upload` veroeffentlicht. `release.config.js` haengt nur ein Asset an, wenn `RELEASE_ASSET_PATH` gesetzt ist.

`scripts/resolve-release-version.mjs` kennt den erwarteten Assetnamen `FinanceManager-v{version}-win-x64.zip` und prueft vorhandene GitHub-Releases auf dieses Asset.

## Luecken fuer Self-Update

- Es wird keine `update.json` erzeugt oder veroeffentlicht.
- Es gibt kein Linux-Release-Asset.
- Die Anforderung nennt `https://github.com/martin-stromberg/FinanceManager`; Bestand verweist in README/Code teils auf `Muesli84/FinanceManager`.
- Die Anwendung hat keinen klar erkennbaren zentralen Service fuer die aktuell installierte Version.
- Die Pipeline berechnet keinen SHA-256 und keine Dateigroesse als separates Manifest.

## Relevanz fuer Umsetzung

Fuer die Planung gibt es zwei Optionen:

- Pipeline erweitert um `update.json` pro Release mit Version, Beschreibung, Datum, Assetname, Groesse, SHA-256 und Plattform.
- Updater liest direkt GitHub Release API plus Asset-Metadaten und nutzt optional ein separat berechnetes Hash-Asset.

Da die Anforderung explizit `update.json` nennt, sollte die Pipeline erweitert werden. Dazu sind Tests in `scripts/resolve-release-version.test.mjs` sinnvoll, z. B. fuer Assetnamen, Manifest-Erzeugung und Hash-Felder.

