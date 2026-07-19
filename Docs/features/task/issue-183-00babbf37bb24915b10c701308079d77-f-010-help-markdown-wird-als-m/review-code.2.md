# Code-Review: Help-Markdown sicher rendern

## Status

Befunde vorhanden

## Befunde

1. **Hoch - Reale interne Help-Links werden beim Rendern funktionslos:** Die vorhandenen Help-Dokumente verlinken umfangreich relative Markdown-Dateien wie `beschreibung.md`, `index.md` und `budgetplanung/index.md` (`Docs/help/budgetplanung/index.md:7-10`, `Docs/help/index.md:9-30`). `HelpContentRenderer.RewriteInternalHelpLinks` schreibt aber nur Ziele um, die mit `F...` beginnen (`FinanceManager.Web/Services/Help/HelpContentRenderer.cs:93-105`, Regex in `:191-195`). Danach entfernt `RemoveUnsafeAnchorHrefs` alle nicht als `/help/view/{slug}` oder `http(s)` erkannten `href`-Werte (`FinanceManager.Web/Services/Help/HelpContentRenderer.cs:163-185`). Ergebnis: Aus realen Links wie `[Beschreibung](beschreibung.md)` wird ein `<a>` ohne `href`; Unterseiten sind aus der Help-UI nicht mehr erreichbar. Das verletzt das Akzeptanzkriterium, dass erlaubte Markdown-Formatierung erhalten bleibt, und ist durch die bestehenden Tests nicht abgedeckt, weil dort nur ein synthetischer `F001-konten.md`-Link getestet wird (`FinanceManager.Tests/Web/Help/HelpContentRendererTests.cs:31-51`).

2. **Mittel - Help-CSP ist nicht gegen die globale Blazor-Ausgabe verifiziert:** Die CSP fuer `/help` und `/help/view/...` setzt `script-src 'self'` ohne `unsafe-inline` oder Nonce/Hash (`FinanceManager.Web/Services/Help/HelpSecurityPolicy.cs:10-12`, `FinanceManager.Web/ProgramExtensions.cs:361-369`). Dieselben Seiten rendern aber weiterhin das globale App-Shell-Markup mit `<ImportMap />` (`FinanceManager.Web/Components/App.razor:67`) und den Blazor-Skripten (`FinanceManager.Web/Components/App.razor:76-78`). Ein Inline-Importmap-Script ist unter dieser CSP browserseitig blockiert; ob die Help-Oberflaeche und Interactive-Server-Funktionen damit stabil laufen, wird aktuell nur per Header-Test, nicht per Browser-/DOM-Test nachgewiesen. Der Plan fordert explizit die Pruefung, ob Interactive Server weitere CSP-Quellen oder Ausnahmen benoetigt.

## Geschlossene Befunde aus `review-code.1.md`

1. **Markdown-Quellpfad und Suchindex:** geschlossen. `GetHelpSourcePath()` zeigt auf `../Docs/help`, `GenerateSearchIndex` baut den Hub-Index aus dieser Quelle, und das Manifest enthaelt `../Docs/help/**/*.md`.
2. **Integritaetscache:** geschlossen. `HelpAssetIntegrityValidator` cached nicht mehr erfolgreiche Dateipruefungen, sondern berechnet den SHA-256-Hash bei jedem Aufruf neu.
3. **Unbekannte statische Help-Dateien:** geschlossen. `IsStaticHelpAssetPath` erfasst jede Datei mit Endung unter `/help`, und die Middleware blockiert nicht manifestierte Assets vor `UseStaticFiles()`.
4. **Unsicheres `rel` bei externen Legacy-Links:** geschlossen. Vorhandenes `rel="opener"` wird entfernt und `noopener noreferrer` wird ergaenzt.

## Fehlende Tests

- Kein Renderer- oder API-Test nutzt reale relative Links aus `Docs/help` und weist nach, dass sichere interne Help-Navigation nach der Sanitization erhalten bleibt.
- Kein browsernaher Test laedt `/help` oder `/help/view/{slug}` unter der gesetzten CSP und prueft Konsole/CSP-Verletzungen, Suche, interne Links, externe Links und beide Sprachen.
- Die HTTP-Auslieferung mit echtem Integritaetsvalidator ist weiterhin nur teilweise abgedeckt; manipulierte Markdown-, JSON-, CSS- und JS-Dateien samt fehlendem Manifest werden nicht durchgaengig ueber echte Requests getestet.
- Die Paketwarnungen zu `HtmlSanitizer`/`AngleSharp`, `AngleSharp` und `SQLitePCLRaw.lib.e_sqlite3` bleiben offen und sollten vor Abschluss des Security-Features bewertet werden.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests|FullyQualifiedName~HelpAssetIntegrityValidatorTests"`: 15 Tests bestanden.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 7 Tests bestanden.
- `dotnet build FinanceManager.Web\FinanceManager.Web.csproj --no-restore`: erfolgreich, 0 Fehler und 8 Warnungen.
