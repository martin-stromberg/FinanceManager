# Übersetzte Anforderung – Issue #502: Posten-Karte: Aktionen der Gruppe „Verknüpfte Informationen" ohne Handler (tote Ribbon-Buttons)

## Fachliche Zusammenfassung

Auf der Posten-Detailkarte (`PostingsCardViewModel`, gehostet von `CardPage.razor` unter `/card/postings/{id}`) laufen die fünf Ribbon-Aktionen der Gruppe „Verknüpfte Informationen" (`Ribbon_Group_Linked`) ins Leere: `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` feuern `UiActionRequested` ohne Payload und ohne passenden `case` in der Host-Seite; `OpenAttachments` feuert ohne `UiOverlaySpec`-Payload. Gemäß verbindlicher Anwenderentscheidung im Issue-Kommentar bleiben die Aktionen erhalten — es sind die Handler so zu verdrahten, dass die vier Navigationsaktionen zur Karte des jeweils verknüpften Datensatzes führen und `OpenAttachments` das Anhangs-Overlay für den Posten öffnet.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine Ribbon-Aktion der `Linked`-Gruppe auf der Posten-Detailkarte — konkret das `UiRibbonAction`-Element samt `RaiseUiActionRequested(...)`-Aufruf in `PostingsCardViewModel.GetRibbonRegisterDefinition` (Zeilen 137–144) und dem zugehörigen Handler-Pfad in der Host-Seite `CardPage.razor` (`OnUiActionRequested`-Switch, Zeilen 128–365, bzw. `OverlayHost`).
- **Auslösende Ereignisse:** Nutzerklick auf einen der fünf Ribbon-Buttons der `Linked`-Gruppe auf der Posten-Karte (`/card/postings/{id}`).
- **Erwartete Wirkung:**
  - `OpenAccount` → Navigation zur Konto-Karte des verknüpften Kontos (`/card/accounts/{Posting.AccountId}`; Ziel-ViewModel `BankAccountCardViewModel` mit `CardRoute("accounts")`).
  - `OpenContact` → Navigation zur Kontakt-Karte (`/card/contacts/{Posting.ContactId}`; `ContactCardViewModel` mit `CardRoute("contacts")`).
  - `OpenSavingsPlan` → Navigation zur Sparplan-Karte (`/card/savings-plans/{Posting.SavingsPlanId}`; `SavingsPlanCardViewModel` mit `CardRoute("savings-plans")`).
  - `OpenSecurity` → Navigation zur Wertpapier-Karte (`/card/securities/{Posting.SecurityId}`; `SecurityCardViewModel` mit `CardRoute("securities")`).
  - `OpenAttachments` → Öffnen des Anhangs-Overlays (`AttachmentsPanel` via `UiOverlaySpec`) für `AttachmentEntityKind.Posting` mit `ParentId = Id` — analog `BankAccountCardViewModel` (Zeile 279) und `SavingsPlanCardViewModel` (Zeile 862).
  - Konsistent zu den übrigen Karten wird der `back`-Parameter an die Zielkarte übergeben, damit deren Zurück-Button zur Posten-Karte zurückführt (Muster: `StatementDraftEntryCardViewModel`, Zeilen 997–1053; `CardPage`-Handler `OpenBankContact`/`OpenAccountDetails`, Zeilen 226–249).
  - Deaktivierungslogik: Jede Navigationsaktion ist nur aktiv, wenn die zugehörige Verknüpfung (`Posting.AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId`) gesetzt ist; `OpenAttachments` nur, wenn ein Posten geladen ist.
- **Umgang mit fehlenden Objekten:**
  - Verknüpfte Entität nicht gesetzt (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId == null`): Der Button ist bereits deaktiviert (`disabled`-Flag der `UiRibbonAction`s) — beibehalten, kein Klick möglich.
  - Kein Posten geladen (`Posting == null`, z. B. Ladefehler oder `Id == Guid.Empty`): Alle Linked-Aktionen deaktiviert; für `OpenAttachments` ist der aktuell hartkodierte `disabled`-Wert `false` (Zeile 143) auf das Geschwistermuster zu ziehen (`Posting == null`, vgl. `Account == null` in `BankAccountCardViewModel`), damit kein Overlay mit leerer `ParentId` geöffnet wird.
  - Ziel-Entität zur Laufzeit nicht mehr vorhanden: Das NotFound-/Error-Handling der Zielkarte greift — kein Zusatzverhalten auf der Posten-Karte erforderlich.
- **Scope-Ausschlüsse:**
  - Keine Entfernung der fünf Aktionen — durch den Issue-Kommentar verbindlich entschieden.
  - Keine Änderung an Labels, Icons, `MobileShortcut`-Flags, Gruppenzuordnung oder Reihenfolge des Ribbons (die Gruppenreihenfolge wurde mit #498 korrigiert; `Linked` steht bereits an letzter Position).
  - Keine neuen Ribbon-Aktionen und keine Änderung an den bestehenden Aktionen `Back`, `Reverse`/`NavigateToPosting`.
  - Keine Datenmodell-/Persistenzänderung: `AttachmentEntityKind.Posting` existiert bereits (`= 7`), die generische Attachments-API (`Attachments_ListAsync`, `Attachments_UploadFileAsync` u. a. in `IApiClient`) ist art-agnostisch, und `StatementDraftService` erzeugt bereits Posten-Anhänge (Zeilen 58–63). Damit keine Backup-/Migrationsfolgen und keine resx-Änderungen (`Ribbon_OpenAccount`/`Ribbon_OpenContact`/`Ribbon_OpenSavingsPlan`/`Ribbon_OpenSecurity`/`Ribbon_Attachments` sind bereits vorhanden).
  - Keine Veränderung der Handler-Infrastruktur (`UiActionEventArgs`, `UiOverlaySpec`, `OverlayHost`, `Ribbon`).

## Betroffene Klassen und Komponenten

- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` — `Linked`-Tab in `GetRibbonRegisterDefinition` (Zeilen 137–144): Für `OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity` Ziel-URL- oder Id-Payloads ergänzen; `OpenAttachments` durch `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` ersetzen und `disabled`-Flag anpassen.
- `FinanceManager.Web/Components/Pages/CardPage.razor` — `OnUiActionRequested`-Switch (Zeilen 128–365): Je nach Verdrahtungsvariante neue `case`-Handler für `OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity` (Muster `OpenBankContact`, Zeilen 226–236, und `OpenAccountDetails`, Zeilen 237–249) — oder unverändert bei Nutzung des vorhandenen generischen `case "OpenPostings"` (Zeilen 192–194).
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` — `RequestOpenAttachments(AttachmentEntityKind, Guid)` (Zeilen 325–334) und `UiOverlaySpec` (Zeile 235): wiederverwendet, keine Änderung.
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` — rendert `UiOverlaySpec`-Payloads (`AttachmentsPanel`), keine Änderung; `case "OpenAttachments"` in `CardPage.razor` bleibt bewusst No-Op (Kommentar Zeilen 253–255, 369).
- `FinanceManager.Web/Components/Shared/AttachmentsPanel.razor` — Overlay-Komponente für Anhänge, keine Änderung.
- `FinanceManager.Domain/Attachments/AttachmentEntityKind.cs` — `Posting = 7` (Zeile 51), nur referenziert.
- Referenzimplementierungen (unverändert): `StatementDraftEntryCardViewModel` (Zeilen 997–1053: URL-Payload via `OpenPostings` inkl. `?back=`), `BankAccountCardViewModel` (Zeilen 263–281), `SavingsPlanCardViewModel` (Zeile 862), `SecurityCardViewModel` (Zeile 383), `ContactCardViewModel` (Zeile 301).
- Tests: `FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs` (Testmuster mit `Mock<IApiClient>`, `DummyLocalizer`, `ServiceCollection`); neue bzw. erweiterte ViewModel-Tests, die pro Ribbon-Aktion das gefeuerte `UiActionRequested` (Action-Id plus String-Payload bzw. `UiOverlaySpec` mit `AttachmentsPanel`, `ParentKind = Posting`, `ParentId = Id`) assertieren; `OverlayHostTests`/`RibbonTests` als Referenz. UI-/E2E-Abnahme der fünf Buttons gemäß Projektregeln.

## Implementierungsansatz

Für die vier Navigationsaktionen stehen zwei im Bestand etablierte Verdrahtungsvarianten zur Auswahl (Entscheidung in der Planung):

1. **URL-Payload über den vorhandenen generischen Handler** (Muster `StatementDraftEntryCardViewModel`): Die `UiRibbonAction`s feuern `RaiseUiActionRequested("OpenPostings", url)` mit fertiger Ziel-URL (`/card/accounts/{id}` usw.) inkl. `?back=` aus `NavigationManager.Uri` (auflösbar über `ServiceProvider`). `CardPage.razor` benötigt dann keine neuen `case`s — der vorhandene `case "OpenPostings"` navigiert via `NavigationManager.NavigateTo(payload)`.
2. **Typspezifische `case`s in `CardPage.razor`** (Muster `OpenBankContact`/`OpenAccountDetails`): Die `UiRibbonAction`s feuern `RaiseUiActionRequested("OpenAccount", Posting.AccountId.ToString())` usw.; `CardPage.razor` erhält vier neue `case`s, die zur jeweiligen Karte navigieren und den `back`-Parameter per `BuildCardPath(Kind, SubKind, Id.ToString(), Back)` anhängen.

Für `OpenAttachments` in beiden Varianten einheitlich: `RaiseUiActionRequested("OpenAttachments")` wird durch `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` ersetzt; das dadurch erzeugte `UiOverlaySpec` (`AttachmentsPanel` mit `ParentKind`/`ParentId`) wird vom `OverlayHost` gerendert. Das `disabled`-Flag von `OpenAttachments` wird an das Geschwistermuster angeglichen (`Posting == null`).

Keine neuen Klassen, Interfaces, Enums oder API-Endpunkte erforderlich. Absicherung über ViewModel-Unit-Tests (gefeuerte Action-Ids und Payloads/`UiOverlaySpec`) sowie Handler-Verifikation der `CardPage` gemäß der im Issue genannten Projektregel.

## Konfiguration

Keine — das Verhalten ist fest verdrahtet und nicht konfigurierbar.

## Offene Fragen

1. Welche der beiden etablierten Verdrahtungsvarianten soll für die vier Navigationsaktionen genutzt werden: generischer `OpenPostings`-URL-Payload (Muster `StatementDraftEntryCardViewModel`, keine `CardPage`-Änderung) oder neue typspezifische `case`s in `CardPage.razor` (Muster `OpenBankContact`/`OpenAccountDetails`)? — Annahme: Entscheidung in der Planung; beide Varianten erfüllen die Handler-Regel.
2. Soll `OpenAttachments` bei `Posting == null` deaktiviert werden (Geschwistermuster; `disabled`-Flag aktuell hart `false`)? — Annahme: ja.
3. Ist die `back`-Navigation zu den Zielkarten verbindlich gewünscht (beide Referenzmuster übergeben `?back=`)? — Annahme: ja, für konsistentes Zurück-Verhalten.
