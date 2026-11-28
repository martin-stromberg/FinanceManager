# Usability-Review — Schritt 4 „Neu"-Aktionen absichern (#605, #609)

**Reviewer:** unabhängiger Review-Subagent (Read-only).

## Befunde und Auflösung

| # | Schwere | Befund | Auflösung |
|---|---------|--------|-----------|
| B1 | mittel | Tooltip auf disabled-Button unsichtbar für Touch-/Tastatur-Nutzer (kein Hover, Firefox feuert keine Mouse-Events auf disabled Controls, `aria-label` ohne Begründung) | Muted Hint-Record in `BudgetRuleListViewModel.BuildRecords` bei fehlendem Kontext — Muster aus `McpAccessListViewModel`; Begründung jetzt geräteunabhängig sichtbar; Unit-Test `NewRule_NoContext_ShowsHintRecord` + E2E-Assertion auf sichtbaren Hint-Text |
| B2 | mittel | Asymmetrie: neue Purpose-Karte zeigt Regeln-Sektion disabled, neue Kategorie-Karte zeigte gar keine Regeln-Sektion | `BudgetCategoryCardViewModel` erzeugt `EmbeddedList` jetzt auch bei `Guid.Empty` (`InitializeForCategoryAsync(Guid.Empty)`) → identisches disabled+Hint-Verhalten; E2E-Test `BudgetCategoryCard_Unsaved_NewRuleDisabled` |
| B3 | niedrig | Doppelter Leer-Zustand: Hint-Record + „Keine Einträge" darunter | `GenericListPage` unterdrückt `EmptyText`/`EndOfListText`, wenn Records gerendert wurden ohne Items — Screenshot verifiziert: nur noch Hint-Zeile |
| B4 | niedrig | Wizard-Disabled-State ohne Weiterführung (kein Link zu KI-Sicherheit) | Bewusst zurückgestellt — der Pfad ist ein seltener Defense-in-depth-Fallback (Race zwischen Listen-Load und Wizard-Öffnen); der Hint nennt das Ziel bereits |
| B5 | info | Delete-Button auf neuen Karten disabled ohne Tooltip — bestehende Inkonsistenz, nicht commit-spezifisch | außerhalb des Scopes |

## Positiv vermerkt

- Disabled+Tooltip folgt etablierter Konvention (`SavingsPlanCardViewModel` `ActualReport_Disabled_Linked`).
- Resx-Texte de+en verständlich und handlungsorientiert.
- Selbstauflösender Zustand: Speichern navigiert auf `…/{id}` → Liste + Button werden automatisch aktiv.
- Graceful Degradation: die in disabled-Liste enabled bleibenden Links (Genehmigungen/Protokoll/KI-Sicherheit) führen in eigene Hint-Views, nicht in Fehlerseiten.

## Ergebnis

Alle mittleren Befunde umgesetzt; B4 bewusst zurückgestellt (seltener Fallback-Pfad). Abnahme-Screenshots zeigen das vereinheitlichte Pattern auf beiden Karten und die bereinigte MCP-Liste.
