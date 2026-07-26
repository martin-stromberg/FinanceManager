← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Technischer Ablauf

## Übersicht

Die responsive Darstellung wird in der Web-Schicht über Layout-Markup und CSS-Media-Queries umgesetzt.  
Listen-, Karten- und Berichtskomponenten behalten ihre bestehende Fachlogik, erhalten aber mobile Container- und Umbruchregeln.  
Die mobile Funktionsfähigkeit wird durch zusätzliche Playwright-E2E-Szenarien mit expliziten Mobile-Session-Optionen abgesichert.

## Ablauf

### 1. Einstieg und Layout-Umschaltung

`MainLayout` rendert neben Sidebar und Content eine mobile Topbar und ein Overlay für Navigation auf kleinen Viewports.

Beteiligte Komponenten:
- `MainLayout.UpdateLogo(string uri)` — setzt Logo und Full-Width-Verhalten abhängig von der Route.
- `MainLayout.HandleLocationChanged(...)` — aktualisiert Layout-Zustand bei Navigation.
- `FinanceManager.Web/wwwroot/css/app.css` — aktiviert mobile Layoutregeln über `@media (max-width: 900px)`.

### 2. Responsives Rendering von Listen und Karten

Listen- und Kartenkomponenten verwenden responsive Wrapper für Tabellen, Eingaben und eingebettete Inhalte.

Beteiligte Komponenten:
- `GenericListPage<TItem>` — rendert Liste in `.table-responsive.generic-list-table-wrap`.
- `GenericCardPage<TKeyValue>.RenderEditableField(CardField)` — rendert bearbeitbare Felder mit mobilem Kartenlayout.
- `ListPage` / `CardPage` — hosten Ribbon, Status/Overlay und die generischen Komponenten.

### 3. Mobile Ribbon-Shortcuts

`Ribbon` rendert geschlossene mobile Gruppen mit separatem Toggle-Button und optionalen Shortcut-Buttons im Header. Ein Shortcut entsteht entweder aus `UiRibbonAction.MobileShortcut` oder automatisch, wenn eine Gruppe genau eine sichtbare Aktion ohne `FileCallback` enthält. Versteckte Aktionen werden nicht berücksichtigt, deaktivierte Aktionen bleiben als deaktivierte Shortcuts sichtbar.

Die Shortcut-Buttons nutzen den bestehenden Aktionspfad des Ribbon-Eintrags und stoppen die Klick-Weitergabe, damit der Gruppentoggle nicht ausgelöst wird. Geöffnete mobile Gruppen rendern keine Header-Shortcuts; Dateiaktionen mit `FileCallback` werden nicht als mobile Shortcuts angezeigt.

Beteiligte Komponenten:
- `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` — transportiert die Shortcut-Markierung über `UiRibbonAction.MobileShortcut`.
- `FinanceManager.Web/Components/Shared/Ribbon.razor` — filtert sichtbare Aktionen, wendet die Ein-Aktions-Regel an und rendert die mobilen Header-Shortcuts.
- `FinanceManager.Web/wwwroot/css/ribbon.css` — begrenzt und positioniert Titel, Toggle und Icon-Shortcuts im mobilen Header.

### 4. Seitenbezogene Mobile-Regeln

Seiten mit komplexen Inhalten erhalten ergänzende Styles für Mobile-Breakpoints.

Beteiligte Komponenten:
- `Home`, `ReportDashboard`, `ReportsHome`, `BudgetReport`, `SetupSections`, `SecurityPerformancePage`.
- CSS-Dateien wie `app.Home.css`, `app.ReportDashboard.css`, `app.ReportsHome.css`, `app.BudgetReport.css`, `app.Setup.css`, `app.ReturnAnalysis.css`.
- Dark-Theme-Pendants: `theme.Dark.*.css`.

### 5. Mobile E2E-Ausführung

Tests erzeugen mobile Browserkontexte und führen bestehende End-to-End-Flows unter Mobile-Bedingungen aus.

Beteiligte Komponenten:
- `PlaywrightWebAppFixture.PlaywrightSessionOptions` — enthält `ViewportSize`, `IsMobile`, `HasTouch`.
- `PlaywrightWebAppFixture.CreateMobileSessionAsync()` — startet Sessions mit `390x844`, Touch und Mobile-Flag.
- Testmethoden mit Suffix `_OnMobileViewport` in `AuthenticationFlowPlaywrightTests`, `ListNavigationPlaywrightTests`, `ReportingFlowPlaywrightTests`, `HomeMassImportPlaywrightTests`.

## Fehlerbehandlung

- Bei nicht initialisiertem Browserkontext wirft `PlaywrightWebAppFixture.CreateSessionAsync(...)` eine `InvalidOperationException`.
- In UI-Komponenten bleiben bestehende Fallbacks aktiv (z. B. Laden/Leerzustände und defensive `try/catch`-Abschnitte bei JS-Interop).
- Für die responsive Darstellung wurden keine neuen fachlichen Fehlercodes oder API-Fehlerpfade eingeführt.

