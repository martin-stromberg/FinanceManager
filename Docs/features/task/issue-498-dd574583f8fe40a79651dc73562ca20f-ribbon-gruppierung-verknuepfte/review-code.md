# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine Befunde im neu geschriebenen bzw. geänderten Code.

### Verifikationsanmerkungen (keine Befunde)

- **Produktivcode:** Die Änderung ist in allen drei ViewModels eine reine Umordnung der
  `UiRibbonTab`-Einträge — `BankAccountCardViewModel.cs:302-308` (Navigation → Manage → Bookings →
  Linked), `PostingsCardViewModel.cs:154` (`{ nav, actions, linked }`),
  `SavingsPlanCardViewModel.cs:895` (`{ nav, manage, analysis, bookings, linked }`). Keine neue
  Logik, keine Smells. Die `AGENT.md`-Ergänzung (Navigation-Gruppe an Position 0) ist mit dem Code
  konsistent — Stichproben bei `ContactCardViewModel.cs:306`, `SecurityCardViewModel.cs:364+`,
  `StatementDraftCardViewModel.cs:355` und `SavingsPlanCategoryCardViewModel.cs:198` zeigen
  durchgehend `nav` als ersten Tab; `Ribbon_Group_Linked` steht dort ebenfalls am Ende.

- **Unit-/Komponententests:** Alle drei Reihenfolge-Tests heißen einheitlich
  `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (`AccountDetailViewModelTests.cs:211`,
  `PostingsCardViewModelReversalTests.cs:247`, `SavingsPlanCardLinkTests.cs:333`) und prüfen die
  öffentliche `GetRibbonRegisters(...)`-Sicht — also inklusive der Aggregation aus
  `BaseViewModel.GetRibbonRegisters` (`BaseViewModel.cs:628-644`), was der produktive Aufrufpfad
  des Ribbon ist. Stil pro Datei konsistent (xUnit-Asserts in `AccountDetailViewModelTests`,
  FluentAssertions in den beiden anderen). `Groups_RenderInTabListOrder` (`RibbonTests.cs:128`)
  prüft beobachtbares DOM-Verhalten (Reihenfolge der `.fm-ribbon-group-footer`) und belegt korrekt
  die Stabilität des `OrderBy(g => g.Sort)` bei Default-`Sort = 0` (`Ribbon.razor:251`,
  `RibbonModels.cs:85` — Gruppen werden in First-Seen-Reihenfolge gemergt, `Ribbon.razor:174-219`).
  Gegenprobe im Working Tree: `dotnet test FinanceManager.Tests --filter
  "FullyQualifiedName~PlacesLinkedLast|FullyQualifiedName~Groups_RenderInTabListOrder"` → 4/4
  bestanden.

- **E2E-Test `RibbonGroupOrderPlaywrightTests.cs` (neu, untracked):** Build von
  `FinanceManager.Tests.E2E` fehlerfrei (0 Fehler, nur preexisting NU1510-Warnungen). Alle
  referenzierten Helper existieren mit passenden Signaturen
  (`PlaywrightWebAppFixture.CreateSessionAsync`/`BaseUrl`/`DatabasePath`,
  `PlaywrightBrowserSession.Page`, `PlaywrightCollection.CollectionName`,
  `AuthGateway(IPage, string)`/`LoginAsync`, `TestUserSeeder.EnsureUserAsync(..., timeZoneId:)`,
  `AccountsApiSeedHelper` mit beiden Konstruktoren, `CreateAccountAsync` und
  `CreateAccountWithBalanceAsync` inkl. `SeededPosting`/`SeededAccount`,
  `BrowserApiHelper.PostJsonAsync`/`GetJsonAsync`, 7-Argumenten-`SavingsPlanCreateRequest` in
  `SavingsPlanRequests.cs:68`). `GET /api/postings/account/{id}` existiert und liefert
  `IReadOnlyList<PostingServiceDto>` (`PostingsController.cs:226-237`), die Card-Routen
  (`savings-plans`, `accounts`, `postings`) sind per `CardRoute`-Attribut belegt. Das private
  `LoginNewUserAsync`-Pattern entspricht der Codebasis-Konvention (pro Testklasse eigener
  Login-Helper, vgl. `ListNavigationPlaywrightTests.EnsureAuthenticatedAsync`). Die Selektoren sind
  korrekt: `.fm-ribbon-group` matcht ausschließlich Desktop-Gruppen (`Ribbon.razor:66`; mobile
  Panels/Menüeinträge tragen `fm-ribbon-mobile-group-panel` bzw. ids `{id}-mobile`/`{id}-mobile-shortcut`),
  `#Back` kommt nur in der Navigations-, `#OpenAttachments` nur in der Linked-Gruppe aller drei
  Karten vor — der Helper-Assert verifiziert damit eindeutig „Navigation erste, Linked letzte
  Gruppe". Die drei `[Fact]`s sind im dokumentierten E2E-Lauf bestanden
  (`post-change-e2e-ribbonorder.trx`, 3/3).

- **Handler-Regel (`RaiseUiActionRequested` → Handler in der Blazor-Seite):** Der Change fügt
  keine neuen `RaiseUiActionRequested`-Aufrufe hinzu — nur Umordnung. Die preexisting handlerlosen
  Aktionen in `PostingsCardViewModel.cs:139-143` (`OpenAccount`/`OpenContact`/`OpenSavingsPlan`/
  `OpenSecurity` — kein `case` in `CardPage.razor`; `OpenAttachments` trifft nur den expliziten
  No-op-`case` in `CardPage.razor:253-255`) sowie `Archived` in `SavingsPlanCardViewModel.cs:920`
  sind bereits auf `staging` vorhanden, nicht Teil dieses Changes und als Folge-Issue
  <https://github.com/martin-stromberg/FinanceManager/issues/502> triagiert. Alle übrigen erhobenen
  Aktionen haben Handler (`Back`, `Delete`/`Deleted`, `Saved`, `OpenPostings`, `OpenBankContact`,
  `OpenAccountDetails`, `OpenCategory`, `OpenStatementDraft`, `NavigateToPosting` in
  `CardPage.razor`; spec-basierte Attachments-/Istmeldung-Overlays generisch via `OverlayHost`).

- **Nebenbefund außerhalb des Codes (kein Review-Befund):** Die Tracking-Datei
  `Docs/features/task-issue-498-...-tasks.md` nennt in den Testnachweisen noch die alten Testnamen
  `GetRibbon_GroupOrder_PlacesLinkedLast`/`Ribbon_GroupOrder_PlacesLinkedLast`; der tatsächliche
  Name ist überall `GetRibbonRegisters_GroupOrder_PlacesLinkedLast`. Da `tasks.md` ein
  Lifecycle-Arbeitsartefakt ist (wird bei Abschluss gelöscht), ist das kein Code-Qualitätsbefund —
  kann bei Gelegenheit korrigiert werden.

## Geprüfte Dateien

- `AGENT.md`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`
- `FinanceManager.Tests/Components/RibbonTests.cs`
- `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs`
- `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` (neu, untracked)

Querschnittlich geprüft (Kontext, nicht im Diff): `FinanceManager.Web/Components/Shared/Ribbon.razor`,
`FinanceManager.Web/Components/Pages/CardPage.razor`,
`FinanceManager.Web/Components/Shared/OverlayHost.razor` (frühere Iteration),
`FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`,
`FinanceManager.Web/ViewModels/Common/RibbonModels.cs`,
`FinanceManager.Web/Controllers/PostingsController.cs`,
`FinanceManager.Web/Resources/Pages*.resx` (Gruppen-Keys vorhanden, de/en),
`FinanceManager.Tests.E2E/Infrastructure/*`, `FinanceManager.Tests.E2E/Helpers/*`,
weitere `*CardViewModel.cs` mit `Ribbon_Group_Navigation` (Konventionskonsistenz).
