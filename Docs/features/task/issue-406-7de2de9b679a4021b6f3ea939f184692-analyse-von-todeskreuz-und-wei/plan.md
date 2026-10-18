# Umsetzungsplan: Analyse von Todeskreuz und weiteren Umkehrsignalen

## Übersicht

Neues Feature „Trendwendesignale" (bärische Umkehrpunkte) für Wertpapiere, parallel zur bestehenden Golden-Cross-Analyse. Ein statischer Analyzer in `FinanceManager.Application` wertet die gespeicherten Schlusskurse aus (Todeskreuz, 200-Tage-Linien-Bruch, Marktstruktur, MACD-Crossover, RSI-Divergenz), aggregiert sie zu einem Trendstatus und liefert das Ergebnis über einen neuen API-Endpunkt an ein Widget auf der Wertpapier-Karteikarte. Zusätzlich werden — wie beim Golden Cross — Home-Page-Benachrichtigungen ausgelöst, sobald sich der Trendstatus verschlechtert (nutzerspezifisch abschaltbar).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Berechnung | Statischer Analyzer `TrendReversalAnalyzer` (Transaction-Script-ähnlicher Rechenkern), Daten laden und mappen via scoped `TrendReversalService` | Identisches Muster wie `GoldenCrossAnalyzer`/`GoldenCrossService`; testbar ohne DB |
| Persistenz „Death-Cross-Date" | Kein DB-Feld; das Kreuzungsdatum wird in `TrendReversalAnalysis`/`TrendReversalDto` als `CrossDate` ausgegeben | Anwenderentscheidung: berechnete Ausgabe genügt; konsistent zur Golden-Cross-Statistik |
| Benachrichtigungen | `EvaluateAndNotifyAsync` auf `ITrendReversalService`, einmalig pro Status und Zyklus via persistiertem `Security.TrendReversalNotifiedStatus`; Benutzereinstellung `User.TrendReversalNotificationsEnabled` | Anwenderentscheidung: Benachrichtigungen gewünscht — exakte Spiegelung des Golden-Cross-Mechanismus |
| Signalbewertung | Gewichteter Score: `ReversalSignal` mit `Strength` (Weak=1, Moderate=2, Strong=3); `SignalScore` = Summe; Status via Schwellen `WeakenedThreshold`/`ReversalLikelyThreshold` | Anforderung 5 verlangt Bewertung „anhand der Anzahl und Stärke" — ein Score-Modell bildet beides ab |
| Widget-Sichtbarkeit | Widget nur sichtbar, wenn mindestens ein Umkehrsignal vorliegt (`Signals.Count > 0`) oder Status `Weakened`/`ReversalLikely`; unsichtbar bei `Intact`/`InsufficientData`/Fehler | Anwenderentscheidung: gleiches Ausblendeverhalten wie `GoldenCrossWidget` |
| `TrendStatus`-Enum | Ablage in `FinanceManager.Domain/Securities/` | Wird auf `Security` persistiert (`TrendReversalNotifiedStatus`) — Domain kann nicht auf Application verweisen; exakt wie `GoldenCrossPhase` |
| Übrige Enums/Records | Im Analyzer-File (Application) bzw. DTO-File (Shared) | Wie `GoldenCrossEntryScenario` & Co. |
| Namespace/Ordner | `Securities/TrendReversal` in Application + Infrastructure; DTOs in `Shared/Dtos/Securities/TrendReversalDto.cs` | Konsistente Schicht- und Ordnerkonvention zum Golden Cross |
| Swing-Erkennung | Eigene private Implementierung im Analyzer (wie `GoldenCrossAnalyzer.AnalyzeTrend`), Lookaround konfigurierbar via Options | Vorhandene Logik ist `private` und nicht wiederverwendbar; Extraktion wäre Refactoring außerhalb der Anforderung |
| EMA/RSI | Eigene Implementierung (EMA: `k = 2/(p+1)`; RSI: Wilder-Glättung) | Keine Indikator-Bibliothek im Projekt; kein neues NuGet nötig |

## Programmabläufe

### Trendwendeanalyse pro Wertpapier (API-Abfrage)

1. `SecuritiesController.GetTrendReversalAsync(id, ct)` ruft `ITrendReversalService.GetAsync(id, _current.UserId, ct)`.
2. `TrendReversalService.GetAsync` lädt `Security` mit Owner-Check (`AsNoTracking`); `null` bei Fremdzugriff.
3. `AnalyzeAsync` lädt die `LongWindow * 3` neuesten `SecurityPrices` (desc → reverse → chronologisch) — Muster aus `GoldenCrossService`.
4. `TrendReversalAnalyzer.Analyze(series, options)`:
   - `n < LongWindow` → `Status = InsufficientData`, nur `AsOfDate`/`LastClose`.
   - SMA50/SMA200 via Präfixsummen; `DistancePercent` = (short−long)/long·100.
   - **Todeskreuz:** Wenn `shortAvg < longAvg`: Rückwärtssuche nach dem letzten Index, an dem `prevShort >= prevLong` galt → `CrossDate` („Death-Cross-Date"), `DaysSinceCross`. Signal `DeathCross` (Strong).
   - **200-Tage-Bruch:** Wenn `lastClose < longAvg`: Anzahl aufeinanderfolgender Schlusskurse unter ihrer jeweiligen Tages-SMA200 von hinten zählen → `ConsecutiveDaysBelow`, `BreakDate` = erster Tag der Sequenz, `PercentBelow`. Klassifikation: `>= StructuralBreakDays` → `StructuralReversal` (Strong), sonst `ShortTermWeakness` (Weak). Liegt der Kurs über der Linie → `Classification = None`, kein Signal.
   - **Marktstruktur:** Swing-Highs/Lows (`SwingLookaround`) über die letzten `MarketStructureLookbackDays` Handelstage. `LowerHighs`/`LowerLows` zählen abwärts gerichtete Swing-Paare. Klassifikation: letzte zwei Hochs UND letzte zwei Tiefs niedriger → `EmergingDowntrend` (Strong); nur Tiefs niedriger → `TrendLoss` (Moderate); sonst negative Veränderung im Zeitraum → `SidewaysBearish` (Weak); sonst `Intact`; weniger als 2 Swings einer Seite → `Unknown` (kein Signal).
   - **MACD:** `macd = EMA(MacdFastPeriod) − EMA(MacdSlowPeriod)`; `signal = EMA(MacdSignalPeriod)` über die MACD-Reihe. Letztes Crossover von `macd >= signal` nach `macd < signal` suchen → `CrossoverDate`; Signal `MacdBearishCrossover` (Moderate) wenn aktuell `macd < signal`.
   - **RSI:** Wilder-RSI(`RsiPeriod`). Bärische Divergenz: letztes Swing-High im Kurs höher als vorheriges, aber RSI am zweiten Hoch niedriger → Signal `RsiBearishDivergence` (Moderate) mit `DivergenceDate`.
   - **Aggregation:** `Signals` = Liste aller erkannten `ReversalSignal` (Type, Strength, Date); `SignalScore` = Summe der Stärken; `Status`: 0 → `Intact`, `< ReversalLikelyThreshold` → `Weakened`, `>= ReversalLikelyThreshold` → `ReversalLikely`.
5. Service mappt Analyzer-Records auf DTOs (int-Cast-Enum-Mapping wie `GoldenCrossService`) inkl. `CurrencyCode`, gibt `TrendReversalDto` zurück; Controller liefert 200 bzw. 404.

### Benachrichtigung bei verschlechtertem Trendstatus (Worker)

1. `SecurityPriceWorker.RunOnceAsync` ruft nach dem Speichern neuer Kurse zusätzlich `trendReversal.EvaluateAndNotifyAsync(sec.Id, ct)` auf (direkt nach dem `goldenCross`-Aufruf, gleiches Fehler-Handling: loggen, nicht abbrechen).
2. `TrendReversalService.EvaluateAndNotifyAsync`:
   - `Status = InsufficientData` → nichts.
   - `Status = Intact` → falls `security.TrendReversalNotifiedStatus != Intact`: auf `Intact` zurücksetzen (Zyklenende), `SaveChanges`; keine Benachrichtigung.
   - `Status <= security.TrendReversalNotifiedStatus` → bereits benachrichtigt, nichts.
   - Sonst: `SetTrendReversalNotifiedStatus(status)` + `SaveChanges`; Benutzereinstellung `TrendReversalNotificationsEnabled` prüfen; Notification via `INotificationWriter.CreateForUserAsync` (`NotificationType.EventDriven`, `NotificationTarget.HomePage`, TriggerKey `security:trend-reversal:{id}`); Texte via `ITrendReversalLocalizer` in `PreferredLanguage` des Users; Titel/Message-Keys `TrendReversal_{Weakened|ReversalLikely}_{Title,Message}` mit Signalanzahl/-namen als Argumente.
3. Reihenfolge der Stati für den Vergleich: `Intact < Weakened < ReversalLikely` (Enum-Werte entsprechend aufsteigend; `InsufficientData = 0` wird vor dem Vergleich abgefangen).

### Widget-Anzeige auf der Wertpapier-Karte

1. `CardPage.razor` rendert für `Kind == "securities"` zusätzlich `<TrendReversalWidget SecurityId="Id" />` (nach `GoldenCrossWidget`).
2. `TrendReversalWidget.OnParametersSetAsync` ruft `ApiClient.Securities_GetTrendReversalAsync(Id)`; bei Fehler, `InsufficientData` oder `Intact` ohne Signale bleibt das Widget leer (wie `GoldenCrossWidget`).
3. Anzeige (nur wenn Signale vorliegen): Titel + Status-Badge (`Weakened`/`ReversalLikely` lokalisiert), je Signal eine Zeile mit lokalisiertem Namen + Datum; Info-Button öffnet Side-Panel (Pattern `rsw-panel`) mit Bedeutungstexten je Signaltyp, Statuserklärung, Formelbox (SMA/MACD/RSI) und Disclaimer.

### Benachrichtigungs-Einstellung (Setup)

1. `SetupNotificationsTab.razor`: neue Checkbox `Notifications_TrendReversal` (Label + Hilfetext aus `Pages.*.resx`, Muster `Notifications_GoldenCross`).
2. `SetupNotificationsViewModel`: `Model.TrendReversalNotificationsEnabled` in Dirty-Check (`OnChanged`/Zeile ~229), Load (Zeile ~245) und Save (`User_UpdateNotificationSettingsAsync`-Aufruf, Zeile ~138) aufnehmen.
3. `UserSettingsController`: `NotificationSettingsDto`/`NotificationSettingsRequests` um `TrendReversalNotificationsEnabled` erweitern; `user.SetTrendReversalNotificationsEnabled(req.TrendReversalNotificationsEnabled)` (Zeile ~225).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `FinanceManager.Domain/Securities/TrendStatus.cs` → `TrendStatus` | Enum | `InsufficientData = 0`, `Intact = 1`, `Weakened = 2`, `ReversalLikely = 3` — persistiert auf `Security` |
| `FinanceManager.Application/Securities/TrendReversal/TrendReversalOptions.cs` → `TrendReversalOptions` | Optionsklasse | `SectionName = "TrendReversal"`, `ShortWindow` (50), `LongWindow` (200), `StructuralBreakDays` (5), `MacdFastPeriod` (12), `MacdSlowPeriod` (26), `MacdSignalPeriod` (9), `RsiPeriod` (14), `MarketStructureLookbackDays` (120), `SwingLookaround` (3), `ReversalLikelyThreshold` (4) |
| `FinanceManager.Application/Securities/TrendReversal/TrendReversalAnalyzer.cs` → `TrendReversalAnalyzer` | statische Klasse | `Analyze(IReadOnlyList<(DateTime Date, decimal Close)>, TrendReversalOptions)` → `TrendReversalAnalysis` |
| `TrendReversalSignalType` | Enum (im Analyzer-File) | `DeathCross`, `LongTermLineBreak`, `BearishMarketStructure`, `MacdBearishCrossover`, `RsiBearishDivergence` |
| `TrendReversalSignalStrength` | Enum | `Weak = 1`, `Moderate = 2`, `Strong = 3` (Werte = Score-Beitrag) |
| `LineBreakClassification` | Enum | `None`, `ShortTermWeakness`, `StructuralReversal` |
| `MarketStructureClassification` | Enum | `Unknown`, `Intact`, `SidewaysBearish`, `TrendLoss`, `EmergingDowntrend` |
| `ReversalSignal` | Record | `Type`, `Strength`, `Date` — ein erkanntes Signal für die Übersicht |
| `DeathCrossResult` | Record | `CrossDate`, `DaysSinceCross`, `Active` |
| `LongTermLineBreakResult` | Record | `Classification`, `BreakDate`, `ConsecutiveDaysBelow`, `PercentBelow` |
| `MarketStructureResult` | Record | `Classification`, `LowerHighs`, `LowerLows`, `SwingHighs`, `SwingLows`, `ChangePercent` |
| `MacdResult` | Record | `Macd`, `Signal`, `Histogram`, `BearishCrossover`, `CrossoverDate` |
| `RsiResult` | Record | `Rsi`, `BearishDivergence`, `DivergenceDate` |
| `TrendReversalAnalysis` | Record | Aggregat: `Status`, `AsOfDate`, `ShortAverage`, `LongAverage`, `DistancePercent`, `LastClose`, Teil-Records, `Signals`, `SignalScore` |
| `FinanceManager.Application/Securities/TrendReversal/ITrendReversalService.cs` → `ITrendReversalService` | Interface | `GetAsync(...)` → `Task<TrendReversalDto?>`; `EvaluateAndNotifyAsync(Guid securityId, CancellationToken ct)` → `Task<bool>` |
| `FinanceManager.Application/Securities/TrendReversal/ITrendReversalLocalizer.cs` → `ITrendReversalLocalizer` | Interface | `Format(key, cultureName, args)` — wie `IGoldenCrossLocalizer` |
| `FinanceManager.Infrastructure/Securities/TrendReversal/TrendReversalService.cs` → `TrendReversalService` | Klasse (scoped) | DB-Zugriff + DTO-Mapping + Benachrichtigungszyklus; `TriggerKeyPrefix = "security:trend-reversal:"` |
| `FinanceManager.Web/Services/TrendReversalLocalizer.cs` → `TrendReversalLocalizer` | Klasse | Web-Implementierung via `IStringLocalizer` (Muster `GoldenCrossLocalizer`) |
| `FinanceManager.Web/Resources/Services/TrendReversalLocalizer.de.resx` / `.en.resx` | Ressourcen | `TrendReversal_Weakened_{Title,Message}`, `TrendReversal_ReversalLikely_{Title,Message}` |
| `FinanceManager.Shared/Dtos/Securities/TrendReversalDto.cs` | DTO-File | `TrendReversalDto` + DTO-Enums/Records (`TrendStatusDto`, `TrendReversalSignalTypeDto`, `TrendReversalSignalStrengthDto`, `LineBreakClassificationDto`, `MarketStructureClassificationDto`, `ReversalSignalDto`, `DeathCrossResultDto`, `LongTermLineBreakResultDto`, `MarketStructureResultDto`, `MacdResultDto`, `RsiResultDto`) — int-gleiche Spiegelung |
| `FinanceManager.Web/Components/Shared/TrendReversalWidget.razor` | Razor-Komponente | Anzeige der Signale + Status + Info-Panel |
| `FinanceManager.Web/Resources/Components/Shared/TrendReversalWidget.de.resx` / `.en.resx` | Ressourcen | Alle Widget-Texte de/en |

## Änderungen an bestehenden Klassen

### `Security` (Domain)

- **Neue Eigenschaft:** `TrendReversalNotifiedStatus` (`TrendStatus`, private set, Default `TrendStatus.Intact`)
- **Neue Methode:** `SetTrendReversalNotifiedStatus(TrendStatus status)` — Muster `SetGoldenCrossNotifiedPhase`.

### `User` (Domain, `User.Notifications.cs`)

- **Neue Eigenschaft:** `TrendReversalNotificationsEnabled` (`bool`, private set, Default `true`)
- **Neue Methode:** `SetTrendReversalNotificationsEnabled(bool enabled)` — Muster `SetGoldenCrossNotificationsEnabled`.

### `AppDbContext` (Infrastructure)

- `Security`: `TrendReversalNotifiedStatus` mit `HasConversion<string>()`, `HasDefaultValue(TrendStatus.Intact)`, `HasSentinel(TrendStatus.Intact)` (Zeile ~519, neben `GoldenCrossNotifiedPhase`).
- `User`: `TrendReversalNotificationsEnabled` mit `HasDefaultValue(true)`, `IsRequired()` (Zeile ~356).

### `TrendReversalService` → Benachrichtigung

siehe Programmablauf „Benachrichtigung"; zieht `INotificationWriter`, `ITrendReversalLocalizer`, `IOptions<TrendReversalOptions>`, `ILogger`.

### `SecuritiesController` (Controller)

- **Ctor:** zusätzlicher Parameter `ITrendReversalService trendReversal` (Feld `_trendReversal`).
- **Neue Methode:** `GetTrendReversalAsync(Guid id, CancellationToken ct)` — `GET api/securities/{id}/trend-reversal`, 200 mit `TrendReversalDto`, 404 bei `null`.

### `IApiClient` (Interface)

- **Neue Methode:** `Securities_GetTrendReversalAsync(Guid id, CancellationToken ct)` → `Task<TrendReversalDto?>`.
- **Geänderte Signatur:** `User_UpdateNotificationSettingsAsync` um `trendReversalNotificationsEnabled`-Parameter ergänzen (entsprechend `NotificationSettingsRequests`).

### `ApiClient.Securities.cs` / `ApiClient.User.cs` (Shared)

- `Securities_GetTrendReversalAsync` implementieren (Muster `Securities_GetGoldenCrossAsync`).
- `User_UpdateNotificationSettingsAsync` um neuen Request-Parameter erweitern.

### `UserSettingsController` (Web)

- Get-Endpoint: `TrendReversalNotificationsEnabled` in die DTO-Projektion (Zeile ~191) aufnehmen.
- Update-Endpoint: `user.SetTrendReversalNotificationsEnabled(req.TrendReversalNotificationsEnabled)` (Zeile ~225).

### `NotificationSettingsDto` / `NotificationSettingsRequests` (Shared, `Dtos/Admin/`)

- Je neue `bool`-Eigenschaft `TrendReversalNotificationsEnabled`.

### `SetupNotificationsViewModel` (Web)

- `Model.TrendReversalNotificationsEnabled` in Load (Zeile ~245), Dirty-Check (Zeile ~229) und Save-Aufruf (Zeile ~138) aufnehmen.

### `SetupNotificationsTab.razor` (Web)

- Neue Checkbox-Zeile `Notifications_TrendReversal` mit `Notifications_TrendReversal_Help` (Muster Zeilen 38–41).

### `Pages.resx` / `Pages.de.resx` / `Pages.en.resx` (Web)

- `Notifications_TrendReversal` + `Notifications_TrendReversal_Help` de/en.

### `SecurityPriceWorker` (Web)

- `ITrendReversalService` im Scope auflösen; nach dem `goldenCross.EvaluateAndNotifyAsync`-Block analogen Aufruf mit eigenem try/catch hinzufügen.

### `ServiceCollectionExtensions` (Infrastructure)

- `services.AddScoped<ITrendReversalService, TrendReversalService>();` neben der Golden-Cross-Registrierung.

### `ProgramExtensions` (Web)

- `builder.Services.AddScoped<ITrendReversalLocalizer, TrendReversalLocalizer>();`
- `builder.Services.Configure<TrendReversalOptions>(builder.Configuration.GetSection(TrendReversalOptions.SectionName));`

### `CardPage.razor` (Page)

- `<TrendReversalWidget SecurityId="Id" />` im bestehenden `securities`-Block.

### `app.ReturnSummaryWidget.css`

- Neue `trw-*`-Klassen analog `gcw-*` (Status-Farben: `trw-weakened`, `trw-reversal`, Signal-Listenzeilen).

### `SecuritiesControllerTests` (Test)

- `Create()`: zusätzlicher `Mock<ITrendReversalService>` im Ctor-Aufruf.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddTrendReversal` | `Securities.TrendReversalNotifiedStatus` (string, Default `Intact`), `Users.TrendReversalNotificationsEnabled` (bool, Default `true`) | Notification-Zyklusstatus + Benutzereinstellung (Muster `AddGoldenCross`) |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `TrendReversalOptions` | `ShortWindow`, `LongWindow`, `StructuralBreakDays`, MACD-/RSI-Perioden > 0; `ShortWindow < LongWindow`; `MacdFastPeriod < MacdSlowPeriod` | `ArgumentException` in `Analyze` (Analog `GoldenCrossAnalyzer`) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `TrendReversal:*` (Optionssektion) | `TrendReversalOptions` | siehe Defaults oben | Fensterlängen, Schwellen, Lookbacks — optional in `appsettings.json` übersteuerbar; kein Eintrag nötig |

## Seiteneffekte und Risiken

- **SecuritiesController-Ctor:** neuer Pflichtparameter → `SecuritiesControllerTests.Create()` muss angepasst werden; DI-Registrierung ist Pflicht.
- **`User_UpdateNotificationSettingsAsync`-Signatur:** wächst um einen Parameter → alle Aufrufer (`SetupNotificationsViewModel`) und ggf. Mock-basierte Tests müssen angepasst werden.
- **Migration `AddTrendReversal`:** laufende Installationen erhalten zwei neue Spalten — mit Defaults abwärtskompatibel.
- **`SecurityPriceWorker`:** ein weiterer Service-Call pro Wertpapier und Lauf; Fehler werden wie beim Golden Cross geloggt und brechen den Lauf nicht.
- **CardPage:** zusätzlicher API-Call pro geöffneter Wertpapierkarte; Fehler werden im Widget geschluckt.
- **Datenmenge:** bis zu 600 Kurszeilen pro Aufruf (wie Golden Cross) — kein neues Lastprofil.
- **Golden-Cross-Code:** bleibt unverändert; kein Shared-Refactoring.

## Umsetzungsreihenfolge

1. **`TrendStatus` (Domain) + `TrendReversalOptions` + Enums + Records + `TrendReversalAnalyzer` anlegen** (`FinanceManager.Application/Securities/TrendReversal/`)
   - Voraussetzungen: Keine
   - Beschreibung: Rechenkern mit allen fünf Signalen und Score/Status-Aggregation.
2. **`Security.TrendReversalNotifiedStatus` + `User.TrendReversalNotificationsEnabled` + `AppDbContext`-Konfiguration + Migration `AddTrendReversal`**
   - Voraussetzungen: Schritt 1 (`TrendStatus` in Domain)
   - Beschreibung: `dotnet ef migrations add AddTrendReversal` (Muster `AddGoldenCross`).
3. **`TrendReversalDto` + DTO-Enums in `FinanceManager.Shared`**
   - Voraussetzungen: Schritt 1 (Enum-Namen/-Werte)
4. **`ITrendReversalService` + `ITrendReversalLocalizer` (Application) + `TrendReversalService` (Infrastructure) + DI-Registrierung**
   - Voraussetzungen: Schritte 1–3
5. **Controller-Endpunkt + `IApiClient.Securities_GetTrendReversalAsync`**
   - Voraussetzungen: Schritt 4; `SecuritiesControllerTests.Create()` anpassen
6. **Benachrichtigungs-Einstellung: `NotificationSettingsDto`/`Requests`, `UserSettingsController`, `ApiClient.User`, `IApiClient`, `SetupNotificationsViewModel`, `SetupNotificationsTab.razor`, `Pages.*.resx`**
   - Voraussetzungen: Schritt 2
7. **`TrendReversalLocalizer` (Web) + resx + `SecurityPriceWorker`-Aufruf**
   - Voraussetzungen: Schritt 4
8. **`TrendReversalWidget.razor` + resx de/en + CSS + `CardPage`-Einbindung**
   - Voraussetzungen: Schritt 5
   - Beschreibung: Muster `GoldenCrossWidget`/`ReturnSummaryWidget` (rsw-Klassen, Panel, Localizer).
9. **Unit-Tests** (siehe Tests)
   - Voraussetzungen: Schritte 1–7
10. **E2E-Test** (siehe E2E-Tests)
    - Voraussetzungen: Schritte 6, 8
11. **Dokumentation** `Docs/help/wertpapiermanagement/` (neue Datei `umkehrsignale.md`, `index.md`-Verlinkung, ggf. `business-rules.md`)
    - Voraussetzungen: Schritt 8

## Tests

### Neue Tests

Testklassen nach Themengruppen aufgeteilt (`{Klasse}Tests_{Thema}`), alle unter `FinanceManager.Tests/Securities/`:

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Analyze_BelowLongAverage_FindsDeathCrossDate` | `TrendReversalAnalyzerTests_DeathCross` | Kreuzungsdatum bei Übergang von oben nach unten |
| `Analyze_ShortAboveLong_NoDeathCross` | `TrendReversalAnalyzerTests_DeathCross` | Kein Signal, wenn 50er über 200er |
| `Analyze_TooFewPrices_ReturnsInsufficientData` | `TrendReversalAnalyzerTests_DeathCross` | `Status = InsufficientData` bei < LongWindow |
| `Analyze_InvalidOptions_Throws` | `TrendReversalAnalyzerTests_DeathCross` | Optionsvalidierung |
| `Analyze_CloseBelowLongFewDays_ShortTermWeakness` | `TrendReversalAnalyzerTests_LineBreak` | < StructuralBreakDays → `ShortTermWeakness`, `ConsecutiveDaysBelow`, `BreakDate` |
| `Analyze_CloseBelowLongManyDays_StructuralReversal` | `TrendReversalAnalyzerTests_LineBreak` | >= Schwelle → `StructuralReversal` |
| `Analyze_CloseAboveLong_NoBreak` | `TrendReversalAnalyzerTests_LineBreak` | `Classification = None`, kein Signal |
| `Analyze_LowerHighsAndLows_EmergingDowntrend` | `TrendReversalAnalyzerTests_MarketStructure` | zwei niedrigere Hochs + Tiefs → `EmergingDowntrend` |
| `Analyze_LowerLowsOnly_TrendLoss` | `TrendReversalAnalyzerTests_MarketStructure` | nur niedrigere Tiefs → `TrendLoss` |
| `Analyze_NegativeDriftWithoutStructure_SidewaysBearish` | `TrendReversalAnalyzerTests_MarketStructure` | negative Veränderung → `SidewaysBearish` |
| `Analyze_Uptrend_Intact` | `TrendReversalAnalyzerTests_MarketStructure` | höhere Hochs/Tiefs → `Intact`, kein Signal |
| `Analyze_MacdCrossesBelowSignal_BearishCrossover` | `TrendReversalAnalyzerTests_Momentum` | `BearishCrossover = true`, `CrossoverDate` gesetzt |
| `Analyze_MacdAboveSignal_NoCrossover` | `TrendReversalAnalyzerTests_Momentum` | kein Signal |
| `Analyze_RsiLowerHighAtHigherPriceHigh_BearishDivergence` | `TrendReversalAnalyzerTests_Momentum` | Divergenz erkannt, `DivergenceDate` |
| `Analyze_RsiConfirmsHighs_NoDivergence` | `TrendReversalAnalyzerTests_Momentum` | kein Signal |
| `Analyze_NoSignals_StatusIntact` | `TrendReversalAnalyzerTests_Scoring` | Score 0 → `Intact`, leere `Signals` |
| `Analyze_SingleWeakSignal_StatusWeakened` | `TrendReversalAnalyzerTests_Scoring` | Score < Schwelle → `Weakened` |
| `Analyze_CombinedStrongSignals_StatusReversalLikely` | `TrendReversalAnalyzerTests_Scoring` | Score >= Schwelle → `ReversalLikely`, `Signals` enthält alle |
| `GetAsync_ForOwner_ReturnsDto` | `TrendReversalServiceTests` | Owner-Scope, Mapping, `CurrencyCode` |
| `GetAsync_ForOtherUser_ReturnsNull` | `TrendReversalServiceTests` | kein Datenleck |
| `GetAsync_InsufficientData_ReturnsStatus` | `TrendReversalServiceTests` | `StatusDto.InsufficientData` bei wenig Daten |
| `EvaluateAndNotify_Weakened_NotifiesOnlyOncePerCycle` | `TrendReversalServiceTests` | einmalige Benachrichtigung je Status |
| `EvaluateAndNotify_WeakenedThenReversalLikely_NotifiesEachStatusOnce` | `TrendReversalServiceTests` | Benachrichtigung je Statusstufe |
| `EvaluateAndNotify_ReversalLikelyThenWeakened_DoesNotNotifyAgain` | `TrendReversalServiceTests` | kein Downgrade-Notify im selben Zyklus |
| `EvaluateAndNotify_AfterReturningToIntact_NotifiesNewCycle` | `TrendReversalServiceTests` | Zyklus-Reset bei `Intact` |
| `EvaluateAndNotify_Intact_DoesNotNotify` | `TrendReversalServiceTests` | intakter Trend benachrichtigt nie |
| `EvaluateAndNotify_InsufficientData_DoesNothing` | `TrendReversalServiceTests` | Zustand unverändert |
| `EvaluateAndNotify_WhenDisabledByUser_TracksStatusButDoesNotNotify` | `TrendReversalServiceTests` | Status wird getrackt, keine Notification |
| `User_TrendReversalNotificationsEnabled_DefaultsToTrue` | `TrendReversalServiceTests` | Default + Setter |
| Enum-Mapping `[Theory]` je Enum-Paar + MemberCount | `TrendReversalDtoEnumMappingTests` | int-Mapping Domain↔DTO |
| `GetTrendReversal_UnknownOrForeign_ReturnsNotFound` | `SecuritiesControllerTests` | 404-Pfad des neuen Endpunkts |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SecuritiesControllerTests.Create()` | `SecuritiesController`-Ctor erhält `ITrendReversalService` |
| Tests rund um `User_UpdateNotificationSettingsAsync`/`SetupNotificationsViewModel` (soweit vorhanden) | Signatur-/Modell-Erweiterung um `TrendReversalNotificationsEnabled` |

### E2E-Tests (primärer Funktionsnachweis)

Benutzerflüsse: Wertpapier-Karte öffnen → Umkehrsignal-Widget sehen; Setup-Checkbox für die Benachrichtigung umschalten. Testdaten werden direkt in die SQLite-Datenbank der E2E-Fixture geschrieben (`new TestUserSeeder(_fixture.DatabasePath)`, eigener `AppDbContext` auf `_fixture.DatabasePath` mit `db.Securities`/`db.SecurityPrices`), Navigation via `AuthGateway`/`page.GotoAsync("/card/securities/{id}")`.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Karte mit bärisch verlaufenden Kursen zeigt Widget mit Trendstatus und mindestens einem gelisteten Umkehrsignal | `FinanceManager.Tests.E2E/Tests/Securities/SecurityTrendReversalE2ETests.cs` | Strukturierte Ausgabe der erkannten Umkehrsignale + Trendstatus | Die Anzeige ist der einzige vom Anwender sichtbare Teil der Anforderung |
| Pflicht | Karte mit intaktem Aufwärtstrend zeigt kein Umkehrsignal-Widget | `SecurityTrendReversalE2ETests` | Ausblendeverhalten bei fehlendem Signal | Entscheidung des Anwenders: Widget nur bei Signalen sichtbar |
| Pflicht | Setup-Tab „Benachrichtigungen" enthält Checkbox für Umkehrsignal-Hinweise; Umschalten + Speichern persistiert den Wert | `SecurityTrendReversalE2ETests` (oder bestehende Setup-Testklasse) | Neue Benutzereinstellung über UI erreichbar | Sichtbarer neuer Benutzerfluss in den Einstellungen |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine bestehenden E2E-Tests betroffen | — |

## Offene Punkte

Keine.
