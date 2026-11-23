# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Anforderungen (requirement.md / Issue #560)

### Vertrag `statistics`

- [x] `securityCount` = Anzahl sichtbarer Wertpapiere nach `securityId`/`onlyActive`/Record-Scope — `FmSecuritiesAnalysisTool.cs:235` (`visible.Count`).
- [x] `positionCount` = Wertpapiere mit `openQuantity != 0` — `FmSecuritiesAnalysisTool.cs:196` (`(s.OpenQuantity ?? 0m) != 0m`); `null` zählt als keine Position — konsistent, der Service befüllt `OpenQuantity` immer (`SecurityService.cs:271`).
- [x] `positionsWithoutPrice` `{ count, securityIds }` — nur Positionen (`openQuantity != 0`) ohne Eintrag im Kurs-Batch — `FmSecuritiesAnalysisTool.cs:198, 237`; fließen in keinen Marktwert ein.
- [x] `byCurrency` `[{ currencyCode, positionCount, positionsWithoutPrice, marketValue }]` — `FmSecuritiesAnalysisTool.cs:200-209`; `marketValue = Σ openQuantity × Close` über die bewertbaren Positionen der Gruppe (0 bei ausschließlich unbewerteten Positionen).
- [x] `byCategory` `[{ categoryId, positionCount, positionsWithoutPrice, marketValue, currencyCode, untrusted_data.name }]` — `FmSecuritiesAnalysisTool.cs:211-227`; `marketValue`/`currencyCode` nur bei genau einer Währung unter den bewertbaren Positionen der Gruppe, sonst `null` (auch bei null bewertbaren Positionen — s. Hinweis unten).
- [x] `totalMarketValue`/`totalCurrencyCode` nur bei einheitlicher Währung aller bewertbaren Positionen, sonst `null` — `FmSecuritiesAnalysisTool.cs:229-231, 240-241`; kein Mehrwährungs-Gesamtwert.
- [x] `oldestPriceDate`/`newestPriceDate` = Min/Max der verwendeten Kursdaten, `null` ohne bewertbare Position — `FmSecuritiesAnalysisTool.cs:231, 242-243`.
- [x] Opt-in: ohne `includeStatistics` (oder `false`) ist `statistics: null` — `FmSecuritiesAnalysisTool.cs:89, 184`; per Unit-Test (`Invoke_WithoutIncludeStatistics_EmitsNullStatistics`) abgesichert.

### Filter-Semantik

- [x] Statistik über dieselbe Menge wie die Liste: fachlicher Filter (`securityId`-Einzelabruf bzw. `onlyActive`) → Record-Scope-Filter (`McpRecordScopeFilter.ApplyAsync`) → `visible`; `skip`/`take` wirken nicht — `FmSecuritiesAnalysisTool.cs:123-131`.
- [x] Reihenfolge-Korrektur: Record-Scope-Filter läuft jetzt **vor** dem Paging (`visible` → `paged`, `FmSecuritiesAnalysisTool.cs:123-126`); zuvor wurde das Paging-Fenster erst geschnitten und dann gefiltert — Seiten konnten sich nicht füllen und hätten von der Statistik abgewichen.
- [x] `securityId`-Pfad: `paged = visible` (kein `skip`/`take`, wie bisher); Statistik über genau dieses Papier — Unit-Test `Invoke_IncludeStatistics_SecurityId_AggregatesSingleSecurity`.
- [x] Kategoriennamen ausschließlich unter `untrusted_data` — `FmSecuritiesAnalysisTool.cs:225`; Test assertiert zusätzlich das Fehlen eines Top-Level-`name` (`KeyNotFoundException`).
- [x] Kein N+1: ein `GetLatestPricesAsync`-Batch — über `visible` bei Statistik, über `paged` sonst (`FmSecuritiesAnalysisTool.cs:130-131`); per `Verify(...Times.Once)` im Test belegt. Pro-Papier-Kurshistorie (`_prices.ListAsync`) nur über die ausgegebene Seite — unverändertes Altverhalten.

### onlyActive-Korrektur (im Zuge aufgedeckter Bestandsfehler)

- [x] `ListAsync(owner, false)` ist der ungefilterte „alle"-Aufruf — verifiziert an `SecurityService.cs:28-31` (`if (onlyActive) q = q.Where(s => s.IsActive)`).
- [x] Neue Semantik: `null` → alle, `true` → `ListAsync(true)` (aktiv), `false` → `all.Where(!IsActive)` (inaktiv, in-memory) — `FmSecuritiesAnalysisTool.cs:105-116`. Keine Doppelzählung mehr (alt: Merge aktiv + alle); `onlyActive:false` liefert jetzt inaktive statt aller Papiere.
- [x] Wire-Test deckt den `null`-Fall implizit ab: `securityCount == 3` wäre unter dem alten Merge 6 gewesen.

### Tests

- [x] 6 Unit-Tests in `FmSecuritiesAnalysisStatisticsTests` (Opt-out, Währungsgruppierung + reinheit, Einzelwährungs-Total + unbewertete Positionen, `byCategory` inkl. `untrusted_data` + Mischwährung-null, skip/take-Immunität inkl. Batch-Verify, Record-Scope, `securityId`-Pfad).
- [x] 1 Wire-Test `ToolsCall_SecuritiesAnalysis_IncludeStatistics_ReturnsAggregate` in `McpSecuritiesAnalysisTests` (3 Positionen, 2 Währungen, 1 unbewertet; Zählung, `byCurrency`, `positionsWithoutPrice`, `totalMarketValue = null`).
- [x] Lokal nachgeprüft: `FinanceManager.Tests` gefiltert `FmSecuritiesAnalysis` → **17/17**; `FinanceManager.Tests.Integration` gefiltert `McpSecuritiesAnalysisTests` → **3/3**.

### Doku

- [x] `Docs/help/mcp-server/api.md` (T07-Zeile): vollständiger `statistics`-Vertrag inkl. Null-Semantiken und Filter-Reihenfolge.
- [x] `Docs/help/mcp-server/business-rules.md`: neuer Abschnitt „Wertpapier-Statistik aggregiert serverseitig" — konsistent zum Code inkl. `onlyActive`-Korrektur.
- [x] `Docs/help/mcp-server/beschreibung.md`: Feature-Absatz ergänzt.
- [x] `steps.md` des Projekts: Schritt 1 → Abgeschlossen, Schritt 2 → In Arbeit.
- [x] Descriptor-Text + `inputSchema` (`includeStatistics`) in `FmSecuritiesAnalysisTool.cs:21, 33` entsprechen dem implementierten Verhalten.

## Hinweise (keine Befunde)

- **`byCategory.currencyCode` bei ausschließlich unbewerteten Positionen:** leitet sich nur aus bewertbaren Positionen ab → `null` (statt `marketValue = 0` mit Währung wie bei `byCurrency`). Konsistent mit der Spezifikation „einheitliche Währung der **bewertbaren** Positionen" und mit der Total-Semantik; von `api.md`/`business-rules.md` abgedeckt („`null` bei Mischwährung" liest sich strenger als der Code — bei null bewertbaren Positionen gilt ebenfalls `null`; inhaltlich vertretbar, nicht irreführend).
- **Testlücken (unkritisch):** kein dedizierter Test für `onlyActive:false` → inaktiv (nur `null`-/`true`-Pfade abgesichert); negatives `openQuantity` (zählt als Position, negativer Marktwert — dokumentierte Intent) und leere/unbewertete Sets (`priceDates`/`marketValue`-null) nicht explizit getestet — Semantik aus dem Code eindeutig ablesbar.
- Reihenfolge-Änderung Filter↔Paging verändert Seiteninhalte bei gesetztem individuellem Record-Scope — bewusste, dokumentierte Korrektur (s. `business-rules.md`).
