# Plan-Review: Help-Markdown sicher rendern

## Status

Offene Aufgaben vorhanden

## Zusammenfassung

Die zentralen Sicherheitsbausteine aus dem Plan sind in der aktuellen Implementierung angelegt: ein gemeinsamer Markdown-/HTML-Renderer, ein begrenztes Suchindex-Datenmodell, DOM-basierte Suchergebnisdarstellung, eine Help-CSP und ein SHA-256-Manifest. Die Umsetzung ist jedoch noch nicht vollständig nachgewiesen und im aktuellen Repository ist der produktive Markdown-Pfad nicht funktionsfähig.

## Planabgleich

| Planbereich | Bewertung | Nachweis |
|---|---|---|
| Zentrale Rendering-Grenze | Weitgehend umgesetzt | `IHelpContentRenderer`/`HelpContentRenderer` sind vorhanden; `Markdig` deaktiviert HTML und `Ganss.Xss` sanitisiert die Ausgabe. `HelpPageView` gibt nur das API-Ergebnis als `MarkupString` aus. |
| API und statische Help-Ausgabe | Teilweise umgesetzt | Alle drei Controller-Pfade verwenden Validierung bzw. Sanitization. Der Markdown-Quellpfad und die Integritätsprüfung sind im aktuellen Repository jedoch nicht konsistent mit den vorhandenen Help-Dokumenten. |
| Suchindex ohne DOM-XSS | Umgesetzt | `help-search.js` verwendet DOM-Methoden, `textContent` und Event-Listener; indexabhängiges `innerHTML` und Inline-Handler sind entfernt. |
| CSP und statische Dateien | Teilweise umgesetzt | CSP und CSS-Auslagerung sind vorhanden. Die Middleware schützt aber nur bekannte Dateiendungen; unbekannte Dateien unter `/help` werden weiterhin durch `UseStaticFiles()` ausgeliefert. |
| Build-Artefakte und Integrität | Teilweise umgesetzt | Das Projekt erzeugt ein SHA-256-Manifest und validiert bekannte statische Assets. Vollständige Positiv-/Negativnachweise sowie eine restriktive Build-Eingabemenge fehlen. |
| Tests und Nachweise | Teilweise umgesetzt | Renderer-, Controller- und zwei Middleware-Tests existieren und bestehen. Die im Plan geforderten Browser-, Manipulations-, vollständigen API-/CSP- und Build-Integritätstests fehlen. |

## Offene Aufgaben

1. **Markdown-Quellpfad und Artefaktquelle korrigieren (hoch):** `HelpController.GetMarkdown` sucht unter `../docs/business/features` (`FinanceManager.Web/Controllers/HelpController.cs:91-101`). Dieses Verzeichnis existiert im aktuellen Repository nicht; die vorhandenen Help-Markdown-Dateien liegen unter `Docs/help`. Der Build-Target verwendet denselben nicht vorhandenen Pfad (`FinanceManager.Web/FinanceManager.Web.csproj:77-88`). Eine tatsächlich verwendbare, explizite Help-Quelle muss festgelegt und für Laufzeit, Build und Manifest identisch konfiguriert werden.
2. **Unbekannte statische Help-Dateien fail-closed behandeln (hoch):** `wwwroot/help/**` wird weiterhin pauschal als Output übernommen (`FinanceManager.Web/FinanceManager.Web.csproj:13-14`) und `UseStaticFiles()` liefert alle Dateien aus. Die Integritätsmiddleware prüft nur `.css`, `.js`, `.json`, `.html` und `.md` (`FinanceManager.Web/ProgramExtensions.cs:371-396`). Dateien mit anderen Endungen können daher ohne Manifestprüfung ausgeliefert werden. Die erlaubten Dateien müssen im Build explizit begrenzt und nicht gelistete Help-Dateien blockiert werden.
3. **Manifest-Integrität vollständig testen (mittel):** Es fehlen Tests für fehlendes Manifest, nicht gelistete Assets, Hash-Abweichungen, manipulierte Markdown-/JSON-/CSS-/JS-Dateien und die tatsächliche Auslieferungsverweigerung. Der Validator cached Ergebnisse (`FinanceManager.Web/Services/Help/HelpAssetIntegrityValidator.cs:30-55`); das gewünschte Verhalten nach einer Dateiänderung muss ausdrücklich festgelegt und getestet werden.
4. **API- und Renderer-Abdeckung an den Plan angleichen (mittel):** Es fehlen Nachweise für verschachtelte Payloads, Event-Handler in verschiedenen Tags, `data:`- und `javascript:`-URLs, Codeblöcke, Tabellenränder, externe Linkattribute, ungültiges JSON sowie fehlende Pflichtfelder. Ein JSON-Dokument ohne `documents` wird derzeit als leeres erfolgreiches Ergebnis (`200`) behandelt statt als kontrollierter ungültiger Index.
5. **CSP-/Browser-Nachweise ergänzen (mittel):** Die vorhandenen Middleware-Tests prüfen nur `/help/js/help-search.js` und `/api/help/search-index/de.json`. Tests für `/help`, `/help/view/...`, Legacy-HTML und beide Help-API-Routen sowie ein browsernaher Test für Suche, interne/externe Links und manipulierte Indexwerte fehlen. Die geforderte manuelle Prüfung beider Sprachen ist ebenfalls nicht dokumentiert.

## Verifikation

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests"`: 7 Tests bestanden.
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 2 Tests bestanden.
- Build/Test-Ausgaben enthalten bestehende Paketwarnungen, unter anderem `NU1608` für `HtmlSanitizer`/`AngleSharp` sowie bekannte `NU1902`/`NU1903`-Sicherheitswarnungen. Diese verhindern den Lauf nicht, sollten aber vor Abschluss des Features bewertet werden.
