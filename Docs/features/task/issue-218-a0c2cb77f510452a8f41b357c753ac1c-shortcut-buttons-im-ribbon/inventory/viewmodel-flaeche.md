# ViewModel-Flaeche

## Umfang

Die Suche nach `GetRibbonRegisterDefinition` zeigt eine breite UI-Flaeche. Die Ribbon-Aktionen werden in vielen ViewModels direkt als `new UiRibbonAction(...)` deklariert. Fuer Mehr-Aktions-Tabs muss dort fachlich entschieden werden, welche Aktionen als mobile Shortcuts geeignet sind.

## Zentrale Muster

### Listenansichten

Typische Listen-ViewModels enthalten Aktionen wie `New`, `Reload`, `ClearSearch`, Kategorien/Unterlisten oder Bereichsnavigation.

Relevante Dateien:

- `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetCategoryListViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleListViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/Groups/ContactGroupListViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoriesListViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/Prices/SecurityPricesListViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/UserListViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftsListViewModel.cs`

Wahrscheinliche Shortcut-Kandidaten: `New`, `Reload`, `ClearSearch`, Import, Bereichswechsel. Mehr-Aktions-Gruppen sollten nicht pauschal alle Aktionen als Shortcut bekommen.

### Kartenansichten

Typische Karten-ViewModels enthalten Navigation, Bearbeiten/Speichern, Loeschen und verknuepfte Informationen.

Relevante Dateien:

- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Contacts/Groups/ContactGroupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`
- `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/UserCardViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs`

Wahrscheinliche Shortcut-Kandidaten: `Back`, `Save`, `Edit`, wichtige naechste/previous Navigation. Destruktive Aktionen wie `Delete` sollten nur mit bewusster fachlicher Entscheidung als Shortcut markiert werden.

### Spezialseiten und Reports

Relevante Dateien:

- `FinanceManager.Web/ViewModels/Home/HomeViewModel.cs`
- `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`
- `FinanceManager.Web/ViewModels/Reports/ReportsHomeViewModel.cs`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- `FinanceManager.Web/ViewModels/Securities/ReturnAnalysis/SecurityPerformancePageViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupNotificationsViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`

Diese Seiten enthalten teils fachlich dichte Aktionen, Filter oder Import-/Export-Funktionen. Shortcut-Auswahl sollte hier besonders konservativ sein.

## FileCallback-Aktionen

`FileCallback` wird in diesen ViewModels genutzt:

- `FinanceManager.Web/ViewModels/Home/HomeViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftsListViewModel.cs`

Fuer Upload-Aktionen muss vor der Umsetzung entschieden werden, ob sie als Shortcut erlaubt sind. Technisch ist es machbar, aber die bestehende `InputFile`-Overlay-Loesung muss im Icon-only Button sauber funktionieren.

## Hidden-Aktionen

`Hidden` wird bereits fuer zustandsabhaengige Ribbon-Aktionen genutzt, z. B. bei QuickEdit in `StatementDraftCardViewModel`. Die Shortcut-Logik muss Hidden nach vorhandener Semantik respektieren. Eine versteckte Aktion darf weder im mobilen Menue noch als Header-Shortcut erscheinen.

## Automatische Ein-Aktions-Regel

Einige Tabs/Gruppen enthalten genau eine Aktion, z. B. einfache Back- oder New-Gruppen. Diese koennen zentral automatisch Shortcuts erhalten. Entscheidend ist, dass die Regel nach Filterung von `Hidden` gilt und nicht dazu fuehrt, dass eine durch Zustand versteckte Aktion als Shortcut zurueckkommt.

## Fachliche Bewertung fuer Mehr-Aktions-Gruppen

Die Anforderung sagt, dass Tabs mit mehreren Aktionen individuell bewertet werden. Das ist kein reines Infrastrukturthema. Die Planung sollte pro Bereich eine Liste der markierten Aktionen festlegen oder eine konservative Default-Auswahl definieren, z. B.:

- primare nicht-destruktive Aktion (`Save`, `New`, `Reload`) bevorzugen,
- Navigation (`Back`, `Prev`, `Next`) auf mobilen Detailseiten bevorzugen,
- destruktive Aktionen (`Delete`, `Archive`) nicht automatisch markieren,
- seltene Spezialaktionen im aufgeklappten Menue lassen.
