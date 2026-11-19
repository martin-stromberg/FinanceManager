# Enums

## `BankPostingType`
Datei: `FinanceManager.Shared/Dtos/Postings/BankPostingType.cs`

| Wert | Bedeutung |
|---|---|
| `Unspecified = 0` | Keine Postenart bestimmt (Default; Nicht-Bankpostings und nicht klassifizierte Einträge) |
| `Inflow = 1` | Eingehender Posten (positiver Betrag) |
| `Outflow = 2` | Ausgehender Posten (negativer Betrag) |
| `Interest = 3` | Zinsposten |
| `Tax = 4` | Steuerposten |

Bemerkungen:
- In `ReportDashboard.razor` (Zeilen 602–605) werden die Filterwerte als harte Ints `1`–`4` an `ToggleTempBankPostingType` übergeben, nicht über die Enum-Member — `Unspecified` ist bewusst keine Filterauswahl.
- `EnumCoverageTests.BankPostingType_Values` (`FinanceManager.Tests/Common/EnumCoverageTests.cs:169-175`) assertiert exakt diese fünf Member.

## Verwandte Enums im Filter-/Projektionspfad

| Enum | Datei | Rolle |
|---|---|---|
| `PostingKind` | `FinanceManager.Shared/Dtos/Postings/` | `Bank`, `Contact`, `SavingsPlan`, `Security`; steuert, ob `BankPostingTypes` überhaupt ausgewertet wird (`kinds.Contains(PostingKind.Bank)`) |
| `SecurityPostingSubType` | `FinanceManager.Shared/Dtos/Securities/` | Analoger SubType-Filter für Security-Posten |
| `SavingsPlanPostingSource` | `FinanceManager.Shared/Dtos/Reports/SavingsPlanPostingSource.cs` | `PlanPostings` vs. `LinkedAccountPostings`; letzteres leitet Sparplan-Posten aus Bankposten ab — per Abgrenzung **ohne** Postenart-Filterung |
| `ReportInterval` | `FinanceManager.Shared/Dtos/Reports/ReportInterval.cs` | `Month`, `Quarter`, `HalfYear`, `Year`, `Ytd`, `AllHistory`; bei `AllHistory` ist die Projektion deaktiviert |
