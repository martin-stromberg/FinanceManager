# Abnahme — Schritt 4 „Neu"-Aktionen absichern (#605, #609)

## E2E-Abnahme (Playwright, `NewActionGuardsPlaywrightTests`)

| Test | Nachweis |
|------|----------|
| `BudgetPurposeCard_Unsaved_NewRuleDisabled` — `card/budget/purposes/new`: `button#NewRule` disabled + Tooltip-Text, Hint-Zeile in der eingebetteten Liste, Force-Klick ohne Navigation | `s4-budget-purpose-newrule-disabled.png` |
| `BudgetCategoryCard_Unsaved_NewRuleDisabled` — `card/budget/categories/new`: identisches Pattern (Regeln-Sektion mit disabled NewRule + Hint) | `s4-budget-category-newrule-disabled.png` |
| `BudgetPurposeCard_Saved_NewRuleEnabled` — gespeicherter Budgetzweck: `button#NewRule` enabled | `s4-budget-purpose-newrule-enabled.png` |
| `McpAccessList_McpDisabled_NewDisabled` — `list/mcp-accesses` mit abgeschalteter MCP-Integration: `button#New` disabled + Tooltip, Hint-Zeile, Force-Klick öffnet keinen Wizard | `s4-mcp-accesses-new-disabled.png` |

Alle 4 Screenshots einzeln sichtgeprüft; Ablage in `attachmentsPath` (screenshots/).

## Unit-/Komponententests

- `BudgetRuleListNewActionTests` (4): disabled bei fehlendem Kontext, kein Throw bei unsaved Purpose (Regression #609), purpose-/category-Payload, Hint-Record ohne API-Aufrufe.
- `McpAccessListNewActionTests` (4): disabled New + `RequestCreateWizard`-No-Op bei `IsMcpDisabled` (#605), enabled-Pfad mit `UiOverlaySpec`, Status-Call-Fehler best-effort, `Err_Mcp_Disabled`-Fallback.
- `McpAccessCreateWizardTests` (bUnit, 4): Hint statt Steps bei disabled, Cancel→OverlayClose, Steps bei enabled, Fehleranzeige bei Status-Call-Fehler.

## Issues-Abdeckung

- **#609:** `BudgetCategoryId!.Value`-Throw auf ungespeicherter Purpose-Karte eliminiert — Action disabled + null-safes Payload, zusätzlich gleiches Pattern auf der Kategorie-Karte.
- **#605:** „Neu" bei deaktivierter MCP-Integration disabled (Ribbon + `RequestCreateWizard`-Guard + Wizard-Statuscheck als dritte Verteidigungslinie).
