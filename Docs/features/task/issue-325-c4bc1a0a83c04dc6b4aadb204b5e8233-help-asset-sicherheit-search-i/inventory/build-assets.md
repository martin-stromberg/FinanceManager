# Build, Generator und Help-Assets

## Projektbestand

Im Repository sind derzeit unter `FinanceManager.Web/wwwroot/help` nur CSS, JavaScript und `.gitkeep`-Dateien fuer `de` und `en` vorhanden. Die erwarteten `search-index.json`-Dateien und `help-assets.sha256` sind keine statischen Quellartefakte im Arbeitsbaum; sie werden beim Build erzeugt oder sind aktuell nicht vorhanden.

## MSBuild-Target

`FinanceManager.Web/FinanceManager.Web.csproj` enthaelt:

- Content-Regeln fuer `wwwroot/help/css`, `wwwroot/help/js`, JSON/HTML und `help-assets.sha256`, jeweils mit `CopyToOutputDirectory=PreserveNewest`.
- `GenerateHelpAssetManifest` vor `ResolveProjectStaticWebAssets` und `Build`.
- Das Target hasht vorhandene CSS-, JS-, JSON- und HTML-Dateien unter `wwwroot/help` sowie `../Docs/help/**/*.md` und schreibt `wwwroot/help/help-assets.sha256`.

Es gibt kein separates Help-Asset-Generator-Skript, das `search-index.json` erzeugt. Das vorhandene Target listet Dateien nur fuer das Manifest; es erzeugt keinen Search Index. Damit werden fehlende Sprachdateien nicht automatisch erstellt und koennen auch nicht in das Manifest gelangen.

## Sprach- und Pfadquellen

- `HelpController.TryNormalizeLanguage` akzeptiert `de` und `en`.
- `ProgramExtensions.BuildLocalizationOptions` verwendet ebenfalls `de` und `en` (`ProgramExtensions.cs:375-381`).
- `HelpDocumentPathResolver` leitet `Docs/help` aus dem Content Root ab.

Die zentrale Sprachkonfiguration ist damit faktisch doppelt vorhanden. Fuer den Build muss eine gemeinsame Quelle verwendet oder zumindest eine konsistente Abdeckung sichergestellt werden.

## Relevante Konsequenz

Der Buildpfad muss vor `ResolveProjectStaticWebAssets` beziehungsweise vor Teststart je Sprache einen deterministischen `search-index.json` erzeugen. Erst danach kann das bestehende Hash-Target diese Dateien erfassen. Reihenfolge, Eingabepfad, Ausgabepfad und Manifest-Schluessel muessen mit Runtime und Tests identisch sein.

