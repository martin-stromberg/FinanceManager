# Enums

## `PostingKind`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Bankkonto-Buchung |
| `Contact = 1` | Kontakt-Buchung |
| `SavingsPlan = 2` | Sparplan-Buchung |
| `Security = 3` | Wertpapier-Buchung |

Die vier Buchungsarten entsprechen genau den in der Anforderung geforderten Textfilter-Dimensionen.

## `ReportInterval`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportInterval.cs`

| Wert | Bedeutung |
|------|-----------|
| `Month = 0` | Monatliche Aggregation |
| `Quarter = 1` | Quartalsweise Aggregation |
| `HalfYear = 2` | Halbjährliche Aggregation |
| `Year = 3` | Jährliche Aggregation |
| `Ytd = 4` | Year-to-date (kumuliert, alle Jahre einzeln) |
| `AllHistory = 5` | Gesamthistorie (eine Zeile je Gruppe) |

## `SavingsPlanPostingSource`
Datei: `FinanceManager.Shared/Dtos/Reports/SavingsPlanPostingSource.cs`

| Wert | Bedeutung |
|------|-----------|
| `PlanPostings = 0` | Eigene Sparplan-Buchungen |
| `LinkedAccountPostings = 1` | Bank-Buchungen der verknüpften Konten |

## `SecurityPostingSubType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf |
| `Sell = 1` | Verkauf |
| `Dividend = 2` | Dividende |
| `Fee = 3` | Gebühr |
| `Tax = 4` | Steuer |

Im Filter-DTO als `IReadOnlyCollection<int>` transportiert; `QuerySecurityDividendsNetAsync` verwendet die Konstanten 0–4 (`Dividend`/`Fee`/`Tax` netted, `Buy`/`Sell` für Holding-Prüfung).

## `AggregatePeriod`
Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs` (Z. 8–29)

| Wert | Bedeutung |
|------|-----------|
| `Month = 0` | Monatsaggregat |
| `Quarter = 1` | Quartalsaggregat |
| `HalfYear = 2` | Halbjahresaggregat |
| `Year = 3` | Jahresaggregat |

`Ytd`/`AllHistory` lesen Monatsaggregate (`sourcePeriod`-Mapping in `QueryAsync`, Z. 66–75).

## `AggregateDateKind`
Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs` (Z. 34–45)

| Wert | Bedeutung |
|------|-----------|
| `Booking = 0` | Aggregation nach `BookingDate` |
| `Valuta = 1` | Aggregation nach `ValutaDate` |

## `SavingsPlanPostingSubType`
Datei: `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `ActualReport = 0` | Istmeldung: manuelle Korrekturbuchung auf den gemeldeten Ist-Stand; nur für Sparpläne ohne verknüpfte Konten, keine Kontobewegung, aus Budget-Ist-Werten ausgenommen |

Property-Typ von `Posting.SavingsPlanSubType`; für die Anforderung nur als Posting-Feld relevant, nicht als Filterdimension.

## `HomeKpiKind` / `HomeKpiDisplayMode`
Dateien: `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiKind.cs`, `HomeKpiDisplayMode.cs`

`HomeKpiKind`: `Predefined` vs. `ReportFavorite` (nur letzteres referenziert `ReportFavoriteId` und damit die Filter). `HomeKpiDisplayMode`: `TotalOnly`, `TotalWithComparisons`, `ReportGraph` — beeinflusst nur die Kachel-Darstellung.

## Hinweis zu `EnumCoverageTests`
`FinanceManager.Tests/Common/EnumCoverageTests.cs` assertiert die Memberlisten vieler Enums — **nicht abgedeckt** sind jedoch `PostingKind`, `ReportInterval`, `SavingsPlanPostingSource`, `SecurityPostingSubType`, `AggregatePeriod`, `AggregateDateKind`, `SavingsPlanPostingSubType`, `HomeKpiKind` und `HomeKpiDisplayMode`. Für die Anforderung werden keine neuen Enum-Mitglieder erwartet.
