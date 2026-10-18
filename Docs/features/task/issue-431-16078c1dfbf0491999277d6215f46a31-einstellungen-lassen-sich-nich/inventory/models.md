# Datenmodelle

## `ImportSplitSettingsDto`
Datei: `FinanceManager.Shared/Dtos/Statements/ImportSplitSettingsDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Mode` | `ImportSplitMode` | Aufteilungsmodus (`FixedSize`, `Monthly`, `MonthlyOrFixed`) |
| `MaxEntriesPerDraft` | `int` | Maximale Einträge pro Draft (Default 250) |
| `MonthlySplitThreshold` | `int?` | Schwelle für monatliche Aufteilung (nur Hybrid-Modus) |
| `MinEntriesPerDraft` | `int` | Minimale Einträge pro Monats-Draft (Default 8) |
| `MassImportDialogPolicy` | `MassImportDialogPolicy` | Dialogverhalten beim Massenimport (`AlwaysConfirm`, `OnMissingInformation`) |
| `KnownContactAutoCreateEnabled` | `bool` | Bekannte Kontakte automatisch anlegen (Default `true`) |

Update-Request: `ImportSplitSettingsUpdateRequest` in `FinanceManager.Shared/Dtos/Statements/ImportSplitSettingsRequests.cs` (gleiche sechs Felder).

## `ReturnAnalysisSettingsResponse`
Datei: `FinanceManager.Shared/Dtos/Securities/ReturnAnalysisDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `BenchmarkSecurityId` | `Guid?` | Benchmark-Wertpapier, `null` = kein Benchmark |
| `BenchmarkSecurityName` | `string?` | Anzeigename des Benchmarks |
| `ShowSharpeRatio` | `bool` | Sharpe Ratio anzeigen |
| `RiskFreeRate` | `decimal` | Risikofreier Zinssatz (Dezimal, z. B. 0.04) |

Update-Request: `ReturnAnalysisSettingsUpdateRequest(BenchmarkSecurityId, ShowSharpeRatio, RiskFreeRate)` in derselben Datei.
