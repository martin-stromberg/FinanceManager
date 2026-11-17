# Bestandsaufnahme: Ribbon-Gruppierung „Verknüpfte Informationen" nicht an letzter Position (Issue #498)

Analysiert wurde die Ribbon-Tab-Erzeugung der drei in der Anforderung genannten Card-ViewModels sowie der Rendering-Pfad (`BaseViewModel.GetRibbonRegisters` → `Ribbon.razor`), bezogen auf die geforderte Umordnung der `Linked`-Gruppe an die letzte Position.

## Zusammenfassung

- **Befund bestätigt:** Alle drei ViewModels verletzen die AGENT.md-Reihenfolge — die Gruppe `Ribbon_Group_Linked` steht vor Aktionsgruppen:
  - `SavingsPlanCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs:895`): `{ nav, manage, linked, analysis, bookings }` → `linked` muss hinter `bookings`.
  - `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs:302–308`): Navigation → Manage → Linked → Bookings → `Linked` muss hinter `Bookings`.
  - `PostingsCardViewModel` (`FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs:154`): `{ nav, linked, actions }` → `linked` muss hinter `actions`.
- **Render-Mechanik:** `Ribbon.razor` (`FinanceManager.Web/Components/Shared/Ribbon.razor:174–251`) merged Gruppen nach Titel in First-Seen-Reihenfolge und sortiert stabil per `OrderBy(g => g.Sort)`; alle betroffenen Tabs haben `Sort = 0` (Default, `RibbonModels.cs:85`). Die Listenposition im ViewModel entscheidet also die sichtbare Reihenfolge — eine reine Umordnung genügt.
- **Referenzen konform:** `SecurityCardViewModel`, `ContactCardViewModel`, `StatementDraftCardViewModel` und `StatementDraftEntryCardViewModel` haben `Linked` bereits als letzte Gruppe.
- **Kein Umbau nötig:** Keine Änderung an `UiRibbonTab`/`UiRibbonRegister`/`Ribbon.razor`, keine resx-Änderung, keine neuen Aktionen.
- **Test-Ausgangszustand:** Unit-Tests 2148/2148 bestanden, Integration 165/165 bestanden, E2E 131/133 bestanden mit **2 nachgewiesenen vorher existierenden Fehlschlägen** (`ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi`, `SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle`) — beide ohne Bezug zur Ribbon-Reihenfolge. Details und TRX-Nachweise: [Tests](inventory/tests.md).
- **Testlücke:** Kein bestehender Test assertiert die Gruppenreihenfolge; vorhandene Tests prüfen nur Vorhandensein von Gruppen/Aktionen (z. B. `AccountDetailViewModelTests.GetRibbon_ContainsExpectedGroups`).

## Details

- [Datenmodell (Ribbon-Typen)](inventory/models.md)
- [Logik (ViewModels, BaseViewModel, Ribbon.razor, Referenzen)](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
