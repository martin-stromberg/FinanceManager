## `Login` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/Login.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Prüft via `IUserReadService.HasAnyUsersAsync`, ob ein Redirect nach `/register` nötig ist. |
| `OnAfterRender(bool firstRender)` | `protected override` | Führt Redirect nach `/register` aus, wenn `_shouldRedirectToRegister` gesetzt ist. |
| `SubmitAsync()` | `private` | Führt Login über JS-Funktion `fmAuthLogin` aus und navigiert bei Erfolg nach `/`. |

Abonnierte Events: keine  
Publizierte Events: keine

Querverweise: `SubmitAsync()` ruft `fmAuthLogin` aus `FinanceManager.Web/wwwroot/auth.js` auf.

## `Register` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/Register.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnAfterRenderAsync(bool firstRender)` | `protected override` | Lädt bei erstem Rendern das JS-Modul `/js/profile.js`. |
| `RegisterAsync()` | `private` | Ermittelt Locale/Zeitzone via JS und ruft `IApiClient.Auth_RegisterAsync`. |
| `InvalidSubmit(EditContext)` | `private` | Leerer Handler für invalides Submit. |
| `OnUsernameChanged()` | `private` | Trimmt den eingegebenen Benutzernamen. |
| `OnPasswordChanged()` | `private` | Platzhalter ohne weitere Logik. |

Abonnierte Events: keine  
Publizierte Events: keine

Querverweise: `RegisterAsync()` ruft `getLocale`/`getTimeZone` aus `FinanceManager.Web/wwwroot/js/profile.js` und danach `IApiClient.Auth_RegisterAsync`.

## `Home` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/Home.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Erstellt `HomeViewModel` und registriert Event-Handler. |
| `VmOnUiActionRequested(object?, string?)` | `private` | Placeholder für UI-Aktionen (Import läuft über Ribbon `FileCallback`). |
| `VmOnStateChanged(object?, EventArgs?)` | `private` | Triggert `StateHasChanged`. |
| `ConfirmPendingMassImportAsync()` | `private` | Delegiert an `HomeViewModel.ConfirmMassImportAsync()`. |
| `CancelPendingMassImport()` | `private` | Schließt Pending-Dialog über ViewModel. |
| `Dispose()` | `public` | Entfernt Event-Abonnements auf dem ViewModel. |

Abonnierte Events: `HomeViewModel.StateChanged`, `HomeViewModel.UiActionRequested`  
Publizierte Events: keine

Querverweise: Wird von `HomeViewModel` gesteuert; verarbeitet `MassImportBatchFileResultDto`-Daten für den Dialog.

## `ListPage` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/ListPage.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnParametersSetAsync()` | `protected override` | Erstellt passenden Provider über `ListViewModelFactory.Create(...)`, initialisiert ihn und verdrahtet Events. |
| `OnItemClick(object item)` | `private` | Navigiert über `IListItemNavigation.GetNavigateUrl()`. |
| `ProviderOnUiActionRequested(...)` | `private` | Verarbeitet UI-Aktionen/Overlay-Payloads aus `BaseViewModel`. |
| `HandleProviderActionAsync(string action, string? payload)` | `private` | Reagiert auf Aktionen wie `New`, `Back`, `Reload`, `ExportCsv`, `ExportXlsx`. |
| `CloseOverlay()` | `private` | Schließt generisches Overlay. |

Abonnierte Events: `IListProvider.StateChanged`, `BaseViewModel.UiActionRequested`  
Publizierte Events: keine

Querverweise: Nutzt `ListViewModelFactory`, `IListProvider` und `IListItemNavigation`; Exportpfad für Postings wird in `HandleProviderActionAsync` gebaut.

## `ReportDashboard` (Razor-Komponente)
Datei: `FinanceManager.Web/Components/Pages/ReportDashboard.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync()` | `protected override` | Erstellt `ReportDashboardViewModel` und verdrahtet Event-Handler. |
| `OnParametersSetAsync()` | `protected override` | Lädt Favorite- und Filterzustand, triggert initiales Laden. |
| `LoadAsync()` | `private` | Synchronisiert UI-Zustand in ViewModel und ruft `ReportDashboardViewModel.ReloadAsync`. |
| `HandleRibbonAction(string action)` | `private` | Verarbeitet Ribbon-Aktionen (`Back`, `ToggleEdit`, `Save`, `FiltersOpen`, …). |
| `ApplyFavorite(ReportFavoriteDto fav)` | `private` | Übernimmt gespeicherte Favorite-Parameter in den aktuellen Dashboard-Zustand. |
| `BuildFilters()` | `private` | Baut `ReportAggregatesFiltersRequest` aus aktuellen Selektionen. |

Abonnierte Events: `ReportDashboardViewModel.StateChanged`, `ReportDashboardViewModel.UiActionRequestedEx`  
Publizierte Events: keine

Querverweise: Nutzt `ReportDashboardViewModel` als zentrale Logik; lädt ergänzende Symboldaten über `IApiClient` (Kontakte, Sparpläne, Securities, Kategorien).

## `HomeViewModel`
Datei: `FinanceManager.Web/ViewModels/Home/HomeViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartUpload(int total)` | `public` | Initialisiert Upload-/Importstatus für Batch-Verarbeitung. |
| `ProcessMassImportSelectionAsync(IReadOnlyList<IBrowserFile>)` | `private` | Liest Dateien ein, ruft `StatementDrafts_ProcessMassImportAsync`, öffnet ggf. Bestätigungsdialog. |
| `ConfirmMassImportAsync(CancellationToken)` | `public` | Bestätigt Dialogentscheidungen und führt Batch final aus. |
| `CancelMassImportDialog()` | `public` | Verwirft pending Batchdaten. |
| `SetPendingFileExcluded(Guid, bool)` | `public` | Aktualisiert Ausschlussstatus einzelner Dateien. |
| `SetPendingFileSecurity(Guid, Guid?)` | `public` | Setzt Security-Zuordnung für Preisimportdateien. |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Definiert Ribbon-Aktionen (`Import`, `ToggleKpi`) inkl. `FileCallback`. |

Abonnierte Events: keine (nutzt API direkt)  
Publizierte Events: `StateChanged` (via `RaiseStateChanged`), UI-Aktionsereignisse über Ribbon-Callbacks

Querverweise: Wird von `Home.razor` instanziert und dort über `StateChanged`/`UiActionRequested` konsumiert; ruft `IApiClient.StatementDrafts_ProcessMassImportAsync`.

## `ReportDashboardViewModel`
Datei: `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReloadAsync(DateTime?, CancellationToken)` | `public` | Lädt Aggregatedaten auf Basis des aktuellen Zustands. |
| `LoadAsync(...)` | `public` | Baut `ReportAggregatesQueryRequest` und ruft `IApiClient.Reports_QueryAggregatesAsync`. |
| `LoadFilterOptionsAsync(CancellationToken)` | `public` | Lädt dialogfähige Filteroptionen aus mehreren API-Listen. |
| `OpenFilterDialog()` / `CloseFilterDialog()` | `public` | Öffnet/schließt Filterdialog mit temporären Puffern. |
| `ApplyTempAndReloadAsync(DateTime?, CancellationToken)` | `public` | Übernimmt Temp-Filter in aktive Filter und lädt neu. |
| `SaveFavoriteAsync(...)` / `UpdateFavoriteAsync(...)` / `DeleteFavoriteAsync(...)` | `public` | CRUD für Report-Favoriten. |
| `SubmitFavoriteDialogAsync(...)` | `public` | Dialog-Flow für Save/Update inklusive Fehlerzustand. |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Definiert Ribbon-Gruppen (`Navigation`, `Actions`, `Filter`) und löst Aktionen aus. |

Abonnierte Events: keine (nutzt API direkt)  
Publizierte Events: `StateChanged`, `UiActionRequested`, `UiActionRequestedEx` (über `RaiseUiActionRequested`)

Querverweise: Wird von `ReportDashboard.razor` gesteuert; dessen `HandleRibbonAction` verarbeitet die von `ReportDashboardViewModel` publizierten Action-IDs.

## `ListViewModelFactory`
Datei: `FinanceManager.Web/ViewModels/ListViewModelFactory.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Create(string kind, string subKind, string id)` | `public` | Erstellt je Route passenden `IListProvider` (`accounts`, `contacts`, `statement-drafts`, `postings`, …). |
| `CreatePostings(string subKind, Guid? id)` | `private` | Erzeugt postings-spezifische List-ViewModels (`account`, `contact`, `savings-plan`, `security`). |

Abonnierte Events: keine  
Publizierte Events: keine

Querverweise: Wird in `ListPage.razor` in `OnParametersSetAsync()` aufgerufen.

## `TestWebApplicationFactory`
Datei: `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ConfigureWebHost(IWebHostBuilder builder)` | `protected override` | Baut Testhost mit In-Memory-SQLite, deaktivierten Worker-Services und Bootstrap-Admin. |
| `Dispose(bool disposing)` | `protected override` | Schließt die offene SQLite-Verbindung. |

Abonnierte Events: keine  
Publizierte Events: keine

Querverweise: Wird von allen `FinanceManager.Tests.Integration.ApiClient.*`-Tests via `IClassFixture<TestWebApplicationFactory>` verwendet.
