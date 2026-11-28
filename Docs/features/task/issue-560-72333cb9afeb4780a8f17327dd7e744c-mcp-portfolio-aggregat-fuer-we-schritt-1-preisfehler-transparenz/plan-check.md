# Planprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Anforderung ↔ Plan

| Anforderung (Issue #562) | Plan-Abdeckung | Code-Verifikation |
|---|---|---|
| `PriceErrorMessage` + `PriceErrorSinceUtc` über `SecurityDto` exponieren | Schritt 1+2 | `Security.cs:105–117` persistiert beide Felder; `SecurityDto` fehlen sie; `MapToDtoAsync` ist triviale Zuordnung. |
| `priceErrorSinceUtc` Top-Level, `priceErrorMessage` unter `untrusted_data` | Schritt 3 | Handler `hasPriceError` Top-Level, `untrusted_data`-Block vorhanden; Listen-/Einzelpfad über dieselbe Schleife. |
| Felder `null` bei fehlerfreien Positionen | Szenario 1 | Default-Serialisierung emittiert `null` (Präzedenz `prices`-Assert in `FmSecuritiesAnalysisToolTests.cs:70`). |
| Tool-Beschreibung: drei Erläuterungen | Schritt 4 | `Description` im Descriptor; `tools/list`-Ausgabe verifiziert; Referenzmuster `FmSavingsPlanAnalysisDescriptor`. |
| Keine Persistenz-/Backup-Änderung | implizit | `SecurityBackupDto` enthält die Felder bereits mit Defaults. |
| Tests Liste+Single, Mapping, Integration | Schritt 5 | Testdateien und -Muster vorhanden. |
| Doku | Schritt 6 | `Docs/help/mcp-server/api.md:88`, `business-rules.md`, `scope-und-toolkatalog.md` T07. |

## Fehlende oder unvollständige Punkte

Keine.

## Hinweise (bei Umsetzung beachten)

1. `FmSecuritiesManageTool` ist nicht betroffen (DTO-Erweiterung additiv).
2. REST-API-Nebeneffekt: `SecuritiesController` liefert `SecurityDto` direkt — neue Felder erscheinen dort Top-Level (durch dokumentierte Annahme abgedeckt).
3. Beschreibung präzis formulieren: „letzter bekannter Provider-Fehler" (Flag wird nur bei Domainfehlern gesetzt, nicht bei Rate-Limits).
4. Edge-Case: manipuliertes Backup könnte `HasPriceError=false` mit gesetzter Meldung erzeugen — Ausgabe der Werte erfolgt ungeprüft (Felder `null` bei fehlenden Werten).
5. Ergänzend: Description-Assert-Test für die drei Schlüsselbegriffe (Konvention `McpStatementWriteToolsTests.cs:122–125`).
6. `EnumCoverageTests` nicht betroffen; keine Resx-/UI-Arbeit.
