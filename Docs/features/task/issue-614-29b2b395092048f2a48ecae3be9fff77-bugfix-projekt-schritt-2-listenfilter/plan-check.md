# Planprüfung — Schritt 2: Listenfilter

Prüfung durch Unteragent (read-only) gegen requirement.md, inventory.md, plan.md und Ist-Code.

## Gesamturteil: solide, mit kleineren Lücken → geschlossen

Der Plan ist korrekt und gegen den Code verifiziert. Gefundene Lücken wurden behoben:

| # | Finding | Schwere | Auflösung |
|---|---------|---------|-----------|
| 1 | `ArchivedUtc` nicht berücksichtigt — archivierte Recurring-Pläne träfen bei offenem `to`/Zukunftsranges | medium | `upper = min(to, ArchivedUtc)` als Obergrenze; `!to → true` nur für aktive Pläne; Test `Range_Recurring_Archived_BoundedByArchiveDate` |
| 2 | Rückwärts-Rundreise `Add(Sub(d))` bei Monatsende-Drift nicht exakt — rekonstruierte Vergangenheits-Fälligkeiten können um ±1–2 Tage abweichen | medium | Als akzeptierte Semantik in requirement.md + plan.md dokumentiert; Test `Range_Recurring_MonthEndDrift_ReconstructedSeries` |
| 3 | Ungültiger `Interval`-Enum-Wert → Exception im Where → ganze Liste leer | low | Guard `IntervalMonths(plan.Interval) <= 0` (VM-Helfer liefert 0 für unbekannt) → Einzeldatum-Fallback |
| 4 | Testlücken: nur-`to`, inklusive Recurring-Grenzen, `TargetDate < CreatedUtc`, `Interval = null`, archiviert | low | 6 Tests ergänzt |
| 5 | Plan-Pseudocode ≠ Implementierung (effizienterer Zwei-Schleifen-Ansatz) | low | plan.md nachgezogen |

## Verifizierte Kernpunkte

- Recurring-Logik: Vorwärts-/Rückwärts-Schleifen decken `{TargetDate ± n·Interval}` lückenlos ab
  (TargetDate < from, TargetDate > to, to < floor, target < floor geprüft).
- Cache-Konsistenz: `_intervalByPlan`/`_typeByPlan`/`_analysisByPlan` nur für gefilterte Pläne.
- `AllowRangeFiltering => false` entspricht der Bestandskonvention (Getter-Override, Basis-Setter vererbt).
- UND-Verknüpfung Suche/Range/OnlyActive; `ToggleActive` ohne Verhaltensbruch.
- Keine neuen Enum-Member, keine persistierten Felder, keine resx-Änderungen nötig.

## Bekannte, akzeptierte Einschränkungen

- Kein Debounce der Suche (Generisches List-Page-Verhalten unverändert; Filterung vor Analyse-Aufrufen).
- `_skip`/`PageSize` ist Altbestand-Totcode (`CanLoadMore` immer false) — unverändert gelassen.
