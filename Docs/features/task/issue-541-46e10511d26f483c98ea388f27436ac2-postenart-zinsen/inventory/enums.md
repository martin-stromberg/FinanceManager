# Enums

## `PostingRuleField`

Datei: `FinanceManager.Shared/Dtos/Accounts/PostingRuleField.cs`

| Wert | Bedeutung |
|------|-----------|
| `Subject = 0` | Match gegen Verwendungszweck (`EnumType_PostingRuleField_Subject` = „Verwendungszweck"/„Purpose") |
| `BookingDescription = 1` | Match gegen Buchungsbeschreibung (`EnumType_PostingRuleField_BookingDescription` = „Buchungsbeschreibung"/„Booking description") |

In `EnumCoverageTests.PostingRuleField_Values` abgesichert (`FinanceManager.Tests/Common/EnumCoverageTests.cs` Z. 181–184). Im Panel als `_ruleFields` fest verdrahtet (beide Werte).

## `BankPostingType`

Datei: `FinanceManager.Shared/Dtos/Postings/BankPostingType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unspecified = 0` | Keine Postenart (im Panel-Dropdown **nicht** auswählbar; serverseitig beim Upsert abgelehnt) |
| `Inflow = 1` | Eingang |
| `Outflow = 2` | Ausgang |
| `Interest = 3` | Zinsen |
| `Tax = 4` | Steuer |

In `EnumCoverageTests` abgesichert (Z. 170–175). Im Panel als `_postingTypes` = {Inflow, Outflow, Interest, Tax}.

## `EmbeddedPanelPosition`

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Z. 33–49)

| Wert | Bedeutung |
|------|-----------|
| `AfterRibbon` | Panel nach der Ribbon |
| `AfterCard` | Panel nach dem Kartenbereich |
| `AfterList` | Panel nach dem Listenbereich |

Nur Kontext (`EmbeddedPanelHost`); `PostingRulesPanel` nutzt diesen Mechanismus nicht.

## `UiRibbonItemSize`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Z. 6–12)

| Wert | Bedeutung |
|------|-----------|
| `Small` | Sekundäre Aktion |
| `Large` | Primäre/prominente Aktion (Label unter dem Icon) |

## `UiRibbonRegisterKind`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Z. 17–29)

| Wert | Bedeutung |
|------|-----------|
| `QuickAccess` | Schnellzugriff |
| `Actions` | Hauptaktionen (der Bankkonto-Karte nutzt nur dieses Register) |
| `LinkedInfo` | Verknüpfte Informationen |
| `Reports` | Berichte |
| `Custom` | Erweiterungen |
