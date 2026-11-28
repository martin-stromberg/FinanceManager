<!-- Automatisch extrahiert aus den ViewModels (IRibbonProvider.GetRibbonRegisters).
     Stand: 440b2e2c, 2026-11-26 -->

# Menüband-Inventar (Bestandsaufnahme für Designplanung)

## BankAccountCardViewModel  (`ViewModels/Accounts/BankAccountCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 3 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Reset` — Reset (Small) — disabled: `!HasPendingChanges`
  - `Delete` — Delete (Small) — disabled: `isNew`
- **Gruppe:** Bookings (`Ribbon_Group_Bookings`) — 3 Aktion(en)
  - `CreatePreliminaryDraft` — Create preliminary statement (Large) — disabled: `isNew`
  - `ReevaluatePostingTypes` — Re-evaluate posting types (Small) — disabled: `isNew`
  - `OpenPostingRules` — Posting rules (Small) — disabled: `isNew`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 1 Aktion(en)
  - `OpenPostings` — Postings (Small) — disabled: `Account == null` [MobileShortcut]

## BankAccountListViewModels  (`ViewModels/Accounts/BankAccountListViewModels.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 3 Aktion(en)
  - `Back` — Back (Small)
  - `New` — New (Small)
  - `ClearSearch` — Clear search (Small)

## BudgetCategoryCardViewModel  (`ViewModels/Budget/BudgetCategoryCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Delete` — Delete (Small) — disabled: `Id == Guid.Empty`

## BudgetCategoryListViewModel  (`ViewModels/Budget/BudgetCategoryListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `New` — New (Large)

## BudgetPurposeCardViewModel  (`ViewModels/Budget/BudgetPurposeCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Delete` — Delete (Small) — disabled: `Id == Guid.Empty`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 1 Aktion(en)
  - `Category` — Category (Small) — disabled: `Purpose?.BudgetCategoryId == null || Purpose?.BudgetCategoryId == Guid.Empty`

## BudgetPurposeListViewModel  (`ViewModels/Budget/BudgetPurposeListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 4 Aktion(en)
  - `New` — New (Large)
  - `Reload` — Reload (Small)
  - `Categories` — Categories (Small)
  - `ClearFilter` — Clear search (Small) — disabled: `string.IsNullOrWhiteSpace(Search)`
- **Gruppe:** Analysis (`Ribbon_Group_Analysis`) — 4 Aktion(en)
  - `PrevMonth` — Previous month (Small)
  - `ThisMonth` — This month (Small)
  - `PrevYear` — Previous year (Small)
  - `ThisYear` — This year (Small)
- **Gruppe:** Reports (`Ribbon_Group_Reports`) — 1 Aktion(en)
  - `BudgetReport` — Report (Small)

## BudgetReportViewModel  (`ViewModels/Budget/BudgetReportViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `Settings` — Settings (Small)
- **Gruppe:** Analysis (`Ribbon_Group_Analysis`) — 5 Aktion(en)
  - `PrevMonth` — Previous month (Small)
  - `ThisMonth` — This month (Small)
  - `NextMonth` — Next month (Small)
  - `PrevYear` — Previous year (Small)
  - `NextYear` — Next year (Small)
- **Gruppe:** Export (`Ribbon_Group_Export`) — 1 Aktion(en)
  - `ExportExcel` — Export Excel (Small)

## BudgetRuleCardViewModel  (`ViewModels/Budget/BudgetRuleCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Delete` — Delete (Small) — disabled: `Id == Guid.Empty`
- **Gruppe:** Fields (`Ribbon_Group_Fields`) — 1 Aktion(en)
  - `ClearPurposePattern` — Clear purpose pattern (Small) — disabled: `Rule?.PurposePattern == null`

## BudgetRuleListViewModel  (`ViewModels/Budget/BudgetRuleListViewModel.cs`)

- **Gruppe:** Rules (`Ribbon_Group_Rules`) — 1 Aktion(en)
  - `NewRule` — New (Large)

## ContactCardViewModel  (`ViewModels/Contacts/ContactCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Delete` — Delete (Small) — disabled: `Contact==null`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 3 Aktion(en)
  - `OpenPostings` — Postings (Small) — disabled: `Contact==null`
  - `OpenMerge` — Merge (Small) — disabled: `Contact==null`
  - `Attachments` — Attachments (Small) — disabled: `Contact==null`

## ContactListViewModel  (`ViewModels/Contacts/ContactListViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 4 Aktion(en)
  - `New` — New (Large)
  - `Groups` — Groups (Large)
  - `Reload` — Reload (Large)
  - `ClearFilter` — Clear search (Large) — disabled: `string.IsNullOrWhiteSpace(Search)`

## ContactGroupCardViewModel  (`ViewModels/Contacts/Groups/ContactGroupCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!canSave`
  - `Delete` — Delete (Small) — disabled: `Id == Guid.Empty`

## ContactGroupListViewModel  (`ViewModels/Contacts/Groups/ContactGroupListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 2 Aktion(en)
  - `New` — New (Large)
  - `Back` — Back (Small)

## HomeViewModel  (`ViewModels/Home/HomeViewModel.cs`)

- **Gruppe:** Import (`Ribbon_Group_Import`) — 1 Aktion(en)
  - `Import` — Import (Large)
- **Gruppe:** KPI (`Ribbon_Group_KPI`) — 1 Aktion(en)
  - `ToggleKpi` — Done (Large)

## McpAccessCardViewModel  (`ViewModels/Mcp/McpAccessCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `Save` — Save (Large) — disabled: `Access is null`

## McpAccessListViewModel  (`ViewModels/Mcp/McpAccessListViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 5 Aktion(en)
  - `New` — New (Large)
  - `Approvals` — Approvals (Small)
  - `AuditLog` — Audit log (Small)
  - `Security` — AI security (Small) — disabled: `!(ServiceProvider.GetService(typeof(FinanceManager.Application.ICurrentUserService)) is FinanceManager.Application.ICurrentUserService cu && cu.IsAdmin)`
  - `Reload` — Reload (Small)

## McpApprovalCenterViewModel  (`ViewModels/Mcp/McpApprovalCenterViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `Reload` — Reload (Small)

## McpAuditViewModel  (`ViewModels/Mcp/McpAuditViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `Reload` — Reload (Small)

## McpSecurityOverviewViewModel  (`ViewModels/Mcp/McpSecurityOverviewViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `Reload` — Reload (Small)

## PortfolioAnalysisReportPageViewModel  (`ViewModels/Portfolio/PortfolioAnalysisReportPageViewModel.cs`)

- **Gruppe:** Actions (`Ribbon_Group_Actions`) — 2 Aktion(en)
  - `Back` — Back (Large)
  - `Refresh` — Refresh (Large)

## BasePostingsListViewModel  (`ViewModels/Postings/Common/BasePostingsListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Export (`Ribbon_Group_Export`) — 2 Aktion(en)
  - `ExportCsv` — Export CSV (Small) — disabled: `Loading`
  - `ExportXlsx` — Export Excel (Small) — disabled: `Loading`

## PostingsCardViewModel  (`ViewModels/Postings/Common/PostingsCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 5 Aktion(en)
  - `OpenAccount` — Account (Small) — disabled: `Posting?.AccountId == null`
  - `OpenContact` — Contact (Small) — disabled: `Posting?.ContactId == null`
  - `OpenSavingsPlan` — Savings plan (Small) — disabled: `Posting?.SavingsPlanId == null`
  - `OpenSecurity` — Security (Small) — disabled: `Posting?.SecurityId == null`
  - `OpenAttachments` — Attachments (Small) — disabled: `Posting == null`
- **Gruppe:** Actions (`Ribbon_Group_Actions`) — 1 Aktion(en)
  - `Reverse` — Reverse (Large) — disabled: `actionsDisabled`

## ReportDashboardViewModel  (`ViewModels/Reports/ReportDashboardViewModel.cs`)

- **Gruppe:** Ribbon_Navigation (`Ribbon_Navigation`) — 1 Aktion(en)
  - `Back` — Ribbon_BackToOverview (Large)
- **Gruppe:** Ribbon_ReportActions (`Ribbon_ReportActions`) — 5 Aktion(en)
  - `ToggleEdit` — Ribbon_View (Large)
  - `Save` — Save (Small) — disabled: `!EditMode && !FiltersDirty`
  - `SaveAs` — Ribbon_SaveAs (Small) — disabled: `!EditMode && !FiltersDirty`
  - `DeleteFavorite` — Ribbon_DeleteFavorite (Small) — disabled: `!ActiveFavoriteId.HasValue`
  - `Reload` — Ribbon_ReloadData (Small)
- **Gruppe:** Ribbon_Filter (`Ribbon_Filter`) — 2 Aktion(en)
  - `FiltersOpen` — Ribbon_OpenFilters (Small) — disabled: `FilterOptionsLoading`
  - `FiltersClear` — Ribbon_ClearFilters (Small) — disabled: `GetSelectedFiltersCount()==0`

## ReportsHomeViewModel  (`ViewModels/Reports/ReportsHomeViewModel.cs`)

- **Gruppe:** Actions (`Ribbon_Group_Actions`) — 2 Aktion(en)
  - `Reload` — Reload (Small) — disabled: `Loading`
  - `NewReport` — Ribbon_NewReport (Large)

## SavingsPlanCategoryCardViewModel  (`ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!canSave`
  - `Delete` — Delete (Small) — disabled: `Id==Guid.Empty`

## SavingsPlanCategoryListViewModel  (`ViewModels/SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `New` — New (Large)

## SavingsPlanCardViewModel  (`ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 3 Aktion(en)
  - `Save` — Save (Large) — disabled: `!canSave`
  - `Archive` — Archive (Small) — disabled: `!IsEdit`
  - `Delete` — Delete (Small) — disabled: `!IsEdit`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 3 Aktion(en)
  - `Category` — Category (Small) — disabled: `Model.CategoryId is null`
  - `OpenPostings` — Postings (Small) — disabled: `!IsEdit`
  - `OpenAttachments` — Attachments (Small) — disabled: `!IsEdit`
- **Gruppe:** Analysis (`Ribbon_Group_Analysis`) — 1 Aktion(en)
  - `Recalculate` — Recalculate (Small) — disabled: `!IsEdit`
- **Gruppe:** Bookings (`Ribbon_Group_Bookings`) — 1 Aktion(en)
  - `ActualReport` — Actual-balance report (Small) — disabled: `!CanReportActualBalance`

## SavingsPlansListViewModel  (`ViewModels/SavingsPlans/SavingsPlansListViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `New` — New (Large)
  - `Categories` — Categories (Small)
- **Gruppe:** Filter (`Ribbon_Group_Filter`) — 1 Aktion(en)
  - `ToggleActive` — Only active (Small)

## SecurityCategoriesListViewModel  (`ViewModels/Securities/Categories/SecurityCategoriesListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Small)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `New` — New (Small)

## SecurityCategoryCardViewModel  (`ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `!canSave`
  - `Delete` — Delete (Small) — disabled: `Id == Guid.Empty`

## SecurityPricesListViewModel  (`ViewModels/Securities/Prices/SecurityPricesListViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `ImportPrices` — Import (Small) — disabled: `SecurityId == Guid.Empty`
  - `BackfillPrices` — Load historical prices (Small) — disabled: `SecurityId == Guid.Empty`

## SecurityPerformancePageViewModel  (`ViewModels/Securities/ReturnAnalysis/SecurityPerformancePageViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Period (`Ribbon_Group_Period`) — 1 Aktion(en)
  - `$"Range_{range}` — (range == OverviewTabVm.SelectedRange ? "▸ " : string.Empty) (Small)
- **Gruppe:** Settings (`Ribbon_Group_Settings`) — 1 Aktion(en)
  - `BenchmarkSetup` — Configure benchmark (Large)

## SecuritiesListViewModel  (`ViewModels/Securities/SecuritiesListViewModel.cs`)

- **Gruppe:** Actions (`Ribbon_Group_Actions`) — 2 Aktion(en)
  - `New` — New (Large)
  - `Categories` — Categories (Small)
- **Gruppe:** Reports (`Ribbon_Group_Reports`) — 1 Aktion(en)
  - `PortfolioAnalysisReport` — Portfolio report (Small)
- **Gruppe:** Filter (`Ribbon_Group_Filter`) — 1 Aktion(en)
  - `ToggleActive` — Only active (Small)

## SecurityCardViewModel  (`ViewModels/Securities/SecurityCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 3 Aktion(en)
  - `Save` — Save (Large) — disabled: `!canSave`
  - `Archive` — Archive (Small) — disabled: `!(Id != Guid.Empty && Security != null && Security.IsActive)`
  - `Delete` — Delete (Small) — disabled: `!(Id != Guid.Empty && Security != null && !Security.IsActive)`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 4 Aktion(en)
  - `Postings` — Postings (Small) — disabled: `!(Id != Guid.Empty)`
  - `Prices` — Prices (Small) — disabled: `!(Id != Guid.Empty)`
  - `Performance` — Detailed analysis (Small) — disabled: `!(Id != Guid.Empty)`
  - `Attachments` — Attachments (Small) — disabled: `!(Id != Guid.Empty)`

## SetupBackupsViewModel  (`ViewModels/Setup/SetupBackupsViewModel.cs`)

- **Gruppe:** groupTitle (`groupTitle`) — 2 Aktion(en)
  - `CreateBackup` — Create backup (Large)
  - `UploadBackup` — Upload backup (Large)

## SetupCardViewModel  (`ViewModels/Setup/SetupCardViewModel.cs`)

- **Gruppe:** groupTitle (`groupTitle`) — 4 Aktion(en)
  - `Save` — Save (Large) — disabled: `Saving || !HasPendingChanges`
  - `Reset` — Reset (Small) — disabled: `Saving || !HasPendingChanges`
  - `RebuildAggregates` — Rebuild aggregates (Small)
  - `ResetReportCache` — Reset cache (Small)

## SetupMcpViewModel  (`ViewModels/Setup/SetupMcpViewModel.cs`)

- **Gruppe:** AI (`Ribbon_Group_Mcp`) — 3 Aktion(en)
  - `McpApprovals` — approvalsLabel (Large)
  - `McpAudit` — Audit log (Small)
  - `McpSecurity` — AI security (Small)

## SetupProfileViewModel  (`ViewModels/Setup/SetupProfileViewModel.cs`)

- **Gruppe:** lblProfileGroup (`lblProfileGroup`) — 1 Aktion(en)
  - `DetectTimezone` — lblDetect (Small)

## UserCardViewModel  (`ViewModels/Setup/UserCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 1 Aktion(en)
  - `Back` — Back (Large)
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 2 Aktion(en)
  - `Save` — Save (Large) — disabled: `User != null && User.Id != Guid.Empty`
  - `Delete` — Delete (Small) — disabled: `!(UserId != Guid.Empty && User != null)`

## UserListViewModel  (`ViewModels/Setup/UserListViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 1 Aktion(en)
  - `New` — New (Large)

## StatementDraftCardViewModel  (`ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 3 Aktion(en)
  - `Back` — Back (Large) — disabled: `Draft == null`
  - `Prev` — Previous (Small) — disabled: `Draft == null || Draft.PrevInUpload == null`
  - `Next` — Next (Small) — disabled: `Draft == null || Draft.NextInUpload == null`
- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 6 Aktion(en)
  - `Save` — Save (Large) — disabled: `!HasPendingChanges`
  - `Add` — New entry (Small) — disabled: `Draft == null`
  - `Book` — Book (Small) — disabled: `Draft == null`
  - `DeleteDraft` — Delete (Small) — disabled: `Draft == null`
  - `Classify` — Reclassify (Small) — disabled: `Draft == null`
  - `Validate` — Validate (Small) — disabled: `Draft == null`
- **Gruppe:** Quick edit (`Ribbon_Group_QuickEdit`) — 3 Aktion(en)
  - `ToggleQuickEdit` — Quick edit (Large) — disabled: `Draft == null`
  - `SaveQuickEdit` — Save edits (Small) — disabled: `// compute Disabled dynamically using embedded list state
                !(EmbeddedList is StatementDraftEntriesListViewModel sevm && sevm.HasPendingQuickEditChanges() && sevm.QuickEditRowsAreValid() && !Loading)`
  - `CancelQuickEdit` — Cancel edits (Small) — disabled: `Draft == null`
- **Gruppe:** Statement (`Ribbon_Group_Statement`) — 2 Aktion(en)
  - `ViewOriginal` — View original (Small) — disabled: `Draft == null`
  - `DownloadOriginal` — Download (Small) — disabled: `Draft == null`
- **Gruppe:** Export (`Ribbon_Group_Export`) — 2 Aktion(en)
  - `ExportCsv` — Export CSV (Small) — disabled: `Draft == null`
  - `ExportXlsx` — Export Excel (Small) — disabled: `Draft == null`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 1 Aktion(en)
  - `OpenAccountDetails` — Bank account (Small) — disabled: `Draft == null || Draft.DetectedAccountId == null`

## StatementDraftEntryCardViewModel  (`ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs`)

- **Gruppe:** Navigation (`Ribbon_Group_Navigation`) — 3 Aktion(en)
  - `Back` — Back (Large) — disabled: `Entry == null`
  - `Prev` — Previous (Small) — disabled: `Entry == null`
  - `Next` — Next (Small) — disabled: `Entry == null`
- **Gruppe:** Actions (`Ribbon_Group_Actions`) — 3 Aktion(en)
  - `Save` — Save (Small) — disabled: `(Entry == null && EntryId != Guid.Empty)`
  - `Validate` — Validate (Small) — disabled: `Entry == null`
  - `BookEntry` — Book (Small) — disabled: `Entry == null`
- **Gruppe:** Linked data (`Ribbon_Group_Linked`) — 3 Aktion(en)
  - `OpenContact` — Contact (Small) — disabled: `Entry == null || Entry.ContactId == null`
  - `OpenSavingsPlan` — Savings plan (Small) — disabled: `Entry == null || Entry.SavingsPlanId == null`
  - `OpenSecurity` — Security (Small) — disabled: `Entry == null || Entry.SecurityId == null`

## StatementDraftsListViewModel  (`ViewModels/StatementDrafts/StatementDraftsListViewModel.cs`)

- **Gruppe:** Manage (`Ribbon_Group_Manage`) — 5 Aktion(en)
  - `New` — New (Large)
  - `DeleteAll` — Delete all (Large)
  - `Reclassify` — Reclassify (Large)
  - `MassBooking` — Mass booking (Large)
  - `Import` — Import (Large)

---
**Gesamt:** 43 ViewModels, 93 Gruppen-Definitionen, ~178 statisch erfasste Aktionen.

> Hinweis: Aktionen, die dynamisch in Schleifen erzeugt oder aus Kind-ViewModels aggregiert werden, sind hier nicht einzeln aufgeführt. Disabled-/Hidden-Bedingungen sind rohe C#-Ausdrücke aus dem Code.