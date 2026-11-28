# Umsetzungsplan — Schritt 2 (#560): includeStatistics

1. **Input-Schema + Descriptor**: `includeStatistics` (boolean, optional) ins JSON-Schema; Descriptor-Description um Statistik-Hinweis erweitern (Menge = gefiltert, skip/take-frei; Marktwerte je Währungsgruppe; kein Gesamtwert bei Mehrwährung).
2. **Handler-Reihenfolge**: In `FmSecuritiesAnalysisHandler.InvokeAsync` den Record-Scope-Filter auf die volle fachlich gefilterte Menge anwenden und `skip`/`take` erst danach schneiden (bewusste Korrektur, dokumentiert). Kurs-Batch über die volle sichtbare ID-Menge, wenn `includeStatistics` gesetzt, sonst über die Seite (bisheriges Verhalten).
3. **Aggregation**: Private Methode `BuildStatistics(visible, latestPrices)` im Handler — anonyme Ausgabe analog zum übrigen Contract; Gruppierung in LINQ; `byCategory.Name` aus `SecurityDto.CategoryName` (untrusted). Kein neues Shared-DTO — Entscheidung: Statistik ist tool-interner Contract wie der übrige Output (entspricht Plan-Alternative „Tool-interne Aggregation").
4. **Tests**: Neue Klasse `FmSecuritiesAnalysisStatisticsTests` (eigenes Thema, test-class-structure): Opt-out, securityCount/positionCount, byCurrency-Gruppierung + Währungsreinheit des Totals, byCategory incl. untrusted Name + Mischwährung-null, positionsWithoutPrice, skip/take wirken nicht, Record-Scope wirkt, securityId-Pfad. Wire-Level: `McpSecuritiesAnalysisTests` ergänzen.
5. **Doku**: `Docs/help/mcp-server/api.md`, `business-rules.md`, `beschreibung.md` aktualisieren.
6. **Lifecycle**: Implementierungs-/Code-Review (Subagent), Testlauf, test-results.md, Commit, Merge in Projekt-Basisbranch.

## Risiken
- Reihenfolge-Änderung Filter↔Paging verändert Seiteninhalt bei gesetztem Record-Scope-Filter — gewollt, dokumentiert, per Test abgesichert.
