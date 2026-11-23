# Testergebnisse — Schritt 2 (#560): includeStatistics-Aggregat

## Ausgeführte Tests

### Unit-Tests (`FinanceManager.Tests`)

```
dotnet test FinanceManager.Tests --filter "FullyQualifiedName~FmSecuritiesAnalysis"
```

Ergebnis: **17/17 bestanden** (davon 6 neue Statistik-Tests, 4 Preisfehler-Tests aus
Schritt 1 unverändert grün, 7 Bestandstests).

Neue Klasse `FmSecuritiesAnalysisStatisticsTests` — abgedeckt:

- Opt-out: `statistics: null` ohne `includeStatistics`
- `securityCount`/`positionCount`, `byCurrency`-Gruppierung mit `marketValue = openQuantity × latestPrice`
- Währungsreinheit: `totalMarketValue`/`totalCurrencyCode` `null` bei Mischwährung; Einzelwährung → Gesamtwert
- `positionsWithoutPrice` (Anzahl + IDs, nicht in Marktwerten)
- `byCategory`: `untrusted_data.name`, `marketValue`/`currencyCode` `null` bei Mischwährung
- `skip`/`take` wirken nicht auf die Statistik; Kurs-Batch über volle sichtbare ID-Menge (Verify: 1 Aufruf, 2 IDs)
- Record-Scope-Filter wirkt auf die Statistik
- `securityId`-Pfad: Statistik über genau ein Papier

### Integrationstests (`FinanceManager.Tests.Integration`)

```
dotnet test FinanceManager.Tests.Integration --filter "FullyQualifiedName~McpSecuritiesAnalysisTests"
```

Ergebnis: **3/3 bestanden** — neuer Wire-Level-Test
`ToolsCall_SecuritiesAnalysis_IncludeStatistics_ReturnsAggregate` (3 Positionen in 2
Währungen, davon eine ohne Kurs: Zählung, `byCurrency`, `positionsWithoutPrice`,
`totalMarketValue = null` bei Mischwährung).

## Befund während der Testentwicklung

Der Wire-Level-Test deckte einen vorbestehenden Handler-Fehler auf: `onlyActive` nicht
gesetzt führte zum doppelten Zählen aktiver Papiere (`ListAsync(owner,false)` ist der
ungefilterte „alle"-Aufruf, kein „nur inaktiv"); `onlyActive:false` lieferte alle statt
inaktiver Papiere. Behoben in `FmSecuritiesAnalysisHandler` (in-memory-`!IsActive`-Filter);
dokumentiert in `business-rules.md`.

## Build

`dotnet build` der betroffenen Projekte — 0 Fehler (nur bekannte Warnungen).

## Abnahme

Kein UI-Anteil → kein Screenshot-/Usability-Nachweis. Akzeptanz über Wire-Level-
Integrationstest (Happy Path mit Mischwährung + unbewerteter Position) sowie Unit-Tests
für Opt-out, Filter- und Paging-Semantik.
