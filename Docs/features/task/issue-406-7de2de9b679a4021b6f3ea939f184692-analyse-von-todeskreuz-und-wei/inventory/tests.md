# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-18, ~11:30–11:45 (Europe/Berlin, UTC+2)
- Branch und Commit-ID: `task/issue-406-7de2de9b679a4021b6f3ea939f184692-analyse-von-todeskreuz-und-wei` @ `64bc68fa` (= `origin/staging` nach Backmerge PR #433, Release v1.27.0)
- Uncommittete Änderungen im getesteten Stand: nur neue Dokumentationsartefakte unter `Docs/features/task/issue-406-*/` (kein Produktivcode, keine Tests verändert)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.402, Target `net10.0`, Playwright-Browser lokal installiert (`~/AppData/Local/ms-playwright`, chromium 1169–1248 u. a.)
- Ermittelte Testsuiten und Quellen der Testbefehle: `FinanceManager.Tests` (xUnit/Moq/FluentAssertions, Unit), `FinanceManager.Tests.Integration` (WebApplicationFactory), `FinanceManager.Tests.E2E` (Playwright). Befehle analog `.github/workflows/pr-staging-ci.yml` (Microsoft.Testing.Platform: Argumente nach `--`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename baseline-unit.trx` | Repo-Root | 0 | 1397 | 0 | 0 | [baseline-unit.trx](test-results/baseline-unit.trx) |
| Baseline Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug -- --report-xunit-trx --report-xunit-trx-filename baseline-integration.trx` | Repo-Root | 0 | 134 | 0 | 0 | [baseline-integration.trx](test-results/baseline-integration.trx) |
| Baseline E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug -- --report-xunit-trx --report-xunit-trx-filename baseline-e2e.trx` | Repo-Root | 2 | 104 | 3 | 0 | [baseline-e2e.trx](test-results/baseline-e2e.trx) |

Vorausgehender Build: `dotnet build FinanceManager.sln -c Debug` — 0 Fehler, 60 Warnungen (NU1510, xUnit-Analyzer; alle preexisting).

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | E2E / `Tests/Auth/ChangePasswordWellKnownPlaywrightTests.cs` | `PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:51625/login` (Navigation in `AuthGateway.LogoutAsync`) | Baseline E2E, baseline-e2e.trx |
| `FinanceManager.Tests.E2E.StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` | E2E / `Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs` | `Expected response.Status to be 204, but found 401` | Baseline E2E, baseline-e2e.trx |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows` | E2E / `Tests/Budget/BudgetReportDemoDataE2ETests.cs` | `TimeoutException`: `waiting for Locator("#create-demo-data") to be visible` | Baseline E2E, baseline-e2e.trx |

Alle drei Fehler stehen in keinem Bezug zum Wertpapier-Bereich und wirken umgebungs-/infrastrukturbedingt (HTTPS-Navigation, Auth-Cookie, Demo-Daten-Button).

### Testlücken und Ausführungsprobleme

- Keine — alle drei Suiten waren ausführbar und liefen vollständig durch.

## Testklassen (Wertpapier-/Golden-Cross-Bereich)

### `GoldenCrossAnalyzerTests`
Datei: `FinanceManager.Tests/Securities/GoldenCrossAnalyzerTests.cs`
- `Analyze_WithTooFewPrices_ReturnsInsufficientData` — InsufficientData bei < LongWindow Kursen
- `Analyze_ComputesTrailingMovingAverages` — SMA-Berechnung
- `Analyze_ShortWellBelowLong_ReturnsFar` / `Analyze_ShortSlightlyBelowLong_ReturnsApproaching` — Phasenklassifikation
- `Analyze_ShortAboveLong_ReturnsCrossedWithCrossDate` — Kreuzungsdatum
- `Analyze_WithShortWindowNotSmallerThanLong_Throws` — Optionsvalidierung
- `Analyze_WithoutCross_HasNoExtendedAnalysis` — kein Cross → keine Fenster/Retest/Trend
- `Analyze_AfterCross_BuildsThreeEntryWindows` — drei Eintrittsfenster
- `Analyze_WithoutPullback_ReportsNoRetest`, `Analyze_PullbackToShortLineWithBounce_ReportsSuccessfulRetest`, `Analyze_OngoingPullback_ReportsInProgress`, `Analyze_PullbackNeverRecovering_ReportsFailedRetest` — Retest
- `Analyze_ShortlyAfterCross_TrendIsUnknown`, `Analyze_HigherHighsAndLows_ReportsStableUptrend`, `Analyze_NoProgressSinceCross_ReportsSideways`, `Analyze_RisingWithoutSwingStructure_ReportsWeakUptrend` — Trendstruktur
- `Analyze_LongAfterCross_AllWindowsAreClosed` — Fensterstatus
- Muster: kleine Fenster (`ShortWindow = 3/2`, `LongWindow = 6/10`) und synthetische Kursreihen per `Series(params decimal[])`-Helfer.

### `GoldenCrossServiceTests`
Datei: `FinanceManager.Tests/Securities/GoldenCrossServiceTests.cs`
- `GetAsync_ForOwner_ReturnsAnalysis` — Owner-Scope + DTO-Mapping
- `GetAsync_ForOtherUser_ReturnsNull` — kein Datenleck
- `GetAsync_WithFewPrices_ReturnsInsufficientData` — InsufficientData-DTO
- `EvaluateAndNotify_*` (6 Tests) — Notification-Zyklus (einmalig pro Phase, Reset bei `Far`, Disabled-Flag, InsufficientData)
- `User_GoldenCrossNotificationsEnabled_DefaultsToTrue`
- Muster: `AppDbContext` mit `UseInMemoryDatabase`, `Mock<INotificationWriter>`, `Mock<IGoldenCrossLocalizer>` (key-echo), `TestEntityHelper.SetEntityId`, Fixture-Helfer `SetPricesAsync`.

### `GoldenCrossDtoEnumMappingTests`
Datei: `FinanceManager.Tests/Securities/GoldenCrossDtoEnumMappingTests.cs`
- `[Theory]`/`[InlineData]` je Enum-Paar + `DomainAndDtoEnums_HaveMatchingMemberCounts` — garantiert int-Mapping-Konsistenz Domain↔DTO.

### `SecuritiesControllerTests`
Datei: `FinanceManager.Tests/Controllers/SecuritiesControllerTests.cs`
- Mocking aller Controller-Dependencies (`Mock<IGoldenCrossService>` u. a.), `TestCurrentUserService`. Fokus bisher: Create/Update mit `Region`/`Sector`.

## Hilfsmethoden

### `TestEntityHelper` (`FinanceManager.Tests/TestHelpers/`)
- `SetEntityId(entity, guid)` — setzt private `Id` per Reflection für InMemory-Entities.
