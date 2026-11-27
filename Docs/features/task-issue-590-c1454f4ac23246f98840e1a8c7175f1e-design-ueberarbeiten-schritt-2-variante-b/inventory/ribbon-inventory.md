# Ribbon-Inventar (Detaildokument zur Inventur)

Stand: Commit `67121fa8` (HEAD von `task/issue-590-…-variante-b`), 2026-11-27.
Erfasst wurde jeweils `GetRibbonRegisterDefinition(IStringLocalizer)` bzw. `GetRibbonRegisters(...)` der betroffenen ViewModels.

> **Veraltetes Referenzdokument:** Unter `Docs/features/task/issue-590-…-variante-b/ribbon-inventory.md`
> (Pfad mit `task/` statt `task-issue-…`) liegt eine automatisch erzeugte Bestandsaufnahme vom
> 2026-11-26 (Commit `440b2e2c`). Sie ist teilweise veraltet — u. a. fehlen dort die inzwischen
> vorhandenen Aktionen `RotateToken`/`Deactivate`/`Activate`/`Revoke` in `McpAccessCardViewModel`
> sowie `Activate`/`Deactivate`/`Unblock`/`SetPassword` in `UserCardViewModel`.

**Legende:** `L` = `UiRibbonItemSize.Large`, `S` = `UiRibbonItemSize.Small`, `MS` = `MobileShortcut = true`,
`dis:` = `Disabled`-Bedingung, `hid:` = `Hidden`-Bedingung. „→" beschreibt das Navigationsziel/Callback.

---

## 1. Kontenübersicht — `BankAccountListViewModels`

Datei: `FinanceManager.Web/ViewModels/Accounts/BankAccountListViewModels.cs` (`GetRibbonRegisterDefinition` ab L133)

- Ein Register, eine Gruppe `Ribbon_Group_Navigation`:
  - `Back` — S, MS → `list/home` o. ä.
  - `New` — S, MS
  - `ClearSearch` — S, MS, dis: `string.IsNullOrWhiteSpace(Search)`

**Auffällig:** „Zurück" ist **immer** sichtbar (kein `Hidden`, kein Navigationskontext). Alle drei
Aktionen sind `Small` (Ziel: `New` und `ClearSearch` groß, `Back` groß + kontextabhängig `Hidden`).
`AllowRangeFiltering = false` gesetzt.

## 2. Kontaktübersicht — `ContactListViewModel`

Datei: `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs` (ab L108)

- Gruppe `Ribbon_Group_Manage`:
  - `New` — L, MS
  - `Groups` — L → `list/contact-groups` o. ä.
  - `Reload` — L, MS
  - `ClearFilter` — L, MS, dis: `string.IsNullOrWhiteSpace(Search)`

**Auffällig:** Ziel verlangt zwei Gruppen („Verwalten": New+Groups groß; „Ansicht": Reload groß,
ClearFilter **klein**). Heute alles in einer Gruppe, `Reload`/`ClearFilter` groß.

## 3. Kontaktdetailsansicht — `ContactCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` (ab L253)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!HasPendingChanges`; `Delete` — S, dis: `Contact == null`
- `Ribbon_Group_Linked`: `OpenPostings` — S, dis: `Contact == null`; `OpenMerge` — S, dis: `Contact == null`;
  `Attachments` — S, dis: `Contact == null`

**Auffällig:** Ziel verlagert `OpenMerge` nach „Verwalten" (klein) und streicht es aus „Verknüpfte
Informationen" (dort nur `OpenPostings`/`Attachments`); „Zurücksetzen" existiert nicht (→ Issue).

## 4. Sparplanübersicht — `SavingsPlansListViewModel`

Datei: `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs` (ab L293)

- Register `Actions` → Gruppe `Ribbon_Group_Manage`: `New` — L, MS; `Categories` — S → Kategorienliste
- Register `Custom` → Gruppe `Ribbon_Group_Filter`: `ToggleActive` — S (Label wechselt
  `Ribbon_OnlyActive`/`Ribbon_StatusArchived`)

**Auffällig:** `Categories` klein (Ziel: groß); keine „Ansicht"-Gruppe mit `Reload`/`ClearSearch`
(→ Issues; die Liste hat Suche, diese filtert aber nicht — siehe Fehlerliste Punkt 1).

## 5. Sparplandetailansicht — `SavingsPlanCardViewModel`

Datei: `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` (ab L801;
Basis-Register werden am Ende angehängt, L897)

- `Ribbon_Group_Navigation`: `Back` — L
- `Ribbon_Group_Manage`: `Save` — L, dis: `!canSave`; `Archive` — S, dis: `!IsEdit`;
  `Delete` — S, dis: `!IsEdit`
- `Ribbon_Group_Analysis`: `Recalculate` — S, dis: `!IsEdit`
- `Ribbon_Group_Bookings`: `ActualReport` — S, dis: `!CanReportActualBalance`,
  Tooltip `ActualReport_Disabled_Linked`
- `Ribbon_Group_Linked`: `Category` — S, dis: `Model.CategoryId is null`; `OpenPostings` — S,
  dis: `!IsEdit`; `OpenAttachments` — S, dis: `!IsEdit`

**Auffällig:** Reihenfolge heute Navigation → Manage → Analysis → Bookings → Linked (Ziel:
Navigation → Manage → Analysis → Bookings → Linked — identisch, aber `Recalculate`/`ActualReport`
sollen **groß** werden). `Save` ohne `MS`; `Back` ohne `MS` — primäre Aktion je Ansicht braucht `MS`
(Projektregel). „Zurücksetzen" fehlt (→ Issue).

## 6. Sparplankategorieübersicht — `SavingsPlanCategoryListViewModel`

Datei: `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs` (ab L83)

- `Ribbon_Group_Navigation`: `Back` — L → `list/savings-plans`
- `Ribbon_Group_Manage`: `New` — L

**Auffällig:** Kein `Reload`, kein `ClearSearch` (→ Issues, falls Liste Suche unterstützt).
`Back` navigiert fest auf `list/savings-plans` — relevant für Fehlerliste Punkt 2 (dynamischer
Zurück-Button beim Navigation von Sparplandetail → Kategoriedetail).

## 7. Sparplankategoriedetailansicht — `SavingsPlanCategoryCardViewModel`

Datei: `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs` (ab L184)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!canSave`; `Delete` — S, dis: `Id == Guid.Empty`

**Auffällig:** Entspricht im Wesentlichen der Zielstruktur; „Zurücksetzen" fehlt (→ Issue).

## 8. Wertpapierübersicht — `SecuritiesListViewModel`

Datei: `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs` (ab L154)

- Register `Actions` mit **zwei Tabs**:
  - `Ribbon_Group_Actions`: `New` — L, MS; `Categories` — S
  - `Ribbon_Group_Reports`: `PortfolioAnalysisReport` — S → `portfolio/analysis-report`
- Register `Custom` → `Ribbon_Group_Filter`: `ToggleActive` — S

**Auffällig:** Aktionsgruppe heißt `Ribbon_Group_Actions` (Ziel: „Verwalten" =
`Ribbon_Group_Manage`); `Categories` und `PortfolioAnalysisReport` klein (Ziel: groß); keine
„Ansicht"-Gruppe mit `Reload`/`ClearSearch`/`ToggleActive` (Filter-Gruppe stattdessen separat).

## 9. Wertpapierdetailansicht — `SecurityCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs` (ab L355)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!canSave`; `Archive` — S, dis: `!(Id != Guid.Empty
  && Security != null && Security.IsActive)`; `Delete` — S, dis: `!(Id != Guid.Empty && Security !=
  null && !Security.IsActive)`
- `Ribbon_Group_Linked`: `Postings` — S, dis: `!(Id != Guid.Empty)`; `Prices` — S, dis: `!(Id !=
  Guid.Empty)`; `Performance` — S, dis: `!(Id != Guid.Empty)`; `Attachments` — S, dis: `!(Id !=
  Guid.Empty)`

**Auffällig:** Keine `Category`-Aktion (→ Issue „Kategorie als verknüpfte Information"). Ziel will
`Performance` als „Detaillierte Analyse" in eine eigene Gruppe „Berichte" (groß) umgruppieren.

## 10. Wertpapierkategorieübersicht — `SecurityCategoriesListViewModel`

Datei: `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoriesListViewModel.cs` (ab L114)

- `Ribbon_Group_Navigation`: `Back` — S, MS
- `Ribbon_Group_Manage`: `New` — S, MS

**Auffällig:** **Beide Aktionen `Small`** (Ziel: beide groß). `AllowSearchFiltering = false` und
`AllowRangeFiltering = false` — die Liste hat bewusst keine Suche/Datumsfilter; die Anforderung
verlangt hier auch keine „Ansicht"-Gruppe.

## 11. Wertpapierkategoriedetailansicht — `SecurityCategoryCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs` (ab L198)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!canSave`; `Delete` — S, dis: `Id == Guid.Empty`

**Auffällig:** „Zurücksetzen" fehlt (→ Issue).

## 12. Budgetübersicht — `BudgetPurposeListViewModel`

Datei: `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs` (ab L136)

- Ein Register `Actions` mit drei Tabs:
  - `Ribbon_Group_Navigation`: `New` — L, MS; `Reload` — S, MS; `Categories` — S →
    `OpenBudgetCategories`; `ClearFilter` — S, MS, dis: `string.IsNullOrWhiteSpace(Search)`
  - `Ribbon_Group_Analysis`: `PrevMonth` — S; `ThisMonth` — S; `PrevYear` — S; `ThisYear` — S
  - `Ribbon_Group_Reports`: `BudgetReport` — S → `OpenBudgetReport` (`reports/budget`)

**Auffällig:** Navigation-Gruppe enthält Manage- und Ansichts-Aktionen gemischt (Ziel: `New`+
`Categories` in „Verwalten", `Reload`/`ClearFilter` in „Ansicht"); `Categories` klein (Ziel: groß),
`BudgetReport` klein (Ziel: groß). Semantik der Analyse-Buttons weicht vom Budgetbericht ab
(Fehlerliste Punkt 5).

## 13. Budgetzweckdetailansicht — `BudgetPurposeCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs` (ab L451)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!HasPendingChanges`; `Delete` — S
- `Ribbon_Group_Linked`: `Category` — S (tag-Icon), dis: kein `BudgetCategoryId`; navigiert mit
  `?back=` auf die Kartenseite
- Kind-VM `BudgetRuleListViewModel` (eingebettet via `CreateSubViewModel`) trägt zusätzlich bei:
  `Ribbon_Group_Rules`: `NewRule` — L (s. `ViewModels/Budget/BudgetRuleListViewModel.cs` L137)

**Auffällig:** Die Ziel-Gruppe „Regeln → Neu" **existiert bereits** über die
Kind-VM-Aggregation (anders als im Requirement vermutet — `NewRule` wird angezeigt, solange das
eingebettete Regel-VM aktiv ist; Prüfung von `IsChildViewModelActive` nötig). „Zurücksetzen" fehlt
(→ Issue).

## 14. Budgetzweckregeldetailansicht — `BudgetRuleCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` (ab L362)

- `Ribbon_Group_Navigation`: `Back` — L (berücksichtigt `InitBack`/`BudgetPurposeId`)
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `!HasPendingChanges`; `Delete` — S
- `Ribbon_Group_Fields`: `ClearPurposePattern` — S, dis: `Rule?.PurposePattern == null`

**Auffällig:** `Back` erzeugt bei Navigation über die Budgetzweckdetailansicht eine ungültige
Route (`/list/budget/card/budget/purposes/{id}`) — Fehlerliste Punkt 4 (→ Issue). „Zurücksetzen"
fehlt (→ Issue).

## 15. Budgetbericht — `BudgetReportViewModel`

Datei: `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs` (ab L325; Basisklasse
`BaseViewModel`, kein List/Card-VM)

- `Ribbon_Group_Manage`: `Settings` — S → `ShowSettings`
- `Ribbon_Group_Analysis`: `PrevMonth` — S; `ThisMonth` — S; `NextMonth` — S; `PrevYear` — S;
  `NextYear` — S
- `Ribbon_Group_Export`: `ExportExcel` — S → `ExportExcel`

**Auffällig:** Keine `Back`-Aktion (Ziel: konditional sichtbar bei Navigation von der
Budgetübersicht — `?back=` existiert nur auf `CardPage`, nicht auf der Berichtsseite;
Erkennungsmechanismus = offene Frage 3). `Settings` klein (Ziel: groß). Analyse-Buttons enthalten
`NextMonth`/`NextYear` (Vorwärts-Navigation) — die Budgetübersicht hat nur Rückwärts+Aktuell
(Fehlerliste Punkt 5).

## 16. Benutzerübersicht — `UserListViewModel`

Datei: `FinanceManager.Web/ViewModels/Setup/UserListViewModel.cs` (ab L109)

- `Ribbon_Group_Manage`: `New` — L

**Auffällig:** Kein `Reload`, kein `ClearSearch` (→ Issues).

## 17. Benutzerdetailansicht — `UserCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Setup/UserCardViewModel.cs` (ab L362)

- `Ribbon_Group_Navigation`: `Back` — L, MS
- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `User != null && User.Id != Guid.Empty`
  (nur für neue Benutzer aktiviert!); `Delete` — S, dis: `!(UserId != Guid.Empty && User != null)`;
  konditional `Deactivate`/`Activate` — S (nur Nicht-Admin); `Unblock` — S (nur wenn gesperrt);
  `SetPassword` — S → `SetPasswordOverlay`

**Auffällig:** `SetPassword` existiert bereits (Ziel-„Passwort zurücksetzen" → vermutlich nur
Label-/Sichtbarkeitsangleichung oder Re-Use). `Activate`/`Deactivate` existieren bereits (Ziel
verlangt sie nicht explizit). „Zurücksetzen" (Discard-Changes) fehlt (→ Issue).

## 18. Einstellungen — `SetupCardViewModel` + Sektions-VMs

Datei: `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs` (ab L463)

- `Ribbon_Group_Manage`: `Save` — L, MS, dis: `Saving || !HasPendingChanges`; `Reset` — S, MS, dis:
  `Saving || !HasPendingChanges`; `RebuildAggregates` — S, Tooltip `Hint_RebuildAggregates` →
  `ApiClient.Aggregates_RebuildAsync`; `ResetReportCache` — S, Tooltip `Hint_ResetReportCache`
- Danach Sektions-VMs als Kind-VMs (`SectionDefinitions`: profile, notifications, statements,
  attachments, backup, update, mcp, …; `IsChildViewModelActive` unterdrückt `SetupMcpViewModel`
  ohne MCP-Berechtigung)

### `SetupBackupsViewModel` (`ViewModels/Setup/SetupBackupsViewModel.cs` L239)

- Gruppen-Titel `SetupBackup_Titel` („Datensicherungen" — **kein** `Ribbon_Group_*`-Key),
  `Sort = int.MaxValue` (ans Ende): `CreateBackup` — L; `UploadBackup` — L (FileCallback)

### `SetupUpdateViewModel` (`ViewModels/Setup/SetupUpdateViewModel.cs` L357)

- Gruppen-Titel `Setup_Section_Update` („Aktualisierungen" — **kein** `Ribbon_Group_*`-Key),
  `Sort = 50`: `UpdateCheckNow` — S; `UpdateInstall` — S; `UpdateResetLock` — S (Tooltips
  `Hint_SetupUpdate_*`, alle in `Pages.*.resx` vorhanden)

### `SetupProfileViewModel` (`ViewModels/Setup/SetupProfileViewModel.cs` L255)

- Gruppen-Titel `SetupProfile_Title` (kein `Ribbon_Group_*`-Key): `DetectTimezone` — S

### `SetupMcpViewModel` (`ViewModels/Setup/SetupMcpViewModel.cs` L199)

- Gruppe `Ribbon_Group_Mcp`: `McpApprovals` — L (mit Badge-Zähler); `McpAudit` — S;
  `McpSecurity` — S, **hid: `!IsAdmin`** (einziger vorhandener `Hidden`-Präzedenzfall)

**Auffällig (Ziel):** `RebuildAggregates`/`ResetReportCache` sollen aus „Verwalten" in eine neue
Gruppe „Daten"; `Setup_Section_Update` → „Programmversion" umbenennen (Gruppe + Setup-Tab);
`SetupBackup_Titel` → „Sicherungen"; `CreateBackup`/`UploadBackup` groß→**klein**;
`McpApprovals`/`McpAudit`/`McpSecurity` groß/klein→alle **klein**.

## 19. KI-Zugänge-Übersicht — `McpAccessListViewModel`

Datei: `FinanceManager.Web/ViewModels/Mcp/McpAccessListViewModel.cs` (ab L168)

- `Ribbon_Group_Manage`: `New` — L, MS (→ Wizard); `Approvals` — S; `AuditLog` — S;
  `Security` — S, dis: `!IsAdmin`; `Reload` — S

**Auffällig:** Ziel: „Verwalten" nur `New`; „Ansicht" mit `Reload` (groß) + `ClearSearch` (klein,
fehlt → Issue); „Verknüpfte Informationen" mit `Approvals`/`AuditLog`/`Security`. `New` bei
deaktivierter KI-Integration nicht sperrbar — Fehlerliste Punkt 7 (→ Issue).

## 20. KI-Zugang-Detailansicht — `McpAccessCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` (ab L734)

- `Ribbon_Group_Navigation`: `Back` — L, MS → `card/setup`
- `Ribbon_Group_Manage`: `Save` — L, MS; konditional `RotateToken` — S, `Deactivate` — S,
  `Activate` — S, `Revoke` — S (zustandsabhängig)

**Auffällig:** `Deactivate` und `Revoke` **existieren bereits** (im Requirement als „neu → Issue"
deklariert — ist tatsächlich vorhanden, ggf. nur Anzeige-/Aktivierungsbedingungen anzupassen).
„Zurücksetzen" fehlt (→ Issue).

## Verwandte, nicht im Scope genannte Ansichten (Referenz)

Aus dem älteren Inventar (Stand `440b2e2c`) und flüchtiger Prüfung — diese bleiben laut Requirement
unverändert: `BankAccountCardViewModel` (Navigation/Bookings/Linked inkl. `Reset`!),
`BudgetCategoryListViewModel`/`BudgetCategoryCardViewModel`, `ContactGroupList/CardViewModel`,
`HomeViewModel`, `McpAuditViewModel`/`McpApprovalCenterViewModel`/`McpSecurityOverviewViewModel`
(jeweils `Back` L MS → `card/setup` + `Reload` S — betroffen von Fehlerliste Punkten 8–10:
`Back` erzeugt ungültige Route `/list/mcp-audit/card/setup`),
`PortfolioAnalysisReportPageViewModel`, `BasePostingsListViewModel`, `PostingsCardViewModel`,
`ReportDashboardViewModel` (eigene resx-Keys `Ribbon_Navigation`/`Ribbon_ReportActions`/`Ribbon_Filter`),
`ReportsHomeViewModel`, `SecurityPricesListViewModel`, `SecurityPerformancePageViewModel`,
Statement-Draft-VMs (3 Stück, umfangreich).

> **Beobachtung:** `BankAccountCardViewModel` besitzt bereits eine `Reset`-Aktion (S, dis:
> `!HasPendingChanges`) — Vorlage für die in den Zielansichten geforderten „Zurücksetzen"-Aktionen.

## Aggregations- und Sortiermechanik (relevant für Gruppenreihenfolge)

- `IRibbonProvider.GetRibbonRegisters` (Basis: `ViewModels/Common/BaseViewModel.cs` L675;
  zweite parallele Hierarchie `ViewModels/ViewModelBase.cs`) liefert eigene
  `GetRibbonRegisterDefinition` + Register aktiver Kind-VMs (`IsChildViewModelActive`).
- `RibbonExtensions.ToUiRibbonGroups` flacht Register → `UiRibbonTab` → `UiRibbonGroup` ab
  (Legacy-Pfad; `Hidden`/`Sort` werden dort nicht ausgewertet).
- `Components/Shared/Ribbon.razor` wertet beides selbst aus: `UiRibbonAction.Hidden` blendet die
  Aktion beim Gruppenaufbau aus (L196), `UiRibbonTab.Sort` fließt in `RibbonGroup.Sort` und ein
  stabiles `OrderBy(g => g.Sort)` (L188/L251) — gleiche `Sort`-Werte behalten die
  Tab-Listenreihenfolge (abgesichert durch `Groups_RenderInTabListOrder`).
- `Ribbon.razor` setzt die Button-`id` auf `UiRibbonAction.Id` (E2E-Selektoren `button#Save` etc.
  hängen daran); `MobileShortcut` steuert die mobile Darstellung.
