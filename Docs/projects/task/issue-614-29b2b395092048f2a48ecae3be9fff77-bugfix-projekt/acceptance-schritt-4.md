# Abnahme Schritt 4 — „Neu"-Aktionen bei ungültigem Kontext absichern (#605, #609)

**Schritt-Branch:** `task/issue-614-…-schritt-4-neu-aktionen-absichern`
**Merge:** `9fd0f372` in den Projekt-Basisbranch

## Umgesetzte Issues

- **#609 — NewRule auf ungespeicherter Budgetzweck-Karte warf `BudgetCategoryId!.Value`-NullRef.**
  `BudgetRuleListViewModel`: `HasContext`-Helper; Ribbon-Aktion „Neu" disabled + Tooltip `Budget_Rule_New_SaveFirst`; Payload-Callback null-safe; muted Hint-Record in der Liste. `BudgetCategoryCardViewModel` zeigt auf neuen Karten dieselbe abgesicherte Regeln-Sektion (Review-Vereinheitlichung).
- **#605 — „Neu"-Zugangswizard bei deaktivierter MCP-Integration erreichbar.**
  `McpAccessListViewModel`: `Disabled = IsMcpDisabled` + Tooltip; `RequestCreateWizard` early-return; `McpAccessCreateWizard` prüft den Status selbst (Defense in depth) und zeigt Fehler bei Status-Call-Ausfall statt halb-leerem Wizard. `GenericListPage` unterdrückt „Keine Einträge"/„Ende der Liste" unter Hint-Records.

## Nachweise

- **E2E (4 Tests, alle grün):** `NewActionGuardsPlaywrightTests` — unsaved Purpose/Kategorie disabled+Hint, saved Purpose enabled, MCP-Liste disabled+Hint ohne Wizard-Overlay. Screenshots: `s4-budget-purpose-newrule-disabled.png`, `s4-budget-category-newrule-disabled.png`, `s4-budget-purpose-newrule-enabled.png`, `s4-mcp-accesses-new-disabled.png` — alle sichtgeprüft, im SCM-attachmentsPath abgelegt.
- **Unit/Komponente (14 Tests):** `BudgetRuleListNewActionTests` (4), `McpAccessListNewActionTests` (4), `McpAccessCreateWizardTests` bUnit (4) + Fehlerpfad-Tests.

## Reviews

- Plan-Check: vollständig/korrekt; `Hidden`-Faktenfehler im Inventory korrigiert, Wizard-Disabled-Test + E2E nachgezogen.
- Code-Review: keine kritischen Befunde; M1 (fail-open Wizard → `_error`), L1 (`HasContext`, `!` entfernt), L3/L4 (Test-Schärfungen) umgesetzt.
- Usability-Review: B1 (Tooltip unsichtbar → Hint-Record), B2 (Purpose/Kategorie-Asymmetrie), B3 (doppelter Leer-Text) umgesetzt; B4 bewusst zurückgestellt.

## Ergebnis

Schritt 4 abgenommen und gemergt. Projektfortschritt: **4 / 5**.
