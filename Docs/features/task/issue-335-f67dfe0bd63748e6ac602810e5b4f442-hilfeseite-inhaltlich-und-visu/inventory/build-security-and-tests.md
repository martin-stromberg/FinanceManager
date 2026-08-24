# Build, Sicherheit und Tests

## Build-Vertrag

`FinanceManager.Web.csproj` erzeugt Suchindizes vor Static-Web-Asset-Aufloesung und Publish. Die Quelle ist `../Docs/help`; generierte JSON-Dateien und `help-assets.sha256` werden als Help-Assets veroeffentlicht. Die Build-Vertraege pruefen damit insbesondere die Vollstaendigkeit und Hashes ausgelieferter Help-Dateien.

## Laufzeitschutz

`HelpAssetIntegrityValidator` wird im Hub und in der Detailseite verwendet. `HelpContentRenderer` entfernt Frontmatter, deaktiviert HTML im Markdown-Pipeline-Schritt, sanitiziert erlaubte HTML-Tags und begrenzt Links. `HelpDocumentPathResolver` validiert die Route gegen erlaubte Segmente.

Diese Schutzmechanismen adressieren Integritaet, Traversal und HTML-Inhalte. Sie definieren jedoch nicht, ob ein Markdown-Dokument fuer Endanwender geeignet ist. Die fachliche Allowlist fuer Help-Inhalte muss daher separat bleiben und die bestehende Asset-Sicherheitslogik ergaenzen.

## Vorhandene Tests

- `HelpContentRendererTests` pruefen Frontmatter, Markdown-Rendering, interne/externe Links und Sanitizing.
- `HelpAssetIntegrityValidatorTests` pruefen Manifestpflicht, Hashabweichungen, Docs-help-Vertrauen, Build-Manifest und Suchindex-Generierung.
- `HelpControllerSecurityTests` sowie `HelpSecurityMiddlewareTests` decken Controller- und Middleware-Sicherheit ab.
- `HelpPagePlaywrightTests.HelpHub_ShouldShowDocumentationContent` prueft, dass `/help` Inhalte und das Thema Konten und Buchungen anzeigt und keine Lade-/Fehlermeldung erscheint.

## Abdeckungsluecke fuer Issue 335

Es gibt nach dem aktuellen Bestand keinen Test, der explizit sicherstellt, dass ein technisches-only Dokument nicht im Anwenderkatalog, Suchindex oder ueber eine Detailroute erscheint. Ebenso fehlt ein E2E-Test fuer die Navigation von Uebersicht zu freigegebenem Detailinhalt und zurueck sowie fuer die responsive Darstellung. Diese Tests sollten bei der Implementierung gezielt ergaenzt werden.
