# Bestandsaufnahme: Playwright-Tests für die wichtigsten Programmabläufe

Analysiert wurden die bestehenden UI-nahen Abläufe, ViewModels, API-Contracts und Tests rund um Authentifizierung, Stammdaten, Import/Buchung und Reporting. Fokus war ausschließlich auf bereits vorhandenem Code als Ausgangsbasis für browserbasierte E2E-Regressionen.

## Zusammenfassung

- Es gibt aktuell **keine** Playwright-spezifischen Artefakte (kein `Playwright`-Package, kein E2E-Projekt, keine `.spec.ts`/`playwright`-Dateien).
- Die Web-UI enthält die in der Anforderung genannten Seiten (`Login`, `Register`, `Home`, `ListPage`, `ReportDashboard`) inklusive bestehender Ablauf-Logik.
- Für Authentifizierung existiert bereits ein browsernaher Mechanismus über JavaScript-Funktionen `fmAuthLogin`/`fmAuthLogout` (`wwwroot/auth.js`), aufgerufen aus den Razor-Komponenten.
- Für Import/Buchung und Reporting existieren umfangreiche DTOs, ViewModel-Logik und API-Contract-Methoden (`IApiClient`).
- Die vorhandene Testbasis deckt zentrale Flows bereits über Integrationstests (API-Endpunkte mit `TestWebApplicationFactory`) und ViewModel-Tests (xUnit/Moq) ab.
- `TestWebApplicationFactory` stellt eine deterministische In-Memory-SQLite-Testumgebung inkl. Bootstrap-Admin und deaktivierten Background-Workern bereit.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
