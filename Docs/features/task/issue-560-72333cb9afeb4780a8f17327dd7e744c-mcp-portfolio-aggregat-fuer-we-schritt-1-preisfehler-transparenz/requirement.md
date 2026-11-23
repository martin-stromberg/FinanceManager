# Anforderung – MCP-Transparenz bei Preisfehlern und Mengenanomalien (Issue #562)

## Ziel

Ein MCP-Client soll Datenauffälligkeiten an Wertpapieren ohne Raten einordnen können: warum `hasPriceError` gesetzt ist, warum Kurse fehlen und was eine negative `openQuantity` bedeutet.

## Fachliche Anforderungen

1. **Bestehende Fehlerdaten exponieren:** `Security.PriceErrorMessage` und `Security.PriceErrorSinceUtc` (beide bereits persistiert und im `SecurityBackupDto` enthalten) über `SecurityDto` bis in die `fm_securities_analysis`-Response durchreichen.
   - `priceErrorSinceUtc` als strukturiertes Top-Level-Feld je Position.
   - `priceErrorMessage` als Provider-Rohtext konventionsgemäß unter `untrusted_data` (sicherheitskonzept.md §5.2).
   - Ausgabe in Listen- wie Einzelabruf (`securityId`); Felder `null` bei fehlerfreien Positionen.
2. **Tool-Beschreibung ergänzen** (analog ausführlicher Doku bei `fm_savings_plan_analysis`):
   - `hasPriceError` = letzter Provider-Abruf für das Symbol fehlgeschlagen; das Flag wird nur durch einen erfolgreichen Abruf gelöscht; `prices` bleibt auf dem letzten erfolgreichen Stand.
   - Leeres `prices` ≠ Fehler: mögliche Ursachen sind fehlender `AlphaVantageCode`, Provider-`NoData` oder kein Kurs im angefragten Zeitraum.
   - Negative `openQuantity` = Σ Verkaufsmengen > Σ Kaufmengen → Hinweis auf fehlende/doppelte Buchungen oder nicht abgebildete Kapitalmaßnahmen, nicht auf Leerverkauf.

## Betroffene Bereiche

- `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs` (Response + Descriptor-Beschreibung)
- `FinanceManager.Shared/Dtos/Securities/SecurityDto.cs` (zwei neue Felder)
- `FinanceManager.Infrastructure/Securities/SecurityService.cs` (`MapToDtoAsync`)
- Tests: `FinanceManager.Tests/Mcp/FmSecuritiesAnalysisToolTests.cs`, ggf. `FinanceManager.Tests.Integration/McpSecuritiesAnalysisTests.cs`, DTO-Mapping-Test

## Akzeptanzkriterien

- Positionen mit `hasPriceError` liefern den Fehlergrund (`priceErrorMessage` in `untrusted_data`) und `priceErrorSinceUtc`; fehlerfreie Positionen bleiben unverändert.
- Die Tool-Beschreibung erklärt `hasPriceError`-Semantik, leeres `prices` und negative `openQuantity`.
- `SecurityDto`-Erweiterung ist abwärtskompatibel (optionale Felder); keine Persistenz-/Backup-Änderung.
- Alle bestehenden Tests bestehen; neue Tests decken die Felder (Listen- und Single-Modus) ab.

## Nicht-Ziele

- Kein neues `priceErrorReason`-Enum / `priceStatus`-Diskriminator (Rohtext deckt den Kernfall).
- Keine Änderung an Fehler-Lifecycle, Worker oder Provider-Verhalten.
- Kein Short-Selling-Feature; negative `openQuantity` wird nur dokumentiert.

## Getroffene Annahmen (aus Projektplan, kein interaktiver Anwender)

- `priceErrorMessage` unter `untrusted_data` (Provider-Text kann Infrastrukturdetails enthalten); `priceErrorSinceUtc` als vertrauenswürdiges Top-Level-Feld.
- `SecurityDto`-Erweiterung akzeptiert (steht der Web-UI ebenfalls zur Verfügung; keine UI-Pflichtanzeige).
