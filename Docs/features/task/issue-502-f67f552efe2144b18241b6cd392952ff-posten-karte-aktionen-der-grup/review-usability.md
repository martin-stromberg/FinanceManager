# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Klick auf Ribbon-Aktion „Konto öffnen" (`OpenAccount`) auf der Posten-Karte → unauffällig: navigiert via generischem `OpenPostings`-Handler (`CardPage.razor` Zeile 192–194) zur Konto-Karte `/card/accounts/{AccountId}` inkl. `?back=`-Parameter; Route durch `CardRoute("accounts")` auf `BankAccountCardViewModel` abgedeckt.
- Klick auf „Kontakt öffnen" (`OpenContact`) → unauffällig: navigiert zu `/card/contacts/{ContactId}` (`CardRoute("contacts")` vorhanden).
- Klick auf „Sparplan öffnen" (`OpenSavingsPlan`) → unauffällig: navigiert zu `/card/savings-plans/{SavingsPlanId}` (`CardRoute("savings-plans")` vorhanden).
- Klick auf „Wertpapier öffnen" (`OpenSecurity`) → unauffällig: navigiert zu `/card/securities/{SecurityId}` (`CardRoute("securities")` vorhanden).
- Klick auf „Anhänge" (`OpenAttachments`) → unauffällig: `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` erzeugt `UiOverlaySpec` mit `AttachmentsPanel` + `ParentKind`/`ParentId`; `OverlayHost` rendert das Overlay modal, `AttachmentsPanel` besitzt die `[Parameter, EditorRequired]`-Properties `ParentKind`/`ParentId`.
- Zurück-Navigation von der Zielkarte zur Posten-Karte → unauffällig: `RequestOpenLinkedCard` hängt `NavigationManager.Uri` als `back`-Parameter an; `CardPage` bindet `back` per `SupplyParameterFromQuery` und der `Back`-Fall navigiert dorthin zurück.
- Deaktivierte Buttons bei fehlender Verknüpfung (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId == null`) bzw. fehlendem Posten (`Posting == null` für `OpenAttachments`) → unauffällig: `Disabled`-Flag an `UiRibbonAction` gesetzt, `Ribbon.razor` rendert `disabled`-Attribut und `aria-disabled`; kein wirkungsloser, scheinbar aktiver Button.
- (Iteration-2-Refactor) Linked-Aktionen auf der Statement-Draft-Entry-Karte → unauffällig: `StatementDraftEntryCardViewModel` nutzt jetzt dieselbe Hilfsmethode `RequestOpenLinkedCard` aus `BaseViewModel`; Verhalten (URL-Payload, `back`-Parameter, Null-Guard, Deaktivierungslogik, `MobileShortcut`) ist gegenüber dem bisherigen Inline-Code unverändert.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien (Branch-Diff, Working Tree gegen `staging`):
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (neue geschützte Hilfsmethode `RequestOpenLinkedCard`, Zeilen 336–366)
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` (Refactor auf `RequestOpenLinkedCard`, Zeilen 995–1009)

Kontext-/Verdrahtungsprüfung (unverändert, nur gelesen):
- `FinanceManager.Web/Components/Pages/CardPage.razor` (`case "OpenPostings"`, `case "OpenAttachments"`-No-Op, `back`-Bindung)
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` (rendert `UiOverlaySpec` via `DynamicComponent`)
- `FinanceManager.Web/Components/Shared/AttachmentsPanel.razor` (`ParentKind`/`ParentId`-Parameter)
- `FinanceManager.Web/Components/Shared/Ribbon.razor` (Disabled-Rendering)
