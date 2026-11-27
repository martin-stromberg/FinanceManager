# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupBackupTabPlaywrightTests.cs (ExpandSectionAsync)

- **Testqualität / fehlerhafte Ablaufsteuerung** — `ExpandSectionAsync` (Zeile 58–77) wartet nach jedem Klick auf `aria-expanded == ""`:

  ```csharp
  await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "", new() { Timeout = 2000 });
  ```

  Blazor rendert `aria-expanded="@isExpanded"` (`SetupSections.razor:19`) für bool-Werte als `"true"`/`"false"` — niemals als leeren String. Die Erfolgsbedingung kann daher nie erfüllt werden: Die Schleife klickt alle 10 Versuche durch (gerade Anzahl → Sektion am Ende wieder eingeklappt bzw. in indeterminiertem Zustand, je nachdem ab welchem Versuch der Blazor-Circuit verbunden war). Die Fallback-Assertion `GetAttributeAsync("aria-expanded")).Should().NotBeNull()` ist wirkungslos, da das Attribut immer einen Wert trägt. Folge: `button#CreateBackup` wird geklickt, während `.setup-backup-tab` nicht expandiert ist → der Delete-Button erscheint nie → Test schlägt nach 120 s Timeout fehl bzw. ist flaky.

  Empfehlung: Auf `aria-expanded == "true"` warten und nach erfolgreicher Expansion abbrechen; zusätzlich nach der Schleife den Endzustand (`"true"`) assertieren, damit ein Nichterreichen als Fehler sichtbar wird.

### ListPage.razor (.card-page-content-Wrapper)

- **Kopplung / unbeabsichtigtes Layout-Containment** — Das neue `<div class="card-page-content">` (Zeile 27–88) umschließt neben Inhalt auch das `position: fixed`-Overlay `.split-center.list-overlay` (Zeile 57) und `ConfirmationDialogHost` (Zeile 31). `.card-page-content` trägt `backdrop-filter: blur(12px)` (`app.css:348–350`); `backdrop-filter` etabliert einen Containing Block für fixe/absolut positionierte Nachfahren. Dadurch bezieht sich `inset: 0` des Overlays auf das Panel statt auf den Viewport: Der Dimmer deckt Nav-Rail/Header nicht mehr ab, Klicks außerhalb des Panels lösen `CloseOverlay` nicht mehr aus und der Rest der Seite bleibt interaktiv — das Overlay ist faktisch nicht mehr modal.

  Empfehlung: Prüfen, ob die Panel-Optik das Overlay-Containment rechtfertigt; andernfalls `ConfirmationDialogHost` und den `.list-overlay`-Block aus dem `.card-page-content`-Div herausziehen (nur Ribbon/Titel/Liste gehören ins Panel). Hinweis: `CardPage.razor` hat `ConfirmationDialogHost` bereits innerhalb desselben Wrappers — ggf. dort dasselbe Verhalten bestehend verifizieren.

### ListPage.razor / BudgetReport.razor (?back=-Navigation)

- **Fehlende Validierung von Eingaben** — `Nav.NavigateTo(Back)` (`ListPage.razor:214`, `BudgetReport.razor:70`) navigiert ungeprüft zu einem nutzerkontrollierbaren Query-Parameter. Produzenten liefern zwar stets `Nav.Uri` (same-origin, korrekt per `Uri.EscapeDataString` kodiert), aber ein präparierter Link `?back=https://extern/` führt beim Klick auf Zurück zu einem Open Redirect. Identisches Muster existiert bereits in `CardPage.razor:144` — die neue Implementierung erweitert die Angriffsfläche um zwei weitere Senken.

  Empfehlung: Vor `NavigateTo` prüfen, dass `Back` app-relativ ist oder mit `Nav.BaseUri` beginnt (`Back.StartsWith(Nav.BaseUri, OrdinalIgnoreCase) || Back.StartsWith("/")`); andernfalls den bestehenden Fallback (`list/{Kind}`) nutzen. Ideal wäre eine gemeinsame Hilfsmethode, die auch `CardPage` absichert.

### theme.Dark.css (.icon-btn.danger)

- **Inkonsistente Korrektur** — Die neue Regel `button.icon-btn.danger { color: #000; }` (Zeile 192–194) macht das Glyph im Nicht-Hover-Zustand lesbar, doch `.icon-btn.danger:hover:not(:disabled)` (Zeile 196–199) setzt weiterhin `color: var(--danger)` (#f87171) auf dem unverändert roten `button.danger`-Hintergrund (#f87171, per `brightness(1.15)` aufgehellt). Beim Hovern wird das Glyph damit wieder nahezu unsichtbar — genau das Problem, das der Fix im Ruhezustand behebt.

  Empfehlung: Für `button.icon-btn.danger:hover` die Farbe ebenfalls auf dunklen Kontrast setzen (z. B. `#2a0606` wie `button.danger` selbst) oder die Hover-Farbregel für diese Kombination ausnehmen.

### scripts/screenshots/out-iter5/

- **Artefakt-Hygiene** — Sieben PNG-Screenshots aus der Verifikation liegen als unversionierte Dateien im Working Tree; `scripts/screenshots/` hat keine `.gitignore` für `out-*`-Verzeichnisse. Beim nächsten Commit würden sie versehentlich eingecheckt.

  Empfehlung: Vor dem Commit entfernen oder ein `.gitignore` (`out-*/`) ergänzen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Web/Components/App.razor`
- `FinanceManager.Web/Components/Pages/BudgetReport.razor`
- `FinanceManager.Web/Components/Pages/ListPage.razor`
- `FinanceManager.Web/Components/Shared/Ribbon.razor`
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleListViewModel.cs`
- `FinanceManager.Web/ViewModels/Common/BaseCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs`
- `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoriesListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupMcpViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/UserCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/UserListViewModel.cs`
- `FinanceManager.Web/wwwroot/css/theme.Dark.css`
- `FinanceManager.Web/wwwroot/manifest.json`
- `FinanceManager.Web/wwwroot/icons/finance_manager.svg` (neu)
- `FinanceManager.Web/wwwroot/icons/finance_manager-64x64.ico` / `finance_manager.png` (regeneriert, ICO-Header valide)
- `scripts/icons/generate_favicon.py` (neu)
- `FinanceManager.Tests/ViewModels/RibbonConfigTests.cs` (neu)
- `FinanceManager.Tests/Components/RibbonTests.cs`
- `FinanceManager.Tests/ViewModels/AccountsViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Tests/Web/UsersViewModelTests.cs`
- `FinanceManager.Tests/Web/ViewModels/ContactsViewModelTests.cs`
- `FinanceManager.Tests.E2E/Tests/Accounts/AccountsOverviewStatisticsPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs`
- `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportBackNavigationPlaywrightTests.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/Navigation/AccountsBackNavigationPlaywrightTests.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/Navigation/ListPageContentPanelPlaywrightTests.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/SetupBackupTabPlaywrightTests.cs` (neu)
- gelöschte Reste: `Pages.resx.new`, `Pages.de.resx.new`, `Pages.en.resx.new`
