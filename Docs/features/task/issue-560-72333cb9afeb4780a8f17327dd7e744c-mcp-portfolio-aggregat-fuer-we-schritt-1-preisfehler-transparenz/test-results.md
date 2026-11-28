# Testergebnisse — Schritt 1 (#562): Preisfehler-Transparenz

Datum: (Ausführung im Rahmen der Implementierung)

## Ausgeführte Tests

### Unit-Tests (`FinanceManager.Tests`)

Befehl:

```
dotnet test FinanceManager.Tests --no-build --filter "FullyQualifiedName~FmSecuritiesAnalysisPriceErrorTests|FullyQualifiedName~FmSecuritiesAnalysisToolTests|FullyQualifiedName~SecurityServicePriceErrorMappingTests"
```

Ergebnis: **12/12 bestanden**, 0 fehlgeschlagen, 0 übersprungen.

Abgedeckte Fälle:

- `FmSecuritiesAnalysisPriceErrorTests`
  - Listenpfad: `priceErrorSinceUtc` (Top-Level), `untrusted_data.priceErrorMessage`, keine Top-Level-Duplikation der Nachricht
  - Listenpfad: `null`-Serialisierung beider Felder bei fehlerfreier Position
  - Einzelpfad (`securityId`): identische Feldausgabe
  - Descriptor: Beschreibung dokumentiert `hasPriceError`, `priceErrorMessage`, leere `prices`, negatives `openQuantity`
- `FmSecuritiesAnalysisToolTests` (Bestand): unverändert grün — kein Verhaltensbruch
- `SecurityServicePriceErrorMappingTests`
  - Mapping `PriceErrorMessage`/`PriceErrorSinceUtc` bei gesetztem Fehler (`SetPriceError`)
  - `null`-Felder ohne Fehler

### Integrationstests (`FinanceManager.Tests.Integration`)

Befehl:

```
dotnet test FinanceManager.Tests.Integration --filter "FullyQualifiedName~McpSecuritiesAnalysisTests"
```

Ergebnis: **2/2 bestanden**.

- Neu: `ToolsCall_SecuritiesAnalysis_ReturnsPriceErrorDetails` — Wire-Level über echte
  `/mcp`-Oberfläche: `hasPriceError=true`, `priceErrorSinceUtc` gesetzt,
  `untrusted_data.priceErrorMessage` enthält Provider-Text, kein Top-Level-Duplikat.
- Bestand: `ToolsCall_SecuritiesAnalysis_ReturnsLatestPrice` — unverändert grün.

### Build

`dotnet build FinanceManager.sln` — 0 Fehler (nur bekannte vorhandene Warnungen).

## Abnahme

Kein UI-Element geändert → Screenshot-/Usability-Nachweis entfällt. Akzeptanz über
Wire-Level-Integrationstest (Happy Path mit Fehler-Fall) und Unit-Tests einschließlich
Null-Fall und Descriptor-Dokumentation erbracht.
