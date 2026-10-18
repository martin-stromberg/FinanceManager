# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Todeskreuz: Kreuzung 50↓200 erkennen, Datum bestimmen, als „Death-Cross-Date" ausgeben | `DeathCrossResult` (CrossDate, DaysSinceCross, Active) im `TrendReversalAnalyzer`; Signal `DeathCross` (Strong); Ausgabe über DTO | `TrendReversalAnalyzerTests_DeathCross` (Datum, kein Signal oberhalb, InsufficientData) | Abgedeckt |
| Bruch der 200-Tage-Linie: Erkennung, Dauer-Bewertung, Klassifikation kurzfristig/strukturell | `LongTermLineBreakResult` mit `ConsecutiveDaysBelow`, `BreakDate`, `StructuralBreakDays`-Schwelle → `ShortTermWeakness`/`StructuralReversal` | `TrendReversalAnalyzerTests_LineBreak` (3 Fälle) | Abgedeckt |
| Marktstruktur-Umkehr: niedrigere Hochs/Tiefs, bärische Struktur, Klassifikationen | `MarketStructureResult` mit `LowerHighs`/`LowerLows`, `EmergingDowntrend`/`TrendLoss`/`SidewaysBearish`/`Intact`/`Unknown` | `TrendReversalAnalyzerTests_MarketStructure` (4 Fälle) | Abgedeckt |
| Indikatorbasierte Signale: MACD bärisches Crossover, RSI negative Divergenz, Momentumverlust | `MacdResult` (EMA12/26/9, CrossoverDate) + `RsiResult` (Wilder-RSI, DivergenceDate) | `TrendReversalAnalyzerTests_Momentum` (4 Fälle) | Abgedeckt |
| Kombination/Bewertung: strukturierte Übersicht, Wahrscheinlichkeit, Trendstatus | `Signals`-Liste (`ReversalSignal`: Type/Strength/Date), `SignalScore`, `TrendStatus` Intact/Weakened/ReversalLikely | `TrendReversalAnalyzerTests_Scoring` (3 Fälle) | Abgedeckt |
| Ausgabe: strukturierte Darstellung mit Bedeutungserklärung; explizite Ausgabe bei keinem Signal; Trendstatus benannt | `TrendReversalDto` mit allen Teil-Records + `Signals`; API-Endpunkt; Widget mit lokalisierten Signal- und Statustexten + Bedeutungspanel. „Kein Signal" ist per API als `Status=Intact` + leere `Signals` explizit ausgegeben; Widget blendet per Anwenderentscheidung bei `Intact` aus | `TrendReversalServiceTests` (DTO/Scope), `TrendReversalDtoEnumMappingTests`, E2E-Widget-Test | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

–

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Wertpapier-Karte zeigt Umkehrsignale + Trendstatus | `SecurityTrendReversalE2ETests` — bärische Kurse → Widget mit Signalliste | Abgedeckt |
| Ausblendeverhalten ohne Signale | `SecurityTrendReversalE2ETests` — Aufwärtstrend → Widget nicht sichtbar | Abgedeckt |
| Benachrichtigungs-Checkbox im Setup umschalten/speichern | `SecurityTrendReversalE2ETests` — Checkbox vorhanden, Wert persistiert | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

–

## Hinweise

- Anwenderentscheidung „Widget nur bei Signalen sichtbar" weicht bewusst von der wörtlichen Ausgabe-Anforderung an die UI ab; die explizite Leerausgabe ist im API-Ergebnis (`Status = Intact`, leere `Signals`) weiterhin strukturiert enthalten.
- Benachrichtigungsfluss spiegelt exakt den Golden-Cross-Zyklus (einmalig pro Status, Reset bei `Intact`), inkl. Benutzereinstellung und resx-Lokalisierung.
- Migration `AddTrendReversal` ist in der Umsetzungsreihenfolge als Schritt 2 mit Voraussetzung `TrendStatus` (Domain) eingeordnet.
