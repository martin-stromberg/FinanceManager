# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine Befunde im neu geschriebenen bzw. geänderten Code.

### Verifikationsanmerkungen (keine Befunde)

- **Iteration-1-Befund „Testnamen" — behoben:** Alle drei Reihenfolge-Tests heißen jetzt einheitlich
  `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (`AccountDetailViewModelTests.cs:211`,
  `PostingsCardViewModelReversalTests.cs:247`, `SavingsPlanCardLinkTests.cs:333`) und rufen wie benannt
  `vm.GetRibbonRegisters(...)` auf. Gegenprobe im Working Tree: `dotnet test FinanceManager.Tests
  --filter-method "*PlacesLinkedLast" --filter-method "*RenderInTabListOrder"` → 4/4 bestanden;
  Builds von `FinanceManager.Tests` und `FinanceManager.Tests.E2E` fehlerfrei.

- **Iteration-1-Befund „tote Ribbon-Aktionen" — preexisting, kein Befund dieses Reviews:**
  `PostingsCardViewModel.cs:139-143` (`OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity`/
  `OpenAttachments` ohne wirksamen Handler) ist unverändert bereits auf `staging` vorhanden
  (verifiziert via `git show staging:...`), wird durch diesen Change nicht berührt und ist als
  Folge-Issue <https://github.com/martin-stromberg/FinanceManager/issues/502> triagiert.
  Handler-Prüfung für den geänderten Code selbst: Alle `RaiseUiActionRequested`-Aktionen der drei
  ViewModels haben einen Handler — `CardPage.razor` deckt `Back`, `Delete`/`Deleted`, `Saved`,
  `OpenPostings`, `OpenBankContact`, `OpenAccountDetails`, `OpenCategory`, `OpenStatementDraft`,
  `NavigateToPosting` ab; spec-basierte Aktionen (`OpenAttachments` via `RequestOpenAttachments`,
  `OpenActualReport` via `UiOverlaySpec`) werden generisch von `OverlayHost.razor:88` behandelt.
  (Auch das auf `staging` bereits vorhandene, handlerlose `RaiseUiActionRequested("Archived")`
  in `SavingsPlanCardViewModel.cs:920` ist preexisting und nicht Teil dieses Changes.)

- **E2E-Test `RibbonGroupOrderPlaywrightTests.cs` — DOM- und API-Verifikation bestanden:**
  `.fm-ribbon-group` matcht ausschließlich die Desktop-Gruppen (`Ribbon.razor:66`; die mobilen
  Panels tragen `fm-ribbon-mobile-group-panel`), die Aktionen rendern `id="@item.Id"`
  (`Ribbon.razor:70`), sodass `#Back`/`#OpenAttachments` eindeutig der Navigations- bzw.
  Linked-Gruppe zugeordnet sind (alle drei ViewModels definieren `OpenAttachments` in der
  Linked-Gruppe: `BankAccountCardViewModel.cs:277`, `SavingsPlanCardViewModel.cs:862`,
  `PostingsCardViewModel.cs:143`). Alle referenzierten Helper existieren mit passenden
  Signaturen (`PlaywrightWebAppFixture.CreateSessionAsync`/`BaseUrl`/`DatabasePath`,
  `PlaywrightBrowserSession` als `IAsyncDisposable` mit `Page`, `PlaywrightCollection.CollectionName`,
  `AuthGateway`, `TestUserSeeder.EnsureUserAsync(..., timeZoneId:)`, `AccountsApiSeedHelper`
  inkl. `CreateAccountWithBalanceAsync`/`SeededPosting`, `BrowserApiHelper`, 7-Argumenten-
  `SavingsPlanCreateRequest`, `GET /api/postings/account/{id}`). Das private
  `LoginNewUserAsync`-Pattern entspricht der Codebasis-Konvention (pro Testklasse eigener
  Login-Helper, vgl. `ListNavigationPlaywrightTests.EnsureAuthenticatedAsync`). Die drei
  `[Fact]`s sind im dokumentierten E2E-Lauf bestanden (`post-change-e2e-ribbonorder.trx`).

- **`Groups_RenderInTabListOrder` (`RibbonTests.cs:128`)** prüft beobachtbares Verhalten
  (DOM-Reihenfolge der Gruppen-Footer) statt Implementierungsdetails und belegt korrekt die
  Stabilität der `OrderBy(g => g.Sort)`-Sortierung bei identischem `Sort = 0` (`Ribbon.razor:251`).

- **Produktivcode:** Die Änderung ist in allen drei ViewModels eine reine Umordnung der
  `UiRibbonTab`-Einträge in der Listen-Initialisierung — keine neue Logik, keine Smells.
  Die `AGENT.md`-Ergänzung (Navigation-Gruppe an Position 0) ist konsistent mit dem Code aller
  drei Karten.

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
`FinanceManager.Web/Components/Shared/OverlayHost.razor`,
`FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`,
`FinanceManager.Web/ViewModels/Common/RibbonModels.cs`,
`FinanceManager.Tests.E2E/Infrastructure/*`, `FinanceManager.Tests.E2E/Helpers/*`.
