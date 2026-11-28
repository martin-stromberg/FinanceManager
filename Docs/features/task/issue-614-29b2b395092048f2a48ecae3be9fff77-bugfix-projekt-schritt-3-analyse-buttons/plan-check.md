# Plan-Check — Schritt 3 (analyse-buttons, #603)

**Reviewer:** Review-Subagent (frischer Kontext)
**Ergebnis:** Plan technisch korrekt und gegen den Code verifiziert — Einstufung „weitgehend vollständig"; gefundene Marginalien wurden übernommen.

## Befunde und Auflösung

| Befund | Auflösung |
|---|---|
| `ShiftRange` verankerte fehlende Range-Grenzen unabhängig auf „heute" → einseitig gesetzte Grenze konnte `Von > Bis` erzeugen (semantisch absurder Riesenzeitraum) | **Behoben:** fehlende Grenze wird aus dem Kalendermonat der gesetzten Grenze abgeleitet (`BudgetPurposeListViewModel.ShiftRange`), Range kann nicht invertieren |
| Plan-Behauptung „Von>Bis kann nicht entstehen" | Behauptung ist jetzt durch die Anker-Ableitung tatsächlich erfüllt |
| `Docs/help/benutzeroberflaeche/business-rules.md` dokumentierte die Übersicht noch mit 4 alten Buttons | **Behoben:** Zeile auf 6 Buttons (5 einheitliche + Aktuelles Jahr) aktualisiert |
| Fehlende Testfälle: Jahreswechsel Dez→Jan, 29.02.-Clamping bei PrevYear, einseitige Range-Bounds | **Behoben:** `NextMonth_OnDecember_RollsIntoJanuary`, `PrevYear_OnLeapDayRange_ClampsToFebruary28`, `PrevMonth_OnlyFromSet_AnchorsToBoundsMonth` ergänzt |
| E2E-Selektoren im Plan unnötig komplex (Label/`title`) — `button#<ActionId>` ist etabliertes Muster | **Übernommen:** E2E-Test nutzt `button#PrevMonth` etc., ISO-`yyyy-MM-dd`-Assertion auf `.list-filter-range input` |
| Bericht-Buttons laden direkt (`LoadAsync`), kein `UiActionRequested` — Wait-Strategie asymmetrisch | **Übernommen:** E2E wartet auf `.budget-report-period`-Textänderung (Muster aus `BudgetReportDemoDataE2ETests`) |
| Asymmetrische Null-Bounds dokumentiert in inventory.md (`ClearSearch` statt `ClearFilter`) | Akzeptiert — reine Doku-Ungenauigkeit, `ListPage` behandelt beide Action-Namen im selben Case |

## Bewertete und zur Kenntnis genommene Semantik-Entscheidungen

1. **„Aktuelles Jahr" als 6. Button** — echte Abweichung von der strikten AC-Lesart („dieselben fünf Buttons"), aber begründet (kein Funktionsverlust; die fünf stehen vorn in identischer Reihenfolge). Wird im PR-Text explizit als Entscheidung genannt.
2. **Breitenerhaltung bei Jahresnavigation** — konsistent mit dem Bericht (`Months` bleibt beim Verschieben unverändert).
3. **Monatsende-Snap** — ergänzt: Range, die auf einem Monatsende endet, bleibt nach dem Shift monatsausgerichtet (paritätisch zur Monatsende-Normierung des Berichts).
4. **Bericht-„Aktueller Monat" bei `Months>1`** setzt nur den AsOf-Anker auf heute-Monatsende; das angezeigte Fenster bleibt N Monate. Bereits im requirement.md dokumentiert; wird im PR-Text zur Abnahme gestellt.

## Nachträglich durch Tests gefundene Korrekturen

- **`ShiftRange`: Monatsende-Erhalt** — `AddMonths(-1)` auf Monatsende (30.11.) ergab 30.10. statt 31.10.; monatsalignede Ansicht verlor den letzten Tag. Durch Snap auf `DaysInMonth` des Zielmonats behoben (Unit-Tests rot → grün).
- **E2E-Test `expectedEnd` off-by-one** — `AddMonths(1).AddDays(-1)` lieferte das aktuelle Monatsende; korrigiert auf `AddMonths(2).AddDays(-1)`. Zusätzlich: Retry-Loop wartet jetzt auf Periodentext-*Änderung* statt Blind-Click (verhindert Overshoot durch Doppel-Shifts).

## Status

✅ Plan-Check abgeschlossen, alle Befunde adressiert oder dokumentiert akzeptiert.
