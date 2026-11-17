# Enums – Bestand

## `AttachmentEntityKind`

Datei: `FinanceManager.Domain/Attachments/AttachmentEntityKind.cs` — `public enum AttachmentEntityKind : short`.

| Wert | Bedeutung |
|------|-----------|
| `None = -1` | Keine Entität. |
| `StatementDraftEntry = 0` | Anhang an Entwurfsposten. |
| `StatementEntry = 1` | Anhang an gebuchten Kontoauszugsposten. |
| `Contact = 2` | Anhang an Kontakt. |
| `SavingsPlan = 3` | Anhang an Sparplan. |
| `Security = 4` | Anhang an Wertpapier. |
| `Account = 5` | Anhang an Konto. |
| `StatementImport = 6` | Anhang an Kontoauszugs-Import. |
| `Posting = 7` | **Anhang an Posten — für `OpenAttachments` der Posten-Karte zu verwenden; existiert bereits.** |
| `StatementDraft = 8` | Anhang an Kontoauszugsentwurf. |
| `ContactCategory = 9` | Anhang an Kontaktkategorie. |
| `SavingsPlanCategory = 10` | Anhang an Sparplankategorie. |
| `SecurityCategory = 11` | Anhang an Wertpapierkategorie. |

Hinweis: `AttachmentEntityKind` ist **nicht** in `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`) abgesichert.

## `PostingKind`

Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`.

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Bankposten → Back-Payload `account/{AccountId}` (`PostingsCardViewModel` Zeile 126). |
| `Contact = 1` | Kontaktposten → `contact/{ContactId}` (Zeile 127). |
| `SavingsPlan = 2` | Sparplanposten → `savings-plan/{SavingsPlanId}` (Zeile 128). |
| `Security = 3` | Wertpapierposten → `security/{SecurityId}` (Zeile 129). |

## `UiRibbonItemSize`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 6–12). `Small`, `Large`. In `EnumCoverageTests.UiRibbonItemSize_Values` abgesichert.

## `UiRibbonRegisterKind`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 17–29).

| Wert | Bedeutung |
|------|-----------|
| `QuickAccess` | Quick-Access-Register. |
| `Actions` | Hauptregister — `PostingsCardViewModel` liefert genau eines davon (Zeile 154). |
| `LinkedInfo` | In AGENT.md als Register-Kind der Linked-Gruppe genannt; **wird von keinem der betrachteten ViewModels verwendet** (Linked-Tab liegt im `Actions`-Register). |
| `Reports` | Report-Register. |
| `Custom` | Erweiterungsregister. |

In `EnumCoverageTests.UiRibbonRegisterKind_Values` abgesichert.

## `EmbeddedPanelPosition` / `BooleanSelection`

Beide in `BaseViewModel.cs` (Zeilen 33–49 bzw. 17–28), in `EnumCoverageTests` abgesichert; für diese Anforderung nicht relevant.
