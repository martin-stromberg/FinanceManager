# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

Anmerkungen zur Iteration 2:

- Iteration-1-Befund (a) „`RequestOpenLinkedCard`-Duplikation" ist behoben: Die Logik liegt nun als geschützte Hilfsmethode `BaseViewModel.RequestOpenLinkedCard` (~Zeilen 336–366), die von `PostingsCardViewModel` (Zeilen 139–142) und den drei Linked-Aktionen in `StatementDraftEntryCardViewModel` (Zeilen ~998–1008) genutzt wird.
- Iteration-1-Befund (b) „kombinierter Test" ist behoben: `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` und `LinkedNavigationActions_ShouldBeEnabled_WhenLinkPresent` sind getrennte Testmethoden.

Verifikation im Rahmen des Reviews:

- `dotnet build` für `FinanceManager.Tests` und `FinanceManager.Tests.E2E`: 0 Fehler.
- `dotnet test --filter PostingsCardViewModelLinkedTests`: 9/9 Tests bestanden.
- Handler-Verdrahtung: `OpenPostings` wird in `CardPage.razor` (Zeile 192) als generische URL-Navigation behandelt; `OpenAttachments` wird über `UiOverlaySpec`/`OverlayHost` gerendert (expliziter No-op-Fall Zeile 253).
- Ribbon-Gruppenreihenfolge in `PostingsCardViewModel` (Navigation → Actions → Linked) und `StatementDraftEntryCardViewModel` entspricht den Projektregeln; Labels kommen aus `Pages.de.resx`/`Pages.en.resx`, Icons aus `/icons/sprite.svg`, primäre Aktion (`Back`) trägt `MobileShortcut = true`.
- Routen `accounts`, `contacts`, `savings-plans`, `securities` existieren als `CardRoute`-Attribute.
- `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`: `Id` entspricht bei geladenem Posting `Posting.Id` (`LoadAsync` setzt `Id` vor dem Laden); `disabled` ist jetzt korrekt an `Posting == null` gekoppelt (vorher `false`).
- `SeededPosting`-Erweiterung in `AccountsApiSeedHelper` ist rückwärtskompatibel (Default-Parameter); der `Posting`-Konstruktoraufruf mit benannten Argumenten vor positionalem `bookingDate`/`amount` ist positionskorrekt.
- Keine neuen persistierten Felder, keine neuen Enum-Mitglieder, keine neuen Blazor-Seiten — Backup-, EnumCoverage- und `@rendermode`-Regeln nicht betroffen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs`
- `FinanceManager.Tests/ViewModels/PostingsCardViewModelLinkedTests.cs`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Tests/Navigation/PostingsCardLinkedActionsPlaywrightTests.cs`
