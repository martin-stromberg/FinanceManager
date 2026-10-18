# Tasks: Analyse von Todeskreuz und weiteren Umkehrsignalen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `TrendReversalOptions` mit allen Parametern anlegen | Erledigt | `Analyze_InvalidOptions_Throws` |
| 2 | Logik | Enums `TrendReversalSignalType`, `TrendReversalSignalStrength`, `TrendStatus`, `LineBreakClassification`, `MarketStructureClassification` anlegen | Erledigt | `TrendReversalDtoEnumMappingTests` (`*_MapsToSameNamedDtoValue`, `DomainAndDtoEnums_HaveMatchingMemberCounts`) |
| 3 | Logik | Records `ReversalSignal`, `DeathCrossResult`, `LongTermLineBreakResult`, `MarketStructureResult`, `MacdResult`, `RsiResult`, `TrendReversalAnalysis` anlegen | Erledigt | `GetAsync_ForOwner_ReturnsDto` |
| 4 | Logik | `TrendReversalAnalyzer.Analyze` — SMA + Todeskreuz-Erkennung | Erledigt | `Analyze_BelowLongAverage_FindsDeathCrossDate`, `Analyze_ShortAboveLong_NoDeathCross`, `Analyze_TooFewPrices_ReturnsInsufficientData` |
| 5 | Logik | `TrendReversalAnalyzer` — 200-Tage-Linien-Bruch mit Klassifikation | Erledigt | `Analyze_CloseBelowLongFewDays_ShortTermWeakness`, `Analyze_CloseBelowLongManyDays_StructuralReversal`, `Analyze_CloseAboveLong_NoBreak` |
| 6 | Logik | `TrendReversalAnalyzer` — Marktstruktur (niedrigere Hochs/Tiefs) | Erledigt | `Analyze_LowerHighsAndLows_EmergingDowntrend`, `Analyze_LowerLowsOnly_TrendLoss`, `Analyze_NegativeDriftWithoutStructure_SidewaysBearish`, `Analyze_Uptrend_Intact` |
| 7 | Logik | `TrendReversalAnalyzer` — MACD + bärisches Crossover | Erledigt | `Analyze_MacdCrossesBelowSignal_BearishCrossover`, `Analyze_MacdAboveSignal_NoCrossover` |
| 8 | Logik | `TrendReversalAnalyzer` — RSI + negative Divergenz | Erledigt | `Analyze_RsiLowerHighAtHigherPriceHigh_BearishDivergence`, `Analyze_RsiConfirmsHighs_NoDivergence` |
| 9 | Logik | `TrendReversalAnalyzer` — Signalaggregation, Score, Trendstatus | Erledigt | `Analyze_NoSignals_StatusIntact`, `Analyze_SingleWeakSignal_StatusWeakened`, `Analyze_CombinedStrongSignals_StatusReversalLikely` |
| 10 | DTO | `TrendReversalDto` + DTO-Enums/Records in Shared anlegen | Erledigt | `TrendReversalDtoEnumMappingTests`, `GetAsync_ForOwner_ReturnsDto` |
| 11 | Logik | `ITrendReversalService` in Application anlegen | Erledigt | `TrendReversalServiceTests` (Interface durch alle Service-Tests abgedeckt) |
| 12 | Logik | `TrendReversalService` in Infrastructure implementieren + DI-Registrierung | Erledigt | `TrendReversalServiceTests` (GetAsync-/EvaluateAndNotify-Tests) |
| 13 | API | `SecuritiesController.GetTrendReversalAsync` (GET `{id}/trend-reversal`) | Erledigt | `GetTrendReversal_UnknownOrForeign_ReturnsNotFound` |
| 14 | API | `IApiClient.Securities_GetTrendReversalAsync` + Implementierung | Erledigt | E2E `CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals` (indirekt über Widget) |
| 15 | Web | `ProgramExtensions`: `TrendReversalOptions` binden | Erledigt | Kein direkter Test (indirekt via E2E-Widget-Test) |
| 16 | UI | `TrendReversalWidget.razor` erstellen (Status, Signalliste, Panel, „keine Signale"-Hinweis) | Erledigt | E2E `CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals`, `CardPage_BullishPrices_HidesTrendReversalWidget` |
| 17 | UI | `TrendReversalWidget.de.resx` + `.en.resx` erstellen | Erledigt | Kein direkter Test |
| 18 | UI | `trw-*`-Styles in `app.ReturnSummaryWidget.css` ergänzen | Erledigt | Kein direkter Test |
| 19 | UI | `CardPage.razor`: Widget einbinden | Erledigt | E2E `CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals` |
| 20 | Tests | `TrendReversalAnalyzerTests_DeathCross` | Erledigt | 4 Facts vorhanden |
| 21 | Tests | `TrendReversalAnalyzerTests_LineBreak` | Erledigt | 3 Facts vorhanden |
| 22 | Tests | `TrendReversalAnalyzerTests_MarketStructure` | Erledigt | 4 Facts vorhanden |
| 23 | Tests | `TrendReversalAnalyzerTests_Momentum` | Erledigt | 4 Facts vorhanden |
| 24 | Tests | `TrendReversalAnalyzerTests_Scoring` | Erledigt | 3 Facts vorhanden |
| 25 | Tests | `TrendReversalServiceTests` | Erledigt | 11 Tests vorhanden (alle geplanten Fälle) |
| 26 | Tests | `TrendReversalDtoEnumMappingTests` | Erledigt | 5 Theories + MemberCount-Fact vorhanden |
| 27 | Tests | `SecuritiesControllerTests.Create()` um `Mock<ITrendReversalService>` erweitern + 404-Test | Erledigt | `GetTrendReversal_UnknownOrForeign_ReturnsNotFound` |
| 28 | E2E-Tests | `SecurityTrendReversalE2ETests` (Signale sichtbar / „keine Signale" sichtbar) | Erledigt | 3 Facts: `CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals`, `CardPage_BullishPrices_HidesTrendReversalWidget`, `SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` |
| 29 | Dokumentation | `Docs/help/wertpapiermanagement/umkehrsignale.md` + `index.md`-Verlinkung | Erledigt | Kein direkter Test |
| 30 | Domain | `Security.TrendReversalNotifiedStatus` + `SetTrendReversalNotifiedStatus` | Erledigt | `TrendReversalServiceTests` (`NotifiedStatusAsync`, EvaluateAndNotify-Tests) |
| 31 | Domain | `User.TrendReversalNotificationsEnabled` + `SetTrendReversalNotificationsEnabled` | Erledigt | `User_TrendReversalNotificationsEnabled_DefaultsToTrue`, `EvaluateAndNotify_WhenDisabledByUser_TracksStatusButDoesNotNotify` |
| 32 | Infrastruktur | `AppDbContext`-Konfiguration (Security-Status + User-Flag) | Erledigt | `TrendReversalServiceTests` (Mapping über InMemory-Context) |
| 33 | Infrastruktur | Migration `AddTrendReversal` + ModelSnapshot | Erledigt | Kein direkter Test |
| 34 | DTO | `NotificationSettingsDto`/`UserNotificationSettingsUpdateRequest` um `TrendReversalNotificationsEnabled` erweitern | Erledigt | `SetupNotificationsViewModelTests`, E2E `SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` |
| 35 | API | `UserSettingsController`: Get-Projektion + Update-Endpunkt | Erledigt | E2E `SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` |
| 36 | API | `IApiClient`/`ApiClient.User`: `User_UpdateNotificationSettingsAsync`-Signatur erweitern | Erledigt | `SetupNotificationsViewModelTests`, `SetupCardViewModelTests` |
| 37 | Web | `SetupNotificationsViewModel`: Load/Dirty-Check/Save um neues Feld | Erledigt | `SetupNotificationsViewModelTests` |
| 38 | UI | `SetupNotificationsTab.razor`: Checkbox `Notifications_TrendReversal` + Hilfetext | Erledigt | E2E `SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` |
| 39 | UI | `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`: `Notifications_TrendReversal`(+`_Help`) | Erledigt | Kein direkter Test |
| 40 | Web | `SecurityPriceWorker`: `EvaluateAndNotifyAsync`-Aufruf mit try/catch | Erledigt | `SecurityPriceErrorRecoveryTests` (DI-Registrierung; Aufruf ohne direkten Test) |
| 41 | Logik | `ITrendReversalLocalizer` (Application) + `TrendReversalLocalizer` (Web) + `.de`/`.en.resx` | Erledigt | `TrendReversalServiceTests` (gemockter Localizer; Web-Localizer kein direkter Test) |
| 42 | Web | `ProgramExtensions`: `ITrendReversalLocalizer`-DI-Registrierung | Erledigt | Kein direkter Test |
| 43 | Tests | Bestehende Tests an neue Signaturen anpassen (`SetupNotificationsViewModelTests`, `SetupCardViewModelTests`, `SecurityPriceErrorRecoveryTests`) | Erledigt | Angepasste Mocks/Verifies vorhanden |
