## Testklassen

### `ApiClientAuthTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`
- `Register_ShouldSetAuthCookie_AndReturnResponse` — prüft Register-Flow inkl. Rückgabeobjekt.
- `Login_ShouldReturnOk_AndUnauthorized_OnInvalid` — prüft gültigen/ungültigen Login.
- `Logout_ShouldClearCookie` — prüft Logout-Endpunkt.

### `ApiClientUsersTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersTests.cs`
- `Users_HasAny_Returns_False_When_No_Users` — prüft Erreichbarkeit/Bool-Rückgabe von `Users_HasAnyAsync`.
- `Users_HasAny_Returns_True_After_Registration` — prüft Nutzererkennung nach Registrierung.

### `ApiClientContactsTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientContactsTests.cs`
- `Contacts_List_Create_Get_Update_Delete_Flow` — CRUD-Flow für Kontakte inkl. Alias-Funktionen.
- `Contacts_Create_WithStatementEntryParent_ShouldAssignCreatedContactToEntry` — prüft Parent-Linking aus Statement-Entry-Kontext.
- `Contacts_Create_WithInvalidParent_ShouldReturnConflictAndRollbackContactCreate` — prüft Konflikt- und Rollback-Verhalten.

### `ApiClientStatementDraftsTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`
- `StatementDrafts_ProcessMassImport_ShouldSupportAnalysisAndConfirmationFlow` — prüft Analyse-/Bestätigungsphase des Massenimports.
- `StatementDrafts_Flow_Upload_List_Get_SetAccount_AddEntry_Validate_Book_DeleteAll` — deckt End-to-End-ähnlichen API-Flow für Import/Buchung ab.
- `StatementDrafts_Upload_And_DeleteAll_Works` — prüft Upload + globales Löschen offener Drafts.
- `StatementDrafts_Book_ShouldReturnBudgetImpactSummary_WhenBudgetPurposeExistsForContact` — prüft Budget-Impact beim Buchen.
- `StatementDrafts_Book_ShouldNotReturnImpactItems_WhenPurposePatternDoesNotMatch` — prüft Pattern-Miss.
- `StatementDrafts_Book_ShouldReturnImpactItems_WhenPurposePatternRegexMatches` — prüft Regex-Pattern-Match.
- `StatementDrafts_Book_ShouldReturnImpactItems_WhenPurposePatternContainsMatchesCaseInsensitive` — prüft case-insensitive Contains-Match.
- `StatementDrafts_BookEntry_ShouldReturnBudgetImpactSummary_WhenBudgetPurposeExistsForContact` — prüft Budget-Impact beim Einzelbuchen.
- `StatementDrafts_Book_ShouldReturnNullBudgetImpactSummary_WhenNoBudgetPurposeExists` — prüft neutrales Verhalten ohne Budget-Zuordnung.

### `ApiClientPostingsTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientPostingsTests.cs`
- `Postings_GroupLinks_Should_Return_Null_For_Empty` — prüft Gruppenlink-Fall ohne Daten.
- `Postings_List_Endpoints_Should_Not_Fail` — prüft Erreichbarkeit der Posting-Listenendpunkte.

### `ApiClientPostingsReversalTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientPostingsReversalTests.cs`
- `ReversePosting_ShouldReturn200_WithReversalResult_ForOwner` — Happy Path Reversal.
- `ReversePosting_ShouldReturn409_WhenAlreadyReversed` — Idempotenz-Guard.
- `ReversePosting_ShouldReturn400_WhenUserIsNotOwner` — Cross-User-Verbot (aktuelles Mapping auf 400).
- `ReversePosting_ShouldReturn400_WhenPostingNotFound` — Nicht vorhandene Posting-ID.
- `ValidateReversal_ShouldReturn200WithIsValidTrue_ForReversiblePosting` — Validierungsendpunkt.
- `ReversePosting_ShouldReturn401_WhenNotAuthenticated` — Authentifizierungspflicht.

### `ApiClientReportsTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientReportsTests.cs`
- `Reports_Aggregates_And_Favorites_Flow` — Aggregatabfrage plus Favorite-CRUD.

### `ApiClientHomeKpisTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientHomeKpisTests.cs`
- `HomeKpis_List_Create_Update_Delete_Flow` — Home-KPI-CRUD-Flow.

### `HomeViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/HomeViewModelTests.cs`
- `ProcessMassImportSelectionAsync_ShouldOpenPendingDialog_WhenConfirmationIsRequired` — Pending-Dialog bei Analyseergebnis.
- `ConfirmMassImportAsync_ShouldSubmitDecisionsAndApplyExecutionResult` — finale Ausführung nach Benutzerentscheidung.
- `ProcessMassImportSelectionAsync_ShouldForceExcludeUnknownType_AndIgnoreManualSelection` — Sperrt nicht selektierbare Dateitypen.

### `ReportDashboardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs`
- `LoadAsync_ReturnsPoints` — Aggregat-Ladevorgang.
- `SaveUpdateDelete_Favorites_Roundtrip` — Favorite-CRUD im ViewModel.
- `GetChartByPeriod_ComputesSums_PerMonth` — Chart-Summenbildung.
- `Totals_And_ColumnVisibility_Work` — Summen und Spaltensichtbarkeit.
- `IsNegative_MarksZeroWithNegativeBaselines` / `IsNegative_Works` — Negativ-Heuristik.
- `PerType_Children_When_IncludeCategory_Multi` — Hierarchie/Child-Logik.

## Hilfsmethoden

### `ApiClientAuthTests`
- `CreateClient` — erstellt `FinanceManager.Shared.ApiClient` auf Basis von `TestWebApplicationFactory`.

### `ApiClientContactsTests`
- `CreateClient` — erstellt testbaren API-Client.
- `EnsureAuthenticatedAsync` — registriert Testnutzer.
- `CreateDraftWithSingleEntryAsync` — legt Konto + Draft + Entry für Parent-Linking-Tests an.

### `ApiClientStatementDraftsTests`
- `CreateClient` — erstellt testbaren API-Client.
- `EnsureAuthenticatedAsync` — registriert Testnutzer.

### `ApiClientPostingsReversalTests`
- `CreateClients` — liefert typed `ApiClient` plus rohen `HttpClient` für Statuscode-Checks.
- `RegisterUserAsync` — registriert Nutzer und setzt Auth-Cookie.
- `BookPostingViaStatementAsync` — führt Import->Kontaktzuordnung->Buchung aus und erzeugt reversierbares Posting.

### `HomeViewModelTests`
- `CreateVm` — erstellt `HomeViewModel` mit gemocktem `IApiClient`.
- `InvokeProcessMassImportSelectionAsync` — ruft private Methode per Reflection auf.
- `FakeBrowserFile` — Test-Implementierung von `IBrowserFile` für Uploadtests.

### `ReportDashboardViewModelTests`
- `CreateVm` — erstellt `ReportDashboardViewModel` mit gemocktem `IApiClient`.
- `CreatePoints` — erzeugt Testdaten (`ReportAggregatePointDto`) für Aggregat-/Chart-Tests.

### `BudgetReportViewModelIntegrationTests`
Datei: `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`
- `CreateAuthenticatedApiClientAsync` — erstellt authentifizierten API-Client.
- `EnsureAccountAsync` — stellt Testkonto sicher.
- `CreateViewModel` — baut `BudgetReportViewModel` mit injiziertem API-Client.
- `AddEntryAsync` — legt Draft-Eintrag an, aktualisiert Core-Daten und setzt Kontakt.

Querverweise:
- Alle `ApiClient*`-Integrationstests nutzen `TestWebApplicationFactory` als gemeinsame Testinfrastruktur.
- `HomeViewModelTests` testen direkt Methoden aus `HomeViewModel` (wird in `Home.razor` verwendet).
- `ReportDashboardViewModelTests` testen direkt Methoden aus `ReportDashboardViewModel` (wird in `ReportDashboard.razor` verwendet).
