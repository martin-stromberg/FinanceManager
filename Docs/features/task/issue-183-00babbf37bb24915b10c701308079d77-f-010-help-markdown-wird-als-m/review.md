# Plan-Review: Help-Markdown sicher rendern

## Status

Offene Aufgaben vorhanden

## Zusammenfassung

Die beiden hoch priorisierten Punkte aus `review.1.md` sowie der relative
Link-Befund aus `review-code.2.md` sind in der aktuellen Implementierung
geschlossen. Die gemeinsame Quelle `Docs/help` wird von Laufzeit, Build und
Manifest verwendet; unbekannte statische Dateien unter `/help` werden vor
`UseStaticFiles()` blockiert; reale relative Markdown-Links werden in interne
Help-Routen umgeschrieben. Die Help-CSP wird auf Help-Seiten ohne ImportMap und
Blazor-Skript ausgegeben.

Die Planerfüllung ist dennoch nicht vollständig nachgewiesen. Es fehlen vor
allem echte HTTP-Manipulationstests für alle Assettypen, Browser-/E2E-Nachweise
für Suche und beide Sprachen sowie eine Entscheidung zu den verbleibenden
Paket-Sicherheits- und Versionswarnungen.

## Planabgleich

| Planbereich | Bewertung | Nachweis |
|---|---|---|
| Zentrale Rendering-Grenze | Weitgehend umgesetzt | `IHelpContentRenderer` nutzt Markdig mit deaktiviertem HTML und eine explizite Sanitizer-Whitelist. `HelpPageView` gibt nur das Renderergebnis als `MarkupString` aus. Frontmatter, XSS-Payloads, Daten-URLs, Tabellen und reale relative Links sind durch Tests abgedeckt. |
| API und statische Help-Ausgabe | Weitgehend umgesetzt | Markdown, Legacy-HTML und Suchindex werden validiert bzw. sanitisiert. Der Suchindex wird als DTO eingelesen; fehlende Pflichtfelder und ein fehlendes `documents`-Array werden behandelt. Die drei Ausgabepfade sind aber nicht durchgängig mit einem echten Validator über HTTP getestet. |
| Suchindex ohne DOM-XSS | Umgesetzt | `help-search.js` verwendet `textContent`, DOM-Erzeugung und Event-Listener. `innerHTML` und Inline-Handler sind entfernt; Feature-IDs werden validiert und URL-kodiert. Ein Browsernachweis mit manipulierten Indexwerten fehlt. |
| CSP und statische Dateien | Weitgehend umgesetzt | CSP gilt für Help-Routen, API und statische Assets; `unsafe-inline` fehlt. `App.razor` lässt auf Help-Flächen ImportMap und Blazor-Skript weg. Die Middleware blockiert unbekannte Dateiendungen. Die Wirkung im realen Browser ist nicht geprüft. |
| Build-Artefakte und Integrität | Teilweise umgesetzt | Das Manifest enthält `Docs/help`-Markdown sowie vorgesehene CSS-, JS-, JSON- und HTML-Assets. Validator-Unit-Tests prüfen fehlendes Manifest, nicht gelistete Dateien, Hash-Abweichung und nachträgliche Änderung. Die reale Build-/Auslieferungskette mit manipulierten Dateien ist nicht vollständig nachgewiesen. |
| Tests und Nachweise | Teilweise umgesetzt | Fokussierte aktuelle Tests bestehen: 25 Unit-Tests und 11 Middleware-Integrationstests. Es fehlen Browser-/E2E-Tests für Suche, Links und manipulierte Indexwerte sowie dokumentierte deutsche und englische Browserprüfung. |

## Geschlossene Punkte aus früheren Reviews

1. **Markdown-Quellpfad und Artefaktquelle:** geschlossen. `HelpDocumentPathResolver.GetHelpSourcePath()` verwendet `../Docs/help`; Build-Target und Manifest verwenden dieselbe Quelle.
2. **Unbekannte statische Help-Dateien:** geschlossen. `IsStaticHelpAssetPath` erfasst jede Datei mit Endung unter `/help`; nicht gelistete Dateien werden vor der statischen Auslieferung mit `404` blockiert.
3. **Integritätscache:** geschlossen. Der Validator berechnet den Hash bei jedem Aufruf neu und akzeptiert keine nachträglich veränderte Datei.
4. **Relative interne Help-Links:** geschlossen. Links wie `beschreibung.md` und `index.md` werden relativ zum aktuellen Dokument in `/help/view/...` umgeschrieben; dafür existieren Renderer- und Integrationstests.
5. **Help-CSP gegen globale App-Ausgabe:** im HTML-Test geschlossen. Auf Help-Flächen werden ImportMap und `_framework/blazor.web.js` nicht gerendert; ein echter Browsertest bleibt offen.

## Offene Aufgaben

1. **Manifest-Integrität vollständig über HTTP nachweisen (mittel):** Es fehlt ein Integrationstest, der fehlendes Manifest sowie manipulierte Markdown-, JSON-, CSS- und JS-Dateien über echte Requests prüft. Zusätzlich fehlt ein Test, der nach einem Build Vollständigkeit und Konsistenz des erzeugten Manifests mit allen tatsächlich ausgelieferten Dateien verifiziert.
2. **API- und Renderer-Abdeckung vervollständigen (mittel):** Die Unit-Abdeckung ist erweitert, aber die drei API-Pfade sind nicht durchgängig mit dem echten Integritätsvalidator und echten HTTP-Responses gegen manipulierte Inhalte getestet. Noch nicht vollständig nachgewiesen sind insbesondere verschachtelte Payloads über die API, mehrere Pflichtfeld-/Grenzfälle und die vollständige Kombination aus Codeblock-, Tabellen- und Linkfällen.
3. **Browser-/beidsprachige Nachweise ergänzen (mittel):** Es gibt keinen E2E-Test, der `/help` und `/help/view/...` im Browser unter der CSP lädt und Suche, interne Links, externe Links, manipulierte Indexwerte sowie CSP-Verletzungen prüft. Eine dokumentierte Prüfung der deutschen und englischen Help-Ansicht fehlt.
4. **Abhängigkeiten bewerten (mittel):** Die fokussierten Läufe melden weiterhin `NU1608` für `HtmlSanitizer`/`AngleSharp`, `NU1902` für AngleSharp und `NU1903` für SQLitePCLRaw. Die AngleSharp-/HtmlSanitizer-Kombination ist unmittelbar für die Sanitization relevant und muss vor Abschluss bewertet bzw. auf eine kompatible, sicher unterstützte Version gebracht werden; die SQLite-Warnung ist ein bestehendes, nicht feature-spezifisches Risiko.

## Verifikation

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests|FullyQualifiedName~HelpAssetIntegrityValidatorTests"`: 25 Tests bestanden.
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 11 Tests bestanden.
- Beide Läufe erfolgreich, jedoch mit den oben genannten Paketwarnungen. Ein Browser-/E2E-Lauf wurde für diese Review-Iteration nicht ausgeführt.

