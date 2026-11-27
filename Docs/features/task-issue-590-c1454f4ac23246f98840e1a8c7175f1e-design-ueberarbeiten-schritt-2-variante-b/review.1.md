# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Infrastruktur

- [x] `BaseViewModel.cs` — `_initBack`-Feld, `public void SetBackNavigation(string?)`, `protected string? InitBack` hinzugefügt (aus `BaseCardViewModel` hochgezogen; `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` L109–121)
- [x] `BaseCardViewModel.cs` — `_initBack`, `SetBackNavigation`, `InitBack` entfernt, Verhalten via Vererbung identisch (L13–45)
- [x] `Ribbon.razor` — `groups.RemoveAll(g => g.Items.Count == 0)` nach Aufbau der `groups`-Liste in `BuildTabsToRender` (L220–221)
- [x] `ListPage.razor` — `[Parameter, SupplyParameterFromQuery(Name = "back")] Back` (L97), `SetBackNavigation(Back)` direkt nach `_provider`-Zuweisung vor `InitializeAsync` (L152), `case "Back"` bevorzugt `Back`-Parameter vor den Fallbacks (L211–216), `case "OpenBudgetReport"` → `reports/budget?back={Uri.EscapeDataString(Nav.Uri)}` (L208), `<div class="card-page-content">`-Wrapper um den gesamten else-Renderblock (L27/L88)
- [x] `BudgetReport.razor` — `Back`-Query-Parameter (L21), `OnParametersSet` → `_vm?.SetBackNavigation(Back)` (L30–33), `case "Back"` → `Nav.NavigateTo(Back)` bei gesetztem Parameter (L66–73)
- [x] `App.razor` — `<link rel="icon" type="image/svg+xml" href="icons/finance_manager.svg">` ergänzt, `.ico`-Link bleibt als Fallback (L8)
- [x] `manifest.json` — `theme_color`/`background_color` auf `#080a0f` gesetzt
- [x] `theme.Dark.css` — neue spezifischere Regel `button.icon-btn.danger { color: #000; }`, Hover-Regel unverändert (L190–193)

### Ressourcen (`Resources/Pages.{resx,de.resx,en.resx}`)

- [x] `Ribbon_Group_View` neu — de „Ansicht", en/neutral „View"
- [x] `Ribbon_Group_Data` neu — de „Daten", en/neutral „Data"
- [x] `Hint_RebuildAggregates` neu — de + en/neutral gemäß Plan
- [x] `Setup_Section_Update` — de „Programmversion", en/neutral „Program version"
- [x] `SetupBackup_Titel` — de „Sicherungen" (en/neutral „Backups" unverändert)
- [x] `Ribbon_SetPassword` — de „Passwort zurücksetzen", en/neutral „Reset password"
- [x] Leere `Pages.*.resx.new`-Artefakte gelöscht

### Ribbon-ViewModels (Zielkonfiguration je Ansicht)

- [x] `BankAccountListViewModels.cs` — Navigieren(0) nur bei `InitBack` mit `Back` L; Verwalten(10): `New` L; Filter(30): `ClearSearch` L, `Disabled` ohne Suchbegriff
- [x] `ContactListViewModel.cs` — Verwalten(10): `New` L, `Groups` L; Ansicht(30): `Reload` L, `ClearFilter` S
- [x] `ContactCardViewModel.cs` — nav 0 / manage 10 (`Save` L, `Delete` S, `OpenMerge` S neu hierhin) / linked 55 (`OpenPostings`, `Attachments`)
- [x] `SavingsPlansListViewModel.cs` — Verwalten(10): `New` L, `Categories` L; Ansicht(30): `ToggleActive` S; Filter-Gruppe/`Custom`-Register entfernt
- [x] `SavingsPlanCardViewModel.cs` — nav 0 / manage 10 (`Save` L +`MobileShortcut`, `Delete` S, `Archive` S) / Analyse 40 (`Recalculate` L) / Buchungen 45 (`ActualReport` L) / linked 55
- [x] `SavingsPlanCategoryListViewModel.cs` — nav 0 (`Back` L → `list/savings-plans`), manage 10 (`New` L); keine leere Ansicht-Gruppe
- [x] `SavingsPlanCategoryCardViewModel.cs` — Sort explizit 0/10
- [x] `SecuritiesListViewModel.cs` — `Ribbon_Group_Actions` → `Ribbon_Group_Manage`(10): `New` L, `Categories` L; Ansicht(30): `ToggleActive` S; Berichte(70): `PortfolioAnalysisReport` L
- [x] `SecurityCardViewModel.cs` — nav 0 / manage 10 (`Save`, `Delete`, `Archive`) / linked 55 (`Postings`, `Attachments`, `Prices`) / Berichte 70 (`Performance` L)
- [x] `SecurityCategoriesListViewModel.cs` — `Back`/`New` S→L, Sort 0/10
- [x] `SecurityCategoryCardViewModel.cs` — Sort explizit 0/10
- [x] `BudgetPurposeListViewModel.cs` — Navigations-Gruppe aufgeteilt: Verwalten(10) `New`/`Categories` L; Ansicht(30) `Reload` L/`ClearFilter` S; Analyse(40) unverändert S; Berichte(70) `BudgetReport` L
- [x] `BudgetPurposeCardViewModel.cs` — Sort 0/10/55; `NewRule` L via Kind-VM `BudgetRuleListViewModel` (Sort 60) unverändert sichtbar
- [x] `BudgetRuleListViewModel.cs` — Regeln-Tab `Sort = 60`
- [x] `BudgetRuleCardViewModel.cs` — nav 0 / manage 10 / Felder(47, `ClearPurposePattern` S an bisheriger Position)
- [x] `BudgetReportViewModel.cs` — Navigieren(0) neu, nur bei `InitBack` mit `Back` L → `RaiseUiActionRequested("Back")`; Verwalten(10) `Settings` L; Analyse(40); Export(80)
- [x] `UserListViewModel.cs` — Verwalten(10) `New` L +`MobileShortcut`; keine Ansicht-Gruppe
- [x] `UserCardViewModel.cs` — nav 0 / manage 10; `SetPassword` S vor die konditionalen `Deactivate`/`Activate`/`Unblock` gezogen (heute am Listenende war)
- [x] `SetupCardViewModel.cs` — Verwalten(10) `Save` L + `Reset` S; Daten(15, `Ribbon_Group_Data`) `RebuildAggregates`/`ResetReportCache` S, Tooltip `Hint_RebuildAggregates` verwendet
- [x] `SetupBackupsViewModel.cs` — `CreateBackup`/`UploadBackup` L→S; Titel `SetupBackup_Titel`, Sort `int.MaxValue` (bestehend)
- [x] `SetupUpdateViewModel.cs` — unverändert korrekt: Sort 50 bestehend, Titel `Setup_Section_Update` (umbenannt via resx); `UpdateResetLock` an bisheriger Position
- [x] `SetupProfileViewModel.cs` — Profil-Tab `Sort = 20`
- [x] `SetupMcpViewModel.cs` — KI(35): `McpApprovals` L→S, `McpAudit`/`McpSecurity` S, `Hidden = !IsAdmin` auf `McpSecurity` erhalten
- [x] `McpAccessListViewModel.cs` — Verwalten(10) `New` L; Ansicht(30) `Reload` L; Linked(55) `Approvals`/`AuditLog`/`Security` S, `Security` disabled bei `!IsAdmin`
- [x] `McpAccessCardViewModel.cs` — nav 0 `Back` L → `card/setup`; manage 10 `Save` L, konditional `RotateToken`/`Deactivate`/`Activate`/`Revoke` S in bisheriger Reihenfolge; Sort explizit

### Favicon

- [x] `scripts/icons/generate_favicon.py` — neu (Pillow-Generator, 143 Zeilen)
- [x] `wwwroot/icons/finance_manager.svg` — neu (SVG-Master, Teal/Violett-Motiv)
- [x] `finance_manager-64x64.ico` — neu gerendert, Multi-Size (4 Icons: 16/32/48/64)
- [x] `finance_manager.png` — neu gerendert, 1024×1024 RGBA
- [x] Einbindung in `App.razor` und `manifest.json` (s. o.)

### Tests

- [x] `FinanceManager.Tests/ViewModels/RibbonConfigTests.cs` (neu) — Zielkonfiguration: `BankAccountListViewModel` (konditionaler Nav-Tab, Größen, Disabled), `BudgetReportViewModel` (konditionaler Nav-Tab, `Settings` L, Analyse/Export), `SecuritiesListViewModel` (Manage/View/Reports), `SecurityCardViewModel` (Linked ohne Performance, Reports L), `SetupCardViewModel` (aggregierte Sort-Reihenfolge inkl. Sektions-VMs)
- [x] `RibbonTests.cs` — neuer Fact `HiddenOnlyGroup_IsNotRendered` (Desktop + Mobile)
- [x] `AccountsViewModelTests.SetFilter_AffectsLoad_AndRibbon` — über `Tabs.SelectMany(t => t.Items)` geflacht
- [x] `SetupCardViewModelTests` — `PlacesManageGroupFirst` auf Sort 10/15 + `Ribbon_Group_Data`; `UsesExpectedActionSizes` auf Manage/Data-Aufteilung angepasst
- [x] `UsersViewModelTests.GetRibbon_ShouldComposeGroups_ByState` — geschärft: Titel `Ribbon_Group_Manage`, kein `Ribbon_Group_View`
- [x] `ContactsViewModelTests.GetRibbon_UsesManageGroup_WithAllActionsLarge` — 2 Tabs, Größen je Gruppe (`ClearFilter` S)
- [x] `BudgetReportDemoDataE2ETests` — `WaitForURLAsync` auf Predicate `url.Contains("/reports/budget")` umgestellt
- [x] `AccountsOverviewStatisticsPlaywrightTests` — neuer Fact `AccountsOverview_ClearSearch_DisabledUntilSearchTermIsSet`
- [x] `RibbonGroupOrderPlaywrightTests` — 5 neue Facts: `ContactsList_RendersManageThenViewGroup`, `SecuritiesList_RendersManageViewReportsGroups`, `UsersList_RendersNoEmptyViewGroup` (Admin), `SetupCard_RendersGroupsInConventionOrder`, `SetupCard_RebuildAggregatesTooltip_IsLocalized`
- [x] `AccountsBackNavigationPlaywrightTests.cs` (neu) — ohne `?back=` kein `#Back`/keine leere Gruppe; mit `?back=` Nav-Gruppe an Position 0 + Navigation
- [x] `BudgetReportBackNavigationPlaywrightTests.cs` (neu) — Kette `list/budgets` → `#BudgetReport` → `?back=` → `#Back`; Direktaufruf ohne Gruppe
- [x] `ListPageContentPanelPlaywrightTests.cs` (neu) — `.card-page-content`-Container auf Listenseite, BoundingBox-Vergleich mit CardPage
- [x] `SetupBackupTabPlaywrightTests.cs` (neu) — CSS-Assertion `color: rgb(0, 0, 0)` auf `button.icon-btn.danger`

### GitHub-Issues (Akzeptanzszenario 9)

- [x] A1–A6 (fehlende Aktionen): #593, #594, #595, #596, #597, #598
- [x] B1–B10 (Fehlverhalten): #599, #600, #601, #602, #603, #604, #605, #606, #607, #608

## Offene Aufgaben

- [ ] `Screenshot-Abnahme` (Umsetzungsschritt 10) — teilweise umgesetzt: Gezielte Shots liegen in `scripts/screenshots/out-iter5/` (7 PNGs: Kontenliste, Setup, Popover, Mobile), aber `scripts/screenshots/generate-screenshots.js` wurde nicht ausgeführt — `Docs/screenshots` zeigt keinen Diff seit Commit `af8c80d0` (2026-11-25, vor dieser Implementierung), und Shots für den Sicherungs-Tab sowie den Favicon fehlen in `out-iter5`

## Hinweise

- `dotnet build FinanceManager.Web.csproj`: 0 Fehler (6 bestehende NU1510-Warnungen, unabhängig von dieser Änderung).
- Zusätzliches Issue #609 („'Neu' in Budgetzweckregelliste wirft Null-Referenz…") wurde über die 16 geforderten Issues hinaus angelegt — vermutlich ein während der Implementierung gefundener Fehler; kein Konflikt mit dem Plan.
- Die Tasks-Datei `docs/features/task/issue-590-…-tasks.md` enthielt noch die Aufgaben des früheren Variante-B-Redesign-Plans (bereits in `af8c80d0` umgesetzt) und wurde für diesen Review auf die Tasks des aktuellen `plan.md` umgeschrieben.
- `ContactListViewModel.Reload` trägt weiterhin `MobileShortcut = true` (neben `New`); der Plan verlangt den Shortcut nur auf der primären Aktion, verbietet weitere aber nicht — als Beobachtung, kein offener Punkt.
