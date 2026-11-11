# Tasks: Posten-Karte — Handler für die Ribbon-Aktionen der Gruppe „Verknüpfte Informationen" (Issue #502)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI/ViewModel | `PostingsCardViewModel`: private Hilfsmethode `RequestOpenLinkedCard(string route, Guid? linkedId)` anlegen (URL `/card/{route}/{id}` inkl. optionalem `?back=` aus `NavigationManager.Uri`, try/catch-Fallback, `RaiseUiActionRequested("OpenPostings", url)`, Guard bei `linkedId == null`) | Offen | — |
| 2 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenAccount`-Callback auf `RequestOpenLinkedCard("accounts", Posting?.AccountId)` umstellen | Offen | — |
| 3 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenContact`-Callback auf `RequestOpenLinkedCard("contacts", Posting?.ContactId)` umstellen | Offen | — |
| 4 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenSavingsPlan`-Callback auf `RequestOpenLinkedCard("savings-plans", Posting?.SavingsPlanId)` umstellen | Offen | — |
| 5 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenSecurity`-Callback auf `RequestOpenLinkedCard("securities", Posting?.SecurityId)` umstellen | Offen | — |
| 6 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenAttachments`-Callback auf `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` umstellen | Offen | — |
| 7 | UI/ViewModel | `PostingsCardViewModel.GetRibbonRegisterDefinition`: `OpenAttachments`-`Disabled` von hart `false` auf `Posting == null` ändern | Offen | — |
| 8 | Tests | Testklasse `PostingsCardViewModelLinkedTests` anlegen inkl. Hilfsmethoden `CreateVm`, `BuildPostingDto` (mit belegbaren Link-Ids), `FindLinkedAction`, `CaptureUiActions` | Offen | — |
| 9 | Tests | Test `OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl` (Action `OpenPostings`, Payload `/card/accounts/{id}`) | Offen | — |
| 10 | Tests | Test `OpenContact_ShouldFireOpenPostings_WithContactCardUrl` (Payload `/card/contacts/{id}`) | Offen | — |
| 11 | Tests | Test `OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl` (Payload `/card/savings-plans/{id}`) | Offen | — |
| 12 | Tests | Test `OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl` (Payload `/card/securities/{id}`) | Offen | — |
| 13 | Tests | Test `OpenAttachments_ShouldFireOverlaySpec_ForPosting` (`UiOverlaySpec`: `AttachmentsPanel`, `ParentKind = Posting`, `ParentId = Id`) | Offen | — |
| 14 | Tests | Test `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` (Disabled-Flags pro Verknüpfung) | Offen | — |
| 15 | Tests | Test `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` (`Posting == null` → `Disabled`) | Offen | — |
| 16 | Tests | Test `LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved` (Guard `linkedId == null`) | Offen | — |
| 17 | E2E-Infrastruktur | `AccountsApiSeedHelper.SeededPosting` um optionale `ContactId`/`SavingsPlanId`/`SecurityId`/`Kind` erweitern und an `Posting`-Konstruktor weitergeben | Offen | — |
| 18 | E2E-Tests | `PostingsCardLinkedActionsPlaywrightTests` anlegen: `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` (Navigation + `?back=`-Rückweg) | Offen | — |
| 19 | E2E-Tests | `PostingsCard_OpenAttachments_OpensOverlay` (`AttachmentsPanel`-Modal sichtbar) | Offen | — |
| 20 | E2E-Tests | `PostingsCard_LinkedActions_Disabled_WhenNoLink` (disabled-Attribut bei fehlender Verknüpfung) | Offen | — |
| 21 | E2E-Tests | `PostingsCard_OpenLinkedEntities_NavigateToCards` (OpenContact/OpenSavingsPlan/OpenSecurity mit geseedeten Verknüpfungs-Ids und Ziel-Entities) | Offen | — |
| 22 | E2E-Tests | `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` (`/card/postings/{nicht-existierende Guid}` → alle fünf Linked-Buttons inkl. `#OpenAttachments` `disabled`/`aria-disabled`) | Offen | — |
| 23 | Verifikation | Vollständige Testausführung: Unit (`--filter-not-trait "Category=OsInterface"`), Integration, E2E; bekannten preexisting Fehlschlag `AuthenticatedUser_ChangesPassword_ViaUi` nicht als Regression werten | Offen | — |
