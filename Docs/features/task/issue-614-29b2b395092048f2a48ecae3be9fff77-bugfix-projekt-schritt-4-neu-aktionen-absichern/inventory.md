# Bestandsaufnahme — Schritt 4 (#605, #609)

## Relevante Codestellen

### #605 (KI-Zugänge)

- `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs`
  - `IsMcpDisabled { get; private set; }` (Z. ~58) — wird in `LoadPageAsync` aus `Mcp_GetStatusAsync` (Z. ~71-73) bzw. `Err_Mcp_Disabled`-Fehler (Z. ~95-97) gesetzt.
  - `RequestCreateWizard()` (Z. ~155-162) — ungeschützt; wirft `UiActionRequested("New", UiOverlaySpec(McpAccessCreateWizard))`. Wird auch von `SetupMcpViewModel.RequestNewAccess()` (Z. 199) genutzt → ein Guard hier deckt beide Einstiegspunkte.
  - Ribbon „New" (Z. ~173-185): `Disabled` fest `false`, `MobileShortcut = true`.
- `FinanceManager.Web/Components/Shared/McpAccessCreateWizard.razor`
  - `OnInitializedAsync` (Z. 151-175) lädt Levels + Options ohne Feature-Status-Prüfung.
  - 3-Schritt-UI (Z. 9-13 Steps, 27-124 Content+Buttons); `_step == 2` rendert `McpTokenDisplayPanel`.
- `IApiClient.Mcp_GetStatusAsync` → `McpStatusDto(bool Enabled)` (`FinanceManager.Shared/Dtos/Mcp/McpSettingsDtos.cs:25`).
- Ressource `Mcp_Disabled_Hint` existiert bereits (de/en, wird in `BuildRecords` Z. ~125 genutzt).

### #609 (Budgetzweckregeln)

- `FinanceManager.Web/ViewModels/Budget/BudgetRuleListViewModel.cs`
  - `BudgetPurposeId`/`BudgetCategoryId` (`Guid?`, privat gesetzt via `InitializeForPurposeAsync(Guid)` Z. 41-46 / `InitializeForCategoryAsync(Guid)` Z. 52-57).
  - `LoadPageAsync` bereits abgesichert: leerer Kontext → leere Liste (Z. 68-70).
  - Ribbon `NewRule` (Z. 141-156): Callback wertet `BudgetPurposeId.HasValue && != Empty ? purpose,... : category,{BudgetCategoryId!.Value}` aus → `InvalidOperationException` bei Purpose=Empty & Category=null.
  - `BudgetCategoryCardViewModel` setzt `EmbeddedList = null` bei ungespeicherter Kategorie → dort nicht betroffen.
- `Ribbon.razor`: deaktivierte Aktionen rufen den Callback nicht auf (`Disabled`-Check vor Invoke).
- `CardPage.razor` `NewRule`-Handler erwartet Payload `purpose,{guid}` / `category,{guid}` → Navigation `card/budget/rules/new?prefill=…&back=…` (Schritt-1-Back-Mechanik).

## Konventionen / Infra

- `UiRibbonAction(Id, Label, IconSvg, Size, Disabled, Tooltip, Func<Task>?)` — `Disabled` wird beim Ribbon-Rebuild (StateChanged) neu ausgewertet; `Hidden` existiert ebenfalls als Member (RibbonModels.cs:75), ist aber die schlechtere UX (versteckt statt sichtbar-erklärend per Tooltip) — Variante ist `Disabled`.
- Neue UI-Texte → `Resources/Pages.{de,en}.resx` (+ neutral `Pages.resx`), wie in Schritt 3 (`Ribbon_Tip_*`).
- Tests: VM-Tests nach Muster `BudgetPurposeListAnalysisNavigationTests` (Mock `IApiClient`, `ServiceCollection` mit `ICurrentUserService`/`IStringLocalizer<>`); Ribbon-Aktionen über `GetRibbonRegisters(loc)` → Items → `Disabled`/`Callback`.
- `BaseListViewModel` hostet `IListProvider`; `GetRibbonRegisterDefinition` wird je Render neu aufgerufen.
