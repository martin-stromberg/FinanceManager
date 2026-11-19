# Datenmodell

## `ReportFavorite`
Datei: `FinanceManager.Domain/Reports/ReportFavorite.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `OwnerUserId` | `Guid` | Besitzer des Favoriten (Zeile 101) |
| `Name` | `string` | Anzeigename (Zeile 107) |
| `PostingKind` | `PostingKind` | Primäre Postenart (Zeile 113) |
| `IncludeCategory` | `bool` | Kategorie-Gruppierung (Zeile 119) |
| `Interval` | `ReportInterval` | Aggregationsintervall (Zeile 125) |
| `ComparePrevious` / `CompareYear` / `CompareProjection` | `bool` | Vergleichsoptionen; `CompareProjection` steuert die Hochrechnung (Zeilen 131/137/143) |
| `ShowChart` / `ChartType` / `ChartPosition` / `Expandable` | div. | UI-Optionen (Zeilen 149–165) |
| `PostingKindsCsv` | `string?` | CSV der Mehrfach-Postenarten (Zeile 171) |
| `Take` | `int` | Anzahl Perioden, Clamp 1–120 (Zeile 177) |
| `AccountIdsCsv` … `SecurityCategoryIdsCsv` | `string?` | Entity-/Kategorie-Filter als CSV (Zeilen 184–207) |
| `SecuritySubTypesCsv` | `string?` | CSV der Security-SubTypes (int) (Zeile 212) |
| `BankPostingTypesCsv` | `string?` | **CSV der gefilterten Bank-Postenarten (int) — persistierter Filter der Anforderung** (Zeile 217) |
| `BankTextFiltersJson` … `SecurityTextFiltersJson` | `string?` | Freitextfilter als JSON (Zeilen 222–234) |
| `IncludeDividendRelated` | `bool?` | Dividenden-Fee/Tax-Option (Zeile 239) |
| `UseValutaDate` | `bool` | Aggregation nach Valuta- statt Buchungsdatum (Zeile 244) |
| `SavingsPlanPostingSource` | `SavingsPlanPostingSource` | Quelle der Sparplan-Posten (Zeile 250) |

Relevante Methoden:
- `SetFilters(...)` (Zeilen 366–397): letzter optionaler Parameter `IEnumerable<int>? bankPostingTypes = null`; persistiert via `ToCsvInt` in `BankPostingTypesCsv` (Zeile 390).
- `GetFilters()` (Zeilen 407–425): liefert `ReportFavoriteFilterTuple` inkl. `BankPostingTypes` via `FromCsvInt(BankPostingTypesCsv)` (Zeile 418).
- `ReportFavoriteBackupDto` (Zeilen 441–472): letzter Parameter `string? BankPostingTypesCsv = null` — Default sichert Lesbarkeit alter Backups.
- `ToBackupDto()` (Zeile 479) und `AssignBackupDto(dto)` (Zeilen 485–517, Zuweisung Zeile 516) mappen `BankPostingTypesCsv` vollständig.
- EF-Mapping: `AppDbContext` (`FinanceManager.Infrastructure/AppDbContext.cs`, `ReportFavorite`-Konfiguration ab Zeile 736); Spalte über Migration `20261118192951_AddReportFavoriteBankPostingTypes` (TEXT, nullable) ergänzt.

## `Posting`
Datei: `FinanceManager.Domain/Postings/Posting.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `PostingType` | `BankPostingType` | Postenart des Bankpostens; `Unspecified` für Nicht-Bankpostings (Zeile 277). Setter `SetPostingType(BankPostingType)` fluent (Zeile 284) |
| `PostingBackupDto.PostingType` | `BankPostingType?` | Nullable mit Default `null`; Restore-Fallback in `AssignBackupDto` auf `PostingTypeEvaluator.EvaluateByAmountSign` für Bankpostings (Zeilen 537, 544, 581–582) |

Weitere relevante Felder im Filterpfad: `Kind` (`PostingKind`), `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId`, `BookingDate`, `ValutaDate`, `Amount`, `Subject`, `Description`, `SecuritySubType`, `GroupId`.

## `StatementDraftEntry`
Datei: `FinanceManager.Domain/Statements/StatementDraft.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `PostingType` | `BankPostingType` | Klassifizierte Postenart der Entwurfszeile; wird beim Buchen auf den Bankposten übertragen (Zeile 425) |
| `IsPostingTypeManual` | `bool` | Markiert manuell gesetzte Postenart; wird bei Reklassifizierung nicht überschrieben (Zeilen 430, 438–443) |
| `StatementDraftEntryBackupDto.PostingType` / `IsPostingTypeManual` | `BankPostingType?` / `bool` | Backup-Defaults `null`/`false`; Restore-Fallback via `EvaluateByAmountSign` (Zeilen 643, 680) |

## `AccountPostingRule`
Datei: `FinanceManager.Domain/Accounts/AccountPostingRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|---|---|---|
| `AccountId` | `Guid` | Zugehöriges Konto (Zeile 29) |
| `Field` | `PostingRuleField` | Regelfeld (`Subject`/`BookingDescription`) (Zeile 34) |
| `Pattern` | `string` | Suchtext der Regel (Zeile 39) |
| `PostingType` | `BankPostingType` | Zugewiesene Postenart bei Regeltreffer (Zeile 44) |
| `OrderIndex` | `int` | Auswertungsreihenfolge (Zeile 49) |

## DTO-Records (Shared)

| Record | Datei | `BankPostingTypes`-Mitglied |
|---|---|---|
| `ReportAggregatesFiltersRequest` | `FinanceManager.Shared/Dtos/Reports/ReportAggregatesFiltersRequest.cs` | `IReadOnlyCollection<int>? BankPostingTypes = null` (Zeile 31) |
| `ReportAggregationFilters` | `FinanceManager.Shared/Dtos/Reports/ReportAggregationFilters.cs` | `IReadOnlyCollection<int>? BankPostingTypes = null` (Zeile 32) — Domänen-Filterobjekt der Aggregation |
| `ReportFavoriteFiltersDto` | `FinanceManager.Shared/Dtos/Reports/ReportFavoriteFiltersDto.cs` | `IReadOnlyCollection<int>? BankPostingTypes = null` (Zeile 31) — Filter-Payload des Favoriten |
| `ReportAggregationQuery` | `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs` | enthält `Filters` vom Typ `ReportAggregationFilters` (Zeile 33) |
| `ReportAggregatePointDto` | `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs` | `ProjectionAmount` (Zeile 28) und `ProjectionExpectedItems` (Zeile 32) — Ergebnisfelder der Hochrechnung |
| `ReportFavoriteDto` | `FinanceManager.Shared/Dtos/Reports/ReportFavoriteDtos.cs` | `Filters` vom Typ `ReportFavoriteFiltersDto?` |
| `ReportFavoriteCreateApiRequest` / `ReportFavoriteUpdateApiRequest` | `FinanceManager.Shared/Dtos/Reports/` | `Filters` vom Typ `ReportFavoriteFiltersDto?` (Zeile 33) |

Hinweis zum Review-Punkt „doppelte `<param name=\"BankPostingTypes\">`-Einträge": In `ReportAggregatesFiltersRequest`, `ReportAggregationFilters` und `ReportFavoriteFiltersDto` existiert aktuell jeweils genau **ein** `BankPostingTypes`-`<param>`-Eintrag; eine Duplikation ist im Code nicht vorhanden.
