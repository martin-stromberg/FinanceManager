# Bestandsaufnahme: Help-Asset-Sicherheit fuer Search Index

## Umfang

Untersucht wurden `HelpController.GetSearchIndex` und `GenerateSearchIndex`, die Help-Asset-Integritaetspruefung, die als Inline-Middleware implementierte Help-Sicherheit, MSBuild-Content-/Manifestregeln, Help-Assets, `TestWebApplicationFactory` sowie die bestehenden Controller-, Validator- und Integrationstests.

## Wichtigste Befunde

1. `GetSearchIndex` liefert bei fehlendem statischem Asset aktuell einen dynamisch erzeugten Index mit `200 OK`. Dieser Pfad ruft keine Manifestpruefung auf und ist der zentrale Sicherheits-/Vertragsbruch.
2. Eine separate `HelpSecurityMiddleware`-Klasse existiert nicht. Die Schutzlogik ist als Inline-Middleware in `ProgramExtensions.ConfigureMiddleware` vor `UseStaticFiles` implementiert.
3. Das MSBuild-Target `GenerateHelpAssetManifest` hasht vorhandene Assets, erzeugt aber keinen `search-index.json`. Im Quellbaum fehlen aktuell Search-Index- und Manifestdateien.
4. `de` und `en` sind sowohl im Controller als auch in der Localization-Konfiguration festgelegt. Eine gemeinsame, vom Generator wiederverwendete Sprachquelle ist nicht vorhanden.
5. Die Factory konfiguriert keinen Content Root explizit. Integrationstests mutieren stattdessen direkt Dateien im Webprojekt-Quellpfad. Das kann von der tatsächlich geladenen Buildausgabe abweichen.
6. Bestehende Tests sichern Manipulationen eines vorhandenen Indexes ab, erwarten aber gleichzeitig den unsicheren Missing-File-Fallback und testen die Erzeugung/Manifestierung je Sprache nicht als Buildvertrag.

## Detaildokumente

- [Controller und Search Index](inventory/controller.md)
- [Help-Asset-Integritaet und Middleware](inventory/security.md)
- [Build, Generator und Help-Assets](inventory/build-assets.md)
- [Tests und TestWebApplicationFactory](inventory/tests.md)

## Relevante Dateien

- `FinanceManager.Web/Controllers/HelpController.cs`
- `FinanceManager.Web/Services/Help/HelpAssetIntegrityValidator.cs`
- `FinanceManager.Web/Services/Help/HelpSecurityPolicy.cs`
- `FinanceManager.Web/Services/Help/HelpDocumentPathResolver.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/FinanceManager.Web.csproj`
- `FinanceManager.Tests/Controllers/HelpControllerSecurityTests.cs`
- `FinanceManager.Tests/Web/Help/HelpAssetIntegrityValidatorTests.cs`
- `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`
- `FinanceManager.Web/wwwroot/help/`

## Eingrenzung fuer die Planung

Die Umsetzung muss zuerst den verbindlichen Vertrag fuer fehlende Search-Index-Dateien festlegen. Danach sind Generator-/Target-Reihenfolge, gemeinsame Sprachkonfiguration, Content-Root-Bindung und Regressionstests gemeinsam anzupassen. Die Integritaetspruefung darf fuer Controller und statische Middleware nicht auf unterschiedliche physische Asset-Basen zeigen.
