# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Klick auf Ribbon-Aktion „Konto öffnen" (`OpenAccount`) in der Gruppe „Verknüpfte Informationen" auf der Posten-Karte → unauffällig. Der Button feuert `RequestOpenLinkedCard("accounts", Posting.AccountId)`, das `RaiseUiActionRequested("OpenPostings", "/card/accounts/{id}?back=…")` auslöst; `CardPage.razor` (`case "OpenPostings"`, Zeile 192–194) navigiert per `NavigationManager.NavigateTo(payload)` zur Konto-Karte (`BankAccountCardViewModel`, `CardRoute("accounts")`).
- Klick auf „Kontakt öffnen" (`OpenContact`) → unauffällig. Gleiches Verdrahtungsmuster, Ziel `/card/contacts/{id}` (`ContactCardViewModel`, `CardRoute("contacts")`).
- Klick auf „Sparplan öffnen" (`OpenSavingsPlan`) → unauffällig. Ziel `/card/savings-plans/{id}` (`SavingsPlanCardViewModel`, `CardRoute("savings-plans")`).
- Klick auf „Wertpapier öffnen" (`OpenSecurity`) → unauffällig. Ziel `/card/securities/{id}` (`SecurityCardViewModel`, `CardRoute("securities")`).
- Klick auf „Anhänge" (`OpenAttachments`) → unauffällig. `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` erzeugt ein `UiOverlaySpec` (`AttachmentsPanel` mit `ParentKind`/`ParentId`), das `OverlayHost.razor` (Zeile 86–94) abfängt und als modales Overlay rendert; `AttachmentsPanel` ist art-agnostisch und arbeitet mit `(short)ParentKind = 7` (`AttachmentEntityKind.Posting`). Der No-Op-`case "OpenAttachments"` in `CardPage.razor` (Zeile 253–255) ist bewusst so vorgesehen.
- Rücknavigation von der Zielkarte zur Posten-Karte (`back`-Parameter) → unauffällig. Die Ziel-URL enthält `?back=` mit der aktuellen URI; der `Back`-Handler der Ziel-`CardPage` (Zeile 139–143) navigiert bevorzugt zu `Back`. Identisch zum etablierten Muster in `StatementDraftEntryCardViewModel` (Zeilen 997–1053).
- Verhalten bei fehlender Verknüpfung (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId == null`) → unauffällig. Die jeweilige Aktion ist deaktiviert (`disabled`-Flag auf das `UiRibbonAction`-Element gesetzt), ein Klick ist nicht möglich — exakt die in der Anforderung geforderte Beibehaltung. Das Ribbon baut seine Tabs bei jedem Render neu auf (`Ribbon.razor` Zeile 163–170), sodass der Disabled-Zustand nach dem Laden des Postens korrekt ist.
- Verhalten bei nicht geladenem Posten (`Posting == null`) → unauffällig. Alle fünf Linked-Aktionen sind deaktiviert; `OpenAttachments` wurde von hart `false` auf `Posting == null` umgestellt, sodass kein Overlay mit leerer `ParentId` geöffnet werden kann.
- Keine interne/technische Kennung erforderlich: Die Anwenderin muss an keiner Stelle Ids, GUIDs oder technische Schlüssel kennen oder eingeben — alle Aktionen wirken direkt auf den angezeigten Posten.
- Wiederverwendung etablierter Muster: Die Implementierung nutzt exakt das im Bestand etablierte `OpenPostings`-URL-Payload-Muster sowie `RequestOpenAttachments` aus `BaseViewModel` — keine abweichende Eigenlösung.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` (geändert — Ribbon-Definition der `Linked`-Gruppe, Zeilen 138–145, neue Hilfsmethode `RequestOpenLinkedCard`, Zeilen 240–263)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (unverändert, zur Handler-Verifikation geprüft — `OnUiActionRequested`-Switch, `case "OpenPostings"` Zeile 192–194, `case "OpenAttachments"` Zeile 253–255, `Back`-Behandlung Zeile 130–191, `OverlayHost`-Einbindung Zeile 73)
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` (unverändert, zur Overlay-Verifikation geprüft)
- `FinanceManager.Web/Components/Shared/AttachmentsPanel.razor` (unverändert, Parameter `ParentKind`/`ParentId` geprüft)
- `FinanceManager.Web/Components/Shared/Ribbon.razor` (unverändert, Disabled-Rendering und Rebuild-Verhalten geprüft)
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (unverändert, `RequestOpenAttachments`/`UiOverlaySpec`/`UiActionEventArgs` geprüft)
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` (unverändert, Referenzmuster zum Abgleich geprüft)

## Hinweise

- Die Implementierung ist uncommitted im Working Tree; Prüfung erfolgte anhand von `git diff` gegen den Working-Tree-Stand. `dotnet build FinanceManager.Web` wurde zur Verifikation erfolgreich ausgeführt (0 Fehler).
- Keine UI-Dateien (`.razor`) wurden geändert — die ausschließlich betroffene UI-Definitionsstelle ist die Ribbon-Definition im ViewModel.
