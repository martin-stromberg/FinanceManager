# E2E-Ausgangsfehler

Alle 112 Testfälle endeten mit demselben Fixture-Setupfehler. Die vollständigen Fehlermeldungen und Stacks stehen im [TRX-Report](baseline-e2e.trx) und im [Laufprotokoll](e2e.log). Der Serverstart wurde in `PlaywrightWebAppFixture.InitializeAsync` abgebrochen, bevor Testaktionen liefen.

Fehlerbild: `PlaywrightWebAppFixture.InitializeAsync` warf `TimeoutException`: Testserver nicht bereit unter `https://127.0.0.1:<port>`. Der letzte Fehler des Readiness-Probes war `HttpRequestException` → `AuthenticationException` → `Win32Exception: Im Sicherheitspaket sind keine Anmeldeinformationen verfügbar.` `dotnet dev-certs https --check` bestätigte zusätzlich: `No valid certificate found.`

Stacktrace (aus TRX): `PlaywrightWebAppFixture.WaitForServerAsync` (`FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs:396`), `PlaywrightWebAppFixture.InitializeAsync` (`.../PlaywrightWebAppFixture.cs:106`).

## Vollständige fehlgeschlagene Test-IDs

| Test-ID | Fehlerbild |
|---------|------------|
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_ShowsStatisticsAlongsideTable` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_Search_UpdatesTableAndStatisticsTogether` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_ShowsTotalYearChangeAndMonthChange` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_ShowsAccountTypeAndBankContactDonuts` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_MixedBalances_ShowsLosslessSignedDistribution` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_UnknownAndLongBankContact_RemainsReadable` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_ClearSearch_RestoresTableAndStatistics` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_InfiniteScroll_DoesNotChangeStatisticsTotal` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_OtherUsersAccountsRemainInvisibleEverywhere` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_Empty_ShowsZeroKpisEmptyChartsAndUsableRibbon` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_ClickingAccountRow_StillNavigatesToCard` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_Mobile_RendersStatisticsWithoutHidingList` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountPlaywrightTests.CreateCollectionAccount_ViaUi_ShouldNavigateToDetailPage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountPlaywrightTests.AddLinkedIban_ViaUi_ShouldAppearInPanel` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountPlaywrightTests.RemoveLinkedIban_ViaUi_ShouldDisappearFromPanel` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.Register_Login_Logout_Flow_ShouldWork` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.Register_Login_Logout_Flow_ShouldWork_OnMobileViewport` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.ExpiredSession_OnProtectedRoute_ShouldRedirectToLoginWithReturnUrl_AndReturnAfterLogin` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.LoginWithoutReturnUrl_ShouldNavigateToHome` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome(returnUrl: "https://example.invalid/reports/dashboard")` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome(returnUrl: "//example.invalid/reports/dashboard")` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome(returnUrl: "/login")` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome(returnUrl: "%252Freports%252Fdashboard")` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.MultipleAuthenticationFailures_ShouldNavigateToLoginOnlyOnce` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.AuthenticationFlowPlaywrightTests.Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AnonymousWellKnown_RedirectsThroughLoginToChangePassword` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.LoginStatus_ChangePasswordLink_NavigatesToChangePassword` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.ChangePassword_WrongCurrent_ShowsError` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ConfirmationDialogE2ETests.AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HelpPagePlaywrightTests.HelpHub_ShouldShowDocumentationContent` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.EnableCache_HomeKpiDataIsPersistedInLocalStorage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountImportPlaywrightTests.UploadCollectionAccountCsv_ViaUi_ShouldShowMultipleDraftsInList` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountImportPlaywrightTests.UploadWithLinkedIban_ViaUi_DraftShouldShowAutoAssignedAccount` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountImportPlaywrightTests.ManualAccountAssignment_ViaUi_ShouldAddIbanToLinkedPanelAfterBooking` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.CollectionAccountImportPlaywrightTests.BookCollectionAccountDraft_WithKnownLinkedIban_ViaUi_ShouldAutoAssignAndBook` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadStatementFile_ShouldShowSuccess_WhenImportCompletes` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadStatementFile_ShouldShowSuccess_WhenImportCompletes_OnMobileViewport` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadUnrecognizedFile_ViaUi_ShouldShowReasonInReviewDialog_WithoutFinalizeWarning` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.Booking_WithErrorsWarnings_AndWithOrWithoutSavingsSecurity_ShouldCreateExpectedPostings` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.ClickAccountRow_ShouldNavigateToDetailPage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.ClickAccountRow_ShouldNavigateToDetailPage_OnMobileViewport` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.MobileRibbonShortcuts_ShouldShowShortcutOnlyWhenGroupIsClosed_OnRepresentativePages` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.Create_Edit_AndAliasContact_FromContactsPage_ShouldWork` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.CreateContact_FromStatementEntryPage_ShouldAssignContactDirectly` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.Create_Edit_Delete_BankAccount_ShouldWork` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.Create_Edit_Delete_BankAccount_ShouldWork_OnMobileViewport` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.Create_Edit_Delete_SavingsPlan_ShouldWork` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ListNavigationPlaywrightTests.Create_Edit_Delete_Security_AndImportPrices_OnPricesPage_ShouldWork` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.LoadingBar_ShouldUseSingleDomNodeAndRestartSameInstance` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.LoadingBar_ShouldBeFixedToViewportTop_OnDesktop` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.LoadingBar_ShouldSitBelowMobileTopbar_OnMobile` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.FormSubmit_WithValidationMessage_ShouldNotLeaveLoadingBarVisible` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.SetupStatementSubmit_ShouldStopLoadingBarAfterNonNavigatingSave` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.BudgetReportInitialLoad_ShouldStartGlobalLoadingBar` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.BudgetReportApplySettings_ShouldStartAndStopGlobalLoadingBar` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PortfolioAnalysisReportE2ETests.LoadReportScenario_ShouldRenderTileGrid` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PortfolioAnalysisReportE2ETests.RibbonNavigationScenario_ShouldNavigateFromSecuritiesList` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PortfolioAnalysisReportE2ETests.EditConfigurationScenario_ShouldHideDeactivatedTileAfterSave` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PortfolioAnalysisReportE2ETests.MultiUserIsolationScenario_ShouldNotLeakConfigurationBetweenUsers` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ProfileSettingsLanguageTests.ChangeLanguage_ToEnglish_SavesAndApplies` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ProfileSettingsLanguageTests.NewUser_WithoutLanguagePreference_UsesBrowserLanguage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ProfileSettingsLanguageTests.ChangeLanguage_UpdatesAuthCookie_WithNewJwtToken` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ProfileSettingsLanguageTests.ChangeLanguage_ToAutomatic_RespectsBrowserLanguage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ProfileSettingsLanguageTests.LanguagePreference_PersistedAcrossSessions` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ReportingFlowPlaywrightTests.SaveFavorite_ShouldPersistAndReload` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ReportingFlowPlaywrightTests.SaveFavorite_ShouldPersistAndReload_OnMobileViewport` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.ReportingFlowPlaywrightTests.CreateBackup_EditMasterData_AndRestoreBackup_ShouldRestoreOriginalState` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTrendReversalE2ETests.CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTrendReversalE2ETests.CardPage_BullishPrices_HidesTrendReversalWidget` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTxtSetupPlaywrightTests.Admin_EditsSecurityTxtSettings_EnableSaveAndPersist` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTxtSetupPlaywrightTests.Admin_EditsCanonical_EnableSaveAndPersist` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTxtSetupPlaywrightTests.PublicSecurityTxt_ContainsConfiguredCanonical` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SecurityTxtSetupPlaywrightTests.PublicSecurityTxt_UsesApiBaseAddressFallback_WhenCanonicalEmpty` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SetupDirtyDetectionPlaywrightTests.Statements_Fields_ToggleSaveButton` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.SetupDirtyDetectionPlaywrightTests.ReturnAnalysis_Fields_ToggleSaveButton` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.WellKnownSetupPlaywrightTests.Admin_ConfiguresChangePasswordUrl_PublicEndpointRedirects` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.WellKnownSetupPlaywrightTests.NonAdmin_SeesSectionButAdminOnlyMessage` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PreliminaryStatementDraftE2ETests.BookPreliminaryDraft_ShouldCreatePreliminaryPostings` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PreliminaryStatementDraftE2ETests.BookRealStatement_ShouldReversePreliminaryPostings` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PreliminaryStatementDraftE2ETests.CreatePreliminaryDraft_ViaRibbon_ShouldCreateAndOpenDraftWithQuickEdit` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.PreliminaryStatementDraftE2ETests.OpenPreliminaryDraft_Card_ShouldShowPreliminaryIndicator` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftExportE2ETests.ExportCsv_ViaRibbon_ShouldDownloadCsvWithEntries` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftExportE2ETests.ExportXlsx_ViaRibbon_ShouldDownloadXlsx` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowUp_ShouldFocusSameFieldInPreviousVisibleRow` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowDown_ShouldFocusSameFieldInNextVisibleRow` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowUpAtFirstRow_ShouldKeepFocus` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowDownAtLastRow_ShouldKeepFocus` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowOnDateFields_ShouldMoveFocusWithoutChangingDate` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_BookingDateChange_ShouldCopyToEmptyOrMatchedValutaDateOnly` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_SaveButton_IsEnabledWhenAllRowsComplete` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_CtrlArrowDown_ShouldSkipHiddenDeletedAndLockedRows` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.CtrlArrowNavigation_OutsideQuickEdit_ShouldNotChangeFocus` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_RegularInputAndF8_ShouldRemainUnaffected` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_PressesF8_ShouldCopySingleFieldFromRowAbove` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_PressesCtrlF8_ShouldCopyAllFieldsFromRowAboveAndOverwriteExisting` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_Blur_ShouldCoalesceKeepaliveRequests` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
| `FinanceManager.Tests.E2E.VersionDisplayPlaywrightTests.Login_ShowsVersionText_InsteadOfUserId` | Fixture-Setupfehler wie oben; [TRX](baseline-e2e.trx) |
