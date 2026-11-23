# Anforderung — Schritt 2: includeStatistics-Aggregat für fm_securities_analysis (Issue #560)

Quelle: GitHub-Issue #560 + Projektplan `Docs/projects/task/issue-560-72333cb9afeb4780a8f17327dd7e744c-mcp-portfolio-aggregat-fuer-we/project-plan.md`, Schritt 2.

## Ziel
Opt-in `includeStatistics` ergänzt die Antwort um ein `statistics`-Objekt mit serverseitigen Portfolio-Aggregaten über dieselbe gefilterte Menge wie die ausgegebene Wertpapierliste.

## Vertrag (Entscheidung laut Projektplan)
- `securityCount`: Anzahl sichtbarer Wertpapiere (nach `securityId`, `onlyActive`, Record-Scope).
- `positionCount`: Wertpapiere mit `openQuantity != 0`.
- `positionsWithoutPrice`: `{ count, securityIds }` — Positionen (`openQuantity != 0`) ohne gespeicherten Kurs; fließen nicht in Marktwerte ein.
- `byCurrency`: `[{ currencyCode, marketValue, positionCount, positionsWithoutPrice }]`, `marketValue = Σ openQuantity × letzter gespeicherter Schlusskurs`.
- `byCategory`: `[{ categoryId, marketValue, currencyCode, positionCount, positionsWithoutPrice, untrusted_data: { name } }]` — `marketValue`/`currencyCode` nur bei einheitlicher Währung der bewertbaren Positionen der Gruppe, sonst `null`.
- `totalMarketValue`/`totalCurrencyCode`: nur bei einheitlicher Währung aller bewertbaren Positionen, sonst `null` — kein irreführender Mehrwährungs-Gesamtwert.
- `oldestPriceDate`/`newestPriceDate`: jüngstes/ältestes verwendetes Kursdatum (Stichtags-Transparenz), `null` ohne Kurse.
- Ohne `includeStatistics` (oder `false`): `statistics: null` — bisheriges Antwortverhalten bleibt.

## Filter-Semantik
- Statistik = Record-Scope-gefilterte (`McpRecordScopeFilter`) + fachlich gefilterte (`securityId`, `onlyActive`) Menge, **ungeachtet `skip`/`take`**.
- Korrektur der Reihenfolge: Record-Scope-Filter läuft vor `skip`/`take` (bisher danach auf dem Paging-Fenster), damit Liste und Statistik dieselbe Menge beschreiben und Seiten sich korrekt füllen.
- `securityId`-Pfad: Statistik über genau dieses Papier.
- Kategorien/Sicherungskonvention: Kategorienamen unter `untrusted_data`; ein einzelner Kurs-Batch (`GetLatestPricesAsync`), kein N+1.

## Tests
Aggregation (Währungsreinheit, positionsWithoutPrice, Kategorien, skip/take-Immunität, Record-Scope, securityId, Opt-out null) + Wire-Level-Integrationstest.
