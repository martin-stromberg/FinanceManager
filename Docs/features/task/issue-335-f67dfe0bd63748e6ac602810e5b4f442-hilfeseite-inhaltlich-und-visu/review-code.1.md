# Code-Review: Hilfeseite inhaltlich und visuell ueberarbeiten

Status: Befunde vorhanden

## Befunde

1. Hoch - Freigegebene Primaerdokumente enthalten weiterhin technische Anwender-ungeeignete Inhalte.

   Der neue Katalog veroeffentlicht mehrere `beschreibung.md`-Dateien als primaere sichtbare Hilfe, z. B. `berichtswesen`, `konten-und-buchungen`, `systemverwaltung-und-setup`, `updates` und `wertpapiermanagement` in `FinanceManager.Web/Services/Help/HelpContentCatalog.cs:73`, `FinanceManager.Web/Services/Help/HelpContentCatalog.cs:76`, `FinanceManager.Web/Services/Help/HelpContentCatalog.cs:80`, `FinanceManager.Web/Services/Help/HelpContentCatalog.cs:81` und `FinanceManager.Web/Services/Help/HelpContentCatalog.cs:82`. Diese Dateien enthalten aber weiterhin technische Implementierungsdetails: `Docs/help/berichtswesen/beschreibung.md:11` nennt `ReportsController` und `HomeKpisController`, `Docs/help/konten-und-buchungen/beschreibung.md:11` nennt `AccountsController` und `PostingsController`, `Docs/help/systemverwaltung-und-setup/beschreibung.md:11` listet interne Controller und API-Abdeckung, `Docs/help/systemverwaltung-und-setup/beschreibung.md:102` enthaelt den Abschnitt `Technische Umsetzung`, `Docs/help/updates/beschreibung.md:58` beschreibt technische Konfigurationswerte, und `Docs/help/wertpapiermanagement/beschreibung.md:11` nennt Controller und API-Endpunkte.

   Damit erfuellt `/help` zwar die neue Dateityp-Filterung, zeigt aber weiterhin genau die technischen Informationen, die laut Anforderung aus der sichtbaren Anwenderhilfe herausgehalten werden sollen. Das ist ein direktes Akzeptanzkriterium: `/help` darf nur anwendergeeignete Inhalte anzeigen. Die aktuellen Tests decken diesen Fall nicht ausreichend ab: `FinanceManager.Tests/Web/Help/HelpContentCatalogTests.cs:70` prueft nur Dateinamen gegen `TechnicalOnlyDocumentNames`, und `FinanceManager.Tests.E2E/Tests/Help/HelpPagePlaywrightTests.cs:45`/`:46` pruefen auf `API` und `Datenmodell` nur im Detailtext von `Konten und Buchungen`; dort wuerde z. B. `AccountsController` weiterhin unentdeckt bleiben.

   Empfehlung: Die veroeffentlichten Markdown-Dateien redaktionell bereinigen oder dedizierte UI-Hilfedokumente einfuehren und im Katalog referenzieren. Zusaetzlich einen Test ergaenzen, der alle katalogisierten Dokumente rendert bzw. einliest und auf technische Marker wie `Controller`, `Endpunkt`, `API-seitig`, `appsettings`, `Jwt:` und `Technische Umsetzung` prueft.

## Keine weiteren Befunde

Die Katalogintegration fuer Hub, Detailroute und Search-Index wirkt aus Code-Sicht konsistent. Nicht katalogisierte Detailrouten werden serverseitig abgewiesen, und die Search-Index-Generierung laeuft ueber denselben Katalog.

## Tests

Nicht ausgefuehrt. Dieses Review basiert auf statischer Diff- und Inhaltspruefung.
