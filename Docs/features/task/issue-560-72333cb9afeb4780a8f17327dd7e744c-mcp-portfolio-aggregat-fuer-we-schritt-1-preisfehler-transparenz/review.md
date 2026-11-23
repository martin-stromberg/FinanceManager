# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Anforderungen (requirement.md / Issue #562)

### Fachliche Anforderung 1 — Bestehende Fehlerdaten exponieren

- [x] `SecurityDto` um `PriceErrorMessage` (`string?`) und `PriceErrorSinceUtc` (`DateTime?`) erweitert — `FinanceManager.Shared/Dtos/Securities/SecurityDto.cs:40-43`, optional und damit abwärtskompatibel.
- [x] `SecurityService.MapToDtoAsync` mappt beide Felder — `FinanceManager.Infrastructure/Securities/SecurityService.cs:269-270`. Die Methode ist die einzige Mapping-Stelle (`ListAsync` Z. 49, `GetAsync` Z. 66 und die übrigen Pfade laufen alle hierdurch), es gibt keinen zweiten `new SecurityDto`-Erzeuger im Produktivcode.
- [x] `priceErrorSinceUtc` als strukturiertes Top-Level-Feld je Position — `FmSecuritiesAnalysisTool.cs:144`, direkt neben `hasPriceError`.
- [x] `priceErrorMessage` als Provider-Rohtext unter `untrusted_data` — `FmSecuritiesAnalysisTool.cs:159`; konform zu `sicherheitskonzept.md` §5.2 (Freitext mit Drittinhalten nur im markierten Wrapper; der Zeitstempel ist strukturierte Systemmetadaten und korrekt Top-Level).
- [x] Ausgabe in Listen- wie Einzelabruf — beide Pfade (`securityId` gesetzt / nicht gesetzt) münden in dieselbe Projektionsschleife (`FmSecuritiesAnalysisTool.cs:126-162`); separate Dedizierung nicht nötig und nicht erfolgt.
- [x] Felder `null` bei fehlerfreien Positionen — Nullable-Typen serialisieren als `null`; durch Unit-Test `Invoke_ListPath_NullErrorFields_WhenNoPriceError` und Integrationstest abgesichert.

### Fachliche Anforderung 2 — Tool-Beschreibung ergänzen

- [x] `hasPriceError`-Semantik dokumentiert: letzter Provider-Abruf fehlgeschlagen, Flag wird nur durch erfolgreichen Abruf gelöscht, `prices` kann letzten gültigen Stand zeigen — `FmSecuritiesAnalysisTool.cs:30`.
- [x] Leeres `prices` ≠ Fehler mit drei Ursachen (kein `AlphaVantageCode`, Provider lieferte keine Daten, kein Kurs im angefragten Zeitraum) — `FmSecuritiesAnalysisTool.cs:31`.
- [x] Negative `openQuantity` = verkaufsüberschüssige Buchungsinkonsistenz (fehlende/doppelte Buchungen, nicht abgebildete Kapitalmaßnahmen), kein Leerverkauf — `FmSecuritiesAnalysisTool.cs:32`.
- [x] Englische, ausführliche Beschreibung entspricht dem Referenzmuster (`fm_savings_plan_analysis`); kein Schema-Feld nötig, `inputSchema` unverändert.

### Akzeptanzkriterien

- [x] Fehlerhafte Positionen liefern Grund + Zeitpunkt; fehlerfreie unverändert (`null`-Felder, keine Top-Level-Duplikation der Nachricht — per Test assertiert).
- [x] Keine Persistenz-/Backup-Änderung: Felder waren bereits persistiert und im `SecurityBackupDto` enthalten (`Security.cs:105-117, 276, 283`); AGENT.md-Backupregel unberührt.
- [x] Alle bestehenden Tests grün; neue Tests decken Liste, Single, Null-Fall, Descriptor und Wire-Level ab.
- [x] Nicht-Ziele eingehalten: kein `priceErrorReason`-Enum, kein `priceStatus`-Diskriminator, keine Änderung an Worker/Provider/Fehler-Lifecycle, kein Short-Selling-Feature.

## Umgesetzte Planelemente (plan.md)

- [x] Schritt 1–2: DTO-Felder + `MapToDtoAsync`-Mapping.
- [x] Schritt 3: Handler-Projektion (`priceErrorSinceUtc` Top-Level, `priceErrorMessage` in `untrusted_data`).
- [x] Schritt 4: Descriptor-Beschreibung mit allen drei Erläuterungen.
- [x] Schritt 5: Tests — `FmSecuritiesAnalysisPriceErrorTests` (4 Fälle), `SecurityServicePriceErrorMappingTests` (2 Fälle), `McpSecuritiesAnalysisTests` (1 neuer Wire-Case).
- [ ] Schritt 6: Hilfeseiten-Doku (`Docs/help/mcp-server/api.md`, `business-rules.md`, `scope-und-toolkatalog.md` T07-Notiz) — **bewusst noch offen**: Dokumentation ist Workflow-Schritt 12 (`todo.md`), fällt nicht unter die Implementierung. Hinweis an den Hauptagenten: T07-Zeile in `api.md:88` dokumentiert die Ausgabefelder detailliert und sollte `priceErrorSinceUtc`/`untrusted_data.priceErrorMessage` aufnehmen.

## Hinweise

- Plan-Check-Hinweis 2 bestätigt sich: `SecurityDto` wird auch vom `SecuritiesController` (REST) direkt ausgeliefert — die neuen Felder erscheinen dort ebenfalls; durch die dokumentierte Annahme („steht der Web-UI ebenfalls zur Verfügung") abgedeckt, kein Sicherheitsproblem (Provider-Fehlertext ist besitzerbezogen).
- `dotnet test FinanceManager.Tests` (gefiltert auf die drei betroffenen Suiten) lokal nachgeprüft: **12/12 bestanden**.
