# Plan-Review: Help-Markdown sicher rendern

## Status

Offene Aufgaben vorhanden

## Zusammenfassung

Die beiden hoch priorisierten Punkte aus `review.1.md` sind in der aktuellen
Implementierung geschlossen: Laufzeit, Build und Manifest verwenden `Docs/help`
als gemeinsame Markdown-Quelle, und statische Help-Dateien mit beliebiger
Dateiendung werden vor `UseStaticFiles()` gegen das Manifest geprüft. Die
Sicherheitslogik ist durch fokussierte Tests und einen erfolgreichen Build
belegt. Für die vollständige Planerfüllung fehlen jedoch weiterhin reale
Auslieferungs-/Manipulationsnachweise, breitere Renderer- und API-Fälle sowie
Browser- und beidsprachige Prüfungen.

## Planabgleich

| Planbereich | Bewertung | Nachweis |
|---|---|---|
| Zentrale Rendering-Grenze | Weitgehend umgesetzt | `IHelpContentRenderer` rendert mit Markdig und sanitisiert per Whitelist; `HelpPageView` gibt das API-Ergebnis als `MarkupString` aus. Die Frontmatter-Entfernung und grundlegende XSS-Fälle sind getestet. |
| API und statische Help-Ausgabe | Weitgehend umgesetzt | Markdown, Legacy-HTML und Suchindex nutzen die zentrale Absicherung. `GetHelpSourcePath()` zeigt auf `../Docs/help`; der Build-Target und das Manifest verwenden dieselbe Quelle. Ungültige Suchindizes ohne `documents` werden abgewiesen, ungültige Dokumente verworfen. |
| Suchindex ohne DOM-XSS | Umgesetzt | `help-search.js` verwendet DOM-Methoden, `textContent` und Event-Listener; `innerHTML` und Inline-Handler sind entfernt. Feature-IDs werden vor der Navigation validiert und URL-kodiert. |
| CSP und statische Dateien | Weitgehend umgesetzt | CSP wird für `/help`, `/api/help` und statische Help-Assets gesetzt. Die Integritätsmiddleware prüft jede `/help`-URL mit Dateiendung, nicht nur bekannte Endungen; ein unbekanntes `.svg` wird per Integrationstest blockiert. Browsernachweise fehlen. |
| Build-Artefakte und Integrität | Teilweise umgesetzt | Die Projektdatei begrenzt die kopierten Help-Assets auf CSS, JS, JSON, HTML und das Manifest; der SHA-256-Target umfasst zusätzlich `../Docs/help/**/*.md`. Validator-Unit-Tests decken fehlendes Manifest, nicht gelistete Assets, Hash-Abweichung und nachträgliche Änderung ab. Die reale Build-/Auslieferungskette ist noch nicht vollständig getestet. |
| Tests und Nachweise | Teilweise umgesetzt | Fokussierte Tests: 15 Unit-Tests und 7 Middleware-Integrationstests bestanden. Der Web-Build besteht mit 0 Fehlern. Browser-/E2E-Tests, Tests für beide Sprachen, vollständige API-Auslieferung mit echtem Validator sowie ein vollständiger Build-Manipulationstest fehlen. |

## Geschlossene Punkte aus `review.1.md`

1. **Markdown-Quellpfad und Artefaktquelle:** geschlossen. `HelpController`
   liest aus `../Docs/help`; `FinanceManager.Web.csproj` erzeugt das Manifest
   aus `../Docs/help/**/*.md` und den statischen Help-Assets. Die vorhandenen
   Dateien unter `Docs/help` werden damit erreicht.
2. **Unbekannte statische Help-Dateien:** geschlossen. `IsStaticHelpAssetPath`
   erkennt jede Datei mit Dateiendung unter `/help`; nicht gelistete Dateien
   werden vor der statischen Auslieferung mit `404` blockiert. Der
   Integrationstest `UnknownHelpFileExtension_IsBlockedBeforeStaticFiles`
   bestätigt dieses Verhalten.

## Offene Aufgaben

1. **Manifest-Integrität vollständig testen (mittel):** Die Validator-Unit-
   Tests decken die Kernfälle ab, aber es fehlt ein Test der realen
   HTTP-Auslieferung für manipulierte Markdown-, JSON-, CSS- und JS-Dateien
   einschließlich fehlendem Manifest. Ebenso fehlt ein expliziter Nachweis,
   dass der Manifestinhalt nach einem Build vollständig und mit den
   tatsächlich ausgelieferten Dateien konsistent ist.
2. **API- und Renderer-Abdeckung erweitern (mittel):** Die vorhandenen Tests
   decken grundlegendes Markdown, Links, Tabellen, Scripts und einen einfachen
   ungültigen Suchindex ab. Nach Plan fehlen noch verschachtelte Payloads,
   Event-Handler in unterschiedlichen Tags, `data:`-URLs, Codeblock-/Tabellen-
   Grenzfälle, fehlende Pflichtfelder in mehreren Varianten sowie Tests der
   drei API-Pfade mit dem echten Integritätsvalidator.
3. **CSP-/Browser-Nachweise ergänzen (mittel):** Die Middleware-Tests prüfen
   inzwischen `/help`, `/help/view/...`, beide API-Ausgabepfade und ein
   unbekanntes statisches Suffix auf CSP bzw. Blockierung. Es fehlen weiterhin
   ein browsernaher Test für Suche, interne/externe Links und manipulierte
   Indexwerte sowie eine dokumentierte Prüfung der deutschen und englischen
   Help-Ansicht.
4. **Abhängigkeiten bewerten (mittel):** Build und Tests bestehen, melden aber
   weiterhin `NU1608` für `HtmlSanitizer`/`AngleSharp`, `NU1902` für AngleSharp
   und `NU1903` für SQLitePCLRaw. Diese Warnungen sind nicht feature-spezifisch
   behoben und müssen vor Abschluss der Sicherheitsanforderung bewertet werden.

## Verifikation

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests|FullyQualifiedName~HelpAssetIntegrityValidatorTests"`: 15 Tests bestanden.
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 7 Tests bestanden.
- `dotnet build FinanceManager.Web/FinanceManager.Web.csproj --no-restore`: erfolgreich, 0 Fehler und 8 Warnungen.

