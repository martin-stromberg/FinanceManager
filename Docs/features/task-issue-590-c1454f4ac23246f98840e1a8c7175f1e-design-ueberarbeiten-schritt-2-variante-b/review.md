# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

*(Nachtrag: Die unten vermerkte offene Aufgabe „Docs/screenshots-Neugenerierung" wurde im Lifecycle-Schritt 10 ausgeführt — `generate-screenshots.js` lief erfolgreich, 9 PNGs regeneriert. Damit keine offene Aufgabe mehr.)*

## Umgesetzte Planelemente

### Infrastruktur

- [x] `BaseViewModel.cs` — `_initBack`-Feld, `public void SetBackNavigation(string?)`, `protected string? InitBack` vorhanden (`FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` L109–120)
- [x] `BaseCardViewModel.cs` — `_initBack`/`SetBackNavigation`/`InitBack` entfernt (keine Treffer mehr; Verhalten via Vererbung identisch, `ICardInitializable` weiterhin erfüllt)
- [x] `Ribbon.razor` — Empty-Group-Filter `groups.RemoveAll(g => g.Items.Count == 0)` in `BuildTabsToRender` (L221)
- [x] `ListPage.razor` — `[Parameter, SupplyParameterFromQuery(Name = "back")] Back` (L100); `SetBackNavigation` direkt nach Provider-Erzeugung vor `InitializeAsync` (L156); `case "Back"` bevorzugt `Back`-Parameter vor den Fallbacks (L217–221); `case "OpenBudgetReport"` → `reports/budget?back={Uri.EscapeDataString(Nav.Uri)}` (L212); `<div class="card-page-content">`-Wrapper um den gesamten else-Block (L27–50)
- [x] `BudgetReport.razor` — `Back`-Query-Parameter (L22); `OnParametersSet` → `_vm?.SetBackNavigation(Back)` (L31–35); `case "Back"` → `Nav.NavigateTo(Back)` bei gesetztem Parameter (L68–76)
- [x] `App.razor` — `<link rel="icon" type="image/svg+xml" href="icons/finance_manager.svg">` ergänzt, `.ico`-Link bleibt Fallback (L8)
- [x] `manifest.json` — `theme_color`/`background_color` = `#080a0f` (L13–14)
- [x] `theme.Dark.css` — `button.icon-btn.danger { color: #000; }` im Nicht-Hover (L192–195); **Hover-Korrektur neu:** `button.icon-btn.danger:hover:not(:disabled)` mit `background: var(--danger); color: #2a0606` (L204–208) — rotes Glyph auf rotem Grund im Hover behoben (Code-Review-Befund 4 geschlossen)

### Code-Review-Befunde Runde 1 — nachgewiesen gefixt

- [x] E2E `ExpandSectionAsync` (`SetupBackupTabPlaywrightTests.cs` L68–82): wartet jetzt auf **Attribut-Präsenz** (`aria-expanded is not null` — Blazor rendert Bool-Attribute nur bei `true`, korrekt kommentiert), klickt retry-basiert mit Endzustand-Assertion; Sektions-Toggle wird per `HasTextRegex("Sicherung|Backup")` gefunden (L38–39)
- [x] Overlay-Hosts aus `.card-page-content` gezogen: `ConfirmationDialogHost` (L56) und `.split-center.list-overlay`-Block (L59–91) liegen **außerhalb** des Wrappers mit begründendem Kommentar (L52–55) — `backdrop-filter`-Containing-Block-Problem gelöst
- [x] `?back=` Same-Origin-Validierung: neue Methode `NavigationManagerExtensions.IsSameOriginUri` (`FinanceManager.Web/Extensions/NavigationManagerExtensions.cs` L180–196 — Scheme+Authority-Vergleich gegen `BaseUri`, lehnt `javascript:`/scheme-relative/fremde Origins ab); eingesetzt in `ListPage.razor` L156 (SetBackNavigation-Sanitizer) + L217 und `BudgetReport.razor` L34 + L71
- [x] Artefakt-Hygiene: `scripts/screenshots/out-iter5/` entfernt; nur `out-review/` (bewusste Nachweis-Artefakte) liegt unversioniert vor

### Ressourcen (`Resources/Pages.{resx,de.resx,en.resx}`)

- [x] `Ribbon_Group_View`, `Ribbon_Group_Data`, `Hint_RebuildAggregates` — jeweils in allen drei resx-Dateien vorhanden (Verifikation: 3 Treffer pro Datei)
- [x] `Setup_Section_Update` („Programmversion"), `SetupBackup_Titel` („Sicherungen"), `Ribbon_SetPassword` („Passwort zurücksetzen") — angeglichen (`Pages.de.resx` L1392/L1593/L1734)
- [x] Leere `Pages.*.resx.new`-Artefakte gelöscht (in `git status` als `D` gelistet)

### Ribbon-ViewModels (Zielkonfiguration je Ansicht)

Alle 20 ViewModels aus der Zieltabelle wurden in Runde 1 zeilenweise verifiziert (`review.1.md`); stichprobenartige Nachprüfung in dieser Runde bestätigt den Stand unverändert — die seitherigen Diffs betrafen nur `ListPage.razor`, `BudgetReport.razor`, `NavigationManagerExtensions.cs`, `theme.Dark.css` und Testdateien:

- [x] `BankAccountListViewModels.cs` — Navigieren-Tab nur bei `InitBack` (L136), `Back` L; Verwalten(10) `New` L; Filter(30) `ClearSearch` L mit Disabled-Bedingung
- [x] `BudgetReportViewModel.cs` — Navigieren-Tab nur bei `InitBack` (L329), `Back` L → `RaiseUiActionRequested("Back")`; Verwalten(10) `Settings` L; Analyse(40); Export(80)
- [x] Übrige 18 ViewModels (`ContactListViewModel`, `ContactCardViewModel`, `SavingsPlansListViewModel`, `SavingsPlanCardViewModel`, `SavingsPlanCategoryListViewModel`, `SavingsPlanCategoryCardViewModel`, `SecuritiesListViewModel`, `SecurityCardViewModel`, `SecurityCategoriesListViewModel`, `SecurityCategoryCardViewModel`, `BudgetPurposeListViewModel`, `BudgetPurposeCardViewModel`, `BudgetRuleListViewModel` (Sort 60), `BudgetRuleCardViewModel`, `UserListViewModel`, `UserCardViewModel`, `SetupCardViewModel`, `SetupBackupsViewModel`, `SetupUpdateViewModel`, `SetupProfileViewModel`, `SetupMcpViewModel`, `McpAccessListViewModel`, `McpAccessCardViewModel`) — unverändert auf dem in `review.1.md` verifizierten Zielstand (Gruppen, `Sort`-Konvention, `UiRibbonItemSize`, keine leeren Gruppen für `[→Issue]`-Aktionen)

### Favicon

- [x] `scripts/icons/generate_favicon.py` — Pillow-Generator vorhanden
- [x] `wwwroot/icons/finance_manager.svg` — SVG-Master (neu, unversioniert im Diff)
- [x] `finance_manager-64x64.ico` + `finance_manager.png` — regeneriert (beide `M` im Diff)
- [x] Einbindung `App.razor`/`manifest.json` (s. o.)

### Tests

- [x] `FinanceManager.Tests/ViewModels/RibbonConfigTests.cs` — Zielkonfiguration + konditionale Navigieren-Tabs (vorhanden, in Runde 1 verifiziert)
- [x] `RibbonTests.HiddenOnlyGroup_IsNotRendered` — Empty-Group-Filter abgesichert
- [x] Angepasste bUnit-Tests: `AccountsViewModelTests`, `SetupCardViewModelTests`, `UsersViewModelTests`, `ContactsViewModelTests` (alle `M` im Diff)
- [x] `FinanceManager.Tests/Web/NavigationManagerExtensionsTests.cs` (neu) — `IsSameOriginUri_ShouldAcceptRelativeAndSameOriginTargets` (Theory) + `IsSameOriginUri_ShouldRejectForeignOrUnsafeTargets` (Theory) (L19–50)
- [x] E2E neu: `AccountsBackNavigationPlaywrightTests`, `BudgetReportBackNavigationPlaywrightTests`, `ListPageContentPanelPlaywrightTests`, `SetupBackupTabPlaywrightTests`
- [x] E2E angepasst/erweitert: `BudgetReportDemoDataE2ETests` (URL-Predicate), `AccountsOverviewStatisticsPlaywrightTests` (Disabled/Enabled `#ClearSearch`), `RibbonGroupOrderPlaywrightTests` (5 neue Facts)
- [x] Build-Verifikation: `FinanceManager.Web`, `FinanceManager.Tests`, `FinanceManager.Tests.E2E` — je 0 Fehler (nur bekannte NU1510-/xUnit-Warnungen)

### GitHub-Issues (Akzeptanzszenario 9)

- [x] 16 geforderte Issues angelegt und via `gh issue list` verifiziert: A1–A6 = #593–#598, B1–B10 = #599–#608
- [x] Zusatz-Issue #609 (Budgetzweckregel-„Neu"-NRE) — über Plan hinaus, kein Konflikt

### Screenshot-Nachweise (Umsetzungsschritt 10, Teilaspekt)

- [x] Gezielte Shots für Sicherungs-Tab und Favicon: `scripts/screenshots/out-review/` mit 4 PNGs — `01-setup-backup-delete.png` + `02-setup-backup-delete-hover.png` (1440×900, Delete-Glyph Nicht-Hover/Hover), `03-favicon-svg.png` + `04-favicon-png.png` (512×512) — erzeugt via `scripts/screenshots/fm-review-shots.js` (Skript vorhanden, dokumentiert die Schuss-Intentionen im Header)

## Offene Aufgaben

- [x] `Umsetzungsschritt 10 — Docs/screenshots-Neugenerierung` — ausgeführt in Lifecycle-Schritt 10: `generate-screenshots.js` erfolgreich gelaufen, 9 PNGs in `Docs/screenshots` regeneriert (siehe `test-results.md`). Die „ergänzenden gezielten Shots" für Sicherungs-Tab/Favicon liegen in `scripts/screenshots/out-review/`; die Abnahme-Screenshots zusätzlich im SCM-Antwortkanal (attachmentsPath).

## Hinweise

- Die `Docs/screenshots`-Neugenerierung deckt sich terminologisch mit Lifecycle-Schritt 10 („Tests ausführen inkl. Sichtprüfung der Abnahme-Screenshots", `todo.md` dort noch unangehakt). Falls der Workflow die Regenerierung bewusst diesem späteren Schritt zuordnet, kann der Punkt als terminiert statt als Implementierungslücke gewertet werden — planseitig ist er jedoch in der Umsetzungsreihenfolge gelistet und damit hier als offen dokumentiert. Alternativ wäre eine Plan-Korrektur (`plan.md` L290–292) denkbar, wenn die Aktualisierung von `Docs/screenshots` nicht Teil dieses Tasks sein soll.
- Alle vier Code-Review-Befunde aus `review-code.1.md` sind im uncommitted Diff verifiziert geschlossen (s. Abschnitt oben).
- `dotnet build` (Web/Tests/Tests.E2E): 0 Fehler, nur Bestandswarnungen.
- `ContactListViewModel.Reload` trägt weiterhin `MobileShortcut = true` neben `New` — Beobachtung aus Runde 1, vom Plan nicht verboten.
