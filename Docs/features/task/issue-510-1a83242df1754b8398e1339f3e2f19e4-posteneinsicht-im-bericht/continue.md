# Offene Aufgaben

Erstellt am: 2026-11-13
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] [Niedrig] `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs:347-355` — Netting-Mitglieder werden auf die Ziel-Securities beschränkt, während die Aggregation Gruppenmitglieder über alle besessenen Securities netted. Bei Cross-Security-Gruppen kann die Overlay-Summe vom Zellwert abweichen. Optional: Mitglieder analog der Aggregation auf `ownedIds` laden und die Zellzuordnung über die Anker-Security steuern oder die Abweichung bewusst dokumentieren.
- [ ] [Hinweis] `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs:336-341` — Gruppen mit mehreren Dividenden-Ankern in verschiedenen Perioden werden im Overlay je Anker-Zelle vollständig gelistet; die Aggregation attribuiert den Nettobetrag nur einmal auf die erste Ankerperiode. Betrifft nur handgearbeitete Multi-Anker-Gruppen.
- [ ] [Hinweis] `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs:455-468` vs. `ReportAggregationService.cs:973-983` — Kategorie-/Entitätsfilter sind im Netting-Pfad nicht deckungsgleich; gleichzeitig gesetzte `SecurityIds`- und `SecurityCategoryIds`-Filter sind über die UI nicht erreichbar, aber für manuelle Requests möglich.
- [x] [Hinweis] `Docs/features/task/issue-510-1a83242df1754b8398e1339f3e2f19e4-posteneinsicht-im-bericht/plan.md` und `review.md` — Die Feature-Dokumente beschreiben teilweise noch den Vertrag vor Iteration 3 (`Take`, `CompareYear`, `TotalAmount`, Testzahlen). In der Dokumentationsphase nachgezogen.

## Fehlgeschlagene Tests

Keine.
