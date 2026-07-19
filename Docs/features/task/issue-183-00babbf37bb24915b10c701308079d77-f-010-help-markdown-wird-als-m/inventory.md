# Bestandsaufnahme: Help-Markdown wird als MarkupString gerendert

Analysiert wurden die Help-API, die Blazor-Help-Ansicht, der clientseitige Suchindex sowie die statische Auslieferung und Build-Einbindung der Help-Dateien bezogen auf die Anforderungen an sichere HTML-Ausgabe und CSP.

## Zusammenfassung

- `HelpController` liest Markdown-Dateien und den JSON-Suchindex aus dem Dateisystem und gibt beide Inhalte ohne Sanitization zurück.
- `HelpPageView` rendert das API-Ergebnis über `MarkupString` und eine eigene Regex-Konvertierung; HTML- und Script-Inhalte werden dabei nicht vorab escaped oder per Whitelist geprüft.
- `help-search.js` schreibt Indexwerte wie `title`, `excerpt` und `id` über `innerHTML` bzw. ein interpoliertes `onclick` in das DOM.
- Statische Help-Dateien werden über `UseStaticFiles()` ausgeliefert; eine Help-spezifische CSP oder ein anderer CSP-Mechanismus ist im geprüften Middleware-Code nicht vorhanden.
- Die Help-Dateien unter `FinanceManager.Web/wwwroot/help` werden per `FinanceManager.Web.csproj` mit `CopyToOutputDirectory="PreserveNewest"` in die Ausgabe übernommen.
- Es existieren keine gefundenen Help-spezifischen Unit-, Integrations- oder E2E-Tests sowie kein dedizierter Sanitizer-, Renderer-, Datenmodell-, Enum- oder Interface-Typ.

## Details

- [Logik und Ausgabewege](inventory/logic.md)
- [Help-Dateien und Build-/Static-File-Einbindung](inventory/assets.md)
- [Tests und Test-Hilfsmethoden](inventory/tests.md)

