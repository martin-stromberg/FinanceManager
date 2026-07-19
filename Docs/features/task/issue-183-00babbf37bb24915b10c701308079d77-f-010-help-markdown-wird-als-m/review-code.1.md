# Code-Review: Help-Markdown sicher rendern

## Status

Befunde vorhanden

## Befunde

1. **Hoch - Produktiver Help-Markdown-Pfad ist nicht funktionsfaehig:** `HelpController.GetMarkdown` sucht Markdown fest unter `../docs/business/features` (`FinanceManager.Web/Controllers/HelpController.cs:91-101`). Dieses Verzeichnis existiert im aktuellen Repository nicht; die vorhandenen Help-Dokumente liegen unter `Docs/help/`. Der Build-Target fuer das Integritaetsmanifest verwendet denselben nicht vorhandenen Pfad (`FinanceManager.Web/FinanceManager.Web.csproj:77-88`). Zusaetzlich erwartet der Hub einen Suchindex unter `/api/help/search-index/{lang}.json`, waehrend unter `FinanceManager.Web/wwwroot/help/de` und `FinanceManager.Web/wwwroot/help/en` im Git nur `.gitkeep` liegt. Ergebnis: Die Help-Ansicht kann vorhandene Dokumentation nicht laden, und die Hub-Suche/Listendarstellung bleibt ohne erzeugten Index defekt. Der vorhandene Controller-Test baut kuenstlich genau `docs/business/features` auf (`FinanceManager.Tests/Controllers/HelpControllerSecurityTests.cs:27-39`) und deckt diese Regression deshalb nicht ab.

2. **Hoch - Integritaetspruefung vertraut nach erstem Treffer dauerhaft demselben Pfad:** `HelpAssetIntegrityValidator.IsTrustedHelpFile` cached das Ergebnis pro absolutem Pfad (`FinanceManager.Web/Services/Help/HelpAssetIntegrityValidator.cs:30-38`). Wird eine Help-Datei nach einer erfolgreichen Pruefung zur Laufzeit veraendert, liefert der Cache weiter `true`, ohne den Hash erneut zu berechnen. Damit kann eine nachtraegliche Manipulation von bereits angefragten Help-Artefakten die Manifestpruefung umgehen. Das verletzt die Anforderung, Help-Dateien gegen Manipulation ausserhalb des vorgesehenen Build-Prozesses zu schuetzen.

3. **Mittel - Unbekannte Dateien unter `/help` umgehen die Manifest-Grenze:** `IsStaticHelpAssetPath` prueft nur `.css`, `.js`, `.json`, `.html` und `.md` (`FinanceManager.Web/Services/Help/HelpSecurityPolicy.cs:30-42`). Fuer alle anderen Dateien unter `/help` laeuft die Middleware weiter bis `UseStaticFiles()` (`FinanceManager.Web/ProgramExtensions.cs:371-396`). Gleichzeitig kopiert das Projekt weiterhin pauschal `wwwroot\help\**` ins Output (`FinanceManager.Web/FinanceManager.Web.csproj:13-14`). Eine unmanifestierte Datei wie `/help/payload.svg` oder ein anderes statisches Artefakt waere damit auslieferbar, obwohl die Anforderung eine explizite, vertrauenswuerdige Help-Artefaktmenge verlangt.

4. **Mittel - Externe Link-Schutzattribute werden bei Legacy-HTML nicht erzwungen:** Der Sanitizer erlaubt `target` und `rel` (`FinanceManager.Web/Services/Help/HelpContentRenderer.cs:80-83`). `AddExternalLinkSafetyAttributes` fuegt `rel="noopener noreferrer"` nur hinzu, wenn noch kein `rel` existiert (`FinanceManager.Web/Services/Help/HelpContentRenderer.cs:116-131`). Legacy-HTML mit `<a href="https://..." target="_blank" rel="opener">` bleibt dadurch mit unsicherem `rel` erhalten. Das ist keine XSS-Ausfuehrung, aber eine vermeidbare Tabnabbing-Luecke und widerspricht dem Plan, externe Links immer mit `noopener noreferrer` auszugeben.

## Fehlende Tests

- Kein Test nutzt die echte `Docs/help`-Struktur oder weist nach, dass Runtime, Build-Manifest und Help-Hub dieselbe Artefaktquelle verwenden.
- Keine Integritaetstests fuer fehlendes Manifest, nicht gelistete Assets, Hash-Abweichungen und besonders eine Dateiaenderung nach einer bereits gecachten erfolgreichen Pruefung.
- Keine Middleware-Tests fuer unbekannte Dateiendungen unter `/help`, die von `UseStaticFiles()` ausgeliefert werden koennten.
- CSP-Tests decken nur `/help/js/help-search.js` und `/api/help/search-index/de.json` ab; es fehlen `/help`, `/help/view/...`, `/api/help/markdown/...` und Legacy-HTML.
- Es fehlt ein browsernaher Test fuer manipulierte Suchindexwerte, DOM-Ausgabe, interne/externe Links und beide Sprachen.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests"`: 7 Tests bestanden.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 2 Tests bestanden.
- Beide Testlaeufe melden Paketwarnungen, unter anderem `NU1608` fuer `HtmlSanitizer`/`AngleSharp`, `NU1902` fuer `AngleSharp` und `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3`; diese Warnungen blockieren die fokussierten Tests nicht, sollten vor Abschluss des Security-Features aber bewertet werden.
