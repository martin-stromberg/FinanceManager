# Datenmodell: Bestehende DTOs und Entities

## Bestehende DTOs

### `BudgetReportRawDataDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`

Wrapper-DTO für einen kompletten Budgetbericht für einen Zeitraum.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| PeriodStart | DateTime | Inclusive Start des Berichtszeitraums |
| PeriodEnd | DateTime | Inclusive End des Berichtszeitraums |
| Categories | BudgetReportCategoryRawDataDto[] | Kategorisierte Zwecke |
| UncategorizedPurposes | BudgetReportPurposeRawDataDto[] | Unkategorisierte Zwecke |
| UnbudgetedPostings | BudgetReportPostingRawDataDto[] | Buchungen ohne Budgetzuordnung |

### `BudgetReportCategoryRawDataDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`

Rohdaten einer Budgetkategorie mit ihren Zwecken und Budgetierungsinformationen.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| CategoryId | Guid | Kategorie-ID |
| CategoryName | string | Kategoriename |
| BudgetedIncome | decimal | Budgetiertes Einkommen (positive Beträge) |
| BudgetedExpense | decimal | Budgetierte Ausgaben (negative Beträge als Negativwert) |
| BudgetedTarget | decimal | Netto-Budgetziel (Einkommen + Ausgaben) |
| BudgetedAmount | decimal | Rückwärtskompatibel: Alias für BudgetedTarget |
| Purposes | BudgetReportPurposeRawDataDto[] | Zwecke in dieser Kategorie |

### `BudgetReportPurposeRawDataDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`

Rohdaten eines Budgetzwecks mit zugeordneten Buchungen und Budgetierungsinformationen.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| PurposeId | Guid | Zweck-ID |
| PurposeName | string | Zweckname |
| BudgetedIncome | decimal | Budgetiertes Einkommen für diesen Zweck |
| BudgetedExpense | decimal | Budgetierte Ausgaben für diesen Zweck |
| BudgetedTarget | decimal | Netto-Budgetziel |
| BudgetSourceType | BudgetSourceType | Typ der Quelle (Contact, ContactGroup, SavingsPlan) |
| SourceId | Guid | ID der Quellentität |
| SourceName | string | Name der Quellentität |
| ValuationType | BudgetValuationType | Wie Buchungen bewertet werden (ExactPostings/TotalBudget) |
| Postings | BudgetReportPostingRawDataDto[] | Diesem Zweck zugeordnete Buchungen |

### `BudgetReportPostingRawDataDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`

Rohdaten einer Buchung mit Budget-Metadaten.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| PostingId | Guid | Eindeutige Buchungs-ID |
| BookingDate | DateTime | Buchungsdatum |
| ValutaDate | DateTime? | Valutadatum (optional) |
| Amount | decimal | Buchungsbetrag |
| PostingKind | PostingKind | Art der Buchung |
| Description | string | Buchungsbeschreibung |
| Subject | string | Buchungsbetreff |
| AccountId | Guid? | Konto-ID (optional) |
| AccountName | string? | Kontoname |
| ContactId | Guid? | Kontakt-ID (optional) |
| ContactName | string? | Kontaktname |
| SavingsPlanId | Guid? | Sparplan-ID (optional) |
| SavingsPlanName | string? | Sparplan-Name |
| SecurityId | Guid? | Wertpapier-ID (optional) |
| SecurityName | string? | Wertpapier-Name |
| BudgetCategoryId | Guid? | Zugeordnete Budgetkategorie (optional) |
| BudgetCategoryName | string? | Kategorienname |
| BudgetPurposeId | Guid? | Zugeordneter Budgetzweck (optional) |
| BudgetPurposeName | string? | Zweckname |
| IsValuedForBudgetPurpose | bool | Ob diese Buchung als Istwert für den Budgetzweck zählt |
| GroupId | Guid? | Gruppen-ID für Spiegelbuchungen (optional) |

### `MonthlyBudgetKpiDto`
Datei: `FinanceManager.Shared/Dtos/Budget/MonthlyBudgetKpiDto.cs`

KPI-Daten für einen einzelnen Monat mit Planung/Ist-Vergleichen und erwarteten Werten.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| PlannedIncome | decimal | Geplantes Einkommen |
| PlannedExpenseAbs | decimal | Geplante Ausgaben (absolute Wert) |
| ActualIncome | decimal | Tatsächliches Einkommen |
| ActualExpenseAbs | decimal | Tatsächliche Ausgaben (absolute Wert) |
| ActualResult | decimal | Tatsächliches Ergebnis (Einkommen - Ausgaben) |
| PlannedResult | decimal | Ziel-Ergebnis (geplant) |
| ExpectedIncome | decimal | Erwartetes Einkommen (geplant + unbudgetiert) |
| ExpectedExpenseAbs | decimal | Erwartete Ausgaben (geplant + unbudgetiert) |
| RemainingPlannedExpenseAbs | decimal | Verbleibende budgetierte Ausgaben |
| RemainingPlannedIncome | decimal | Verbleibendes budgetiertes Einkommen |
| ExpectedTargetResult | decimal | Erwartetes Ziel-Ergebnis |
| UnbudgetedIncome | decimal | Unbudgetiertes Einkommen |
| UnbudgetedExpenseAbs | decimal | Unbudgetierte Ausgaben (absolute Wert) |
| BudgetedRealizedIncome | decimal | Budgetiertes realisiertes Einkommen |
| BudgetedRealizedExpenseAbs | decimal | Budgetierte realisierte Ausgaben (absolute Wert) |

### `BudgetReportDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Report-Response mit aggregierten Daten nach Intervallen (Monat/Quartal/Jahr).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| RangeFrom | DateOnly | Beginn des Berichtszeitraums |
| RangeTo | DateOnly | Ende des Berichtszeitraums |
| Interval | BudgetReportInterval | Aggregations-Intervall (Monat/Quartal/Jahr) |
| Periods | BudgetReportPeriodDto[] | Daten pro Intervall |
| Categories | BudgetReportCategoryDto[] | Kategoriedaten |

### `BudgetReportPeriodDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Eine Periode (Monat/Quartal/Jahr) mit Budget/Ist-Vergleich.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| From | DateOnly | Periode Beginn |
| To | DateOnly | Periode End |
| Budget | decimal | Gesamtbudget für die Periode |
| Actual | decimal | Istwert für die Periode |
| Delta | decimal | Differenz (Ist - Plan) |
| DeltaPct | decimal | Prozentuale Differenz |

### `BudgetReportCategoryDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Kategorie-Zeile im Report mit Semantik-Information.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Kategorie-ID |
| Name | string | Kategoriename |
| Kind | BudgetReportCategoryRowKind | Art der Zeile (Data/Sum/Unbudgeted/etc.) |
| Budget | decimal | Budget für die Kategorie |
| Actual | decimal | Istwert |
| Delta | decimal | Differenz |
| DeltaPct | decimal | Prozentuale Differenz |
| Purposes | BudgetReportPurposeDto[] | Zwecke in dieser Kategorie |

### `BudgetReportPurposeDto`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Zweck-Zeile innerhalb einer Kategorie im Report.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Zweck-ID |
| Name | string | Zweckname |
| Budget | decimal | Budget für diesen Zweck |
| Actual | decimal | Istwert |
| Delta | decimal | Differenz |
| DeltaPct | decimal | Prozentuale Differenz |
| SourceType | BudgetSourceType | Quellentyp |
| SourceId | Guid | Quellen-ID |

### `BudgetReportRequest`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs`

Request-Parameter für Budgetbericht-Generierung.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| AsOfDate | DateOnly | Stichtag für die Berechnung |
| Months | int | Anzahl der zu berücksichtigenden Monate |
| Interval | BudgetReportInterval | Aggregations-Intervall |
| ShowTitle | bool | Titel anzeigen |
| ShowLineChart | bool | Liniendiagramm anzeigen |
| ShowMonthlyTable | bool | Monatstabelle anzeigen |
| ShowDetailsTable | bool | Detailtabelle anzeigen |
| CategoryValueScope | BudgetReportValueScope | Wertbereich (TotalRange/LastInterval) |
| IncludePurposeRows | bool | Zweck-Zeilen einschließen |
| DateBasis | BudgetReportDateBasis | Datumsbasis für Buchungszuordnung |

## Bestehende Domain Entities

### `BudgetCategory`
Datei: `FinanceManager.Domain/Budget/BudgetCategory.cs`

Hauptentität zur Gruppierung von Budgetzwecken.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Eindeutige ID (geerbt von Entity) |
| OwnerUserId | Guid | Benutzer, dem die Kategorie gehört |
| Name | string | Anzeigename der Kategorie |

Methoden: `Rename(name)`, `ToBackupDto()`, `AssignBackupDto(dto)`

### `BudgetPurpose`
Datei: `FinanceManager.Domain/Budget/BudgetPurpose.cs`

Hauptentität, die einen Budgetzweck definiert mit Source-Mapping und Bewertungstyp.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Eindeutige ID |
| OwnerUserId | Guid | Benutzer |
| Name | string | Zweckname |
| Description | string? | Optionale Beschreibung |
| SourceType | BudgetSourceType | Art der Quelle (Contact/ContactGroup/SavingsPlan) |
| SourceId | Guid | ID der Quellentität |
| BudgetCategoryId | Guid? | Optional zugeordnete Kategorie |
| ValuationType | BudgetValuationType | Bewertungstyp (ExactPostings/TotalBudget) |

Methoden: `Rename(name)`, `SetDescription(desc)`, `SetSource(type, id)`, `SetCategory(id)`, `SetValuationType(type)`, `ToBackupDto()`, `AssignBackupDto(dto)`

### `BudgetRule`
Datei: `FinanceManager.Domain/Budget/BudgetRule.cs`

Hauptentität, die einen Budgetregel mit Betrag, Intervall und optionalem Pattern definiert.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Eindeutige ID |
| OwnerUserId | Guid | Benutzer |
| BudgetPurposeId | Guid? | Optional: Zweck-ID (exklusiv mit CategoryId) |
| BudgetCategoryId | Guid? | Optional: Kategorie-ID (exklusiv mit PurposeId) |
| Amount | decimal | Erwarteter Betrag (positiv oder negativ) |
| PurposePattern | string? | Optionales Pattern für Zweck/Vertrag-Matching |
| PurposePatternIsRegex | bool | Ob PurposePattern als Regex behandelt wird |
| Interval | BudgetIntervalType | Intervall-Definition |
| CustomIntervalMonths | int? | Custom Interval in Monaten (wenn CustomMonths) |
| StartDate | DateOnly | Startdatum (inclusive) |
| EndDate | DateOnly? | Optionales Enddatum (inclusive) |

Methoden: `SetAmount(amount)`, `SetSchedule(interval, start, end, customMonths)`, `SetPurposePattern(pattern, isRegex)`, `GetIntervalStepMonths()`, `ToBackupDto()`, `AssignBackupDto(dto)`

## Bestehende Hilfsklassen

### `ReportCacheEntry`
Datei: `FinanceManager.Domain/Reports/ReportCacheEntry.cs`

Datenbankentität zur Persistierung von Budgetbericht-Cache-Daten.

### `BudgetRulePatternMatcher`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetRulePatternMatcher.cs`

Statische Hilfsklasse zum Matching von Budget-Rule-Patterns gegen Buchungstexte (unterstützt einfache Substring-Matches und Regex).

| Methode | Beschreibung |
|---------|-------------|
| MatchesPosting(subject, description, pattern, useRegex, timeout) | Prüft, ob Pattern auf Buchung passt |
