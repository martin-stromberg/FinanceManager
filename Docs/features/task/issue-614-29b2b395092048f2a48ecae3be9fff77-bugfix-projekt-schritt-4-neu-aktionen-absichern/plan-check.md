# Plan-Check — Schritt 4 „Neu"-Aktionen absichern (#605, #609)

**Prüfmethode:** unabhängiger Review-Subagent (Read-only) — Plan gegen Codebestand.
**Gesamteinstufung:** Plan vollständig und korrekt.

## Verifizierte Planannahmen

- Disabled-Aktionen blocken den Callback (`Ribbon.razor`: `if (item.Disabled) return`, `disabled`-/`aria-disabled`-Attribute; identisch im Mobile-Menü; Mobile-Shortcuts filtern `!item.Disabled`).
- Kein stale Ribbon: `TabsToRender` wird bei jedem Render neu aufgebaut; `IsMcpDisabled`-Änderung nach `LoadAsync` löst `StateChanged` → Re-Render aus.
- Wizard-Init läuft im Overlay-Kontext (`DynamicComponent` in `OverlayHost`/`ListPage`); `OverlayClose`-CascadingValue vorhanden.
- Embedded-List-Ribbon auf `card/budget/purposes/new`: `EmbeddedList` wird vor der `Guid.Empty`-Verzweigung via `CreateSubViewModel` erzeugt → Kind-VM registriert → Ribbon-Aggregation liefert `button#NewRule` (eindeutiger Selektor).
- Einziger `NewRule`-Erzeuger (`BudgetRuleListViewModel`) und einziger Payload-Consumer (`CardPage.razor` `case "NewRule"`).
- `RequestCreateWizard`-Caller vollständig: Ribbon-Callback + `SetupMcpViewModel.RequestNewAccess` (Setup-Tab zeigt „Neu" ohnehin nur bei `McpEnabled`; Guard deckt stale Flag ab).
- `Budget_Rule_New_SaveFirst` in allen drei resx-Dateien vorhanden; `Mcp_Disabled_Hint`, `Ribbon_Group_Rules` vorhanden.
- `card/budget/categories/new` unbetroffen: `BudgetCategoryCardViewModel` setzt `EmbeddedList = null` bei `Guid.Empty` — bewusste Entscheidung (kein leeres Regeln-Ribbon).

## Befunde und Auflösung

| # | Befund | Auflösung |
|---|--------|-----------|
| 1 | Faktenfehler inventory.md: `Hidden` existiert doch (`RibbonModels.cs:75`, produktiv genutzt) — `Disabled` bleibt die bessere UX-Wahl | inventory.md korrigiert |
| 2 | Wizard-`Disabled`-Zweig ungetestet | bUnit-Testklasse `McpAccessCreateWizardTests` ergänzt (Hint statt Steps, kein Levels/Options-Call, Cancel→OverlayClose) |
| 3 | E2E fehlte | `NewActionGuardsPlaywrightTests` ergänzt: unsaved purpose → `NewRule` disabled+Tooltip; saved purpose → enabled; `list/mcp-accesses` → `New` disabled+Hint (MCP im Teststandard aus) |
| 4 | Minor: `SaveAsync` nutzt `new BudgetRuleListViewModel` statt `CreateSubViewModel` — funktional irrelevant (`??=` greift nie, LoadAsync erzeugt Instanz immer zuerst) | bewusst nicht geändert (kein Verhaltensunterschied) |
| 5 | MCP-Settings-Endpunkt ist `PUT /api/mcp-audit/settings` (admin) — für E2E nicht nötig, da Testumgebung standardmäßig disabled | dokumentiert |

## Ergebnis

Alle Lücken geschlossen. Tests: 5 VM-Tests (2 Klassen) + 3 bUnit-Tests + 3 E2E-Tests — alle grün; 3 Abnahme-Screenshots sichtgeprüft.
