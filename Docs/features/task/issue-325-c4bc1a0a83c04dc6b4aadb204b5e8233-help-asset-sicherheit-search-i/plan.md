# Umsetzungsplan: Help-Asset-Sicherheit fuer Search Index

## Ziel und Vertragsentscheidung

`search-index.json` ist fuer jede unterstuetzte Sprache ein verpflichtendes, statisches Help-Asset. Der Build erzeugt die Dateien vor der Manifestberechnung; `help-assets.sha256` enthaelt anschliessend deren Hashes. Der Laufzeitpfad verwendet ausschliesslich diese Dateien und die bestehende Integritaetspruefung.

Fehlt der statische Index, ist er nicht im Manifest enthalten, wurde er manipuliert oder ist er nicht lesbar, liefert `HelpController.GetSearchIndex` `NotFound`. Der bisherige dynamische `OK`-Fallback ueber `GenerateSearchIndex` wird aus dem Requestpfad entfernt. Das ist die verbindliche Entscheidung fuer die offene Vertragsfrage, weil ein Laufzeit-Fallback die Build- und Manifestpflicht umgehen wuerde.

Der verbindliche Sprachumfang bleibt `de` und `en`. Er wird als gemeinsame Konstante/Quelle im Webprojekt definiert und von Controller, Localization-Konfiguration und Build-Target wiederverwendet. Die Produktionskonfiguration benoetigt keine separate Content-Root-Aenderung; die Testfactory wird explizit an die Asset-Basis gebunden, die sie in den Tests mutiert.

## Umsetzungsschritte

1. **Gemeinsame Sprachquelle einfuehren**
   - In `FinanceManager.Web` eine kleine, runtime- und MSBuild-kompatible Sprachdefinition schaffen, bevorzugt als vorhandenes Projekt-/Build-Konfigurationsmuster; falls eine MSBuild-Property nicht aus C# wiederverwendbar ist, die Werte in einer zentralen Datei mit klar dokumentierter Ableitung halten.
   - `HelpController.TryNormalizeLanguage` und `ProgramExtensions.BuildLocalizationOptions` auf diese Quelle umstellen.
   - Das Build-Target fuer exakt dieselben Sprachen iterieren lassen; keine zweite unabhaengige Liste fuer `de`/`en` einfuehren.

2. **Deterministische Search-Index-Erzeugung in die Buildpipeline aufnehmen**
   - `FinanceManager.Web/FinanceManager.Web.csproj` um ein Target vor `GenerateHelpAssetManifest` erweitern.
   - Pro Sprache `Docs/help` durchsuchen und `wwwroot/help/{language}/search-index.json` mit dem bestehenden Search-Index-Vertrag erzeugen. Die Ausgabe muss deterministisch sortiert und UTF-8 geschrieben werden.
   - Die Erzeugung darf nur gueltige, integritaetsseitig zulaessige Markdown-Quelldateien beruecksichtigen und muss dieselben IDs, Titel, Excerpts und Keywords wie die bisherige `GenerateSearchIndex`-Logik liefern. Dafuer die gemeinsame Generatorlogik aus `HelpController` herausloesen oder in eine vom Build nutzbare Generator-Komponente verschieben; keine parallelen, abweichenden Algorithmen pflegen.
   - Sicherstellen, dass das neue Target vor `ResolveProjectStaticWebAssets` und vor dem Manifesttarget laeuft. Die bereits vorhandene Manifest-Glob nimmt die erzeugten JSON-Dateien auf; pruefen, dass `de/search-index.json` und `en/search-index.json` jeweils mit korrektem SHA-256-Eintrag geschrieben werden.
   - Falls das Buildtarget keine C#-Komponente direkt ausfuehren kann, ein kleines, deterministisches Generator-Tool im bestehenden Web-/Buildprojekt anlegen und dessen Eingabe-/Ausgabepfade explizit an `$(ProjectDir)` und `Docs/help` binden. Keine generierten Dateien als handgepflegte Quellartefakte einchecken.

3. **Controller auf statischen, geschuetzten Pfad beschraenken**
   - `FinanceManager.Web/Controllers/HelpController.cs`: in `GetSearchIndex` bei fehlender Datei sofort `NotFound` liefern.
   - `GenerateSearchIndex` entfernen oder als ausschliesslich vom Buildgenerator verwendete gemeinsame Logik aus dem Controller herausnehmen; der HTTP-Request darf diese Methode nicht mehr als Fallback aufrufen.
   - Vor dem Parsen weiterhin `IHelpAssetIntegrityValidator.IsTrustedHelpFile` ausfuehren. Fehlerverhalten fuer manipulierte, fehlende und ungueltige Dateien konsistent dokumentieren und die bestehenden JSON-Validierungen beibehalten.

4. **Test-Content-Root eindeutig machen**
   - `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`: `UseContentRoot`/`UseWebRoot` auf die vom Test verwendete Webprojekt-Assetbasis konfigurieren oder die Mutation-Helper auf den effektiven Root der Factory umstellen.
   - Die Testpfade nicht parallel auf Quellbaum und Buildausgabe zeigen lassen. Vor jedem mutierenden Test sicherstellen, dass Manifest, Middleware und Controller dieselbe physische Datei lesen.

5. **Tests und Buildvertrag anpassen**
   - `FinanceManager.Tests/Controllers/HelpControllerSecurityTests.cs`: den bisherigen Missing-File-Fallback-Test in einen `NotFound`-Regressionstest umwandeln und fuer `de` sowie `en` abdecken. Manipulations-, JSON- und Filtertests beibehalten; bei Bedarf ihre Testfixtures um die Manifest-/Asset-Voraussetzung ergaenzen.
   - `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs`: vorhandene und manipulierte Search-Indizes fuer beide Sprachen testen sowie fehlende Indizes ueber den HTTP-Endpunkt als `404` absichern. Die Tests muessen nach der Factory-Anpassung die effektive Assetbasis verwenden.
   - `FinanceManager.Tests/Web/Help/HelpAssetIntegrityValidatorTests.cs` erweitern, falls fuer beide erzeugten Indexdateien oder deren Manifestzeilen noch keine Abdeckung existiert.
   - Einen fokussierten Build-/Manifesttest im passenden bestehenden Testprojekt ergaenzen, der nach dem Help-Asset-Target die Dateien `wwwroot/help/de/search-index.json` und `wwwroot/help/en/search-index.json`, deren Manifesteintraege und die Hashgleichheit prueft. Wenn MSBuild-Targets im Testprojekt nicht direkt pruefbar sind, den Test als Build-Integrationstest mit temporarem Output und klarer Cleanup-Logik ausfuehren.
   - Tests fuer fehlende `Docs/help`-Quelle beziehungsweise fehlende Sprache festlegen: Der Build muss deterministisch fehlschlagen oder eine klar definierte leere Datei erzeugen; bevorzugt wird ein Buildfehler, wenn eine verbindliche Sprache keinen Index erzeugen kann, damit kein unvollstaendiges Manifest ausgeliefert wird.

## Reihenfolge und Abnahmekriterien

Die Reihenfolge ist: Sprachquelle, Generator und Target-Reihenfolge, Controllervertrag, Factory-Pfade, danach Unit-/Integrations-/Buildtests. Vor Abschluss muessen folgende Bedingungen gelten:

- Ein normaler Build erzeugt beide Search-Indizes vor `help-assets.sha256`.
- Beide Indizes stehen mit korrektem SHA-256 im Manifest.
- Ein vorhandener und unveraenderter Index liefert `200`; ein fehlender oder manipulierter Index liefert `404` und kann keinen dynamischen Ersatzindex ausloesen.
- Controller- und HTTP-Tests verwenden dieselbe physische Assetbasis.
- Bestehende Help-Asset-, JSON- und Sicherheitsregressionen bleiben erfolgreich.

## Testausfuehrung

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~Help`
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter FullyQualifiedName~Help`
- `dotnet build FinanceManager.Web/FinanceManager.Web.csproj`
- Anschliessend die erzeugten `wwwroot/help/{de,en}/search-index.json` und `help-assets.sha256` im Buildoutput pruefen; keine generierten Dateien im Quellbaum zuruecklassen.

## Offene Punkte

Keine. Die fehlende Datei wird als `NotFound` behandelt, `de` und `en` sind verbindlich, und die Content-Root-Anpassung ist auf die Testfactory begrenzt.
