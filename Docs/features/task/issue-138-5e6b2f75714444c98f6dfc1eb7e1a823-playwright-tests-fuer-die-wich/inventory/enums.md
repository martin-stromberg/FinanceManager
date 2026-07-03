## `MassImportDialogPolicy`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `AlwaysConfirm` | Dialog wird immer vor Ausführung angezeigt. |
| `OnMissingInformation` | Dialog nur bei fehlenden Informationen. |

## `MassImportFileType`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Dateityp nicht erkannt. |
| `AccountStatement` | Kontoauszugsdatei. |
| `SecurityPrices` | Security-Preisdatei. |

## `MassImportFileExecutionStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `Pending` | Wartet auf Bestätigung. |
| `Skipped` | Übersprungen (ausgeschlossen/nicht importierbar). |
| `Imported` | Erfolgreich importiert. |
| `Failed` | Import fehlgeschlagen. |

## `MassImportDecisionSource`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `AutoDetected` | Automatisch erkannt/gesetzt. |
| `UserConfirmed` | Durch Benutzer bestätigt/gesetzt. |

## `PostingKind`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Bank` | Buchung im Bankkonto-Kontext. |
| `Contact` | Buchung im Kontakt-Kontext. |
| `SavingsPlan` | Buchung im Sparplan-Kontext. |
| `Security` | Buchung im Wertpapier-Kontext. |

## `ReportInterval`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportInterval.cs`

| Wert | Bedeutung |
|------|-----------|
| `Month` | Monatliche Aggregation. |
| `Quarter` | Quartalsweise Aggregation. |
| `HalfYear` | Halbjährliche Aggregation. |
| `Year` | Jährliche Aggregation. |
| `Ytd` | Year-to-date. |
| `AllHistory` | Gesamthistorie. |

## `HomeKpiKind`
Datei: `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Predefined` | Vordefinierte KPI-Kachel. |
| `ReportFavorite` | KPI auf Basis eines Report-Favoriten. |

## `HomeKpiPredefined`
Datei: `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiPredefined.cs`

| Wert | Bedeutung |
|------|-----------|
| `AccountsAggregates` | Konten-Aggregate. |
| `SavingsPlanAggregates` | Sparplan-Aggregate. |
| `SecuritiesDividends` | Dividenden-KPI. |
| `MonthlyBudget` | Monatsbudget-KPI. |
| `ActiveSavingsPlansCount` | Anzahl aktiver Sparpläne. |
| `ContactsCount` | Kontaktanzahl. |
| `SecuritiesCount` | Anzahl Wertpapiere. |
| `OpenStatementDraftsCount` | Anzahl offener Statement-Entwürfe. |

## `HomeKpiDisplayMode`
Datei: `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiDisplayMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `TotalOnly` | Nur Summenwert anzeigen. |
| `TotalWithComparisons` | Summe inkl. Vergleichswerte. |
| `ReportGraph` | Graphische Darstellung statt reiner Summe. |
