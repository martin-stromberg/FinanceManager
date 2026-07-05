### Fachliche Zusammenfassung
Die Anforderung erweitert die bestehende Web-UI um eine durchgängige Responsive-Auslegung für kleine Viewports. Auslöser ist der Aufruf beliebiger Seiten auf mobilen Endgeräten; Ergebnis ist eine nutzbare, ohne horizontales Scrollen bedienbare Darstellung aller Kernfunktionen. Bestehende Layout- und Tabellenansichten werden dabei für Touch-Bedienung, reduzierte Breiten und mobile Navigation angepasst. Die Umsetzung betrifft primär die Präsentationsschicht in `FinanceManager.Web`.

### Betroffene Klassen und Komponenten
- **Datenmodellklassen**
  - Keine Änderungen an Domain- oder DTO-Modellen aus der Anforderung ableitbar.
- **Logikklassen / Services**
  - Primär keine neue Backend-Logik erforderlich; Fokus liegt auf UI-Layout und Darstellung.
  - Ggf. Anpassungen an bestehenden ViewModels nur, falls mobile Darstellung andere Datenaggregation benötigt (Annahme).
- **Interfaces**
  - Kein neues Interface zwingend aus der Anforderung ableitbar.
- **Enums**
  - Kein neues Enum fachlich erforderlich.
- **UI-Komponenten / Controller (falls zutreffend)**
  - Layout/Navigation: `MainLayout.razor`, `App.razor`, `ribbon.css`, `app.css`, `theme.Dark.css`.
  - Generische Seitenbausteine: `ListPage.razor`, `GenericListPage.razor`, `CardPage.razor`, `GenericCardPage.razor`.
  - Inhaltliche Seiten (repräsentative, voraussichtlich betroffene Hauptseiten): `Home.razor`, `ReportDashboard.razor`, `BudgetReport.razor`, `ReportsHome.razor`, `Login.razor`, `Register.razor`, `Legal.razor`, `HelpHub.razor`, `SetupSections.razor` sowie Tabs unter `Components/Pages/Setup/` und `Components/Pages/Securities/`.
  - Stylesheets: `wwwroot/css/app.*.css` und entsprechende `wwwroot/css/theme.Dark.*.css` Dateien.
- **Tests**
  - Erweiterung der E2E-Abdeckung in `FinanceManager.Tests.E2E`, insbesondere bestehende Flows wie `AuthenticationFlowPlaywrightTests`, `ListNavigationPlaywrightTests`, `ReportingFlowPlaywrightTests`, `HomeMassImportPlaywrightTests` um mobile Viewport-Varianten.
  - Ggf. Erweiterung der Browser-Session-Erzeugung in `PlaywrightWebAppFixture` für standardisierte mobile Viewports (Annahme).

### Implementierungsansatz
Technischer Schwerpunkt ist die Vereinheitlichung eines Responsive-Patterns auf Basis bestehender CSS-Layer (`app.css`, `app.*.css`, `theme.Dark*.css`) und der bereits vorhandenen mobilen Navigation in `MainLayout.razor`. Relevante Erweiterungspunkte sind bestehende Tabellen- und Formularlayouts (`GenericListPage.razor`, `GenericCardPage.razor`, seitenbezogene Komponenten), damit Inhalte auf kleinen Displays umbrechen, skalieren oder als horizontal scrollbare Container dargestellt werden. Bestehende Mechanismen wie `.table-responsive`, `flex-wrap`, Grid-/Spacing-Regeln und Media Queries werden projektweit konsistent angewendet und pro Seite ergänzt, statt neue UI-Architektur einzuführen.

### Konfiguration
Für die Fachanforderung ist keine benutzer- oder datensatzspezifische Konfiguration erkennbar. Sinnvoll ist eine zentrale technische Konfiguration der Breakpoints auf Styling-Ebene (z. B. einheitliche `@media`-Grenzen in den globalen CSS-Dateien), damit das Verhalten projektweit konsistent bleibt.

### Offene Fragen
- Welche Ziel-Viewportbreiten gelten als „kleine Bildschirme“ (z. B. ≤`576px`, ≤`768px`, anderes Kriterium)?
- Müssen ausschließlich Smartphones unterstützt werden oder auch kleine Tablets/Querformat?
- Was ist die verbindliche Definition von „optimiert“ (kein horizontaler Scroll, Mindest-Tap-Targets, Performance, visuelle Gleichwertigkeit)?
- Gibt es Seiten mit fachlich priorisierter Mobilnutzung, die zuerst umgesetzt werden sollen?
- Soll für alle Tabellen eine einheitliche Mobilstrategie gelten (Stacking, Spaltenausblendung, horizontaler Scroll) oder je Seite individuell?
- Sind zusätzliche mobile Akzeptanztests in CI verpflichtend (merge-blockierend) oder zunächst optional?
- Ist das Fehlen einer zentralen `docs/features.md` im Repository beabsichtigt, oder gibt es eine alternative Referenz für Feature-Konventionen?
