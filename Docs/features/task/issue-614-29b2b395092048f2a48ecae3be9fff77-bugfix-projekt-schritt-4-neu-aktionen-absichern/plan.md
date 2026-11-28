# Plan — Schritt 4: „Neu"-Aktionen absichern (#605, #609)

## Änderungen

### 1. `BudgetRuleListViewModel` (#609)

- Neuer privater Ausdruck im Ribbon-Aufbau: `var noContext = (BudgetPurposeId ?? Guid.Empty) == Guid.Empty && (BudgetCategoryId ?? Guid.Empty) == Guid.Empty;`
- `NewRule`-Aktion: `Disabled: noContext`, `Tooltip: noContext ? localizer["Budget_Rule_New_SaveFirst"].Value : null`.
- Callback payload null-sicher + Guard:
  ```csharp
  var payload = BudgetPurposeId is { } pid && pid != Guid.Empty
      ? $"purpose,{pid}"
      : BudgetCategoryId is { } cid && cid != Guid.Empty ? $"category,{cid}" : null;
  if (payload is null) return Task.CompletedTask;
  RaiseUiActionRequested("NewRule", payload);
  ```

### 2. `McpAccessListViewModel` (#605)

- Ribbon „New": `Disabled: IsMcpDisabled`, `Tooltip: IsMcpDisabled ? localizer["Mcp_Disabled_Hint"].Value : null` (`MobileShortcut` bleibt).
- `RequestCreateWizard()`: `if (IsMcpDisabled) return;` vor dem Raise — deckt SetupMcpTab-Pfad ab.

### 3. `McpAccessCreateWizard.razor` (#605, Defense in depth)

- Neues Feld `_disabled`; `OnInitializedAsync` fragt zuerst `Api.Mcp_GetStatusAsync()` → `!Enabled` → `_disabled = true` + `return` (keine Level-/Option-Loads).
- Markup: `_disabled` → Hinweis `Mcp_Disabled_Hint` + nur Abbrechen-Button; sonst unverändertes 3-Schritt-UI.

### 4. Ressourcen

- Neuer Key `Budget_Rule_New_SaveFirst` in `Pages.de.resx` („Budgetzweck bzw. Budgetkategorie zuerst speichern"), `Pages.en.resx` („Save the budget purpose or category first"), `Pages.resx` (en-Text).

## Tests

- `FinanceManager.Tests/ViewModels/BudgetRuleListNewActionTests.cs`:
  - `InitializeForPurposeAsync(Guid.Empty)` → `NewRule.Disabled == true`, Callback → kein Raise, keine Exception.
  - `InitializeForPurposeAsync(gültigeId)` → `Disabled == false`, Callback → `UiActionRequested("NewRule", "purpose,{id}")`.
  - `InitializeForCategoryAsync(gültigeId)` → `Disabled == false`, Callback → `"category,{id}"`.
- `FinanceManager.Tests/ViewModels/McpAccessListNewActionTests.cs`:
  - `LoadAsync` mit Status `Enabled=false` → `IsMcpDisabled`, Ribbon „New" `Disabled == true`, `RequestCreateWizard()` → kein `UiActionRequested`; Listenhinweis-Record unverändert.
  - `Enabled=true` → „New" enabled, `RequestCreateWizard()` → Raise mit `UiOverlaySpec(McpAccessCreateWizard)`.

## E2E-Abnahme

- `FinanceManager.Tests.E2E`: Neuer Test `NewActionGuardsPlaywrightTests` — seeded User, Budgetzweck-Neuanlage (`card/budget/purposes/new`): „Regeln"-Gruppe/„Neu" der eingebetteten Liste disabled (oder nicht klickbar) → Screenshot; Klick wirft keine Exception (Seite lebt weiter).
- MCP-disabled-Pfad: Deaktivieren der KI-Integration im Test bedingt Admin-Setup-Mutation — pragmatisch: Ribbon-Disabled-Zustand per Unit-Test abgedeckt; E2E optional, wenn Status-Umschaltung via API verfügbar (`PUT api/mcp-settings`?). Prüfen, sonst Screenshot nur für #609.

## Reihenfolge

1. Implementierung #609 (klein, isoliert)
2. Implementierung #605 (VM + Wizard)
3. resx-Key
4. Unit-Tests
5. E2E-Test + Screenshots
6. Reviews → Findings → Merge
