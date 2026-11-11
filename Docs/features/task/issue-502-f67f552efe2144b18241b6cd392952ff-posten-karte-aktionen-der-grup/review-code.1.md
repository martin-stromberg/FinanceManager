# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PostingsCardViewModel.cs (PostingsCardViewModel)

- **Doppelter Code** — `RequestOpenLinkedCard` (Zeilen 240–263) ist die inzwischen vierte Kopie desselben Codeblocks „NavigationManager aus dem ServiceProvider auflösen → `/card/{route}/{id}?back={nav.Uri}` bauen → `RaiseUiActionRequested("OpenPostings", url)`". Dieselbe Logik steht bereits dreimal nahezu wortgleich inline in `StatementDraftEntryCardViewModel.cs` (Zeilen ~997–1053, OpenContact/OpenSavingsPlan/OpenSecurity).

  Empfehlung: Die Logik als geschützte Hilfsmethode in `BaseViewModel` auslagern (z. B. `RequestOpenLinkedCard(string route, Guid? linkedId)`, analog zum vorhandenen `RequestOpenAttachments`). `PostingsCardViewModel` ruft sie dann direkt auf, und die drei Duplikate in `StatementDraftEntryCardViewModel` können ebenfalls darauf umgestellt werden.

### PostingsCardViewModelLinkedTests.cs (PostingsCardViewModelLinkedTests)

- **Testqualität** — `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` (Zeilen 272–307) enthält zwei getrennte Arrange-Act-Assert-Zyklen und prüft damit zwei fachliche Fälle in einer Methode: (1) fehlende Links → Aktionen disabled, (2) alle Links gesetzt → Aktionen enabled. Der zweite Fall passt zudem nicht zum Methodennamen.

  Empfehlung: In zwei Testmethoden aufteilen, z. B. `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` und `LinkedNavigationActions_ShouldBeEnabled_WhenLinkPresent`.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Tests/ViewModels/PostingsCardViewModelLinkedTests.cs`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/PostingsCardLinkedActionsPlaywrightTests.cs`
