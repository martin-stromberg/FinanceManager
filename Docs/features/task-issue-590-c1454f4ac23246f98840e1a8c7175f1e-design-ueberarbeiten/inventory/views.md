# Ansichten- und Routen-Inventar

Vollständige Bestandsaufnahme aller renderbaren Routen unter `FinanceManager.Web/Components/Pages`, der generischen Routen-Instanzen (Kind/SubKind), Overlays, Panels und Tabs. Quelle: `@page`-Direktiven in den `.razor`-Dateien, `ListViewModelFactory.Create` und `CardRouteAttribute`-Scan (`CardViewModelResolver`).

## 1. Routing-Grundgerüst

- `Components/Routes.razor` (Z. 3–7): `<Router>` mit `RouteView` und `DefaultLayout = typeof(MainLayout)`; kein `NotFound`-Fragment — unbekannte Routen rendern leer.
- `Components/Layout/MainLayout.razor` (Z. 12): `<div class="app-shell dark-mode">` mit `.sidebar` (NavLinks), `.content`/`.inner.app-inner` (max-width 1200 px, Vollbreite für Listen-/Karten-/Report-/Portfolio-Pfade, Z. 206–215), mobile Topbar mit Hamburger (`#nav-toggle`, rein CSS-gesteuert) und `#blazor-error-ui`.
- `Components/Layout/HelpLayout.razor` (Z. 4–7): eigenständige `<main class="help-layout">`-Surface ohne Sidebar/Ribbon für `/help*`.
- `Components/App.razor`: statisches SSR-Grunddokument; `<body class="dark-mode">` (Z. 76); Hilfe-Surface (`_isHelpSurface`, Z. 107–118) unterdrückt `LoadingBar`, `AuthRedirect`, `auth.js`, `scroll.js`, `blazor.web.js` und lädt stattdessen `help/js/help-search.js`.
- `Components/AuthRedirect.razor`: clientseitige Auth-Umleitung; öffentliche Pfade über `AuthReturnUrl.IsPublicPath`; unauthentifiziert → `/register` (kein Benutzer) bzw. `/login` mit `returnUrl`.
- Render-Modus: `@rendermode InteractiveServer` auf allen interaktiven Seiten; `Error.razor`, `Legal.razor`, `HelpHub.razor`, `HelpPageView.razor` sind rein statisch gerendert.

## 2. Konkrete Routen (eigene `@page`-Direktiven)

| Route | Komponente | Render-Modus | Layout | Anmerkungen |
|---|---|---|---|---|
| `/` | `Pages/Home.razor` | InteractiveServer | MainLayout | Ribbon (`HomeViewModel`), `HomeNotifications`, `BackgroundTaskStatusPanel`, `HomeKpiGrid` (Edit-Modus), Upload-Fortschritt, Mass-Import-Dialog (`split-center`/`split-dialog`) |
| `/login` | `Pages/Login.razor` | InteractiveServer | MainLayout | `EditForm`, Felder `#login-user`/`#login-pass`, JS `fmAuthLogin`, `returnUrl`-Query |
| `/register` | `Pages/Register.razor` | InteractiveServer | MainLayout | Felder `#username`/`#password`; Demodaten-Checkbox `#create-demo-data` nur für ersten Benutzer (`_isFirstUser`, Z. 48–52); Selbstregistrierung weiterer Benutzer via `Auth:AllowSelfRegistration` (Z. 88) |
| `/change-password` | `Pages/ChangePassword.razor` | InteractiveServer | MainLayout | Erreichbar über `LoginStatus.razor` (`#change-password-link`) in der Sidebar |
| `/Error` | `Pages/Error.razor` | statisch | MainLayout | Exception-Handler-Ziel (`UseExceptionHandler("/Error")`), RequestId |
| `/legal` | `Pages/Legal.razor` | statisch | MainLayout | Rechtstext, externe Links |
| `/help` | `Pages/HelpHub.razor` | statisch | `@layout HelpLayout` | `help-shell`, Such-Panel, kategorisierte Themenliste; Inhalte aus `Docs/help` (92 Markdown-Dateien, de/en) |
| `/help/view/{*HelpPath}` | `Pages/HelpPageView.razor` | statisch | `@layout HelpLayout` | Rendert Hilfe-Markdown via `IHelpContentRenderer` (`Services/Help/HelpContentRenderer.cs`) |
| `/reports` | `Pages/ReportsHome.razor` | InteractiveServer | MainLayout | Berichtsfavoriten-Grid (`.fav-grid .fav-card`), Ribbon |
| `/reports/dashboard` | `Pages/ReportDashboard.razor` | InteractiveServer | MainLayout | Filter-/Favoriten-Dialoge (`modal-overlay`), Postings-Overlay, Projektions-Detail-Overlay, `query-overlay` |
| `/reports/budget` | `Pages/BudgetReport.razor` | InteractiveServer | MainLayout | `.budget-report-table`, Monats-Navigation (`#PrevMonth`), `BudgetReportSettingsOverlay`, Zweck-Postings-Overlay |
| `/securities/{Id:guid}/performance` und `/securities/{Id:guid}/performance/{InitialTab}` | `Pages/Securities/SecurityPerformancePage.razor` | InteractiveServer | MainLayout | Tab-Bar (`tab-btn`), Tab-Keys `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark` |
| `/portfolio/analysis-report` | `Pages/Portfolio/PortfolioAnalysisReportPage.razor` | InteractiveServer | MainLayout | Ribbon + sechs `Portfolio*Card`-Unterkomponenten (Kpi, Structure, Performance, DepotDevelopment, Cashflow, Risk) |
| `/list/{Kind}/{SubKind?}/{Id?}` | `Pages/ListPage.razor` | InteractiveServer | MainLayout | Generische Liste, siehe Abschnitt 3 |
| `/card/{Kind}`, `/card/{Kind}/{Id:guid}`, `/card/{Kind}/{SubKind?}/{Id:guid}`, `/card/{Kind}/new`, `/card/{Kind}/{SubKind?}/new` | `Pages/CardPage.razor` | InteractiveServer | MainLayout | Generische Karte, siehe Abschnitt 4; Query-Parameter `back` und `prefill` |

### Komponenten ohne eigene Route (in `Components/Pages` enthalten)

| Datei | Verwendung |
|---|---|
| `GenericListPage.razor` | Generische Tabellen-Liste (`TItem`), wird von `ListPage.razor` und als Embedded-List in `GenericCardPage.razor` gerendert; Filterleiste (`.list-filters`), Tabelle `.fm-table`, Infinite-Scroll (`fmInfinite`, scroll.js) |
| `GenericCardPage.razor` | Generische Kartenansicht (`TKeyValue`), rendert `Provider.CardRecord.Fields` als `.fm-table.card.card-table` mit Editierfeldern; embedded List bzw. `QuickEditTable` für Statement-Draft-Entries |
| `ContactDetail.razor` | Alias-Verwaltung eines Kontakts; eingebettet über `ContactCardViewModel` (via `EmbeddedPanelHost`) |
| `BudgetReportPostingOriginCell.razor` | Zell-Komponente innerhalb `BudgetReport.razor` |
| `BudgetReportSettingsOverlay.razor` | Einstellungs-Overlay innerhalb `BudgetReport.razor` |
| `SetupSections.razor` | Akkordeon-Container aller Setup-Tabs; wird von `SetupCardViewModel` (Route `/card/setup`) eingebettet |
| `Setup/*Tab.razor` (11 Dateien) | Setup-Abschnitte via `DynamicComponent`, siehe Abschnitt 5.3 |
| `Securities/*Tab.razor` (5 Dateien) | Tabs der Security-Performance-Seite, siehe Abschnitt 5.2 |
| `Portfolio/Portfolio*Card.razor` (6 Dateien) | Karten der Portfolio-Analyseseite |

## 3. Generische Listenroute `/list/{Kind}/{SubKind?}/{Id?}`

`ListPage.razor` erzeugt den Provider über `ListViewModelFactory.Create` (`FinanceManager.Web/ViewModels/ListViewModelFactory.cs`, Z. 72–149). Konkrete Instanzen:

| URL | Kind/SubKind | ViewModel |
|---|---|---|
| `/list/accounts` | `accounts` | `BankAccountListViewModel` (Embedded Panel `AccountsStatisticsTile`) |
| `/list/contacts` | `contacts` | `ContactListViewModel` |
| `/list/contacts/categories` | `contacts`/`categories` | `ContactGroupListViewModel` |
| `/list/postings/{SubKind}/{Id}` | `postings` + `account`/`contact`/`savings-plan`/`security` + Guid | `AccountPostingsListViewModel`, `ContactPostingsListViewModel`, `SavingsPlanPostingsListViewModel`, `SecurityPostingsListViewModel` |
| `/list/savings-plans` | `savings-plans` | `SavingsPlansListViewModel` |
| `/list/savings-plans/categories` | `savings-plans`/`categories` | `SavingsPlanCategoryListViewModel` |
| `/list/securities` | `securities` | `SecuritiesListViewModel` |
| `/list/securities/categories` | `securities`/`categories` | `SecurityCategoriesListViewModel` |
| `/list/securities/prices/{Id}` | `securities`/`prices` + Guid | `SecurityPricesListViewModel` |
| `/list/statement-drafts` | `statement-drafts` | `StatementDraftsListViewModel` |
| `/list/users` | `users` | `UserListViewModel` (nur Admin in `MainLayout` sichtbar, Z. 31–34) |
| `/list/mcp-accesses` | `mcp-accesses` | `McpAccessListViewModel` |
| `/list/budget` bzw. `/list/budgets` | `budget` (Default = `purposes`) | `BudgetPurposeListViewModel` |
| `/list/budget/purposes` | `budget`/`purposes` | `BudgetPurposeListViewModel` |
| `/list/budget/categories` | `budget`/`categories` | `BudgetCategoryListViewModel` |

Nicht aufgelöste Kind/SubKind-Kombinationen liefern `null` → `ListPage` zeigt Ladezustand/Titel ohne Inhalt.

## 4. Generische Kartenroute `/card/{Kind}/...`

`CardPage.razor` (Z. 386–408) löst Kinds über `CardViewModelResolver.Resolve` → `CardRouteAttribute`-Scan auf. Konkrete Instanzen (Attribut → ViewModel):

| URL | CardRoute | ViewModel |
|---|---|---|
| `/card/accounts/{Id}` | `("accounts")` | `BankAccountCardViewModel` |
| `/card/budget/categories/...` | `("budget","categories")` | `BudgetCategoryCardViewModel` |
| `/card/budget/purposes/...` | `("budget","purposes")` | `BudgetPurposeCardViewModel` |
| `/card/budget/rules/...` | `("budget","rules")` | `BudgetRuleCardViewModel` |
| `/card/contacts/{Id}` | `("contacts")` | `ContactCardViewModel` |
| `/card/contacts/categories/{Id}` | `("contacts","categories")` | `ContactGroupCardViewModel` |
| `/card/mcp-accesses/{Id}` | `("mcp-accesses")` | `McpAccessCardViewModel` |
| `/card/mcp-approvals` | `("mcp-approvals")` | `McpApprovalCenterViewModel` |
| `/card/mcp-audit` | `("mcp-audit")` | `McpAuditViewModel` |
| `/card/mcp-security` | `("mcp-security")` | `McpSecurityOverviewViewModel` |
| `/card/postings/{Id}` | `("postings")` | `PostingsCardViewModel` |
| `/card/savings-plans/{Id}` | `("savings-plans")` | `SavingsPlanCardViewModel` |
| `/card/savings-plans/categories/{Id}` | `("savings-plans","categories")` | `SavingsPlanCategoryCardViewModel` |
| `/card/securities/{Id}` | `("securities")` | `SecurityCardViewModel` (+ `ReturnSummaryWidget`, `GoldenCrossWidget`, `TrendReversalWidget`, `AggregateBarChart` in `CardPage`) |
| `/card/securities/categories/{Id}` | `("securities","categories")` | `SecurityCategoryCardViewModel` |
| `/card/setup` | `("setup")` | `SetupCardViewModel` (rendert `SetupSections` mit allen Setup-Tabs) |
| `/card/users/{Id}` | `("users")` | `UserCardViewModel` |
| `/card/statement-drafts/{Id}` | `("statement-drafts")` | `StatementDraftCardViewModel` (embedded Entries-Liste, Quick-Edit) |
| `/card/statement-drafts/entries/{Id}` | `("statement-drafts","entries")` | `StatementDraftEntryCardViewModel` |

Fallback ohne Resolver-Treffer: Navigation zurück auf `list/{kind}` (`CardPage.razor` Z. 406–408).

## 5. Tabs und aufklappbare Bereiche

### 5.1 Ribbon-Tabs

`Components/Shared/Ribbon.razor` (`.fm-ribbon`): generische Tab-Bar (`TTabEnum`), Register (`UiRibbonTab`) und Gruppen mit Items (`UiRibbonAction`, Größen `Large`/`Small`). Mobile Variante ab `@media (max-width: 900px)`: Desktop-Gruppen ausgeblendet, Akkordeon-Gruppen (`fm-ribbon-mobile-group-panel`) mit `MobileShortcut`-Buttons. Fast alle Seiten instanziieren genau ein Tab (`enum TabId { List }`/`{ Card }`).

### 5.2 Security-Performance-Tabs (`/securities/{Id}/performance[/{InitialTab}]`)

`SecurityPerformancePage.razor` Z. 45–74: Tab-Keys → Komponenten:

- `Overview` → `OverviewTab.razor`
- `TimeSeries` → `TimeSeriesTab.razor`
- `Cashflows` → `CashflowTab.razor`
- `Metrics` → `MetricsTab.razor`
- `Benchmark` → `BenchmarkTab.razor`

### 5.3 Setup-Sektionen (`/card/setup`)

`SetupSections.razor` rendert `Provider.SettingSections` als Akkordeon; Sektions-Definitionen in `SetupCardViewModel.cs` Z. 66–78 (`key`, Lokalisierung, ViewModel, Tab-Komponente):

| Key | Tab-Komponente |
|---|---|
| `profile` | `SetupProfileTab` |
| `notifications` | `SetupNotificationsTab` |
| `statements` | `SetupStatementTab` |
| `attachments` | `SetupAttachmentCategoriesTab` |
| `backup` | `SetupBackupTab` |
| `update` | `SetupUpdateTab` |
| `security` | `SetupSecurityTab` |
| `securitytxt` | `SecurityTxtSettingsTab` |
| `wellknown` | `WellKnownSettingsTab` |
| `returnanalysis` | `SetupReturnAnalysisTab` |
| `mcp` | `SetupMcpTab` (nur sichtbar bei aktivierter MCP-Funktion) |

Whitelist `AllowedSectionComponents` in `SetupSections.razor` Z. 105–118.

## 6. Overlays, Dialoge und eingebettete Panels

### 6.1 Overlay-Mechanik

- `UiOverlaySpec`-Record (`ViewModels/Common/BaseViewModel.cs` Z. 235; zweite, parallele Definition in `ViewModels/ViewModelBase.cs` Z. 145).
- `Components/Shared/OverlayHost.razor` (Karten) und Overlay-Block in `ListPage.razor` Z. 54–86: `split-center`/`split-dialog`-Markup, `DynamicComponent` mit `CascadingValue`s `OverlayClose`/`OverlayOnFinished`.
- Globale Bestätigungsdialoge: `ConfirmationDialogHost.razor` + `ConfirmDialog.razor` (auf den meisten Seiten eingebunden).

### 6.2 Konkrete Overlay-/Panel-Komponenten (Shared)

| Komponente | Auslöser (ViewModel) |
|---|---|
| `AttachmentsPanel` | `BaseViewModel`/`ViewModelBase` (`OpenAttachments`) |
| `AssignStatementOverlay` | `StatementDraftEntryCardViewModel` |
| `ContactMergePanel` | `ContactCardViewModel` |
| `MassBookingOptionsPanel` | `StatementDraftsListViewModel` |
| `McpAccessCreateWizard` | `McpAccessListViewModel` (mehrstufiger Wizard `.mcp-wizard`) |
| `McpTokenDisplayPanel` | `McpAccessCardViewModel` |
| `SecurityPriceImportPanel` | `SecurityPricesListViewModel` |
| `SecurityPricesBackfillPanel` | `SecurityPricesListViewModel` |
| `SetPasswordOverlay` | `UserCardViewModel` |
| `PostingRulesPanel` | `BankAccountCardViewModel` |
| `ContactDetail` (Alias-Panel, in `Pages/`) | `ContactCardViewModel` |

### 6.3 Eingebettete Panels (`EmbeddedPanelSpec`, `EmbeddedPanelHost.razor`, Positionen `AfterRibbon`/`AfterCard`/`AfterList`)

| Komponente | Kontext |
|---|---|
| `AccountsStatisticsTile` | `BankAccountListViewModel` (Accounts-Liste) |
| `LinkedIbansPanel` | `CardPage` für Sammelkonten (`BankAccountCardViewModel`) |
| `SavingsPlanLinkedAccountsPanel` | `CardPage` für `SavingsPlanCardViewModel` im Edit-Zustand |
| `SavingsPlanActualReportPanel` | `SavingsPlanCardViewModel` |
| `ValidationResultPanel` (inkl. `BudgetImpactValidationPanel` aus `Components/Statements/`) | `StatementDraftCardViewModel` |
| `BudgetImpactSummaryPanel` | `StatementDraftEntryCardViewModel` |
| `McpSecurityOverviewPanel` | `McpSecurityOverviewViewModel` (`/card/mcp-security`) |
| `McpAuditPanel` | `McpAuditViewModel` (`/card/mcp-audit`) |
| `McpApprovalsPanel` | `McpApprovalCenterViewModel` (`/card/mcp-approvals`) |
| `McpAccessExtrasPanel` (enthält `McpRightsEditor`) | `McpAccessCardViewModel` |

### 6.4 Seitenlokale Overlays

| Seite | Overlay |
|---|---|
| `Home.razor` | Mass-Import-Dialog (`split-center`/`mass-import-dialog`), Upload-Fortschritt |
| `BudgetReport.razor` | `BudgetReportSettingsOverlay`, Zweck-Postings-Overlay (`split-dialog`) |
| `ReportDashboard.razor` | Projektions-Detail-Overlay, Postings-Overlay, Favoriten-Dialog, Filter-Dialog (`modal-overlay`) |

## 7. Weitere Shared-UI-Bausteine (Darstellung)

`Components/Shared/`: `Ribbon`, `HomeKpiGrid`, `HomeNotifications`, `NumericKpi`, `MonthlyBudgetKpi`, `ReportKpiTile`, `AccountsStatisticsTile`, `KpiInfoButton`, Charts (`AggregateBarChart`, `BudgetLineChart`, `DonutChart`, `MiniBarChart`, `PortfolioBarLineChart`, `PortfolioGroupedBarChart`), Widgets (`ReturnSummaryWidget`, `GoldenCrossWidget`, `TrendReversalWidget`), `BackgroundTaskStatusPanel`, `UploadStatus`, `PositionsExplanationTable`, `SetupPanel`, `Shared/QuickEdit/QuickEditTable.razor` (Inline-Tabellen-Editor für Kontoauszugs-Entwürfe, `.fm-table`).

## 8. Auth-/Zustandslogik der Ansichten

- Öffentliche Pfade via `AuthReturnUrl.IsPublicPath` (u. a. `/login`, `/register`, `/Error`, `/legal`, `/help*`); alle übrigen Ansichten werden von `AuthRedirect` bei fehlender Authentifizierung umgeleitet.
- Admin-Zustand: `MainLayout` zeigt `/list/users` nur bei `CurrentUser.IsAdmin`; `SecurityPerformancePage`/`PortfolioAnalysisReportPage` rendern explizite „NotAuthenticated"-Zustände.
- Auth-Cookie `FinanceManager.Auth` enthält das JWT; API-Aufrufe laufen über Bearer-Token (JWT-Schema ist nicht das Default-Schema, vgl. `App.razor`-Kommentar Z. 120–136).
