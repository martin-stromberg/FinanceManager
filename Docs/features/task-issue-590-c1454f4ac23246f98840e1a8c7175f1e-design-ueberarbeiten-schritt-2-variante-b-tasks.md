# Tasks: Menüband-Aktionen — Standardgröße und Reihenfolge je Ansicht + Korrekturen (Issue #590, Schritt 2, Variante B)

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Ressourcen | `Ribbon_Group_View` („Ansicht"/„View") in `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` anlegen | Offen | — |
|| 2 | Ressourcen | `Ribbon_Group_Data` („Daten"/„Data") in allen drei `Pages.*.resx` anlegen | Offen | — |
|| 3 | Ressourcen | `Hint_RebuildAggregates` in allen drei `Pages.*.resx` nachziehen (Werte aus `Components/Pages/Setup.*.resx`) | Offen | — |
|| 4 | Ressourcen | `Setup_Section_Update`-Wert → „Programmversion"/„Program version" umbenennen | Offen | — |
|| 5 | Ressourcen | `SetupBackup_Titel`-Wert (de) → „Sicherungen" umbenennen | Offen | — |
|| 6 | Ressourcen | `Ribbon_SetPassword`-Wert → „Passwort zurücksetzen"/„Reset password" angleichen | Offen | — |
|| 7 | Ressourcen | Leere `Pages*.resx.new`-Artefakte in `FinanceManager.Web/Resources/` entfernen | Offen | — |
|| 8 | Infrastruktur | `SetBackNavigation`/`InitBack`/`_initBack` von `BaseCardViewModel` nach `BaseViewModel` hochziehen | Offen | — |
|| 9 | Infrastruktur | `Ribbon.razor`: in `BuildTabsToRender` Gruppen mit `Items.Count == 0` nach `Hidden`-Filterung verwerfen (kein leerer Gruppenrahmen/Footer) | Offen | — |
|| 10 | Infrastruktur | `ListPage.razor`: `?back=`-Parameter (`SupplyParameterFromQuery`), `SetBackNavigation(Back)` direkt nach Provider-Erzeugung in `OnParametersSetAsync`, `case "Back"` navigiert bevorzugt zum `Back`-Parameter | Offen | — |
|| 11 | Infrastruktur | `ListPage.razor`: `case "OpenBudgetReport"` navigiert auf `reports/budget?back=<Nav.Uri>` | Offen | — |
|| 12 | Infrastruktur | `BudgetReport.razor`: `?back=`-Parameter, `SetBackNavigation(_vm)` in `OnParametersSet` (nicht `OnInitialized` — Query-only-Wechsel muss `InitBack` aktualisieren), `case "Back"`-Handling | Offen | — |
|| 13 | Ribbon | `BankAccountListViewModels`: Navigieren-Tab mit `Back` L **nur bei gesetztem `InitBack` erzeugen** (bedingte Gruppenerzeugung, kein `Hidden`); Verwalten(`New` L, Sort 10); Filter(`ClearSearch` L, dis: kein Suchbegriff, Sort 30) | Offen | — |
|| 14 | Ribbon | `ContactListViewModel`: `Reload`/`ClearFilter` in neue Ansicht-Gruppe, `ClearFilter` L→S | Offen | — |
|| 15 | Ribbon | `ContactCardViewModel`: `OpenMerge` von Linked → Manage (S) | Offen | — |
|| 16 | Ribbon | `SavingsPlansListViewModel`: `Categories` S→L, `ToggleActive` in neue Ansicht-Gruppe (S), Filter-Register entfällt | Offen | — |
|| 17 | Ribbon | `SavingsPlanCardViewModel`: `Recalculate`/`ActualReport` S→L, `MobileShortcut` auf `Save`, Sorts | Offen | — |
|| 18 | Ribbon | `SavingsPlanCategoryListViewModel`: explizite Sorts setzen | Offen | — |
|| 19 | Ribbon | `SavingsPlanCategoryCardViewModel`: explizite Sorts setzen | Offen | — |
|| 20 | Ribbon | `SecuritiesListViewModel`: `Ribbon_Group_Actions`→`Ribbon_Group_Manage`, `Categories`/`PortfolioAnalysisReport` L, `ToggleActive` in Ansicht-Gruppe | Offen | — |
|| 21 | Ribbon | `SecurityCardViewModel`: `Performance` → neue Berichte-Gruppe (L), Linked-Reihenfolge Postings/Attachments/Prices | Offen | — |
|| 22 | Ribbon | `SecurityCategoriesListViewModel`: `Back`/`New` S→L | Offen | — |
|| 23 | Ribbon | `SecurityCategoryCardViewModel`: explizite Sorts setzen | Offen | — |
|| 24 | Ribbon | `BudgetPurposeListViewModel`: Aufteilung Verwalten/Ansicht/Analyse/Berichte, `Categories`/`BudgetReport`/`Reload` Größen | Offen | — |
|| 25 | Ribbon | `BudgetPurposeCardViewModel`: explizite Sorts setzen | Offen | — |
|| 26 | Ribbon | `BudgetRuleListViewModel`: Regeln-Tab `Sort = 60`; Sichtbarkeit von `NewRule` via Kind-VM-Aggregation verifizieren | Offen | — |
|| 27 | Ribbon | `BudgetRuleCardViewModel`: explizite Sorts (Felder-Gruppe bleibt) | Offen | — |
|| 28 | Ribbon | `BudgetReportViewModel`: Navigieren-Tab mit `Back` L **nur bei gesetztem `InitBack` erzeugen**, `Settings` S→L, Sorts | Offen | — |
|| 29 | Ribbon | `UserListViewModel`: explizite Sorts setzen (Ansicht-Gruppe entsteht nicht — Reload/ClearSearch → Issue) | Offen | — |
|| 30 | Ribbon | `UserCardViewModel`: explizite Sorts setzen; `SetPassword` als genannte Aktion vor die konditionalen `Deactivate`/`Activate`/`Unblock` ziehen | Offen | — |
|| 31 | Ribbon | `SetupCardViewModel`: `RebuildAggregates`/`ResetReportCache` in neue Daten-Gruppe (`Ribbon_Group_Data`, Sort 15) | Offen | — |
|| 32 | Ribbon | `SetupBackupsViewModel`: `CreateBackup`/`UploadBackup` L→S | Offen | — |
|| 33 | Ribbon | `SetupUpdateViewModel`: Sort 50 verifizieren (Gruppen-Titel kommt aus umbenanntem resx-Key) | Offen | — |
|| 34 | Ribbon | `SetupProfileViewModel`: Sort 20 setzen | Offen | — |
|| 35 | Ribbon | `SetupMcpViewModel`: `McpApprovals` L→S, Sort 35 | Offen | — |
|| 36 | Ribbon | `McpAccessListViewModel`: Aufteilung Verwalten/Ansicht/Linked, `Reload` S→L | Offen | — |
|| 37 | Ribbon | `McpAccessCardViewModel`: explizite Sorts setzen (`RotateToken` bleibt an bisheriger Position zwischen `Save`/`[Reset]` und `Deactivate`/`Activate`/`Revoke`) | Offen | — |
|| 38 | Layout | `ListPage.razor`: `.card-page-content`-Wrapper um Inhaltszweig | Offen | — |
|| 39 | Layout | `theme.Dark.css`: `button.icon-btn.danger` `color: #000` im Nicht-Hover | Offen | — |
|| 40 | Favicon | `wwwroot/icons/finance_manager.svg` (SVG-Master, F-Chart-Motiv Teal/Violett auf Dunkel) erstellen | Offen | — |
|| 41 | Favicon | `scripts/icons/generate_favicon.py` erstellen → `finance_manager-64x64.ico` (Multi-Size) + `finance_manager.png` erzeugen | Offen | — |
|| 42 | Favicon | `App.razor`: `<link rel="icon" type="image/svg+xml">` ergänzen; `manifest.json` `theme_color`/`background_color` → `#080a0f` | Offen | — |
|| 43 | GitHub-Issues | 6 Aktions-Issues anlegen (A1–A6 gemäß plan.md: Zurücksetzen ×9 Ansichten, Reload/ClearSearch auf 3 Übersichten, ClearSearch KI-Übersicht, Kategorie Wertpapierdetail, Reload/ClearSearch Sparplan-+Wertpapierübersicht) | Offen | — |
|| 44 | GitHub-Issues | 10 Fehlverhalten-Issues anlegen (B1–B10 gemäß plan.md, Titel + Beschreibung aus Kommentar, Ref #590) — gesamt 16 Issues | Offen | — |
|| 45 | Tests | bUnit/VM-Tests der Zielkonfiguration (Gruppenreihenfolge/-titel, `UiRibbonItemSize`, `Disabled`) für repräsentative VMs | Offen | — |
|| 46 | Tests | VM-Tests bedingte Gruppenerzeugung: `BankAccountListViewModels` + `BudgetReportViewModel` — kein Navigieren-Tab ohne `InitBack`, Tab vorhanden nach `SetBackNavigation` | Offen | — |
|| 47 | Tests | `RibbonTests.cs`: neuer Fact `HiddenOnlyGroup_IsNotRendered` (Tab mit ausschließlich `Hidden`-Items rendert keine Gruppe) | Offen | — |
|| 48 | Tests | `ContactsViewModelTests.GetRibbon_UsesManageGroup_WithAllActionsLarge` anpassen (2 Gruppen Verwalten+Ansicht, `ClearFilter` S) | Offen | — |
|| 49 | Tests | `SetupCardViewModelTests` anpassen: `PlacesManageGroupFirst` (`Sort == 10` statt 0), `UsesExpectedActionSizes` (`RebuildAggregates`/`ResetReportCache` aus `Ribbon_Group_Data`) | Offen | — |
|| 50 | Tests | `AccountsViewModelTests.SetFilter_AffectsLoad_AndRibbon` anpassen (über `r.Tabs.SelectMany(t => t.Items)` flachen — `UiRibbonRegister.Items` = nur `Tabs[0]`) | Offen | — |
|| 51 | Tests | `UsersViewModelTests.GetRibbon_ShouldComposeGroups_ByState` schärfen (genau 1 Tab `Ribbon_Group_Manage`, kein `Ribbon_Group_View`-Tab) | Offen | — |
|| 52 | E2E-Tests | `RibbonGroupOrderPlaywrightTests` auf Zielkonfiguration anpassen/erweitern (inkl. neuem Fact `UsersList_RendersNoEmptyViewGroup` mit Admin-Login: genau eine Gruppe, kein „Ansicht"-Header — Nachweis Szenario 10) | Offen | — |
|| 53 | E2E-Tests | `AccountsOverviewStatisticsPlaywrightTests` erweitern (`#ClearSearch` disabled→enabled bei Suchbegriff) | Offen | — |
|| 54 | E2E-Tests | `BudgetReportDemoDataE2ETests` L79 anpassen (`WaitForURLAsync("**/reports/budget")` bricht durch `?back=` → Predicate/`**`-Glob) | Offen | — |
|| 55 | E2E-Tests | Neu: `AccountsBackNavigationPlaywrightTests` (`?back=` sichtbar/keine Gruppe + Navigation; Negativ-Assertion „keine leere Gruppe") | Offen | — |
|| 56 | E2E-Tests | Neu: `BudgetReportBackNavigationPlaywrightTests` (Kette list/budget → Bericht → zurück; direkter Aufruf ohne `Back` und ohne leere Gruppe) | Offen | — |
|| 57 | E2E-Tests | Neu: `ListPageContentPanelPlaywrightTests` (`.card-page-content`-Wrapper + BoundingBox-Vergleich Liste↔Karte) | Offen | — |
|| 58 | E2E-Tests | Neu: `SetupBackupTabPlaywrightTests` (`.icon-btn.danger` Kontrast: `color` ≠ `background`) | Offen | — |
|| 59 | Abnahme | `scripts/screenshots/generate-screenshots.js` ausführen + gezielte Shots (geänderte Ribbon-Ansichten, Sicherungs-Tab, Favicon) zur Sichtprüfung | Offen | — |
|| 60 | Abnahme | Unit-/Integration-/E2E-Regression ausführen; bekannte Bestandsfehler dokumentieren | Offen | — |
