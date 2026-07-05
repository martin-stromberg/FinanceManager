# Logik

## `MainLayout`
Datei: `FinanceManager.Web/Components/Layout/MainLayout.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized()` | `protected override` | Initialisiert Logo-Status und abonniert Navigation-Änderungen. |
| `HandleLocationChanged(...)` | `private` | Reagiert auf Route-Wechsel und triggert Re-Render. |
| `UpdateLogo(string uri)` | `private` | Setzt bereichsabhängiges Logo und `_useFullWidth` für breite Seiten. |
| `Dispose()` | `public` | Entfernt `LocationChanged`-Subscription. |

Abonnierte Events: `NavigationManager.LocationChanged`  
Publizierte Events: keine (UI reagiert über `StateHasChanged`)

## `ListPage`
Datei: `FinanceManager.Web/Components/Pages/ListPage.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetKindTitle()` | `private` | Liefert lokalisierte Seitentitel nach `Kind`/`SubKind`. |
| `OnParametersSetAsync()` | `protected override` | Erzeugt List-Provider, verdrahtet Events und initialisiert Provider. |
| `OnItemClick(object item)` | `private` | Navigiert auf angeklickte Listeneinträge. |
| `ProviderOnUiActionRequested(...)` | `private` | Nimmt UI-Aktionen/Overlay-Spezifikationen aus ViewModel entgegen. |
| `HandleProviderActionAsync(...)` | `private` | Führt Aktionen wie `Reload`, `Back`, `ExportCsv/Xlsx` aus. |
| `CloseOverlay()` | `private` | Schließt Overlay und leert Overlay-State. |
| `GetOverlayTitle()` | `private` | Ermittelt Overlay-Titel je Komponententyp. |

Abonnierte Events: `IListProvider.StateChanged`, `BaseViewModel.UiActionRequested`  
Publizierte Events: keine expliziten; Navigation über `NavigationManager`

## `GenericListPage<TItem>`
Datei: `FinanceManager.Web/Components/Pages/GenericListPage.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnParametersSet()` | `protected override` | Verdrahtet Provider-Events und synchronisiert Range-Felder. |
| `OnProviderStateChanged(...)` | `private` | Aktualisiert UI bei Provider-Änderungen. |
| `OnAfterRenderAsync(bool)` | `protected override` | Registriert Infinite-Scroll-Sentinel via JS (`fmInfinite.observe`). |
| `LoadMoreFromJs()` | `public` (`[JSInvokable]`) | Lädt weitere Datensätze bei Scroll-Sentinel. |
| `OnSearchInput(ChangeEventArgs)` | `private` | Setzt Suche und lädt Liste neu. |
| `OnRangeChanged()` | `private` | Setzt Datumsrange und lädt Liste neu. |
| `OnItemClicked(object)` | `private` | Reicht Klick an `ItemClick`-Callback durch. |
| `LocalizeInline(string)` | `private` | Ersetzt Inline-Lokalisierungsmarker (`$Key`). |

Abonnierte Events: `IListProvider.StateChanged`  
Publizierte Events: `ItemClick` (EventCallback)

Querverweise: Wird von `ListPage` und `GenericCardPage` eingebettet.

## `CardPage`
Datei: `FinanceManager.Web/Components/Pages/CardPage.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildListPath()` | `private` | Baut Rückroute zur Liste. |
| `BuildCardPath(...)` | `private` | Baut Kartenroute inkl. optionalem `back`-Query. |
| `SetViewModel(...)` | `private` | Wechselt Card-ViewModel und verdrahtet Events. |
| `ViewModelSatteChanged(...)` | `private` | Triggert Re-Render bei VM-Änderungen. |
| `OnUiActionRequested(...)` | `private` | Verarbeitet UI-Aktionen (`Back`, `Saved`, `Delete`, ...). |
| `GetKindTitle()` | `private` | Liefert lokalisierte Titel pro `Kind`. |
| `OnParametersSetAsync()` | `protected override` | Löst VM über Resolver auf und initialisiert. |
| `HandleDeleteAsync()` | `private` | Führt bestätigtes Löschen über `IDeletableViewModel` aus. |

Abonnierte Events: `BaseCardViewModel.StateChanged`, `BaseViewModel.UiActionRequested`  
Publizierte Events: keine expliziten; Navigation/Dialoge über JS/Navigator

## `GenericCardPage<TKeyValue>`
Datei: `FinanceManager.Web/Components/Pages/GenericCardPage.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RenderField(CardField)` | `private` | Rendert nicht-editierbare Feldtypen. |
| `RenderEditableField(CardField)` | `private` | Rendert editierbare Inputs inkl. Lookup/Enum/Symbol/Currency. |
| `OnParametersSetAsync()` | `protected override` | Verdrahtet Provider-State und lädt Enum-Lookups. |
| `OnProviderStateChanged(...)` | `private` | Re-Render bei Provider-Änderungen. |
| `OnLookupInputAsync(...)` | `private` | Führt Lookup-Suche aus und öffnet Dropdown. |
| `OnSelectLookupAsync(...)` | `private` | Übernimmt Lookup-Auswahl in Feld/Provider. |
| `OnEditableCurrencyBlur(...)` | `private` | Parst/normalisiert numerische Eingaben (inkl. Ausdruck). |
| `EvaluateNumericExpression(string)` | `private` | Wertet arithmetische Ausdrücke für Currency-Felder aus. |
| `OpenCreateForLookup(CardField)` | `private` | Navigiert zur Neuanlage aus Lookup-Kontext. |
| `Dispose()` | `public` | Entfernt Provider-State-Subscription. |

Abonnierte Events: `BaseCardViewModel.StateChanged`  
Publizierte Events: keine expliziten

Querverweise: Bettet `GenericListPage` für `EmbeddedList` ein und wird von `CardPage` gerendert.

## `Home`
Datei: `FinanceManager.Web/Components/Pages/Home.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Erstellt Home-VM und verdrahtet Events. |
| `VmOnStateChanged(...)` | `private` | Triggert UI-Update. |
| `OnPendingSecurityChanged(...)` | `private` | Setzt Security-Zuordnung im Mass-Import-Dialog. |
| `ConfirmPendingMassImportAsync()` | `private` | Startet bestätigten Mass-Import. |
| `CancelPendingMassImport()` | `private` | Schließt Mass-Import-Dialog. |
| `CanSavePendingMassImport()` | `private` | Prüft Vollständigkeit der Dialogdaten. |
| `Dispose()` | `public` | Entfernt VM-Event-Subscriptions. |

Abonnierte Events: `HomeViewModel.StateChanged`, `HomeViewModel.UiActionRequested`  
Publizierte Events: keine expliziten

Querverweise: Nutzt `HomeKpiGrid` und `HomeNotifications`.

## `ReportDashboard`
Datei: `FinanceManager.Web/Components/Pages/ReportDashboard.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Erstellt Dashboard-VM und verdrahtet Actions/State. |
| `OnParametersSetAsync()` | `protected override` | Lädt Favorit/Filter initial und startet Datenladung. |
| `LoadAsync()` | `private` | Überträgt UI-Filter an VM und lädt Aggregatdaten. |
| `LoadSymbolsForPointsAsync()` | `private` | Lädt Symbolzuordnungen für Reportzeilen. |
| `HandleRibbonAction(string)` | `private` | Verarbeitet Ribbon-Aktionen (`Save`, `FiltersOpen`, ...). |
| `SubmitFavoriteDialogAsync()` | `private` | Speichert/aktualisiert Favorit. |
| `ApplyFavorite(...)` | `private` | Überträgt Favoritkonfiguration in UI/VM-Status. |
| `ClearFilters()` | `private` | Setzt Filter zurück. |

Abonnierte Events: `ReportDashboardViewModel.StateChanged`, `ReportDashboardViewModel.UiActionRequestedEx`  
Publizierte Events: keine expliziten

Querverweise: Wird von `ReportsHome.OpenFavorite(...)` über `/reports/dashboard` aufgerufen.

## `BudgetReport`
Datei: `FinanceManager.Web/Components/Pages/BudgetReport.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Initialisiert `BudgetReportViewModel` und lädt Daten. |
| `VmOnStateChanged(...)` | `private` | Triggert Re-Render. |
| `OnUiActionRequested(...)` | `private` | Behandelt Aktionen wie `ShowSettings`, `ExportExcel`. |
| `Dispose()` | `public` | Entfernt Event-Subscriptions. |

Abonnierte Events: `BudgetReportViewModel.StateChanged`, `BudgetReportViewModel.UiActionRequested`  
Publizierte Events: keine expliziten

## `ReportsHome`
Datei: `FinanceManager.Web/Components/Pages/ReportsHome.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Erstellt/initialisiert `ReportsHomeViewModel`. |
| `VmOnAuthenticationRequired(...)` | `private` | Navigiert bei Auth-Bedarf nach `/login`. |
| `VmOnStateChanged(...)` | `private` | Triggert Re-Render. |
| `OpenFavorite(Guid)` | `private` | Öffnet Dashboard mit Favorit-ID. |

Abonnierte Events: `ReportsHomeViewModel.StateChanged`, `ReportsHomeViewModel.AuthenticationRequired`  
Publizierte Events: keine

## `Login`
Datei: `FinanceManager.Web/Components/Pages/Login.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Prüft Erstbenutzerfall und Redirect-Flag. |
| `OnAfterRender(bool)` | `protected override` | Leitet beim Erstbenutzer zu `/register` um. |
| `SubmitAsync()` | `private` | Führt Login über JS (`fmAuthLogin`) aus. |

Abonnierte Events: keine  
Publizierte Events: keine

## `Register`
Datei: `FinanceManager.Web/Components/Pages/Register.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnAfterRenderAsync(bool)` | `protected override` | Lädt Browser-Profilmodul (`/js/profile.js`). |
| `RegisterAsync()` | `private` | Registriert Benutzer inkl. Browser-Locale/Timezone. |
| `InvalidSubmit(EditContext)` | `private` | Platzhalter für ungültige Form-Submits. |
| `OnUsernameChanged()` | `private` | Trimmt Username nach Eingabe. |

Abonnierte Events: keine  
Publizierte Events: keine

## `SetupSections`
Datei: `FinanceManager.Web/Components/Pages/SetupSections.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnParametersSet()` | `protected override` | Verdrahtet `SetupCardViewModel.StateChanged`. |
| `Provider_StateChanged(...)` | `private` | Triggert UI-Update. |
| `OnClick(string key)` | `private` | Wechselt aktive Setup-Sektion im ViewModel. |
| `Dispose()` | `public` | Entfernt Event-Subscription. |

Abonnierte Events: `SetupCardViewModel.StateChanged`  
Publizierte Events: keine

Querverweise: Wird innerhalb der Setup-Karte (Route `/card/setup`) verwendet und steuert die darunter eingebetteten Setup-Tabs.

## Setup-Tabs (`SetupStatementTab`, `SetupSecurityTab`, `SetupReturnAnalysisTab`, `SetupProfileTab`, `SetupNotificationsTab`, `SetupBackupTab`, `SetupAttachmentCategoriesTab`)
Dateien: `FinanceManager.Web/Components/Pages/Setup/*.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` (in allen Tabs) | `protected override` | Bindet tab-spezifisches ViewModel und lädt Initialdaten. |
| `Dispose()` / `DisposeAsync()` (tababhängig) | `public` | Entfernt `StateChanged`-Subscriptions. |
| tab-spezifische Handler (z. B. `OnUploadRequested`, `OnBenchmarkChanged`, `OnUiActionRequested`) | `private` | Reagieren auf Tab-Interaktionen und VM-Aktionen. |

Abonnierte Events: vor allem `...ViewModel.StateChanged`; zusätzlich z. B. `SetupBackupsViewModel.UploadRequested`, `SetupProfileViewModel.UiActionRequested`  
Publizierte Events: keine expliziten

## Securities-Performance (`SecurityPerformancePage`, `OverviewTab`, `TimeSeriesTab`, `CashflowTab`, `MetricsTab`, `BenchmarkTab`)
Dateien: `FinanceManager.Web/Components/Pages/Securities/*.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` (pro Komponente) | `protected override` | Erstellt/übernimmt jeweiliges Tab-ViewModel. |
| `OnParametersSetAsync()` (pro Komponente) | `protected override` | Lädt Daten für `SecurityId`/Tab-Status. |
| `SelectTab(string)` (`SecurityPerformancePage`) | `private` | Wechselt Tab und aktualisiert Deep-Link-Route. |
| chart-/table-Helfer (z. B. `BuildDividendSvg`, `BuildCostBars`) | `private` | Erzeugen SVG-/Anzeigeinhalte aus VM-Daten. |
| `Dispose()` (pro Komponente) | `public` | Entfernt `StateChanged`-Subscriptions. |

Abonnierte Events: pro Komponente `...ViewModel.StateChanged`  
Publizierte Events: keine expliziten

Querverweise: Tabs werden von `SecurityPerformancePage` je `ActiveTabKey` eingeblendet.

## Layout-/Style-Artefakte (Responsive-relevant)
Dateien: `FinanceManager.Web/Components/App.razor`, `FinanceManager.Web/wwwroot/css/*.css`

- `App.razor` setzt `<meta name="viewport" content="width=device-width, initial-scale=1.0" />` und bindet globale + seitenbezogene Stylesheets (`app.*.css`, `theme.Dark.*.css`) ein.
- `app.css` enthält zentrale mobile Struktur (`.mobile-topbar`, `.mobile-overlay`, `.nav-toggle`) und Breakpoint `@media (max-width: 900px)`.
- Relevante Tabellen-/Overflow-Helfer sind vorhanden (`.table-responsive`, `overflow-x: auto` u. a. in `app.ReturnAnalysis.css`, `SetupBackupTab`/`SetupSecurityTab`-Markup).
- `ribbon.css`/`theme.Dark.Ribbon.css` definieren responsive-freundliche flexible Gruppenlayouts, aber keine eigene mobile Breakpoint-Strategie.
