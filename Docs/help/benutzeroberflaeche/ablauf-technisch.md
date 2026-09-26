← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Technischer Ablauf

## Übersicht

Die responsive Darstellung wird in der Web-Schicht über Layout-Markup und CSS-Media-Queries umgesetzt.  
Listen-, Karten- und Berichtskomponenten behalten ihre bestehende Fachlogik, erhalten aber mobile Container- und Umbruchregeln.  
Die mobile Funktionsfähigkeit wird durch zusätzliche Playwright-E2E-Szenarien mit expliziten Mobile-Session-Optionen abgesichert.  
Bestätigungsdialoge rendern auf einer eigenen Schicht oberhalb aller Overlay-Container, damit sie unabhängig von der DOM-Reihenfolge der Hosts sichtbar und bedienbar bleiben.

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

`Ribbon` rendert geschlossene mobile Gruppen mit separatem Toggle-Button und optionalen Shortcut-Buttons im Header. Ein Shortcut entsteht entweder aus `UiRibbonAction.MobileShortcut` oder automatisch, wenn eine Gruppe genau eine sichtbare und aktivierte Aktion enthält. Versteckte oder deaktivierte Aktionen werden nicht als Header-Shortcuts berücksichtigt.

Die Shortcut-Buttons nutzen den bestehenden Aktionspfad des Ribbon-Eintrags und stoppen die Klick-Weitergabe, damit der Gruppentoggle nicht ausgelöst wird. Geöffnete mobile Gruppen rendern keine Header-Shortcuts; Dateiaktionen mit `FileCallback` behalten ihr Upload-Overlay auch im kompakten Shortcut.

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

### 6. Globale Ladeleiste

Die globale Ladeleiste wird als wiederverwendbare Blazor-Komponente aus `msTools.Web.Blazor` im Dokumentrahmen gerendert. FinanceManager konfiguriert Farben, Höhe, Z-Index und mobile Positionierung hostseitig über `AddLoadingBar(...)`. Native Klick- und Submit-Listener in `financeManager.js` erkennen relevante interne Navigationen und Formularvorgänge frühzeitig; die JavaScript-Schicht liest die Farbpalette aus der gerenderten Komponente und definiert keine projektspezifischen Farben mehr. `MainLayout` startet die Leiste zusätzlich über `RegisterLocationChangingHandler` und beendet sie über `LocationChanged`; Validierungs- und Abschlussfälle stoppen sie ebenfalls über die JavaScript-Schnittstelle. Länger laufende Blazor-Aktionen werden über `LoadingBarService.RunAsync(...)` zentral umschlossen, damit auch komponenteninterne Ladevorgänge wie der Budgetbericht dieselbe globale Anzeige nutzen.

Beteiligte Komponenten:
- `msTools.Web.Blazor/LoadingBar.razor` — rendert die eindeutige Ladeleisten-Instanz als wiederverwendbare Komponente.
- `msTools.Web.Blazor/LoadingBarService.cs` — kapselt Start/Stop für Blazor-seitige async-Aktionen.
- `msTools.Web.Blazor/LoadingBarOptions.cs` — definiert hostseitige Darstellungseinstellungen wie Farben, Höhe, Z-Index und mobile Positionierung.
- `FinanceManager.Web/Components/App.razor` — bindet die Komponente und das globale Skript ein.
- `FinanceManager.Web/Components/Layout/MainLayout.razor` — startet und beendet die Leiste beim Blazor-Navigationszyklus.
- `FinanceManager.Web/Components/Shared/Ribbon.razor` — führt Ribbon-Aktionen über die zentrale Ladeleisten-Abstraktion aus.
- `FinanceManager.Web/ProgramExtensions.cs` — registriert `AddLoadingBar(...)` mit der FinanceManager-Farbpalette.
- `FinanceManager.Web/wwwroot/js/financeManager.js` — stellt `financeManager.loadingBar.start`, `restart` und `stop` bereit, erkennt globale Browserereignisse und verhindert doppelte Listener.

### 7. Bestätigungsdialog oberhalb von Overlays

Overlay-Container (z. B. `OverlayHost`, das Listen-Overlay in `ListPage`, die `.modal-overlay`-/`.modal-backdrop`-Dialoge) und der Bestätigungsdialog teilen die Grundklasse `.split-center` mit `z-index: 1000`. Damit die Bestätigung unabhängig von der DOM-Reihenfolge der Hosts immer im Vordergrund liegt, trägt der äußere Container von `ConfirmDialog` zusätzlich die Klasse `confirm-dialog-layer` mit `z-index: 1100`. Die Schichtung gilt seitenübergreifend (`CardPage`, `ListPage`, `Home`, `ReportDashboard`), ohne dass die Host-Reihenfolge pro Seite gepflegt werden muss.

Ablauf am Beispiel „Anhang im geöffneten Anhangs-Overlay löschen":

1. `AttachmentsPanel.DeleteAsync` ruft `IConfirmationService.ConfirmAsync` mit `ConfirmationSeverity.Critical` auf.
2. `ConfirmationService.GetShowConfirmationsAsync` liest die Benutzereinstellung `ShowConfirmations` über `Api.UserSettings_GetProfileAsync` (pro Scope gecacht, Standard `true`). Ist sie deaktiviert, kehrt `ConfirmAsync` sofort mit `true` zurück und es wird kein Dialog gerendert. `SetupProfileViewModel.SaveAsync` ruft `ConfirmationService.InvalidateCacheAsync`, sobald die Einstellung geändert wurde.
3. Bei aktivierten Bestätigungen setzt der Service `CurrentRequest` und löst `OnChanged` aus; `ConfirmationDialogHost` rendert daraufhin `ConfirmDialog`.
4. Der Dialog-Backdrop `.split-center.confirm-dialog-layer` (`z-index: 1100`) liegt über allen Overlay-Schichten (`.split-center`, `.modal-overlay`, `.modal-backdrop` = 1000; `.rsw-backdrop`/Panel = 900/901; Sticky-Header = 500) und deckt den gesamten Viewport ab.
5. `ConfirmDialog.OnConfirm` → `ConfirmationService.SetResult(true)` → `ConfirmAsync` kehrt mit `true` zurück → `AttachmentsPanel.DeleteAsync` ruft `Api.Attachments_DeleteAsync` auf und lädt die Liste neu; das Overlay bleibt geöffnet.
6. `OnCancel` (Abbrechen-/Schließen-Schaltfläche) oder `OnBackdropClick` → `ConfirmationService.Cancel()` bricht nur die Bestätigung ab. Da die `confirm-dialog-layer` den kompletten Viewport abdeckt, erreicht der Klick den Overlay-Backdrop nicht — ein versehentliches Schließen des Overlays ist ausgeschlossen.

Beteiligte Komponenten:
- `FinanceManager.Web/Components/Shared/ConfirmDialog.razor` — rendert den Backdrop `.split-center.confirm-dialog-layer` mit `@onclick="OnBackdropClick"` und den Dialog `.split-dialog.confirm-dialog` mit `@onclick:stopPropagation`.
- `FinanceManager.Web/Components/Shared/ConfirmationDialogHost.razor` — blendet `ConfirmDialog` ein, solange `ConfirmationService.CurrentRequest != null`.
- `FinanceManager.Web/Services/ConfirmationService.cs` — `ConfirmAsync`, `SetResult`, `Cancel`, `CurrentRequest`, `OnChanged`, `InvalidateCacheAsync`.
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` und `FinanceManager.Web/Components/Pages/ListPage.razor` — rendern die Overlay-Container auf der Basisebene (`z-index: 1000`).
- `FinanceManager.Web/wwwroot/css/app.css` — `.split-center` (`z-index: 1000`) sowie `.split-center.confirm-dialog-layer` (`z-index: 1100`).
- `FinanceManager.Web/wwwroot/css/theme.Dark.css` — definiert nur die Backdrop-Farbe für `.split-center`, keinen eigenen `z-index`; die Schichtung gilt daher auch im dunklen Theme.

> **Hinweis für neue Dialog- oder Overlay-Ebenen:** Künftige Overlay-Container müssen unterhalb von `z-index: 1100` bleiben. Eine weitere Ebene oberhalb der Bestätigung sollte nur eingeführt werden, wenn ein zweiter Dialogtyp dies fachlich erfordert.

## Fehlerbehandlung

- Schlägt ein geschützter API-Aufruf mit `401 Unauthorized` fehl, veröffentlicht `ApiClient` ein zentrales Authentifizierungssignal. `AuthRedirect` behandelt dieses Signal unabhängig vom aufrufenden Seitenbaustein und leitet einmalig auf `/login` weiter.
- Vor der Weiterleitung wird aus der aktuellen Navigation ein relatives internes Ziel mit Pfad, Querystring und Fragment gebildet und als URL-kodierter `returnUrl` an die Login-Seite übergeben. Öffentliche Routen, API-Routen, absolute oder externe Ziele sowie `/login`, `/register` und `/error` werden nicht als Rückkehrziel verwendet.
- Nach erfolgreicher Anmeldung validiert `Login` den optionalen `returnUrl` und navigiert genau einmal zu diesem Ziel. Fehlt das Ziel oder wird es abgelehnt, erfolgt die Navigation zu `/`. Die Validierung verhindert damit auch eine externe Weiterleitung.
- `403 Forbidden` führt nur bei einem ausdrücklich als Authentifizierungsfehler gekennzeichneten API-Signal zur Login-Weiterleitung. Gewöhnliche Fach- oder Berechtigungsfehler bleiben im normalen Fehlerpfad.
- Bei nicht initialisiertem Browserkontext wirft `PlaywrightWebAppFixture.CreateSessionAsync(...)` eine `InvalidOperationException`.
- In UI-Komponenten bleiben bestehende Fallbacks aktiv (z. B. Laden/Leerzustände und defensive `try/catch`-Abschnitte bei JS-Interop).
- Für die responsive Darstellung wurden keine neuen fachlichen Fehlercodes eingeführt; der Authentifizierungsfehlerpfad wird zentral für geschützte API-Aufrufe verwendet.

