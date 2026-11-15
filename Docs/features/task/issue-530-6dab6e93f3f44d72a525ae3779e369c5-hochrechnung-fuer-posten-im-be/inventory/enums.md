# Enums – Hochrechnung (CompareProjection)

## `PostingKind`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Buchung eines Bankkontos. |
| `Contact = 1` | Buchung eines Kontakts. |
| `SavingsPlan = 2` | Buchung eines Sparplans. |
| `Security = 3` | Buchung eines Wertpapiers — einzige Art mit Hochrechnung heute. |

## `ReportInterval`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportInterval.cs`

| Wert | Bedeutung |
|------|-----------|
| `Month = 0` | Monatlich. |
| `Quarter = 1` | Quartalsweise. |
| `HalfYear = 2` | Halbjährlich. |
| `Year = 3` | Jährlich. |
| `Ytd = 4` | Year-to-date, kumulativ je Kalenderjahr. |
| `AllHistory = 5` | Gesamthistorie, eine Zeile je Gruppe — **von der Hochrechnung ausgeschlossen** (Gate in `ReportAggregationService` Z. 60/887, `ReportPostingsQueryService` Z. 83, ViewModel Z. 227, Page Z. 595). |

## `SecurityPostingSubType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf (Bestands-Plus in `HasPositiveHolding`). |
| `Sell = 1` | Verkauf (Bestands-Minus; positive Menge zählt ebenfalls als Verkauf, Reversal kippt das Vorzeichen). |
| `Dividend = 2` | Dividende — Anker-Subtyp der Dividendengruppen und implizite Bedingung des Netto-Pfads. |
| `Fee = 3` | Gebühr (Netting-Mitglied). |
| `Tax = 4` | Steuer (Netting-Mitglied). |

## `SavingsPlanPostingSource`
Datei: `FinanceManager.Shared/Dtos/Reports/SavingsPlanPostingSource.cs`

| Wert | Bedeutung |
|------|-----------|
| `PlanPostings = 0` | Eigene Sparplan-Postings. |
| `LinkedAccountPostings = 1` | Bank-Postings der verknüpften Konten (abgeleitete Reihen; Wechselwirkung mit der Hochrechnung ungeklärt — Offene Frage 4 der Anforderung). |

## `SavingsPlanPostingSubType`
Datei: `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `ActualReport = 0` | Istmeldung — manuelle Bestandskorrektur ohne Kontobewegung; potenziell relevanter „Event"-Sonderfall für die Sparplan-Hochrechnung. |

## `AggregatePeriod` / `AggregateDateKind`
Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs`

| Enum | Werte | Bedeutung |
|------|-------|-----------|
| `AggregatePeriod` | `Month`, `Quarter`, `HalfYear`, `Year` | Granularität der vorverdichteten `PostingAggregates` (kein Ytd/AllHistory — die nutzen Month-Rohdaten). |
| `AggregateDateKind` | `Booking`, `Valuta` | Datumsbasis der Verdichtung. |

## `DividendPattern` (privat)
Datei: `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (Z. 1631–1637)

| Wert | Bedeutung |
|------|-----------|
| `Monthly` | ≥6 regelmäßige Monatsereignisse im Referenzjahr. |
| `Quarterly` | 2–5 Ereignisse in ≥2 Quartalen, 60–125 Tage Abstand. |
| `Annual` | Genau 1 Referenzereignis. |
| `Irregular` | Fallback. |

Hinweis: `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`) assertiert vollständige Memberlisten öffentlicher Enums; `DividendPattern` ist `private` und davon nicht erfasst. Kein neues öffentliches Enum-Mitglied ist für die Anforderung zu erwarten.
