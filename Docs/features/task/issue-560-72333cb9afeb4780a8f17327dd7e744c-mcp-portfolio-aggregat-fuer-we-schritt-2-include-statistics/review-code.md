# Code-Review

## Ergebnis

**Status:** Keine Befunde — drei Hinweise (Dokumentationsnianzen / Testlücken), nicht blockierend.

## Korrektheit

- **`BuildStatistics` (`FmSecuritiesAnalysisTool.cs:194-245`):**
  - Positionsbegriff `openQuantity != 0` korrekt; `null` → keine Position (Service befüllt den Wert immer, `SecurityService.cs:271`). Negative `openQuantity` zählt als Position und zieht den Marktwert ins Negative — dokumentierte Intent (Buchungsinkonsistenz, Descriptor Z. 32).
  - `byCurrency` (Z. 200-209): Gruppierung nach `CurrencyCode` (non-null, Default `"EUR"`), `marketValue` nur über bewertbare Positionen — `Sum` über leere Teilfolge = 0, korrekt für „Positionen vorhanden, kein Kurs".
  - `byCategory` (Z. 211-227): Gruppierung nach `CategoryId` (`Guid?`, `null`-Gruppe zulässig — Papiere ohne Kategorie). Währungsreinheit über **bewertbare** Positionen (`valuedCurrencies.Count == 1`) → `marketValue`/`currencyCode` `null` bei Mischwährung *und* bei null bewertbaren Positionen. Konsistent zur Spezifikation; minimaler Lesbarkeitsunterschied zu `byCurrency` (dort `marketValue = 0` statt `null`, weil die Währung als Gruppenschlüssel immer bekannt ist) — siehe Hinweis.
  - Total (Z. 229-241): `null` bei Mischwährung **und** bei keiner bewertbaren Position — korrekt („kein irreführender Gesamtwert", keine Scheinwährung).
  - `oldestPriceDate`/`newestPriceDate` (Z. 242-243): `null` bei leerem `valued` — deckt „nur unbewertete Positionen" und „leere Menge" ab.
  - Leere Eingabemenge → alle Zähler 0, leere Arrays, alle Nullables `null` — sauberer Randfall.
- **Reihenfolge Filter/Paging (Z. 123-126):** `visible` = fachlich + Record-Scope-gefiltert, `paged` = `visible.Skip(skip).Take(take)`; `securityId`-Pfad: `paged = visible` — `skip`/`take` greifen wie zuvor nicht im Einzelpfad. Liste (`result` über `paged`) und Statistik (`BuildStatistics(visible, …)`) beschreiben dieselbe Basismenge.
- **onlyActive-Fix (Z. 105-116):** `ListAsync(owner,false)` liefert ungefiltert alle Papiere (`SecurityService.cs:28-31` — `if (onlyActive)` ist das einzige Filterkriterium). `null` → `all` (kein Duplikat), `true` → aktive Teilmenge, `false` → `!IsActive` in-memory. Korrekt; einziger Service-Aufruf pro Pfad.
- **Kurs-Batch (Z. 130-131):** `includeStatistics ? visible : paged` — die Statistik wertet ausschließlich IDs aus, die auch abgefragt wurden; unbewertete Positionen fehlen korrekt im Dictionary und landen in `positionsWithoutPrice`. Kein Query über Datensätze hinaus, die der Aufrufer nicht sehen darf (Batch läuft über `visible`, nie über `securities`).

## Stil und Konventionen

- Anonymes Output-Objekt wie der übrige Tool-Contract — bewusste Entscheidung laut plan.md (kein Shared-DTO), konsistent.
- `untrusted_data` für Kategorienamen (Z. 225) und `FirstOrDefault(n => n is not null)` — Konvention gehalten; der Test assertiert zusätzlich, dass kein Top-Level-`name` existiert.
- Descriptor/`inputSchema` um `includeStatistics` erweitert, Beschreibung deckt Semantik und Währungsreinheit präzise ab.
- Deterministische Sortierung (`byCurrency` nach Code, `byCategory` nach Guid-String) — reproduzierbare Ausgabe.
- Kleine Redundanzen (nicht blockierend): der Summenausdruck `(s.OpenQuantity ?? 0m) * latestPrices[s.Id].Close` steht dreimal im Code; `latestPrices.ContainsKey`/`[s.Id]` wird in den Gruppen-Lambdas wiederholt ausgewertet (statt `TryGetValue`/vorab projiziertem `(Security, Price)`-Tupel). Bei In-Memory-Mengen dieser Größenordnung vernachlässigbar.
- Keine Enum-/Resx-/EF-/Backup-Änderung → AGENT.md-Regeln (EnumCoverageTests, Backup-Vollständigkeit) unberührt.

## Sicherheit

- Owner ausschließlich aus `context.Access.OwnerUserId`; `includeStatistics` ändert nichts an der Zugriffskontrolle — die Aggregation läuft über `visible`, also über die ohnehin emittierbare Menge. `positionsWithoutPrice.securityIds` legt nur IDs sichtbarer Positionen offen — kein Informationsleck über den Record-Scope hinaus.
- Ein einzelner Batch-Aufruf (`GetLatestPricesAsync`) auch bei voller sichtbarer Menge — kein N+1; Größe durch Nutzerdaten begrenzt (kein serverseitiges Cap, aber `visible` ist besitzerbeschränkt).
- Kategorienamen konsequent unter `untrusted_data` — Prompt-Injection-Konvention eingehalten.

## Tests

- `FmSecuritiesAnalysisStatisticsTests` (6 Fälle): Opt-out (`statistics: null`), Währungsgruppierung + Mehrwährungs-`null`-Total, Einzelwährungs-Total + `positionsWithoutPrice` (Zählung + ID), `byCategory` inkl. `untrusted_data.name` und `KeyNotFoundException`-Negativassert, skip/take-Immunität inkl. `Times.Once`-Verify auf den Batch über alle sichtbaren IDs, Record-Scope-Wirkung, `securityId`-Pfad. Mock-Semantik korrekt: `ListAsync(false)` liefert jetzt explizit „alle" (Z. 44-46), passend zur echten Service-Semantik.
- `McpSecuritiesAnalysisTests.ToolsCall_SecuritiesAnalysis_IncludeStatistics_ReturnsAggregate`: Wire-Level über `tools/call`, seeds über `AppDbContext` (3 Papiere, 2 Währungen, 1 unbewertet, Postings für `OpenQuantity`); prüft Zählung, `byCurrency`, `positionsWithoutPrice` und Mehrwährungs-`null`-Total.
- Lokal nachvollzogen: **17/17** (`FmSecuritiesAnalysis*`) + **3/3** (`McpSecuritiesAnalysisTests`) bestanden.

## Hinweise (keine Befunde)

- `byCategory.marketValue`/`currencyCode` ist `null`, wenn eine Kategorie ausschließlich unbewertete Positionen enthält (`valuedCurrencies.Count == 0`) — `api.md`/`business-rules.md` begründen `null` nur mit „Mischwährung"; der Null-Fall ist implizit mitgedacht, könnte bei Bedarf explizit in der Doku nachgezogen werden.
- Kein Unit-Test für `onlyActive:false` → „nur inaktive" (der Fix-Pfad); `null` → „alle" ist über den Wire-Test indirekt abgesichert (`securityCount` würde unter dem alten Merge doppelt zählen). Negative `openQuantity` und die leeren/unbewerteten Randfälle sind ebenfalls ungetestet — Semantik im Code eindeutig, Aufnahme optional.

## Geprüfte Dateien

- `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs` (Diff)
- `FinanceManager.Shared/Dtos/Securities/SecurityDto.cs`, `SecurityPriceDtos.cs`
- `FinanceManager.Application/Securities/ISecurityPriceService.cs`, `ISecurityService.cs`
- `FinanceManager.Infrastructure/Securities/SecurityService.cs` (`ListAsync`-Semantik)
- `FinanceManager.Web/Mcp/McpRecordScopeFilter.cs` (`ApplyAsync`)
- `FinanceManager.Tests/Mcp/FmSecuritiesAnalysisStatisticsTests.cs` (neu), `FmSecuritiesAnalysisToolTests.cs`, `McpToolTestBase.cs`
- `FinanceManager.Tests.Integration/McpSecuritiesAnalysisTests.cs` (Diff)
- `Docs/help/mcp-server/api.md`, `beschreibung.md`, `business-rules.md`; `Docs/projects/task/.../steps.md` (Diff)
- `requirement.md`, `plan.md`, `test-results.md` (Feature-Ordner)
