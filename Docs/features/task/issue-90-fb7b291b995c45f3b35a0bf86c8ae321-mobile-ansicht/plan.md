# Umsetzungsplan: Mobile Ansicht (Responsive Web-UI)

## Übersicht

Die Web-Oberfläche in `FinanceManager.Web` wird für kleine Viewports durchgängig responsiv gemacht, damit Kernabläufe auf Smartphones ohne horizontales Gesamtseiten-Scrollen nutzbar bleiben. Betroffen sind das globale Layout, generische Listen-/Kartenkomponenten sowie zentrale Seiten für Home, Reports, Setup und Securities. Zusätzlich wird die E2E-Suite um mobile Viewport-Varianten erweitert, damit die mobile Nutzbarkeit dauerhaft abgesichert ist.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Tabellen-/Listen-Darstellung auf mobilen Viewports | Einheitliches „Scroll-Container-first“-Muster in der Präsentationsschicht (responsive Wrapper + gezielte Spaltenumbruch-/Ausblendregeln je Tabelle) | Für bestehende datenreiche Tabellen ist horizontales Scrollen innerhalb des Tabellenbereichs der robusteste Minimal-Eingriff, während gezielte Spaltenregeln Lesbarkeit sichern. Das folgt vorhandenen Mustern wie `.table-responsive` statt kompletter UI-Neustrukturierung. |
| Responsive Steuerung über Stylesheets | Zentrale Breakpoint-Definition in globalen CSS-Dateien (`app.css` + passende `theme.Dark*.css`) und seitenbezogene Ergänzungen nur bei Bedarf | Verhindert abweichende Einzelregeln pro Seite und hält das Verhalten konsistent zwischen Light-/Dark-Theme. |
| Mobile E2E-Testausführung | Erweiterung des bestehenden `PlaywrightWebAppFixture` (Service-Layer-nahe Testinfrastruktur) um mobile Session-Erzeugung statt separatem Fixture-Zweig | Minimiert Duplikation in der Testinfrastruktur und erlaubt Wiederverwendung bestehender Flows mit alternativer Session-Erzeugung. |

## Programmabläufe

### Mobiles Rendering einer Liste/Karte

1. Der Benutzer öffnet eine Seite über Routing in `MainLayout.razor`.
2. `ListPage.razor` bzw. `CardPage.razor` lädt wie bisher den Provider/ViewModel-Status.
3. `GenericListPage.razor` bzw. `GenericCardPage.razor` rendert die Inhalte in responsive Containern (`table`-Bereiche, Formular-/Button-Gruppen, Overlay-Bereiche).
4. CSS-Media-Queries aus `app.css` und den seitenbezogenen `app.*.css`/`theme.Dark.*.css` greifen für kleine Viewports.
5. Inhalte umbrechen bzw. scrollen nur innerhalb dafür vorgesehener Container; globale Seite bleibt ohne unkontrolliertes horizontales Scrollen bedienbar.

Beteiligte Klassen/Komponenten: `MainLayout`, `ListPage`, `GenericListPage<TItem>`, `CardPage`, `GenericCardPage<TKeyValue>`, `app.css`, `app.*.css`, `theme.Dark*.css`

### Mobile Navigation und Aktionsleisten

1. Beim Seitenwechsel aktualisiert `MainLayout` weiterhin Navigation/Logo-Zustand (`HandleLocationChanged`, `UpdateLogo`).
2. Für kleine Viewports werden Topbar-/Ribbon-/Aktionsgruppen per CSS auf mehrzeilige oder gestapelte Darstellung umgestellt.
3. Interaktionen (Navigation, Filter öffnen, Speichern, Dialoge) bleiben logisch unverändert, erhalten aber touch-taugliche Abstände und Umbrüche.

Beteiligte Klassen/Komponenten: `MainLayout`, `ReportDashboard`, `ReportsHome`, `SetupSections`, `ribbon.css`, `theme.Dark.Ribbon.css`, `app.css`

### Mobile E2E-Absicherung

1. Ein E2E-Test fordert über `PlaywrightWebAppFixture` explizit eine mobile Browser-Session an.
2. Die bestehenden Flows (Auth, Navigation, Reporting, Import) laufen unverändert gegen denselben Server.
3. Assertions prüfen, dass zentrale Aktionen im mobilen Viewport erreichbar und ausführbar sind (Happy Path je geänderter Nutzerinteraktion).
4. Bestehende Desktop-Flows bleiben erhalten und werden nur dort angepasst, wo gemeinsame Helfer-Signaturen geändert werden.

Beteiligte Klassen/Komponenten: `PlaywrightWebAppFixture`, `AuthenticationFlowPlaywrightTests`, `ListNavigationPlaywrightTests`, `ReportingFlowPlaywrightTests`, `HomeMassImportPlaywrightTests`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `MainLayout` (Razor-Komponente)

- **Neue Eigenschaften:** Keine zwingend erforderlich (nur falls zusätzlicher UI-Status für mobile Navigation benötigt wird).
- **Neue Methoden:** Keine zwingend erforderlich.
- **Geänderte Methoden:** `UpdateLogo(string uri)` / Markup-Anteile — Layoutcontainer und Navigationsbereiche werden für kleine Breiten responsiv strukturiert.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `GenericListPage<TItem>` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine zwingend erforderlich.
- **Geänderte Methoden:** `OnParametersSet()`/Markup-Rendering — Tabellen- und Filterbereiche erhalten konsistente responsive Wrapper/Klassen; bestehende Lade- und Suchlogik bleibt unverändert.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `GenericCardPage<TKeyValue>` (Razor-Komponente)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine zwingend erforderlich.
- **Geänderte Methoden:** `RenderField(CardField)` und `RenderEditableField(CardField)` — Feldlayout, Inline-Aktionen und eingebettete Listen werden für kleine Viewports umgestellt.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `ListPage` / `CardPage` (Razor-Komponenten)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** Markup-nahe Bereiche (Titel-/Aktionsleisten, Overlay-Einbettung) werden für mobile Darstellung angepasst.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `Home`, `ReportDashboard`, `BudgetReport`, `ReportsHome`, `SetupSections` (Razor-Komponenten)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** Seiten-Markup und Containerklassen werden je Seite angepasst (KPI-Kacheln, Filter-/Favoritenbereiche, Dialogtrigger, Tab-Navigation).
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### Setup- und Securities-Tabs unter `Components/Pages/Setup/*` und `Components/Pages/Securities/*` (Razor-Komponenten)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Keine.
- **Geänderte Methoden:** Markup-Abschnitte mit Tabellen, Formularen und Button-Zeilen werden in responsive Muster überführt.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `app.css`, `app.*.css`, `theme.Dark*.css`, `ribbon.css`, `theme.Dark.Ribbon.css` (Stylesheets)

- **Neue Eigenschaften:** Neue/erweiterte CSS-Klassen für responsive Container, Umbrüche, Abstände und Touch-Flächen.
- **Neue Methoden:** Nicht zutreffend.
- **Geänderte Methoden:** Nicht zutreffend.
- **Neue Events:** Nicht zutreffend.
- **Neue Event-Handler:** Nicht zutreffend.

### `PlaywrightWebAppFixture` (Test-Infrastrukturklasse)

- **Neue Eigenschaften:** Optionaler Parameter-/Optionsträger für Session-Erzeugung (z. B. mobile Viewport-Wahl).
- **Neue Methoden:** Neue Fixture-Hilfsmethode für mobile Session-Erzeugung (oder Erweiterung von `CreateSessionAsync(...)` um entsprechende Parameter).
- **Geänderte Methoden:** `CreateSessionAsync()` (Signatur oder interne Context-Konfiguration) zur Unterstützung von `ViewportSize`/`IsMobile`/`HasTouch`.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `AuthenticationFlowPlaywrightTests`, `ListNavigationPlaywrightTests`, `ReportingFlowPlaywrightTests`, `HomeMassImportPlaywrightTests` (E2E-Testklassen)

- **Neue Eigenschaften:** Keine.
- **Neue Methoden:** Neue mobile Testmethoden pro geänderter Benutzerinteraktion (mindestens Happy Path).
- **Geänderte Methoden:** Bestehende Helferaufrufe werden angepasst, falls sich Fixture-Signaturen ändern.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `@media`-Breakpoint für kleine Viewports in globalen Styles (`app.css` + Dark-Theme-Pendants) | CSS-Regelwerk | Konsistent zu bestehendem Projekt-Breakpoint (auf einen zentralen Wert vereinheitlicht) | Einheitliches responsives Verhalten über alle Seiten |

## Seiteneffekte und Risiken

- **Globales Layout/CSS-Kaskade:** Änderungen in globalen Styles können unbeabsichtigt Desktop-Darstellungen beeinflussen.
- **Dark-Theme-Parität:** Responsive Anpassungen müssen in Light- und Dark-Styles synchron gepflegt werden, sonst entstehen Darstellungsabweichungen.
- **Tabellen-Lesbarkeit:** Mobile Scroll-Container lösen Breitenprobleme, können aber wichtige Spalten aus dem initial sichtbaren Bereich verdrängen.
- **E2E-Laufzeit:** Zusätzliche mobile Szenarien erhöhen die CI-Laufzeit und können bei instabilen Selektoren zu Flakiness führen.

## Umsetzungsreihenfolge

1. **Responsive Basis-Strategie und zentrale Breakpoints festlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Einheitliche Mobile-Regeln (Breakpoint, Container, Abstände, Tabellenstrategie) in `app.css` und `theme.Dark*.css` definieren.

2. **Globale Layout- und Navigationscontainer mobilfähig machen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: `MainLayout` sowie `ribbon.css`/`theme.Dark.Ribbon.css` auf konsistente mobile Topbar-/Aktionsleisten-Umbrüche anpassen.

3. **Generische Seitenbausteine für Mobile standardisieren**
   - Voraussetzungen: Schritt 1–2 abgeschlossen.
   - Beschreibung: `GenericListPage<TItem>`, `GenericCardPage<TKeyValue>`, `ListPage`, `CardPage` so anpassen, dass Tabellen/Formulare/Overlays auf kleinen Viewports robust funktionieren.

4. **Seiten mit hoher Nutzerrelevanz auf mobile Muster umstellen**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: `Home`, `ReportDashboard`, `BudgetReport`, `ReportsHome`, `SetupSections` inkl. zentraler Inhaltsbereiche auf die neuen responsive Muster umstellen.

5. **Setup- und Securities-Tabs nachziehen**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: Alle betroffenen Tabs unter `Components/Pages/Setup/*` und `Components/Pages/Securities/*` mit denselben Mobile-Mustern harmonisieren.

6. **E2E-Fixture um mobile Session-Erzeugung erweitern**
   - Voraussetzungen: Bestehende Playwright-Infrastruktur vorhanden (`PlaywrightWebAppFixture`).
   - Beschreibung: Session-Erstellung so erweitern, dass mobile Viewport-/Touch-Parameter zentral nutzbar sind.

7. **Mobile E2E-Tests pro geändertem Benutzerablauf ergänzen**
   - Voraussetzungen: Schritt 6 abgeschlossen; betroffene UI-Anpassungen aus Schritt 2–5 umgesetzt.
   - Beschreibung: Für Auth, Navigation/CRUD, Reporting und Import je mindestens ein mobiler Happy-Path-Test erstellen.

8. **Regression und Stabilisierung**
   - Voraussetzungen: Schritt 1–7 abgeschlossen.
   - Beschreibung: Bestehende E2E-Tests bei Selektor-/Layout-Änderungen aktualisieren und mobile + Desktop-Ausführung stabilisieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateMobileSessionAsync()` (oder parametrisierte Session-Erzeugung) | `PlaywrightWebAppFixture` | Standardisierte mobile Browser-Session mit kleinem Viewport und Touch-Optionen |
| `Register_Login_Logout_Flow_ShouldWork_OnMobileViewport` | `AuthenticationFlowPlaywrightTests` | Happy Path Authentifizierung auf mobilem Viewport |
| `ClickAccountRow_ShouldNavigateToDetailPage_OnMobileViewport` | `ListNavigationPlaywrightTests` | Liste→Detail-Navigation auf kleiner Breite |
| `Create_Edit_Delete_BankAccount_ShouldWork_OnMobileViewport` | `ListNavigationPlaywrightTests` | Zentraler CRUD-Flow unter mobiler Darstellung |
| `SaveFavorite_ShouldPersistAndReload_OnMobileViewport` | `ReportingFlowPlaywrightTests` | Reporting-Favoriten inkl. Filterbedienung auf Mobile |
| `UploadStatementFile_ShouldShowSuccess_WhenImportCompletes_OnMobileViewport` | `HomeMassImportPlaywrightTests` | Import-Happy-Path im mobilen Layout |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AuthenticationFlowPlaywrightTests` (bestehende Methoden) | Anpassung gemeinsamer Session-Helfer, falls `CreateSessionAsync(...)` erweitert wird |
| `ListNavigationPlaywrightTests` (bestehende Methoden) | Selektoren/Interaktionspfade können sich durch responsive Container ändern |
| `ReportingFlowPlaywrightTests` (bestehende Methoden) | Ribbon-/Filterbereich kann DOM-Struktur ändern |
| `HomeMassImportPlaywrightTests` (bestehende Methoden) | Dialog-/Button-Platzierung kann durch mobiles Layout variieren |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Login, Nutzung und Logout auf Smartphone-Viewport | `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs` | Kernfunktion Authentifizierung bleibt mobil vollständig nutzbar |
| Navigation von Kontenliste zur Detailansicht mobil | `FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs` | Listenansichten sind ohne Layoutbruch nutzbar |
| Mobiler Konto-CRUD (Anlegen/Bearbeiten/Löschen) | `FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs` | Zentrale Pflegeabläufe funktionieren auf kleinen Bildschirmen |
| Reporting-Favorit speichern und wieder laden mobil | `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs` | Berichtsfilter und Favoritenbedienung sind mobil möglich |
| Kontoauszug hochladen und Erfolg prüfen mobil | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs` | Import-Happy-Path ist mobil bedienbar |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Bestehende Tests in den oben genannten vier Klassen | Gemeinsame Fixture-Signatur und/oder Selektoren nach Responsive-Refactoring |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Welche Ziel-Viewportbreiten gelten als „kleine Bildschirme“? | Primär Smartphone-Portrait als Pflichtziel (z. B. 390x844) plus ein kleiner generischer Breakpoint-Bereich bis ca. 768px für CSS-Regeln. |
| 2 | Nur Smartphones oder auch kleine Tablets/Querformat? | Mindestumfang: Smartphones (Portrait + Landscape). Kleine Tablets als „sollte funktionieren“, aber nicht blocker-kritisch in der ersten Lieferung. |
| 3 | Was ist die verbindliche Definition von „optimiert“? | Mindestkriterien definieren: keine horizontale Gesamtseiten-Scrollbar, alle primären Aktionen erreichbar, Touch-Ziele ausreichend groß, Happy Path pro Kernflow per E2E abgesichert. |
| 4 | Gibt es fachlich priorisierte Seiten für die Umsetzung? | Priorisierung nach Nutzungswert: Auth/Login → Home/Import → Kernlisten+Karten (Konten/Kontakte/Buchungen) → Reporting → Setup/Securities. |
| 5 | Einheitliche Tabellenstrategie oder je Seite individuell? | Standardstrategie: `.table-responsive` als Default; nur bei fachlich begründeter Ausnahme seitenindividuelle Spaltenreduktion/Stacking. |
| 6 | Sind mobile Akzeptanztests in CI merge-blockierend? | Empfehlung: Ja für mindestens einen mobilen Smoke-Happy-Path je Kernbereich, weitere mobile Tests zunächst nicht-blockierend und schrittweise hochziehen. |
| 7 | Ist das Fehlen einer zentralen `docs/features.md` beabsichtigt? | Vorschlag: Kurz klären; falls nicht vorhanden, Konventionen im jeweiligen Feature-Ordner belassen und später zentral dokumentieren. |

