# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SavingsPlansListViewModel.cs (Sparplanübersicht)

- **Erreichbarkeit** — Die Anforderung sieht in der Gruppe „Ansicht" drei Aktionen vor: „Neu laden" (groß), „Suche zurücksetzen" (klein, nur aktiviert bei Suchbegriff) und „Nur aktive/Archiviert" (klein). Gebaut ist nur der Toggle „Nur aktive/Archiviert" (`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`, `GetRibbonRegisterDefinition`, Gruppe `Ribbon_Group_View`, Sort 30). „Neu laden" und „Suche zurücksetzen" fehlen — anders als bei Sparplankategorie-, Benutzer- und KI-Zugänge-Übersicht sind sie hier nicht als „heute nicht vorhanden → Issue" markiert, d. h. die Anforderung erwartet sie auf dieser Ansicht. Eine Anwenderin, die die Liste neu laden oder einen eingegebenen Suchbegriff über das Menüband zurücksetzen will, findet dafür kein Bedienelement (das Suchfeld lässt sich zwar manuell leeren, die geforderte Menüband-Aktion fehlt jedoch).

  Empfehlung: In der Gruppe „Ansicht" eine `UiRibbonAction` „Reload" (Label `Ribbon_Reload`, `UiRibbonItemSize.Large`, `RaiseUiActionRequested("Reload")`) sowie „ClearSearch" (Label `Ribbon_ClearSearch`, `UiRibbonItemSize.Small`, `Disabled: string.IsNullOrWhiteSpace(Search)`, `RaiseUiActionRequested("ClearSearch")`) ergänzen — beide Handler sind in `ListPage.razor` bereits verdrahtet, analog zur Kontakt- und Budgetübersicht.

### SecuritiesListViewModel.cs (Wertpapierübersicht)

- **Erreichbarkeit** — Gleiches Fehlbild: Die Anforderung fordert in der Gruppe „Ansicht" „Neu laden" (groß), „Suche zurücksetzen" (klein, nur aktiviert bei Suchbegriff) und „Nur aktive/Archiviert" (klein). Gebaut ist nur der Toggle (`FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs`, `GetRibbonRegisterDefinition`, `view`-Liste, Sort 30). „Neu laden" und „Suche zurücksetzen" sind nicht über das Menüband erreichbar, obwohl die Ansicht ein Suchfeld anbietet (`AllowSearchFiltering = true`).

  Empfehlung: Wie bei der Sparplanübersicht — „Reload"- und „ClearSearch"-Aktionen in der „Ansicht"-Gruppe ergänzen; die Handler in `ListPage.razor` (`Reload`, `ClearSearch`/`ClearFilter`) existieren bereits.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Menüband-Gruppen, -Reihenfolge (`UiRibbonTab.Sort` → `Ribbon.razor` sortiert via `OrderBy(g => g.Sort)`) und Symbolgrößen je Ansicht → unauffällig (Stichproben: Kontakte, Budget, Einstellungen, Wertpapiere, Sparpläne)
- Konditionales „Zurück" auf der Kontenübersicht (nur bei `?back=`-Kontext, groß, sonst ausgeblendet) → unauffällig (`BankAccountListViewModels` erzeugt die Navigationsgruppe nur bei `InitBack`; `ListPage` reicht den Query-Parameter via `SetBackNavigation` durch und navigiert beim Klick zum übergebenen Ziel)
- Konditionales „Zurück" auf dem Budgetbericht (nur nach Navigation von der Budgetübersicht) → unauffällig (`ListPage` hängt `?back=<Herkunfts-URL>` an `reports/budget`; `BudgetReport.razor` blendet die Gruppe kontextabhängig ein und navigiert zurück; ohne Kontext erscheint keine Navigationsgruppe)
- „Suche zurücksetzen" nur aktiviert bei eingegebenem Suchbegriff → unauffällig (`Disabled: string.IsNullOrWhiteSpace(Search)` auf Konten-, Kontakt- und Budgetübersicht)
- „Neu laden" und „Suche zurücksetzen" auf der Sparplanübersicht → Befund vorhanden (s. o.)
- „Neu laden" und „Suche zurücksetzen" auf der Wertpapierübersicht → Befund vorhanden (s. o.)
- Umgruppierte Aktionen: „Zusammenführen" (Kontaktdetails → Verwalten), „Detaillierte Analyse" (Wertpapierdetails → Berichte), „Depot-Bericht"/„Bericht" (groß) → unauffällig, alle Klicks sind verdrahtet (Callbacks bzw. `RaiseUiActionRequested` mit Handler in `ListPage`/`CardPage`/`BudgetReport.razor`)
- Umbenannte/neue Gruppen- und Registerlabels („Ansicht", „Daten", „Programmversion" statt „Aktualisierungen", „Sicherungen" statt „Datensicherungen") → unauffällig (resx de + en vorhanden; `Setup_Section_Update` steuert Gruppe und Register gleichermaßen)
- Korrigierter Tooltip „Aggregatposten neu berechnen" → unauffällig (`Hint_RebuildAggregates` in de + en angelegt und verdrahtet)
- Inhaltspanel auf Übersichtsseiten (kein Layout-Sprung Übersicht ↔ Detail) → unauffällig (`ListPage.razor` umschließt Menüband und Listeninhalt jetzt mit `card-page-content` wie `CardPage.razor`; `GenericListPage` wird nur eingebettet verwendet)
- Leere Menüband-Gruppen bei ausgeblendeten Aktionen → unauffällig (`Ribbon.razor` entfernt Gruppen ohne sichtbare Items, kein leerer Gruppenrahmen)
- Löschen-Symbol in der Sicherungs-Tabelle lesbar → unauffällig (`button.icon-btn.danger { color:#000 }` in `theme.Dark.css` stellt schwarzes Symbol auf rotem `button.danger`-Hintergrund sicher; Hover-Farbe bleibt)
- Favicon → unauffällig (neues `finance_manager.svg` mit Farbgestaltung passend zum Redesign, in `App.razor` eingebunden, `.ico`/`.png` neu generiert, `manifest.json` Theme-Farben angepasst)
- Keine internen Kennungen/IDs erforderlich; keine Auswahl-/Suchaufgaben ohne Lookup → unauffällig (reine Umorganisation vorhandener, benannter Menüband-Aktionen)

## Geprüfte Dateien

- `FinanceManager.Web/Components/App.razor`
- `FinanceManager.Web/Components/Pages/BudgetReport.razor`
- `FinanceManager.Web/Components/Pages/ListPage.razor`
- `FinanceManager.Web/Components/Pages/GenericListPage.razor` (Eingebettetes Listenelement — unverändert, Kontext geprüft)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (Vergleichs-Layout — unverändert, Kontext geprüft)
- `FinanceManager.Web/Components/Shared/Ribbon.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor` (unverändert, Kontext zum Farbfix geprüft)
- `FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`, `BaseCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs`, `ContactCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`, `SavingsPlanCardViewModel.cs`, `Categories/SavingsPlanCategoryListViewModel.cs`, `Categories/SavingsPlanCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs`, `SecurityCardViewModel.cs`, `Categories/SecurityCategoriesListViewModel.cs`, `Categories/SecurityCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`, `BudgetPurposeCardViewModel.cs`, `BudgetRuleCardViewModel.cs`, `BudgetRuleListViewModel.cs`, `BudgetReportViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`, `SetupBackupsViewModel.cs`, `SetupProfileViewModel.cs`, `SetupMcpViewModel.cs`, `UserListViewModel.cs`, `UserCardViewModel.cs` (sowie `SetupUpdateViewModel.cs` im Kontext der Register-Umbenennung)
- `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs`, `McpAccessCardViewModel.cs`
- `FinanceManager.Web/wwwroot/css/theme.Dark.css`
- `FinanceManager.Web/wwwroot/icons/finance_manager.svg`, `finance_manager-64x64.ico`, `finance_manager.png`, `manifest.json`

Hinweis: `dotnet build FinanceManager.Web` erfolgreich (0 Fehler). Die von der Anforderung ausdrücklich als GitHub-Issue ausgelagerten Punkte (fehlende Aktionen wie „Zurücksetzen", „Passwort zurücksetzen", „Neu laden"/„Suche zurücksetzen" auf Sparplankategorie-/Benutzer-/KI-Zugänge-Übersicht, defekte Zurück-Routen der KI-Ansichten u. a.) wurden nicht als Befund gewertet, da ihre Implementierung explizit aus dem Scope ausgeschlossen ist.
