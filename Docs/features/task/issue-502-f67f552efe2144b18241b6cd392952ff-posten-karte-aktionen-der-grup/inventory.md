# Bestandsaufnahme: Posten-Karte — Aktionen der Gruppe „Verknüpfte Informationen" ohne Handler (Issue #502)

Analysiert wurden `PostingsCardViewModel` (Ribbon-Definition der `Linked`-Gruppe), der Handler-Pfad in `CardPage.razor` (`OnUiActionRequested`-Switch, `OverlayHost`) sowie die etablierten Referenzmuster in `StatementDraftEntryCardViewModel`, `BankAccountCardViewModel`, `SavingsPlanCardViewModel`, `SecurityCardViewModel` und `ContactCardViewModel`.

## Zusammenfassung

- **Befund bestätigt — fünf tote Ribbon-Buttons** (`PostingsCardViewModel.cs` Zeilen 137–144):
  - `OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity` feuern `RaiseUiActionRequested(...)` **ohne Payload**; `CardPage.razor` enthält **keine** passenden `case`s (Switch Zeilen 128–365, kein `default`-Zweig) → Klicks laufen wirkungslos ins Leere.
  - `OpenAttachments` feuert `RaiseUiActionRequested("OpenAttachments")` **ohne `UiOverlaySpec`-Payload**; der `case "OpenAttachments"` in `CardPage.razor` ist bewusst No-Op (Zeilen 253–255, 369) und `OverlayHost` reagiert nur auf `UiOverlaySpec`-`PayloadObject`s (Zeilen 86–94) → kein Overlay.
- **Deaktivierungslogik teilweise vorhanden:** Die vier Navigations-Buttons sind bereits korrekt deaktiviert, wenn die jeweilige Verknüpfung fehlt (`Posting?.X == null`); `OpenAttachments` ist dagegen hart `disabled = false` (Zeile 143) — nicht ans Geschwistermuster (`Posting == null`, vgl. `Account == null` in `BankAccountCardViewModel.cs` Zeile 277) angeglichen.
- **Beide etablierten Verdrahtungsvarianten existieren im Bestand:** (1) generischer `case "OpenPostings"` mit fertiger Ziel-URL inkl. `?back=` aus `NavigationManager.Uri` (Muster `StatementDraftEntryCardViewModel` Zeilen 997–1053, `CardPage` Zeilen 192–194); (2) typspezifische `case`s mit `BuildCardPath`-`back`-Parameter (Muster `OpenBankContact`/`OpenAccountDetails`, `CardPage` Zeilen 226–249).
- **Overlay-Muster vorhanden:** `BaseViewModel.RequestOpenAttachments(AttachmentEntityKind, Guid)` (Zeilen 325–334) erzeugt `UiOverlaySpec(typeof(AttachmentsPanel), {ParentKind, ParentId})` — Referenznutzung in `BankAccountCardViewModel` (Z. 279), `SavingsPlanCardViewModel` (Z. 862), `SecurityCardViewModel` (Z. 383), `ContactCardViewModel` (Z. 301).
- **Keine Infrastruktur-/Datenänderung nötig:** `AttachmentEntityKind.Posting = 7` existiert (`AttachmentEntityKind.cs` Zeile 51), die Attachments-API ist art-agnostisch, `StatementDraftService.PropagateEntryAttachmentsAsync` erzeugt bereits Posten-Anhänge (Zeilen 58–63); alle resx-Labels (`Ribbon_OpenAccount`, `Ribbon_OpenContact`, `Ribbon_OpenSavingsPlan`, `Ribbon_OpenSecurity`, `Ribbon_Attachments`) sind in `Pages.resx`/`.de`/`.en` vorhanden. `Linked`-Gruppe steht seit #498 an letzter Position (Zeile 154).
- **Test-Ausgangszustand:** Unit 2155/2155 bestanden, Integration 165/165 bestanden, E2E 135/136 bestanden mit **1 nachgewiesenem preexisting Fehlschlag** (`ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi`, `ERR_ABORTED` bei `/login` — identisch zum Fehlerbild der Issue-#498-Baseline, ohne Bezug zu Ribbon-Handlern). Details und TRX-Nachweise: [Tests](inventory/tests.md).
- **Testlücke:** Kein bestehender Test assertiert Action-Id oder Payload der fünf `Linked`-Actions auf der Posten-Karte und kein Test deckt den `CardPage`-Dispatch ab; vorhandene Tests prüfen nur `Reverse`-Verhalten und Gruppenreihenfolge (`PostingsCardViewModelReversalTests`).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
