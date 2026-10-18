# Logik

## `GoldenCrossAnalyzer` (statisch, reiner Rechenkern)
Datei: `FinanceManager.Application/Securities/GoldenCross/GoldenCrossAnalyzer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Analyze(IReadOnlyList<(DateTime, decimal)>, GoldenCrossOptions)` | public static | SMA-Kurz/Lang, Phase, Kreuzungsdatum, Einstiegsfenster, Retest, Trendstruktur |
| `BuildWindow(...)` | private static | Baut ein `GoldenCrossEntryWindow` |
| `AnalyzeRetest(...)` | private static | Pullback-/Retest-Auswertung (TouchTolerance 1 %) |
| `AnalyzeTrend(...)` | private static | Swing-Highs/Lows (`SwingLookaround = 3`), `MinTrendDays = 10`, Klassifikation StableUptrend/WeakUptrend/Sideways |

Konstanten: `EarlyEntryFromDays/ToDays` (0–5), `AverageEntryFromDays/ToDays` (5–30), `LateEntryFromDays/ToDays` (30–60). SMA per Präfixsummen (`prefix[]`). Kreuzungsdatum = letzter Index, an dem vorher `short < long` und jetzt `short >= long` gilt (Rückwärtssuche von Serienende).

Records im selben File: `GoldenCrossEntryWindow`, `GoldenCrossRetest`, `GoldenCrossTrend`, `GoldenCrossAnalysis` (mit `Windows`-Property).

## `GoldenCrossService` (infrastructure, scoped)
Datei: `FinanceManager.Infrastructure/Securities/GoldenCross/GoldenCrossService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync(securityId, ownerUserId, ct)` | public | Owner-Check, `AnalyzeAsync`, Mapping Domain→DTO (`(DtoEnum)(int)domainEnum`) |
| `EvaluateAndNotifyAsync(securityId, ct)` | public | Zykluslogik via `GoldenCrossNotifiedPhase`; Notification via `INotificationWriter`; Texte via `IGoldenCrossLocalizer` |
| `AnalyzeAsync(securityId, ct)` | private | Lädt `LongWindow * 3` neueste `SecurityPrices` (desc → reverse → chronologisch) |

`TriggerKeyPrefix = "security:golden-cross:"` für Notification-Deduplizierung.

## `SecurityPriceWorker` (BackgroundService)
Datei: `FinanceManager.Web/Services/SecurityPriceWorker.cs`

- `RunOnceAsync` ruft nach dem Speichern neuer Kurse `goldenCross.EvaluateAndNotifyAsync(sec.Id, ct)` auf (Zeile 179), Fehler werden nur geloggt.
- Kein Bezug zu Umkehrsignalen.

## `SecuritiesController` (Web)
Datei: `FinanceManager.Web/Controllers/SecuritiesController.cs`

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `GetGoldenCrossAsync(id, ct)` | `GET api/securities/{id}/golden-cross` | Liefert `GoldenCrossDto`, 404 wenn nicht gefunden/fremd |
| `GetReturnSummaryAsync` | `GET api/securities/{id}/return-summary` | Vorbild analoger Statistik-Endpunkt (`IReturnAnalysisService`) |

Ctor injiziert u. a. `IGoldenCrossService _goldenCross` (Zeile 48/79).

## `GoldenCrossLocalizer` (Web)
Datei: `FinanceManager.Web/Services/GoldenCrossLocalizer.cs`

- Implementiert `IGoldenCrossLocalizer.Format(key, cultureName, args)` mittels `IStringLocalizer<GoldenCrossLocalizer>` + temporärem Culture-Switch. Ressourcen: `Resources/Services/GoldenCrossLocalizer.{de,en}.resx` (Keys `GoldenCross_{Approaching,Crossed}_{Title,Message}`).

## `ReturnAnalysisService` / `IReturnAnalysisService`
`FinanceManager.Application/Securities/ReturnAnalysis/` — liefert Return-Summary/Sparkline für dasselbe Widget-Layout (`ReturnSummaryWidget`). Zweites Referenzmuster für „berechnete Statistik pro Wertpapier".
