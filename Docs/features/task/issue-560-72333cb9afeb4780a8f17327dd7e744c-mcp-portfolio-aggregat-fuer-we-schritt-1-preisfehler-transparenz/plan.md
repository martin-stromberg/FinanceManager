# Umsetzungsplan – #562 Preisfehler-Transparenz

## Scope-Klärung

- **Verarbeitungseinheit:** `fm_securities_analysis`-Response je Wertpapierposition (Listen- und Einzelabruf).
- **Auslösendes Ereignis:** `tools/call` mit `name = fm_securities_analysis` eines berechtigten MCP-Zugangs.
- **Erwartete Wirkung:** Response enthält Fehlergrund/-zeitpunkt; Tool-Beschreibung erklärt Auffälligkeiten.
- **Scope-Ausschlüsse:** Keine (keine Ausschlüsse gegenüber naheliegenden Erwartungen — `priceErrorReason`-Enum und `priceStatus` sind ausdrückliche Issue-Nicht-Ziele).

## Akzeptanzszenarien

1. **Happy Path (Liste):** Zugang mit Scopes ruft `fm_securities_analysis {}` auf; eine Position mit `HasPriceError=true`, `PriceErrorMessage="Symbol not found"`, `PriceErrorSinceUtc=2026-10-01T…` liefert im `securities[]`-Eintrag `priceErrorSinceUtc` (Top-Level) und `untrusted_data.priceErrorMessage`; eine fehlerfreie Position liefert `priceErrorSinceUtc: null` und `untrusted_data.priceErrorMessage: null`.
2. **Einzelabruf:** `securityId`-Aufruf derselben Position zeigt identische Felder.
3. **Descriptor:** `tools/list` zeigt die erweiterte Beschreibung mit den drei Erläuterungen (`hasPriceError`-Semantik, leeres `prices`, negative `openQuantity`).

## Umsetzung

1. `SecurityDto`: Felder `PriceErrorMessage` (`string?`, XML-Doc „normalized provider error message when <see cref=\"HasPriceError\"/> is set") und `PriceErrorSinceUtc` (`DateTime?`) ergänzen — optional, abwärtskompatibel.
2. `SecurityService.MapToDtoAsync`: `PriceErrorMessage = s.PriceErrorMessage`, `PriceErrorSinceUtc = s.PriceErrorSinceUtc` mappen.
3. `FmSecuritiesAnalysisHandler`: im Positions-Objekt `priceErrorSinceUtc = s.PriceErrorSinceUtc` (Top-Level, neben `hasPriceError`) sowie `priceErrorMessage = s.PriceErrorMessage` im `untrusted_data`-Block ausgeben.
4. `FmSecuritiesAnalysisDescriptor.Description` erweitern (englisch, wie bisher): Semantik `hasPriceError` (last provider fetch failed; cleared only on success; `prices` keeps last good state), leeres `prices` ≠ Fehler (kein `AlphaVantageCode`, Provider `NoData`, kein Kurs im Zeitraum), negative `openQuantity` = verkaufsüberschüssige Buchungsinkonsistenz, kein Leerverkauf. Kein Schema-Feld nötig.
5. Tests:
   - `FmSecuritiesAnalysisToolTests`: List-Modus liefert `priceErrorSinceUtc` Top-Level und `priceErrorMessage` unter `untrusted_data`; fehlerfreie Position → beide `null`; Single-Modus (`securityId`) ebenso.
   - `SecurityService`-Mapping-Test (oder vorhandenen Mapping-Test erweitern): Entity → DTO überträgt beide Felder.
   - `McpSecuritiesAnalysisTests` (Integration): Wire-Assert, dass `untrusted_data.priceErrorMessage` serialisiert wird.
6. Doku: Hilfeseite `Docs/help/mcp-server/` um Feldhinweise ergänzen (soweit vorhandene Struktur es hergibt); `Docs/planung/mcp-integration/scope-und-toolkatalog.md` T07-Notiz zu den neuen Feldern.

## Offene Punkte

Keine (Annahmen im Projektplan dokumentiert: untrusted-Platzierung, kein Enum, SecurityDto-Erweiterung).

## Testbedarf

- Unit (neu/erweitert): `FmSecuritiesAnalysisToolTests` (3–4 Cases), Mapping-Test.
- Integration: `McpSecuritiesAnalysisTests` (1 Wire-Case).
- E2E: nicht erforderlich (kein UI-Fluss).
- Reale E2E-Abnahme: über echten `/mcp`-`tools/call` gegen gestartete Demo-Instanz oder Integrationstest-Host — Szenarien 1–3.
