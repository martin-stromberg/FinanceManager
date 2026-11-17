# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PostingsCardViewModel.cs (Posten-Karte, Ribbon-Gruppe „Verknüpfte Informationen")

- **Wirkungslose Aktion** — Die in diesem Change an die letzte Position verschobene Gruppe „Verknüpfte Informationen" enthält auf der Posten-Karte fünf Schaltflächen („Konto öffnen", „Kontakt öffnen", „Sparplan öffnen", „Wertpapier öffnen", „Anhänge"), von denen **keine einzige eine Wirkung hat**. Eine Anwenderin öffnet einen Posten (z. B. eine Bankbuchung), sieht die verständlich beschrifteten, aktiven Buttons und klickt sie — es passiert schlicht nichts: keine Navigation, kein Overlay, keine Fehlermeldung, keine Erklärung der Blockade.

  Konkret (`PostingsCardViewModel.cs`, Zeilen 139–143):
  - `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` rufen `RaiseUiActionRequested("<Action>")` **ohne Payload** auf. In `CardPage.razor` (OnUiActionRequested, Zeilen 128–365) existiert für keinen dieser Aktionsnamen ein `case` — es gibt nur `OpenAccountDetails` (andere ID, anderer Aufrufer). Da zusätzlich kein Payload (Ziel-Id) mitgesendet wird, könnte selbst ein generischer Handler das Ziel nicht bestimmen. Die Buttons sind aktiv, sobald die jeweilige Id am Posting gesetzt ist (`Disabled: Posting?.AccountId == null` usw.).
  - `OpenAttachments` (Zeile 143, `Disabled: false` — immer klickbar) ruft `RaiseUiActionRequested("OpenAttachments")` ebenfalls ohne Payload auf. `CardPage.razor` Zeile 253–255 vermerkt explizit „No fallback" — das Attachments-Overlay wird nur über ein `UiOverlaySpec`-Payload geöffnet (vgl. `RequestOpenAttachments` in `BaseViewModel.cs`, Zeile 325, und `OverlayHost.razor`, Zeile 86–94). Der Klick bleibt folgenlos.

  Hinweis zur Einordnung: Der Defekt besteht bereits vor diesem Change (die Umordnung hat ihn nicht verursacht — im Diff wurde ausschließlich die Tab-Reihenfolge geändert). Er ist jedoch auf genau der geänderten Oberfläche unmittelbar sichtbar: Das Issue positioniert eine Gruppe neu, deren gesamter Inhalt auf dieser einen Seite funktionslos ist.

  Empfehlung: Verdrahtung nach dem im selben Codebestand etablierten Muster herstellen — `StatementDraftEntryCardViewModel` (Zeilen 997–1049) verschickt für dieselben Button-IDs URLs als `OpenPostings`-Payload, die `CardPage.razor` bereits verarbeitet (`NavigationManager.NavigateTo(payload)`). Für Anhänge `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` verwenden (wie in `BankAccountCardViewModel.cs` Zeile 279 und `SavingsPlanCardViewModel.cs` Zeile 862). Alternativ eigene `case`s in `CardPage.razor` ergänzen, die die Ziel-Id aus `Posting.AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` lesen.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen bzw. sichtbaren Effekte:

- Anwenderin öffnet die Sparplan-Karte → Ribbon-Gruppen sichtbar in Reihenfolge Navigation → Verwalten → Analyse → Buchungen → **Verknüpfte Informationen** (letzte Position) → unauffällig (Umsetzung exakt wie gefordert; `Ribbon.razor` übernimmt die Listenreihenfolge stabil, alle `Sort = 0`)
- Anwenderin öffnet die Bankkonto-Karte → Navigation → Verwalten → Buchungen → **Verknüpfte Informationen** → unauffällig
- Anwenderin öffnet die Posten-Karte → Navigation → Aktionen → **Verknüpfte Informationen** → unauffällig (Reihenfolge korrekt)
- Bedienbarkeit der Aktionen in den verschobenen Gruppen (Sparplan-Karte) → unauffällig (Back/Save/Archive/Delete/Kategorie/Postings/Anhänge/Neu berechnen/Istmeldung alle verdrahtet — direkter Callback, `CardPage`-Switch oder `UiOverlaySpec` via `OverlayHost`)
- Bedienbarkeit der Aktionen in den verschobenen Gruppen (Bankkonto-Karte) → unauffällig (Back/Save/Reset/Delete/Postings/Bankkontakt/Anhänge/Vorläufige Buchungen verdrahtet)
- Bedienbarkeit der Aktionen in den verschobenen Gruppen (Posten-Karte) → **Befund vorhanden** (Navigation „Zurück" und Aktion „Stornieren" funktionieren; alle fünf Buttons der Gruppe „Verknüpfte Informationen" sind ohne Handler/Payload und bleiben wirkungslos)
- Beschriftung/Erreichbarkeit: Alle Gruppen- und Aktionslabels aus resx (de + en vorhanden, u. a. `Ribbon_Group_Linked`, `Ribbon_Group_Bookings`, `Ribbon_Group_Actions`, `Ribbon_Group_Analysis`); keine internen Kennungen erforderlich; keine neuen Eingabe-/Auswahlaufgaben in der Anforderung → unauffällig

## Geprüfte Dateien

- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` (geändert; `GetRibbonRegisterDefinition`, Zeilen 801–900)
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` (geändert; `GetRibbonRegisterDefinition`, Zeilen 244–311)
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` (geändert; `GetRibbonRegisterDefinition`, Zeilen 122–155)
- `FinanceManager.Web/Components/Shared/Ribbon.razor` (referenziert, unverändert — Render-/Sortierlogik, Zeilen 166–254)
- `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (referenziert, unverändert — `UiRibbonTab`/`UiRibbonRegister`)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (referenziert — Handler-Verdrahtung `OnUiActionRequested`, Zeilen 120–367)
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` (referenziert — `UiOverlaySpec`-Verarbeitung)
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` / `BaseCardViewModel.cs` (referenziert — Register-Aggregation, `RequestOpenAttachments`)

Keine weiteren UI-Dateien im Branch-Diff gegen `staging` (übrige Änderungen: Tests, `AGENT.md`, Feature-Dokumentation).
