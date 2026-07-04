## `IApiClient`
Datei: `FinanceManager.Shared/IApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Auth_LoginAsync` | `LoginRequest request, CancellationToken ct` | `Task<AuthOkResponse>` | Login-Endpunkt für Authentifizierung. |
| `Auth_RegisterAsync` | `RegisterRequest request, CancellationToken ct` | `Task<AuthOkResponse>` | Registrierung neuer Benutzer. |
| `Auth_LogoutAsync` | `CancellationToken ct` | `Task<bool>` | Logout und Cookie-Clearing. |
| `Users_HasAnyAsync` | `CancellationToken ct` | `Task<bool>` | Prüft, ob Benutzer vorhanden sind (wird im Login-Flow genutzt). |
| `Contacts_ListAsync` | `skip, take, type, all, nameFilter, ct` | `Task<IReadOnlyList<ContactDto>>` | Stammdatenliste Kontakte. |
| `Contacts_CreateAsync` | `ContactCreateRequest request, CancellationToken ct` | `Task<ContactDto>` | Anlage Kontakt (inkl. Flows aus Statement-Entry-Kontext). |
| `Postings_GetAccountAsync` | `accountId, skip, take, q, from, to, ct` | `Task<IReadOnlyList<PostingServiceDto>>` | Postings pro Konto für Listen/Regressionen. |
| `Postings_ReverseAsync` | `Guid id, CancellationToken ct` | `Task<ReversalResultDto?>` | Buchungsstorno/Reversal. |
| `Postings_ValidateReversalAsync` | `Guid id, CancellationToken ct` | `Task<ReversalValidationDto?>` | Vorabprüfung Reversal. |
| `StatementDrafts_UploadAsync` | `Stream stream, string fileName, CancellationToken ct` | `Task<StatementDraftUploadResult?>` | Upload Kontoauszug für Importfluss. |
| `StatementDrafts_ProcessMassImportAsync` | `MassImportBatchRequestDto request, CancellationToken ct` | `Task<MassImportBatchResultDto?>` | Analyse/Ausführung Massenimport. |
| `StatementDrafts_BookAsync` | `Guid draftId, bool forceWarnings, CancellationToken ct` | `Task<BookingResult?>` | Bucht kompletten Draft. |
| `StatementDrafts_BookEntryAsync` | `Guid draftId, Guid entryId, bool forceWarnings, CancellationToken ct` | `Task<BookingResult?>` | Bucht einzelne Draft-Position. |
| `Reports_QueryAggregatesAsync` | `ReportAggregatesQueryRequest req, CancellationToken ct` | `Task<ReportAggregationResult>` | Lädt Report-Dashboard-Aggregate. |
| `Reports_ListFavoritesAsync` | `CancellationToken ct` | `Task<IReadOnlyList<ReportFavoriteDto>>` | Listet Dashboard-Favoriten. |
| `Reports_CreateFavoriteAsync` | `ReportFavoriteCreateApiRequest req, CancellationToken ct` | `Task<ReportFavoriteDto>` | Erstellt Report-Favorit. |
| `Reports_UpdateFavoriteAsync` | `Guid id, ReportFavoriteUpdateApiRequest req, CancellationToken ct` | `Task<ReportFavoriteDto?>` | Aktualisiert Report-Favorit. |
| `Reports_DeleteFavoriteAsync` | `Guid id, CancellationToken ct` | `Task<bool>` | Löscht Report-Favorit. |
| `HomeKpis_ListAsync` | `CancellationToken ct` | `Task<IReadOnlyList<HomeKpiDto>>` | Lädt Home-KPI-Konfigurationen. |
| `HomeKpis_CreateAsync` | `HomeKpiCreateRequest request, CancellationToken ct` | `Task<HomeKpiDto>` | Erstellt Home-KPI. |

## `IUserReadService`
Datei: `FinanceManager.Application/Users/IUserReadService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `HasAnyUsersAsync` | `CancellationToken ct` | `Task<bool>` | Prüft Vorhandensein von Benutzern (wird von `Login.razor` für Redirect-Entscheidung genutzt). |

## `IListProvider`
Datei: `FinanceManager.Web/ViewModels/Common/IListProvider.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `InitializeAsync` | – | `Task` | Initialisiert Provider und triggert ggf. Initial-Load. |
| `LoadAsync` | – | `Task` | Lädt erste Seite und baut Records. |
| `LoadMoreAsync` | – | `Task` | Lädt Folgeseiten. |
| `ClearSearch` | – | `void` | Löscht Suchbegriff. |
| `ClearRange` | – | `void` | Löscht Datumsbereich. |
| `SetSearch` | `string value` | `void` | Setzt Suchfilter. |
| `SetRange` | `DateTime? from, DateTime? to` | `void` | Setzt Bereichsfilter. |
| `ResetAndSearch` | – | `void` | Setzt Paging zurück für neue Suche. |
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Liefert Ribbon-Definitionen für `ListPage`. |

## `IListItemNavigation`
Datei: `FinanceManager.Web/ViewModels/ListViewModelFactory.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNavigateUrl` | – | `string` | Liefert Ziel-URL für Listeneinträge; wird in `ListPage.OnItemClick` genutzt. |

## `IRibbonProvider`
Datei: `FinanceManager.Web/ViewModels/ViewModelBase.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Liefert Ribbon-Register für Seitenkomponenten. |
| `GetActiveTab<TTabEnum>` | – | `TTabEnum?` | Gibt aktuell aktiven Tab zurück. |
| `SetActiveTab<TTabEnum>` | `TTabEnum id` | `void` | Setzt aktiven Tab. |
