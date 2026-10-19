# Enums

## `PostingExportFormat`
Datei: `FinanceManager.Application/Reports/IPostingExportService.cs` (Zeilen 6–17)

| Wert | Bedeutung |
|------|-----------|
| `Csv` | Comma-separated values (tatsächlich `;`-separiert) |
| `Xlsx` | Excel .xlsx-Format |

## `StatementDraftStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Draft = 0` | Entwurf offen/editierbar |
| `Committed = 1` | Committed und gebucht |
| `Expired = 2` | Abgelaufen/ungültig |

## `StatementDraftEntryStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Open = 0` | Offen, benötigt Klassifizierung |
| `Announced = 1` | Angekündigt, wartet auf Verarbeitung |
| `Accounted = 2` | Hat zugeordneten Kontakt |
| `AlreadyBooked = 3` | Bereits gebucht / Duplikat erkannt |

Hinweis: `StatementDraftCardViewModel` schließt `AlreadyBooked` und `Announced` bei Summen-/Zählanzeige aus; die Embedded-Liste sortiert `AlreadyBooked` nach hinten.

## `PostingKind`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Posten bezogen auf Bankkonto |
| `Contact = 1` | Posten bezogen auf Kontakt |
| `SavingsPlan = 2` | Posten bezogen auf Sparplan |
| `Security = 3` | Posten bezogen auf Wertpapier |

Steuert im Posten-Export Kontext + Besitzprüfung + Dateinamen-Präfix.

## `SecurityTransactionType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityTransactionType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf |
| `Sell = 1` | Verkauf |
| `Dividend = 2` | Dividende |

Feld von `StatementDraftEntry`/`StatementDraftEntryDto` (nullable).

## `UiRibbonItemSize`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 6–12)

| Wert | Bedeutung |
|------|-----------|
| `Small` | Kleine Sekundäraktion |
| `Large` | Große Primäraktion |

## `UiRibbonRegisterKind`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 17–29)

| Wert | Bedeutung |
|------|-----------|
| `QuickAccess` | Schnellzugriffsbereich |
| `Actions` | Hauptaktionen (hier fügt der Kontoauszug seine Tabs hinzu) |
| `LinkedInfo` | Verknüpfte Informationen |
| `Reports` | Berichtsaktionen |
| `Custom` | Erweiterungen |
