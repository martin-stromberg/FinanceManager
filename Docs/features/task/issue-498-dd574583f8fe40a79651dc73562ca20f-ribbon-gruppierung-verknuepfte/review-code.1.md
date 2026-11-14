# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PostingsCardViewModel.cs (PostingsCardViewModel)

- **Projektregel: `RaiseUiActionRequested`-Aktionen ohne Handler** — Die vier Linked-Aktionen `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` (Zeilen 139–142) rufen `RaiseUiActionRequested(...)` ohne Payload auf. Die Karte wird ausschließlich über `CardRoute("postings")` von `CardPage.razor` gehostet; deren `OnUiActionRequested`-Switch enthält keinen `case` für diese Aktionen (geprüft: `CardPage.razor` Zeilen 128–365, `OverlayHost.razor` reagiert nur auf `UiOverlaySpec`-Payloads, `EmbeddedPanelHost.razor` nur auf `EmbeddedPanelSpec`/`ClearEmbeddedPanel`). Ein Klick auf die Buttons bewirkt nichts.

  Ebenso `OpenAttachments` (Zeile 143): Es wird `RaiseUiActionRequested("OpenAttachments")` ohne Payload aufgerufen. `CardPage.razor` behandelt `case "OpenAttachments"` explizit als No-op („viewmodels must send UiOverlaySpec"), und `OverlayHost` ignoriert Aufrufe ohne `UiOverlaySpec`-Payload — der Button ist ebenfalls wirkungslos. Die anderen beiden Karten verwenden korrekt `RequestOpenAttachments(kind, id)`, das den Overlay-Spec mitsendet.

  Hinweis: Der Defekt ist **nicht durch diesen Branch entstanden** — die Aktionen existieren unverändert bereits auf `staging` (verifiziert via `git show staging:...`). Er wurde bei der geforderten Handler-Verifikation der geänderten Datei festgestellt und sollte entweder hier oder in einem Folge-Issue behoben werden.

  Empfehlung: In `CardPage.razor` `case`-Handler für `OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity` ergänzen (Navigation zur jeweiligen Karte, analog `OpenBankContact`), oder die Aktionen direkt mit URL-Payload über ein vorhandenes generisches `case` (z. B. `OpenPostings`-Muster `NavigateTo(payload)`) abfahren. Für `OpenAttachments` `RaiseUiActionRequested("OpenAttachments")` durch `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` ersetzen.

### AccountDetailViewModelTests.cs / PostingsCardViewModelReversalTests.cs / SavingsPlanCardLinkTests.cs (Testbenennung)

- **Namenskonventionen und Einheitlichkeit** — Drei neue Tests prüfen dieselbe Konvention (Gruppenreihenfolge, `Linked` letzter Tab), sind aber uneinheitlich benannt: `GetRibbon_GroupOrder_PlacesLinkedLast` (`AccountDetailViewModelTests.cs:211`), `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (`PostingsCardViewModelReversalTests.cs:247`) und `Ribbon_GroupOrder_PlacesLinkedLast` (`SavingsPlanCardLinkTests.cs:333`). Zusätzlich ruft `GetRibbon_GroupOrder_PlacesLinkedLast` fälschlich benannt `vm.GetRibbonRegisters(loc)` auf (Zeile 216), nicht `GetRibbon` — im selben File heißt der Geschwistertest `GetRibbon_ContainsExpectedGroups`, weil er tatsächlich `GetRibbon` aufruft.

  Empfehlung: Alle drei Tests einheitlich `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` benennen (entspricht dem tatsächlich aufgerufenen API und der Bennung im Postings-Test).

## Geprüfte Dateien

- `AGENT.md`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`
- `FinanceManager.Tests/Components/RibbonTests.cs`
- `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs`
- `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs`
