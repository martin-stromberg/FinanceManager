# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TrendReversalDto.cs (TrendReversalDto / Detail-Records)
- **Code-Smell (Speculative Generality / ungenutzte API-Oberfläche)** — Die Detail-Ergebnisse `DeathCross`, `LineBreak`, `MarketStructure`, `Macd` und `Rsi` (samt den Records `DeathCrossResultDto`, `LongTermLineBreakResultDto`, `MarketStructureResultDto`, `MacdResultDto`, `RsiResultDto`, ca. 90 Zeilen) werden in `TrendReversalService.GetAsync` (Zeilen 69–93) berechnet, gemappt und über den API-Endpunkt serialisiert, aber von keinem Consumer gelesen: `TrendReversalWidget.razor` nutzt nur `Status`, `ShortWindow`, `LongWindow`, `DistancePercent`, `Signals` (Type/Strength/Date), `SignalScore` und `AsOfDate`. Abweichung vom GoldenCross-Muster, wo sämtliche Detail-DTOs (`Retest`, `Trend`, `EntryWindows`) vom `GoldenCrossWidget`-Panel gerendert werden.
  Empfehlung: Entweder die Detail-Analysen im Info-Panel des Widgets darstellen (analog zum GoldenCrossWidget) oder die ungenutzten DTO-Member/-Records entfernen und das DTO auf die konsumierten Felder reduzieren.

### TrendReversalAnalyzer.cs (DeathCrossResult / AnalyzeDeathCross)
- **Dokumentation widerspricht Implementierung** — Die XML-Doku von `DeathCrossResult.CrossDate` (Zeilen 68–70) und gespiegelt `DeathCrossResultDto.CrossDate` (TrendReversalDto.cs Zeile 81) beschreibt „Date at which the short-term average was last at or above the long-term average". Tatsächlich liefert `AnalyzeDeathCross` (Zeilen 279–290, `crossIdx = end - 1`) das Datum des *ersten* Tages, an dem der kurze Durchschnitt unterhalb liegt (d. h. den Kreuzungstag selbst) — wie im Test `Analyze_BelowLongAverage_FindsDeathCrossDate` („the death cross date is the first day below") und analog zur GoldenCross-Konvention (`crossIdx = end - 1` = erster Tag oberhalb). Der beschriebene „letzte Tag at/above" wäre einen Tag früher.
  Empfehlung: XML-Doku in beiden Dateien korrigieren, z. B. „date on which the short-term average crossed below / was first below the long-term average".

### SecurityTrendReversalE2ETests.cs (SessionWithSecurity)
- **Toter/redundanter Code** — Zeilen 149–154 deklarieren innerhalb des positionalen Records `SessionWithSecurity` die Properties `Session` und `SecurityId` erneut (`public PlaywrightBrowserSession Session { get; } = Session;` bzw. `public Guid SecurityId { get; } = SecurityId;`). Diese Properties werden durch die Record-Positionsparameter bereits generiert; die Redeklaration ohne `init` schränkt zusätzlich `with`-Ausdrücke ein, ohne Mehrwert.
  Empfehlung: Beide Redeklarationen entfernen, nur die Hilfsproperty `Page` behalten.

### TrendReversalAnalyzerTests_DeathCross.cs (Analyze_InvalidOptions_Throws)
- **Testqualität (mehrere Fälle pro Test)** — Der Test `Analyze_InvalidOptions_Throws` (Zeilen 75–88) prüft drei unabhängige ungültige Optionskonfigurationen (Window, MACD-Perioden, ReversalLikelyThreshold) in einem Test. Schlägt der erste Fall fehl, werden die übrigen nicht mehr ausgeführt und die Fehlerursache ist nicht direkt am Testnamen erkennbar; Abweichung vom Projektstil (`GoldenCrossAnalyzerTests`: ein Fall pro Test, z. B. `Analyze_WithShortWindowNotSmallerThanLong_Throws`).
  Empfehlung: In drei Einzeltests oder eine `[Theory]` aufteilen.

## Geprüfte Dateien

- `FinanceManager.Domain/Securities/TrendStatus.cs`
- `FinanceManager.Domain/Securities/Security.cs`
- `FinanceManager.Domain/Users/User.Notifications.cs`
- `FinanceManager.Application/Securities/TrendReversal/TrendReversalAnalyzer.cs`
- `FinanceManager.Application/Securities/TrendReversal/TrendReversalOptions.cs`
- `FinanceManager.Application/Securities/TrendReversal/ITrendReversalService.cs`
- `FinanceManager.Application/Securities/TrendReversal/ITrendReversalLocalizer.cs`
- `FinanceManager.Infrastructure/Securities/TrendReversal/TrendReversalService.cs`
- `FinanceManager.Infrastructure/AppDbContext.cs`
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
- `FinanceManager.Infrastructure/Migrations/20261018121308_AddTrendReversal.cs`
- `FinanceManager.Infrastructure/Migrations/20261018121308_AddTrendReversal.Designer.cs`
- `FinanceManager.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `FinanceManager.Shared/Dtos/Securities/TrendReversalDto.cs`
- `FinanceManager.Shared/Dtos/Admin/NotificationSettingsDto.cs`
- `FinanceManager.Shared/Dtos/Admin/NotificationSettingsRequests.cs`
- `FinanceManager.Shared/IApiClient.cs`
- `FinanceManager.Shared/ApiClient.Securities.cs`
- `FinanceManager.Shared/ApiClient.User.cs`
- `FinanceManager.Web/Controllers/SecuritiesController.cs`
- `FinanceManager.Web/Controllers/UserSettingsController.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Services/TrendReversalLocalizer.cs`
- `FinanceManager.Web/Services/SecurityPriceWorker.cs`
- `FinanceManager.Web/Components/Shared/TrendReversalWidget.razor`
- `FinanceManager.Web/Components/Pages/CardPage.razor`
- `FinanceManager.Web/Components/Pages/Setup/SetupNotificationsTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupNotificationsViewModel.cs`
- `FinanceManager.Web/wwwroot/css/app.ReturnSummaryWidget.css`
- `FinanceManager.Web/Resources/Pages.resx` / `Pages.de.resx` / `Pages.en.resx`
- `FinanceManager.Web/Resources/Components/Shared/TrendReversalWidget.de.resx` / `.en.resx`
- `FinanceManager.Web/Resources/Services/TrendReversalLocalizer.de.resx` / `.en.resx`
- `FinanceManager.Tests/Securities/TrendReversalAnalyzerTests_DeathCross.cs`
- `FinanceManager.Tests/Securities/TrendReversalAnalyzerTests_LineBreak.cs`
- `FinanceManager.Tests/Securities/TrendReversalAnalyzerTests_MarketStructure.cs`
- `FinanceManager.Tests/Securities/TrendReversalAnalyzerTests_Momentum.cs`
- `FinanceManager.Tests/Securities/TrendReversalAnalyzerTests_Scoring.cs`
- `FinanceManager.Tests/Securities/TrendReversalServiceTests.cs`
- `FinanceManager.Tests/Securities/TrendReversalDtoEnumMappingTests.cs`
- `FinanceManager.Tests/Controllers/SecuritiesControllerTests.cs`
- `FinanceManager.Tests/ViewModels/SetupNotificationsViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs`
- `FinanceManager.Tests.E2E/Tests/Securities/SecurityTrendReversalE2ETests.cs`
