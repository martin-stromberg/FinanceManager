# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `TrendStatus` (Enum, `FinanceManager.Domain/Securities/TrendStatus.cs`) — angelegt (InsufficientData=0, Intact=1, Weakened=2, ReversalLikely=3)
- [x] `Security.TrendReversalNotifiedStatus` + `SetTrendReversalNotifiedStatus` (Domain) — erweitert (`Security.cs` ~Zeile 141–155)
- [x] `User.TrendReversalNotificationsEnabled` + `SetTrendReversalNotificationsEnabled` (Domain, `User.Notifications.cs` ~Zeile 64–79) — erweitert, Default `true`
- [x] `TrendReversalOptions` (Optionsklasse, Application) — angelegt; `SectionName = "TrendReversal"`, alle geplanten Parameter mit Defaults (50/200/5/12/26/9/14/120/3/4)
- [x] `TrendReversalSignalType`, `TrendReversalSignalStrength`, `LineBreakClassification`, `MarketStructureClassification` (Enums im Analyzer-File) — angelegt
- [x] `ReversalSignal`, `DeathCrossResult`, `LongTermLineBreakResult`, `MarketStructureResult`, `MacdResult`, `RsiResult`, `TrendReversalAnalysis` (Records im Analyzer-File) — angelegt
- [x] `TrendReversalAnalyzer.Analyze` (statische Klasse, Application) — angelegt; Optionsvalidierung via `ArgumentException`, InsufficientData bei `n < LongWindow`, SMA via Präfixsummen, `DistancePercent`
- [x] Todeskreuz-Erkennung — `CrossDate`, `DaysSinceCross`, Signal `DeathCross` (Strong)
- [x] 200-Tage-Linien-Bruch — `ConsecutiveDaysBelow`, `BreakDate`, `PercentBelow`, Klassifikation `StructuralReversal`/`ShortTermWeakness`/`None` mit Signalen (Strong/Weak)
- [x] Marktstruktur — Swing-Highs/Lows (`SwingLookaround`), `LowerHighs`/`LowerLows`, Klassifikationen `EmergingDowntrend`/`TrendLoss`/`SidewaysBearish`/`Intact`/`Unknown` mit Signalen (Strong/Moderate/Weak)
- [x] MACD — `EMA(fast) − EMA(slow)`, Signal-EMA, letztes bärisches Crossover mit `CrossoverDate`, Signal `MacdBearishCrossover` (Moderate)
- [x] RSI — Wilder-Glättung, bärische Divergenz über letzte zwei Swing-Highs, `DivergenceDate`, Signal `RsiBearishDivergence` (Moderate)
- [x] Aggregation — `Signals`-Liste, `SignalScore` = Summe der Stärken, Status 0→`Intact`, `< ReversalLikelyThreshold`→`Weakened`, `>=`→`ReversalLikely`
- [x] `ITrendReversalService` (Interface, Application) — `GetAsync` + `EvaluateAndNotifyAsync`
- [x] `ITrendReversalLocalizer` (Interface, Application) — `Format(key, cultureName, args)`
- [x] `TrendReversalService` (Infrastructure, scoped) — Owner-Check, Laden der `LongWindow*3` neuesten Kurse, int-Cast-DTO-Mapping inkl. `CurrencyCode`, `TriggerKeyPrefix = "security:trend-reversal:"`, Benachrichtigungszyklus (Reset bei `Intact`, einmalig je Status, `TrendReversalNotificationsEnabled`-Prüfung, `INotificationWriter` mit `EventDriven`/`HomePage`, `PreferredLanguage`, `TrendReversal_{Weakened|ReversalLikely}_{Title,Message}`-Keys)
- [x] `TrendReversalLocalizer` (Web) + `Resources/Services/TrendReversalLocalizer.de/.en.resx` — angelegt, inkl. `TrendReversal_Signal_*`-Keys
- [x] `TrendReversalDto` + DTO-Enums/Records (`FinanceManager.Shared/Dtos/Securities/TrendReversalDto.cs`) — int-gleiche Spiegelung aller Domain-Enums/Records
- [x] `SecuritiesController` — Ctor-Parameter `ITrendReversalService` + `GetTrendReversalAsync` (`GET api/securities/{id}/trend-reversal`, 200/404)
- [x] `IApiClient.Securities_GetTrendReversalAsync` + Implementierung in `ApiClient.Securities.cs` (404→`null`)
- [x] `User_UpdateNotificationSettingsAsync` — Signatur in `IApiClient`/`ApiClient.User` um `trendReversalEnabled` erweitert, Payload-Feld `TrendReversalNotificationsEnabled`
- [x] `NotificationSettingsDto` + `UserNotificationSettingsUpdateRequest` — `TrendReversalNotificationsEnabled` ergänzt
- [x] `UserSettingsController` — DTO-Projektion (Get) + `SetTrendReversalNotificationsEnabled` (Update)
- [x] `SetupNotificationsViewModel` — `TrendReversalNotificationsEnabled` in Save-Aufruf, Dirty-Check und Clone/Load
- [x] `SetupNotificationsTab.razor` — neue Checkbox `notif-trend-reversal` mit Label + Hilfetext
- [x] `Pages.resx`/`Pages.de.resx`/`Pages.en.resx` — `Notifications_TrendReversal` + `Notifications_TrendReversal_Help` in allen drei Dateien
- [x] `AppDbContext` — `Security.TrendReversalNotifiedStatus` (Conversion, Default/Sentinel `Intact`) + `User.TrendReversalNotificationsEnabled` (Default `true`, required)
- [x] Migration `AddTrendReversal` + `AppDbContextModelSnapshot` — beide Spalten mit Defaults
- [x] `ServiceCollectionExtensions` — `AddScoped<ITrendReversalService, TrendReversalService>()`
- [x] `ProgramExtensions` — `AddScoped<ITrendReversalLocalizer, TrendReversalLocalizer>()` + `Configure<TrendReversalOptions>`-Bindung
- [x] `SecurityPriceWorker` — `ITrendReversalService` im Scope aufgelöst, Aufruf nach dem Golden-Cross-Block mit eigenem try/catch (loggt, bricht nicht ab)
- [x] `TrendReversalWidget.razor` — nur bei Signalen sichtbar, Status-Badge, Signalliste mit Datum/Stärke, Info-Panel (rsw-Pattern) mit Bedeutungstexten, Statuserklärung, Formelbox und Disclaimer; Fehler/`Intact`/`InsufficientData` → leer
- [x] `TrendReversalWidget.de.resx`/`.en.resx` — alle verwendeten Keys in beiden Sprachen
- [x] `trw-*`-CSS-Klassen in `app.ReturnSummaryWidget.css` — Statusfarben (`trw-weakened`/`trw-reversal`), Signal-Detailzeilen
- [x] `CardPage.razor` — `<TrendReversalWidget SecurityId="Id" />` im `securities`-Block nach `GoldenCrossWidget`
- [x] `TrendReversalAnalyzerTests_DeathCross`/`_LineBreak`/`_MarketStructure`/`_Momentum`/`_Scoring` — alle 18 geplanten Testmethoden vorhanden
- [x] `TrendReversalServiceTests` — alle 11 geplanten Testmethoden inkl. Fixture-Helper (`SetPricesAsync`, `NotifiedStatusAsync`)
- [x] `TrendReversalDtoEnumMappingTests` — Theories je Enum-Paar + Member-Count-Fact
- [x] `SecuritiesControllerTests` — `Create()` um `Mock<ITrendReversalService>` erweitert + `GetTrendReversal_UnknownOrForeign_ReturnsNotFound`
- [x] Bestehende Tests angepasst — `SetupNotificationsViewModelTests`, `SetupCardViewModelTests` (neue Signatur in Mocks/Verifies), `SecurityPriceErrorRecoveryTests` (DI-Mock)
- [x] `SecurityTrendReversalE2ETests` — alle drei Pflichtszenarien (Widget mit Signalen, Widget ausgeblendet, Setup-Checkbox persistiert), Seeding via `TestUserSeeder` + eigener `AppDbContext` auf `_fixture.DatabasePath`
- [x] Dokumentation — `Docs/help/wertpapiermanagement/umkehrsignale.md` angelegt und in `index.md` verlinkt

## Hinweise

- Build der gesamten Solution erfolgreich (`dotnet build`, 0 Fehler).
- Abweichung zum Plantext: `Security.TrendReversalNotifiedStatus` ist mit `HasConversion<short>()` konfiguriert (Plan: `HasConversion<string>()`). Das entspricht dem tatsächlichen `GoldenCrossNotifiedPhase`-Muster (`AppDbContext.cs` Zeile 521 nutzt ebenfalls `HasConversion<short>()`); die Migration legt entsprechend eine `short`/`INTEGER`-Spalte an. Funktional konsistent, nur Plantext ungenau.
- Abweichung zum Plantext: In der Designentscheidung „Signalbewertung" wird ein `WeakenedThreshold` erwähnt; implementiert ist — wie in der Options-Tabelle des Plans — nur `ReversalLikelyThreshold` (`Weakened` = Score > 0 und < Schwelle).
- Tasks-Tabelle wurde um 14 im Plan enthaltene, bisher nicht tabellierte Aufgaben ergänzt (Zeilen 30–43); alle sind im Code umgesetzt und daher als `Erledigt` geführt.
