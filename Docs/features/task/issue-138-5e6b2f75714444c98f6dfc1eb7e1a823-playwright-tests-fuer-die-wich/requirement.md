### Fachliche Zusammenfassung
Die Anforderung erweitert die bestehende Teststrategie um browserbasierte End-to-End-Regressionstests mit `Playwright` für die fachlich kritischen Nutzerabläufe. Auslöser ist jede Weiterentwicklung der Anwendung (insbesondere vor Merge/Release), Ergebnis ist ein automatisierter Nachweis, dass zentrale Funktionen weiterhin nutzbar sind. Ziel ist die frühzeitige Erkennung funktionaler Regressionen auf UI- und Ablaufebene, die durch Unit- oder API-Tests allein nicht vollständig abgedeckt werden.

### Betroffene Klassen und Komponenten
- **Datenmodellklassen**
  - Keine Änderungen an produktiven Domain-Entitäten aus der Anforderung ableitbar.
  - Voraussichtlich neue Testdaten-/Fixture-Artefakte im Testkontext, z. B. `PlaywrightTestData` (Annahme).
- **Logikklassen / Services**
  - Voraussichtlich neue Testinfrastruktur für Browsertests, z. B. `PlaywrightTestFixture` oder `PlaywrightTestBase` (Annahme).
  - Wahrscheinlich Wiederverwendung/Erweiterung von `TestWebApplicationFactory` zur deterministischen Testumgebung.
- **Interfaces**
  - Kein zwingend neues Interface aus der Anforderung ableitbar; ggf. optionales Abstraktionsinterface für Test-Setup (Annahme).
- **Enums**
  - Kein neues Enum fachlich erforderlich.
- **UI-Komponenten / Controller (falls zutreffend)**
  - Betroffene Zielkomponenten der Ablauftests voraussichtlich u. a. `Login.razor`, `Register.razor`, `Home.razor`, `ListPage.razor`, `ReportDashboard.razor` sowie import-/buchungsnahe Seiten (je nach finaler Scope-Definition).
  - Indirekt betroffen: zugehörige API-Endpunkte/Controller, die in den User Journeys aufgerufen werden.
- **Tests**
  - Neue E2E-Testklassen für Kernabläufe, z. B. `AuthenticationFlowPlaywrightTests`, `StatementImportFlowPlaywrightTests`, `PostingFlowPlaywrightTests`, `ReportingFlowPlaywrightTests` (Annahme).
  - Pipeline-/Testausführungsintegration für die neue Testart.

### Implementierungsansatz
Die Umsetzung erfolgt über eine zusätzliche E2E-Testschicht mit `Playwright`, die vollständige Nutzerreisen über die laufende Webanwendung ausführt und fachliche Erfolgszustände verifiziert. Als technische Erweiterungspunkte sind die bestehende Testinfrastruktur (`FinanceManager.Tests.Integration`, `TestWebApplicationFactory`) sowie die CI-Testausführung relevant; produktiver Anwendungscode wird nur dann angepasst, wenn stabile Testanker (z. B. `data-testid`) fehlen. Abhängigkeiten bestehen insbesondere zu Authentifizierung, Stammdatenanlage, Import-/Buchungsabläufen und Reporting, da diese als kritische Regressionstreiber gelten.

### Konfiguration
Konfigurierbarkeit sollte primär auf Test-/Pipeline-Ebene liegen: Auswahl der „kritischen Flows“, Browsermodus (`headless`/`headed`), Ziel-URL/Testhost und Ausführungsprofil (Smoke vs. Full Regression). Eine produktive Laufzeitkonfiguration in `FinanceManager.Web` ist aus der Anforderung nicht zwingend erforderlich.

### Offene Fragen
- Welche konkreten Abläufe gelten als „wichtigste Programmabläufe“ (priorisierte, verbindliche Liste)?
- Soll der Umfang initial als Smoke-Set (wenige Kernpfade) oder als breitere Full-Regression umgesetzt werden?
- Sollen die `Playwright`-Tests Merge-blockierend in CI laufen oder zunächst optional/nächtlich?
- Welche Browser/Plattformen sind verpflichtend (`Chromium` only oder Cross-Browser)?
- Soll die Umsetzung in `FinanceManager.Tests.Integration` erfolgen oder als separates Testprojekt (z. B. `FinanceManager.Tests.E2E`) angelegt werden?
- Dürfen UI-Komponenten gezielt um stabile Selektoren (z. B. `data-testid`) erweitert werden?
- Soll für E2E ein dedizierter Seed-/Testdatenbestand definiert werden, oder wird vollständig über UI/API im Test angelegt?
- Ist das Fehlen von `docs/features.md` im Repository beabsichtigt, oder existiert eine alternative zentrale Feature-Übersicht?
