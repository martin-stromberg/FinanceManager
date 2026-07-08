# Tests – Verloren gegangene Ribbon-Aktionen in den Einstellungen

## Testklassen

### `SetupCardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`

- `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` — Prüft, dass `LoadAsync` ein `UiActionRequested`-Event mit Aktion `"EmbeddedPanel"` und einer `EmbeddedPanelSpec` auslöst, die `SetupPanel` als Komponente und `AfterRibbon` als Position enthält.
- `TryGetSectionComponentType_Profile_ReturnsExpectedComponent` — Prüft, dass `TryGetSectionComponentType("profile", ...)` die Komponente `SetupProfileTab` zurückliefert.
- `CreateSectionViewModel_Profile_CreatesExpectedViewModel` — Prüft, dass `CreateSectionViewModel("profile", sp)` eine Instanz vom Typ `SetupProfileViewModel` erzeugt.

> **Lücke:** Es existiert **kein Test**, der prüft, ob die Ribbon-Aktionen der Section-ViewModels (`SetupBackupsViewModel`, `SetupNotificationsViewModel`, `SetupProfileViewModel`, `SetupStatementsViewModel`) über `SetupCardViewModel.GetRibbonRegisters()` aggregiert werden.

---

### `SetupBackupsViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupBackupsViewModelTests.cs`

- `Initialize_Loads_List` — Prüft, dass `LoadBackupsAsync()` die Backup-Liste korrekt befüllt.
- `Create_Inserts_Item_And_Delete_Removes` — Prüft, dass `CreateAsync()` ein Item einfügt und `DeleteAsync()` es wieder entfernt.
- `StartApply_Sets_Flag_On_Success` — Prüft, dass `StartApplyAsync()` bei Erfolg `HasActiveRestore = true` setzt.

---

### `SetupNotificationsViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupNotificationsViewModelTests.cs`

- `Initialize_Loads_Settings_And_Subdivisions` — Prüft, dass `LoadAsync()` Einstellungen und Feiertagsunterteilungen korrekt lädt.
- `ProviderChange_Memory_Clears_Subdivision_And_Dirty` — Prüft, dass der Wechsel auf `"Memory"` die Unterteilung löscht und `Dirty` setzt.
- `Save_Sets_SavedOk_And_Resets_Dirty` — Prüft, dass `SaveAsync()` die API aufruft, `SavedOk = true` setzt und `Dirty` zurückgesetzt wird.

---

### `SetupProfileViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupProfileViewModelTests.cs`

- `Initialize_Loads_Profile` — Prüft, dass `LoadAsync()` Profildaten korrekt lädt und `Dirty = false` gesetzt ist.
- `Save_Updates_State_And_Resets_Flags_On_Success` — Prüft, dass `SaveAsync()` nach Änderungen `SavedOk = true` setzt und `KeyInput` leert.
- `ClearKey_Sets_Dirty_And_Save_Sends_ClearFlag` — Prüft, dass `ClearKey()` `Dirty` setzt und `SaveAsync()` `ClearAlphaVantageApiKey = true` in der API-Anfrage übergibt.
- `SetDetected_Updates_Model_And_Dirty` — Prüft, dass `SetDetectedTimezone()` Sprache/Zeitzone ins Modell schreibt und `Dirty` setzt.

---

### `SetupImportSplitViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupImportSplitViewModelTests.cs`

(Testet `SetupStatementsViewModel`)

- `Initialize_Loads_Settings` — Prüft, dass `LoadAsync()` den Import-Split-Modus korrekt lädt.
- `Validate_Disallows_Invalid_Combinations` — Prüft mehrere Validierungsregeln (MinMax, MonthlyOrFixed-Schwellenwert).
- `Save_Sets_SavedOk_And_Resets_Dirty` — Prüft, dass `SaveAsync()` nach Änderungen die API aufruft und `SavedOk = true` / `Dirty = false` setzt.
- `Save_ShouldPersistMassImportDialogPolicy` — Prüft, dass `MassImportDialogPolicy` korrekt in der API-Anfrage übermittelt wird.

## Hilfsmethoden

### `SetupBackupsViewModelTests` (inline)
- `CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)` — Erstellt einen `HttpClient` mit einem `DelegateHandler`.
- `CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)` — Erstellt einen `FinanceManager.Shared.ApiClient` über einen Delegate-Handler.
- `CreateSp(IApiClient apiClient)` — Baut einen `IServiceProvider` mit `ICurrentUserService`- und `IApiClient`-Singleton auf.
- `ListJson(params object[] items)` — Serialisiert Objekte als JSON-Array.

### `SetupNotificationsViewModelTests` (inline)
- `CreateVm()` — Gibt `(SetupNotificationsViewModel vm, Mock<IApiClient> apiMock)` zurück; baut `ServiceCollection` mit `TestCurrentUserService` und Moq-Mock auf.

### `SetupImportSplitViewModelTests` (inline)
- `CreateSp(IApiClient api)` — Baut `ServiceCollection` mit `TestCurrentUserService` und übergebenem `IApiClient` auf.
