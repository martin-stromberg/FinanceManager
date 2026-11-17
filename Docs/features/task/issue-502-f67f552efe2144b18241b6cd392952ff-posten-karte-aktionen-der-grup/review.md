# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Methode `RequestOpenLinkedCard(string route, Guid? linkedId)` in `PostingsCardViewModel` — angelegt (`private`, `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs:240-263`); baut `/card/{route}/{linkedId}`, hängt `?back={EscapeDataString(nav.Uri)}` über `ServiceProvider.GetService<NavigationManager>()` im try/catch-Fallback an, guardet `linkedId == null`, feuert `RaiseUiActionRequested("OpenPostings", url)`
- [x] `OpenAccount`-Callback in `GetRibbonRegisterDefinition` — ruft `RequestOpenLinkedCard("accounts", Posting?.AccountId)`, `Disabled` unverändert `Posting?.AccountId == null` (Zeile 140)
- [x] `OpenContact`-Callback — `RequestOpenLinkedCard("contacts", Posting?.ContactId)` (Zeile 141)
- [x] `OpenSavingsPlan`-Callback — `RequestOpenLinkedCard("savings-plans", Posting?.SavingsPlanId)` (Zeile 142)
- [x] `OpenSecurity`-Callback — `RequestOpenLinkedCard("securities", Posting?.SecurityId)` (Zeile 143)
- [x] `OpenAttachments`-Callback — `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`; `Disabled` von hart `false` auf `Posting == null` geändert (Zeile 144)
- [x] Keine Änderung an Ids, Labels, Icons, `MobileShortcut`, Tabs oder Reihenfolge — `CardPage.razor`, `OverlayHost`, `Ribbon` unverändert (git-Diff bestätigt nur `PostingsCardViewModel.cs` als Produktivcode-Änderung)
- [x] `AccountsApiSeedHelper.SeededPosting` — erweitert um optionale `ContactId`/`SavingsPlanId`/`SecurityId`/`Kind` (`FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs:177-183`); `CreateAccountWithBalanceAsync` übergibt die Werte an den `Posting`-Konstruktor (Zeilen 108-116)
- [x] Testklasse `PostingsCardViewModelLinkedTests` — angelegt (`FinanceManager.Tests/ViewModels/PostingsCardViewModelLinkedTests.cs`) inkl. Hilfsmethoden `CreateVm`, `BuildPostingDto` (belegbare Link-Ids), `FindLinkedAction`, `CaptureUiActions`
- [x] Test `OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl` — vorhanden, bestanden
- [x] Test `OpenContact_ShouldFireOpenPostings_WithContactCardUrl` — vorhanden, bestanden
- [x] Test `OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl` — vorhanden, bestanden
- [x] Test `OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl` — vorhanden, bestanden
- [x] Test `OpenAttachments_ShouldFireOverlaySpec_ForPosting` — assertiert `UiOverlaySpec` mit `ComponentType == typeof(AttachmentsPanel)`, `Parameters["ParentKind"] == AttachmentEntityKind.Posting`, `Parameters["ParentId"] == Id`; bestanden
- [x] Test `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` — Disabled-Flags pro Verknüpfung in beiden Konstellationen; bestanden
- [x] Test `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` — alle fünf Linked-Aktionen `Disabled` bei `Posting == null`; bestanden
- [x] Test `LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved` — Guard-Nachweis; bestanden
- [x] Testklasse `PostingsCardLinkedActionsPlaywrightTests` — angelegt (`FinanceManager.Tests.E2E/Tests/Navigation/PostingsCardLinkedActionsPlaywrightTests.cs`)
- [x] E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` — Navigation inkl. `?back=`-Rückweg; bestanden
- [x] E2E `PostingsCard_OpenAttachments_OpensOverlay` — `AttachmentsPanel`-Modal sichtbar; bestanden
- [x] E2E `PostingsCard_LinkedActions_Disabled_WhenNoLink` — disabled-Attribut bei fehlender Verknüpfung; bestanden
- [x] E2E `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` — alle fünf Linked-Buttons inkl. `#OpenAttachments` disabled; bestanden
- [x] E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` — alle drei Navigationsaktionen mit geseedeten Verknüpfungs-Ids und Ziel-Entities (Kontakt via `CreateBankContactAsync`, Sparplan via `POST /api/savings-plans`, Wertpapier via `POST /api/securities`); bestanden
- [x] Vollständige Verifikation — Unit (`--filter-not-trait "Category=OsInterface"`): 2163/2163 bestanden; Integration: 165/165 bestanden; E2E (volle Suite): 141/141 bestanden

## Hinweise

- Der im Plan dokumentierte preexisting E2E-Fehlschlag `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` trat in diesem Lauf nicht auf — die gesamte Suite war grün.
- `git status` zeigt ausschließlich die geplanten Änderungen: `PostingsCardViewModel.cs` (Produktivcode), `AccountsApiSeedHelper.cs` (E2E-Infrastruktur), zwei neue Testdateien sowie `todo.md` (Feature-Doku). Keine Änderungen an `CardPage.razor`, `OverlayHost`, `AttachmentsPanel`, resx oder Datenmodell — Scope-Ausschlüsse eingehalten.
