# Bestandsaufnahme: Mobile Ansicht (Responsive Web-UI)

Analysiert wurde die bestehende Web-Oberfläche in `FinanceManager.Web` sowie die vorhandene E2E-Testabdeckung in `FinanceManager.Tests.E2E` zur Anforderung einer mobilen, responsiven Darstellung.

## Zusammenfassung

- Es gibt bereits eine mobile Navigationsbasis über `MainLayout.razor` + globale Styles in `app.css` (`.mobile-topbar`, `.nav-toggle`, `@media (max-width: 900px)`).
- Das globale Dokument enthält bereits den mobilen Viewport-Meta-Tag (`<meta name="viewport" ...>` in `App.razor`).
- Mehrere UI-Bereiche nutzen bereits responsive Mechanismen wie `flex-wrap`, `grid` und horizontale Scroll-Container (`.table-responsive`, `overflow-x: auto`).
- Seiten und Tabs für Berichte, Setup und Securities sind bereits komponentisiert und enthalten umfangreiche Darstellungslogik, aber ohne einheitlich durchgängiges Mobile-Pattern über alle Seiten.
- In den E2E-Tests existieren die genannten Flows, aber keine standardisierte mobile Viewport-Session im `PlaywrightWebAppFixture` (aktuell Standard-Context ohne mobile Device-Parameter).
- Für die Anforderung wurden keine relevanten Domain-/DTO-Datenmodelle, keine neuen fachlichen Interfaces und keine zentralen Enums speziell für „mobile Ansicht“ im Bestand gefunden.

## Details

- [Logik](inventory/logic.md)
- [Tests](inventory/tests.md)
