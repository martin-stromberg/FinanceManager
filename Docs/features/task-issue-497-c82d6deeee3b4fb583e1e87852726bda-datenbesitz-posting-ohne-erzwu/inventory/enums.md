# Enums — Bestandsaufnahme

## `PostingKind`

Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs` (27 Zeilen)

Bestimmt die Ziel-Domäne eines `Posting`/`PostingAggregate`; Erzeuger wählen daraus jeweils genau eine Referenz (`switch (posting.Kind)` in `PostingAggregateService` Z. 95–116, `SetupImportService` Z. 1558–1574).

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Buchung zu einem Bankkonto (`AccountId`) |
| `Contact = 1` | Buchung zu einem Kontakt (`ContactId`) |
| `SavingsPlan = 2` | Buchung zu einem Sparplan (`SavingsPlanId`) |
| `Security = 3` | Buchung zu einem Wertpapier (`SecurityId` + `SecuritySubType`) |

## `SecurityPostingSubType`

Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs` (18 Zeilen)

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf |
| `Sell = 1` | Verkauf |
| `Dividend = 2` | Dividende |
| `Fee = 3` | Gebühr |
| `Tax = 4` | Steuer |

## `SavingsPlanPostingSubType`

Datei: `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanPostingSubType.cs` (15 Zeilen)

| Wert | Bedeutung |
|------|-----------|
| `ActualReport = 0` | Istmeldungs-Korrekturbuchung ohne Kontobewegung; nur für Sparpläne ohne verknüpfte Konten; von Budget-Ist ausgenommen |

## `AggregatePeriod`

Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs` (Z. 8–29)

| Wert | Bedeutung |
|------|-----------|
| `Month = 0` | Monatliche Aggregation |
| `Quarter = 1` | Quartalsweise Aggregation |
| `HalfYear = 2` | Halbjährliche Aggregation |
| `Year = 3` | Jährliche Aggregation |

## `AggregateDateKind`

Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs` (Z. 34–45)

| Wert | Bedeutung |
|------|-----------|
| `Booking = 0` | Buchungsdatum als Aggregationsbasis |
| `Valuta = 1` | Valutadatum als Aggregationsbasis |

## Abdeckungstest

`FinanceManager.Tests/Common/EnumCoverageTests.cs` (476 Zeilen) assertiert die vollständige Memberliste je Enum (Muster: `AssertValues<TEnum>(nameof(...))`). Keiner der fünf hier betrachteten Enums (`PostingKind`, `SecurityPostingSubType`, `SavingsPlanPostingSubType`, `AggregatePeriod`, `AggregateDateKind`) ist dort enthalten; neue Enum-Mitglieder sind laut `AGENT.md` grundsätzlich dort zu ergänzen — für diese Anforderung ist **kein neues Enum-Mitglied** geplant.
