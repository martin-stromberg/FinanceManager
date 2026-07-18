# Strukturierte Anforderung

## Metadaten

- Aufgaben-ID: `f1d92438-49f5-48b3-b095-a42b1067088c`
- Branch: `task/issue-130-f1d9243849f548b3b095a42b1067088c-fehler-berechnung-der-abweichu`
- Erstellt am: `2026-07-18`
- Thema: Fehlerhafte Berechnung von Budget- und Abweichungswerten im Budgetbericht bei Kategorien mit Einzelpostenplanung

## Ausgangslage

Im Budgetbericht werden Kategoriezeilen inkonsistent berechnet, wenn Budgets nicht direkt auf der Kategorie, sondern bei den zugewiesenen Detailpositionen bzw. Kontakten hinterlegt sind.

Der Budgetwert der Kategoriezeile bleibt in diesem Fall bei `0`, obwohl Detailpositionen darunter eigene Budgetwerte besitzen. Gleichzeitig wird der Istwert der Detailpositionen auf Kategorieebene summiert. Dadurch entsteht eine falsche Abweichung auf Kategorieebene.

## Problem

Die Kategoriezeile aggregiert aktuell Istwerte, aber nicht die Budgetwerte der zugehörigen Detailpositionen. Dadurch werden Budget und Ist auf unterschiedlichen Aggregationsgrundlagen verglichen.

Zusätzlich ist das Vorzeichen der ausgewiesenen Abweichung fachlich falsch und muss negiert werden.

## Beispiel Ist-Verhalten

Kategorie `Unterhaltung & Aktivitäten`:

| Zeile | Budget | Ist | Abweichung |
|-------|--------|-----|------------|
| Unterhaltung & Aktivitäten | 0 EUR | -30 EUR | 30 EUR |
| Fitnessstudio | -15 EUR | -15 EUR | 0 EUR |
| Glücksspiel | -15 EUR | -15 EUR | 0 EUR |
| Streaming | -10 EUR | 0 EUR | -10 EUR |

## Erwartetes Verhalten

Die Budgetwerte der Detailpositionen werden auf den Budgetwert der Kategoriezeile aufaddiert.

Die Abweichung wird mit negiertem Vorzeichen ausgewiesen.

Kategorie `Unterhaltung & Aktivitäten`:

| Zeile | Budget | Ist | Abweichung |
|-------|--------|-----|------------|
| Unterhaltung & Aktivitäten | -40 EUR | -30 EUR | 10 EUR |
| Fitnessstudio | -15 EUR | -15 EUR | 0 EUR |
| Glücksspiel | -15 EUR | -15 EUR | 0 EUR |
| Streaming | -10 EUR | 0 EUR | 10 EUR |

## Fachliche Regeln

- Kategoriezeilen muessen Budgetwerte aus den zugehoerigen Detailpositionen beruecksichtigen.
- Wenn eine Kategorie eigene Budgetwerte und Detailpositionen mit eigenen Budgetwerten besitzt, muss der Kategorie-Budgetwert die relevante Summe fuer die Kategorie darstellen.
- Istwerte werden weiterhin auf Kategorieebene aggregiert.
- Die Abweichung muss konsistent aus Budget und Ist berechnet und mit negiertem Vorzeichen angezeigt werden.
- Detailpositionen behalten ihre eigenen Budget-, Ist- und Abweichungswerte.

## Akzeptanzkriterien

- Eine Kategorie mit Detailpositionen, deren Budgets bei zugewiesenen Kontakten hinterlegt sind, zeigt auf Kategorieebene nicht mehr pauschal `0 EUR` als Budgetwert.
- Der Kategorie-Budgetwert enthaelt die Summe der Budgetwerte der zugehoerigen Detailpositionen.
- Der Kategorie-Istwert bleibt die Summe der Istwerte der zugehoerigen Detailpositionen.
- Die Abweichung wird fuer Kategoriezeilen und Detailpositionen mit negiertem Vorzeichen gegenueber der bisherigen fehlerhaften Anzeige ausgegeben.
- Das Beispiel `Unterhaltung & Aktivitäten` ergibt auf Kategorieebene `Budget -40 EUR`, `Ist -30 EUR`, `Abweichung 10 EUR`.
- Die Detailposition `Streaming` ergibt `Budget -10 EUR`, `Ist 0 EUR`, `Abweichung 10 EUR`.
