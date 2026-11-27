# Inventur — Menüband-Aktionen: Standardgröße/Reihenfolge je Ansicht + Korrekturen (Issue #590, Schritt 2, Variante B)

- **Stand:** Commit `67121fa8` (`fix(issue-590): einzeiliges Menueband mit Popover-Einklappung (Variante B)`), Branch
  `task/issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten-schritt-2-variante-b`, 2026-11-27
- **Requirement:** `requirement.md` (+ Quelle `requirement-source.txt`)
- **Detaildokumente:** `inventory/ribbon-inventory.md` (je ViewModel), `inventory/ui-components.md`
  (Layout/CSS/Favicon/resx), `inventory/tests.md` (Testinventar + Baseline-Läufe)

## 1. Architektur-Befund (Ist)

- Ribbon-Modelle: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` —
  `UiRibbonItemSize{Small,Large}`, `UiRibbonRegisterKind{QuickAccess,Actions,LinkedInfo,Reports,Custom}`,
  `UiRibbonAction` (u. a. `Id`, `Size`, `Disabled`, `Hidden`, `MobileShortcut`, `Tooltip`,
  `FileCallback`), `UiRibbonTab` (`Title`, `Items`, `Sort`), `UiRibbonRegister` (`Kind`, `Tabs`).
- Provider: `IRibbonProvider.GetRibbonRegisters`, implementiert in **zwei parallelen
  Basishierarchien** — `ViewModels/Common/BaseViewModel.cs` (L675; inkl. Kind-VM-Aggregation über
  `IsChildViewModelActive`) und `ViewModels/ViewModelBase.cs`. Alle betroffenen VMs nutzen die
  `Common`-Hierarchie (`BaseViewModel`/`BaseListViewModel<T>`/`BaseCardViewModel<TKeyValue>`).
- Rendering: `Components/Shared/Ribbon.razor` — Button-`id` = `UiRibbonAction.Id` (E2E-Selektoren!),
  `Hidden` wird beim Gruppenaufbau ausgewertet (L196), `UiRibbonTab.Sort` → stabile Gruppensortierung
  (L188/L251). `RibbonExtensions.ToUiRibbonGroups` ist der Legacy-Flatten-Pfad.
- Lokalisierung: `IStringLocalizer<Pages>` (`BaseViewModel` L166) — Keys in
  `Resources/Pages.*.resx` (de/en + neutral).
- Navigation mit Kontext: `?back=`-Parameter existiert **nur** auf `CardPage.razor` (L87);
  `BaseViewModel` L357 baut `?back=` in URLs ein. Listen-/Berichtsseiten kennen `?back=` nicht —
  relevant für konditionale „Zurück"-Buttons (offene Fragen 2/3).

## 2. Ribbon-Ist vs. Ziel je Ansicht (Kurzfassung)

Vollständige Aktionslisten mit Zeilenreferenzen: `inventory/ribbon-inventory.md`.
`[MS]` = MobileShortcut vorhanden. „Δ" markiert die wesentliche Abweichung vom Ziel.

| Ansicht / ViewModel | Ist (Gruppen → Aktionen, Größe) | Wesentliche Δ zum Ziel |
| --- | --- | --- |
| Kontenübersicht `BankAccountListViewModels` | Navigation: Back[S][MS], New[S][MS], ClearSearch[S][MS] | Back immer sichtbar statt `Hidden`-gesteuert; New/ClearSearch S→L; Neuaufteilung in Navigation/Verwalten/Filter |
| Kontaktübersicht `ContactListViewModel` | Manage: New[L][MS], Groups[L], Reload[L][MS], ClearFilter[L][MS] | Reload/ClearFilter in neue Gruppe „Ansicht" (`Ribbon_Group_View` fehlt); ClearFilter L→S |
| Kontaktdetail `ContactCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Delete[S]; Linked: OpenPostings[S], OpenMerge[S], Attachments[S] | OpenMerge → Manage; „Zurücksetzen" fehlt (Issue) |
| Sparplanübersicht `SavingsPlansListViewModel` | Manage: New[L][MS], Categories[S]; Filter: ToggleActive[S] | Categories S→L; „Ansicht" (Reload/ClearSearch/ToggleActive) fehlt/teils Issue |
| Sparplandetail `SavingsPlanCardViewModel` | Navigation: Back[L]; Manage: Save[L], Archive[S], Delete[S]; Analysis: Recalculate[S]; Bookings: ActualReport[S]; Linked: Category/OpenPostings/OpenAttachments[S] | Recalculate/ActualReport S→L; Reset fehlt (Issue); MS auf primärer Aktion fehlt |
| Sparplankategorie-Übersicht `SavingsPlanCategoryListViewModel` | Navigation: Back[L] → `list/savings-plans`; Manage: New[L] | „Ansicht" (Reload/ClearSearch) fehlt (Issue); Back ist statisch (Fehlerliste #2) |
| Sparplankategorie-Detail `SavingsPlanCategoryCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Delete[S] | Reset fehlt (Issue) |
| Wertpapierübersicht `SecuritiesListViewModel` | Actions: New[L][MS], Categories[S]; Reports: PortfolioAnalysisReport[S]; Filter: ToggleActive[S] | Actions→„Verwalten"; Categories/PortfolioAnalysisReport S→L; „Ansicht" (Reload/ClearSearch/ToggleActive) fehlt |
| Wertpapierdetail `SecurityCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Archive[S], Delete[S]; Linked: Postings/Prices/Performance/Attachments[S] | `Category` fehlt (Issue); Performance → neue Gruppe „Berichte" (L); Reset fehlt (Issue) |
| Wertpapierkategorie-Übersicht `SecurityCategoriesListViewModel` | Navigation: Back[S][MS]; Manage: New[S][MS] | **beide S→L** |
| Wertpapierkategorie-Detail `SecurityCategoryCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Delete[S] | Reset fehlt (Issue) |
| Budgetübersicht `BudgetPurposeListViewModel` | Navigation: New[L][MS], Reload[S][MS], Categories[S], ClearFilter[S][MS]; Analysis: PrevMonth/ThisMonth/PrevYear/ThisYear[S]; Reports: BudgetReport[S] | Navigation-Gruppe splitten (New+Categories→Verwalten, Reload+ClearFilter→Ansicht); Categories/BudgetReport S→L |
| Budgetzweckdetail `BudgetPurposeCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Delete[S]; Linked: Category[S]; via Kind-VM: Rules: NewRule[L] | „Regeln→Neu" **existiert bereits** (Kind-VM `BudgetRuleListViewModel`) — Issue evtl. überflüssig/anders; Reset fehlt |
| Budgetzweckregel-Detail `BudgetRuleCardViewModel` | Navigation: Back[L]; Manage: Save[L][MS], Delete[S]; Fields: ClearPurposePattern[S] | Back-Route defekt (Fehlerliste #4 → Issue); Reset fehlt |
| Budgetbericht `BudgetReportViewModel` | Manage: Settings[S]; Analysis: PrevMonth/ThisMonth/NextMonth/PrevYear/NextYear[S]; Export: ExportExcel[S] | Back-Gruppe fehlt (konditional, `Hidden`); Settings S→L |
| Benutzerübersicht `UserListViewModel` | Manage: New[L] | „Ansicht" (Reload/ClearSearch) fehlt (Issue) |
| Benutzerdetail `UserCardViewModel` | Navigation: Back[L][MS]; Manage: Save[L][MS], Delete[S], +konditional Activate/Deactivate[S], Unblock[S], SetPassword[S] | `SetPassword` existiert bereits („Passwort zurücksetzen" ggf. nur umbenennen); Reset fehlt (Issue) |
| KI-Zugänge-Übersicht `McpAccessListViewModel` | Manage: New[L][MS], Approvals[S], AuditLog[S], Security[S], Reload[S] | Aufteilung in Verwalten/Ansicht/Linked; ClearSearch fehlt (Issue); `New` bei deaktivierter KI nicht gesperrt (Fehlerliste #7) |
| KI-Zugang-Detail `McpAccessCardViewModel` | Navigation: Back[L][MS]→`card/setup`; Manage: Save[L][MS] + konditional RotateToken/Deactivate/Activate/Revoke[S] | **Deactivate/Revoke existieren bereits** (Requirement sagt „neu" — Klarstellung); Reset fehlt |
| Einstellungen `SetupCardViewModel` (+Sektions-VMs) | Manage: Save[L][MS], Reset[S][MS], RebuildAggregates[S], ResetReportCache[S]; Profil-Gruppe: DetectTimezone[S]; KI-Gruppe: McpApprovals[L], McpAudit[S], McpSecurity[S,hid:!IsAdmin]; Update-Gruppe (`Setup_Section_Update`, Sort=50): UpdateCheckNow/UpdateInstall/UpdateResetLock[S]; Backup-Gruppe (`SetupBackup_Titel`, Sort=Max): CreateBackup[L], UploadBackup[L] | RebuildAggregates/ResetReportCache → neue Gruppe „Daten"; „Aktualisierungen"→„Programmversion" (Gruppe+Tab); „Datensicherungen"→„Sicherungen"; Create/UploadBackup L→S; MCP-Aktionen alle S; Tooltip `Hint_RebuildAggregates` unübersetzt |

## 3. Korrekturen (Ist-Befund, Details in `inventory/ui-components.md`)

1. **Inhaltspanel Übersichtsseiten:** `ListPage.razor` rendert `Ribbon`+`GenericListPage` ohne
   `.card-page-content`-Wrapper (CardPage.razor L35 hat ihn) → Fix = gleicher Wrapper.
2. **`.icon-btn.danger` unleserlich:** `button.danger` (`theme.Dark.css` L140, roter Hintergrund)
   schlägt `.icon-btn`-Hintergrund; `.icon-btn.danger` (L186) setzt rote Textfarbe → rot auf rot.
   Betroffene Buttons: `SetupBackupTab.razor` L58 + L105.
3. **Favicon:** `App.razor` L8 → `icons/finance_manager-64x64.ico` (altes Symbol); neues Icon +
   Einbindung (ggf. `manifest.json`, verweist auf `finance_manager.png`).

## 4. resx-Lücken (verifiziert)

- **`Ribbon_Group_View` fehlt** in allen drei `Pages.*.resx` → für die neue „Ansicht"-Gruppe anzulegen.
- **`Hint_RebuildAggregates` fehlt im `Pages`-Localizer** (nur in `Components/Pages/Setup.*.resx`)
  → Tooltip zeigt Roh-Key; bestätigtes Übersetzungsproblem.
- `Setup_Section_Update` = „Aktualisierungen"/„Updates", `SetupBackup_Titel` = „Datensicherungen" —
  Werte umbenennen (beide sind zugleich Gruppen-Titel; Setup-Tab-Label offene Frage 5).
- Alle übrigen benötigten Keys (`Ribbon_Group_Manage`, `_Linked`, `_Analysis`, `_Reports`,
  `_Actions`, `_Filter`, `_Export`, `_Bookings`, `_Rules`, `_Fields`, `_Mcp`, `Ribbon_Back/-New/-Save/
  -Delete/-Reload/-Reset/-Archive/-Category` u. a.) existieren in de+en+neutral.

## 5. Tests (Details + Läufe in `inventory/tests.md`)

- bUnit `RibbonTests.cs` (~18 Facts, u. a. `Groups_RenderInTabListOrder`);
  `EnumCoverageTests` deckt beide Ribbon-Enums ab (keine neuen Member nötig).
- E2E nutzt `button#<ActionId>`-Selektoren (`#Save`, `#New`, `#ClearSearch`, `#Back`, `#BudgetReport`,
  `#PrevMonth`, `#UpdateCheckNow`, `#Reload`, `#OpenContact` u. a.) — IDs bleiben bei Umgruppierung
  stabil, aber Sichtbarkeits-Assertions können brechen (`RibbonGroupOrderPlaywrightTests`,
  `AccountsOverviewStatisticsPlaywrightTests`).
- **Baseline:** Unit **2511/2511 grün** (`test-results-unit-baseline.trx`), Integration
  **180/180 grün** (`test-results-integration-baseline.trx`); E2E lief zum Inventur-Zeitpunkt
  noch (TRX folgt automatisch; ein bereits vorhandener, ribbon-unabhängiger Fehler
  `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat…` wurde beobachtet).

## 6. Klarstellungen/Risiken aus dem Ist-Stand

- **Bereits vorhanden, im Requirement als „fehlt" deklariert:** `NewRule` in Budgetzweckdetail
  (Kind-VM), `SetPassword` in Benutzerdetail, `Deactivate`/`Revoke` (sowie `Activate`,
  `RotateToken`) in KI-Zugang-Detail. Vor Issue-Anlage prüfen, ob nur Bedingungen/Labels fehlen.
- **`Reset` existiert bereits** in `BankAccountCardViewModel` (S, `!HasPendingChanges`) — Vorlage
  für die geforderten „Zurücksetzen"-Aktionen.
- **Kontext-„Zurück":** `?back=` nur auf CardPage; für Kontenübersicht/Budgetbericht ist ein eigener
  Mechanismus nötig (offene Fragen 2/3).
- **Suche/Filter:** `SecurityCategoriesListViewModel` deaktiviert Suche+Datum bewusst
  (`AllowSearchFiltering/AllowRangeFiltering=false`); bei `BankAccountListViewModels` ist
  `AllowRangeFiltering=false`. Anforderung verlangt `ClearSearch` nur „nur aktiviert bei
  Suchbegriff" — VMs ohne Suche brauchen keine Ansichts-Gruppe dafür.
- Veraltetes Referenz-Inventar unter `Docs/features/task/issue-590-…/ribbon-inventory.md` (Stand
  `440b2e2c`) — nicht als Quelle verwenden.
