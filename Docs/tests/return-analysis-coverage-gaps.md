# Testlücken: Renditeanalyse-Feature

> **Stand:** 2026-04-25 (Automatisierte Quellcode-Analyse)  
> **Analysierte Quelldateien:**  
> - `FinanceManager.Application/Securities/ReturnAnalysis/ReturnCalculationService.cs`  
> - `FinanceManager.Application/Securities/ReturnAnalysis/FifoCostBasisCalculator.cs`  
> - `FinanceManager.Application/Securities/ReturnAnalysis/ReturnAnalysisInputTypes.cs`  
> - `FinanceManager.Application/Securities/ReturnAnalysis/ReturnAnalysisDtos.cs`  
> - `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs`  
> - `FinanceManager.Infrastructure/Securities/ReturnAnalysis/MemoryReturnAnalysisCache.cs`  
> - `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisCacheKeys.cs`  
> - `FinanceManager.Web/Controllers/SecuritiesController.cs` (Return-Analysis-Endpunkte)  
> - `FinanceManager.Web/Components/Shared/ReturnSummaryWidget.razor`  
> - `FinanceManager.Web/Components/Pages/Securities/SecurityPerformancePage.razor`  
>
> **Vorhandene Testdateien:**  
> - `FinanceManager.Tests/Securities/ReturnCalculationServiceTests.cs` — 24 Tests vorhanden  
> - `FinanceManager.Tests/Securities/FifoCostBasisCalculatorTests.cs` — 9 Tests vorhanden  
> - `FinanceManager.Tests/Securities/ReturnAnalysisCacheTests.cs` — 4 Tests vorhanden  
> - `FinanceManager.Tests.Integration/ApiClient/ApiClientSecuritiesTests.cs` — keine Return-Analysis-Tests enthalten

---

## 1. `ReturnCalculationService`

### 1.1 `CalculateDividendYield` — vollständig ungetestet

- Keine einzige Test-Methode für `CalculateDividendYield` vorhanden.
- Fehlendes Szenario: Normalfall – `totalDividends > 0`, `investedCapital > 0` → Dividendenrendite wird korrekt berechnet
- Fehlendes Szenario: `investedCapital == 0` → Methode muss `null` zurückgeben (Division-by-Zero-Guard)
- Fehlendes Szenario: `totalDividends == 0` → Dividendenrendite = 0 (kein Fehler erwartet)
- Fehlendes Szenario: Negativer `totalDividends`-Wert (z. B. Rückbuchung)

### 1.2 `CalculateTaxRate` — vollständig ungetestet

- Keine einzige Test-Methode für `CalculateTaxRate` vorhanden.
- Fehlendes Szenario: Normalfall – `totalTaxes > 0`, `grossReturn > 0` → Steuerquote wird korrekt berechnet
- Fehlendes Szenario: `grossReturn == 0` → Methode muss `null` zurückgeben (Division-by-Zero-Guard)
- Fehlendes Szenario: `grossReturn < 0` (Verlust) – `Math.Abs(grossReturn)` im Nenner; Steuerquote trotzdem sinnvoll?
- Fehlendes Szenario: `totalTaxes == 0` → Steuerquote = 0

### 1.3 `CalculateTotalReturn` — Randfall fehlt

- Fehlendes Szenario: `netDividends < 0` (negative Nettodividende durch hohe Quellensteuern) – Gesamtrendite kann dadurch unter `(MarketValue - InvestedCapital) / InvestedCapital` sinken

### 1.4 `CalculateIrr` — Randfälle und Pfade fehlen

- Fehlendes Szenario: Genau 1 Cashflow → Methode muss `null` zurückgeben (Guard `cashflows.Count < 2`)
- Fehlendes Szenario: Leere Cashflow-Liste (`Count == 0`) → muss `null` zurückgeben
- Fehlendes Szenario: `null`-Eingabe → muss `null` zurückgeben
- Fehlendes Szenario: Newton-Raphson läuft in den Bisektions-Fallback und konvergiert dort (kein Test für erfolgreiche Bisektion)
- Fehlendes Szenario: Cashflows liegen alle am selben Tag (`years[i] == 0.0` für alle) – NpvDerivative überspringt alle → `derivative == 0.0` → Bisektion

### 1.5 `CalculateCagr` — Randfälle fehlen

- Fehlendes Szenario: `years < 0` (negativer Zeitraum) → Methode muss `null` zurückgeben
- Fehlendes Szenario: `endValue == 0` → `(0/startValue)^(1/years) - 1 = -1` → kein NaN, aber extremer Wert; wird korrekt zurückgegeben?
- Fehlendes Szenario: `endValue < 0` → `Math.Pow` mit negativer Basis und nicht-ganzzahligem Exponenten → `NaN` → muss `null` zurückgeben

### 1.6 `CalculateVolatility` — Randfälle fehlen

- Fehlendes Szenario: `null`-Eingabe → muss `null` zurückgeben
- Fehlendes Szenario: Preisserie enthält Nullwerte oder negative Preise (`<= 0`) → Log-Return wird übersprungen; wenn alle übersprungen werden und `validCount < 2` → muss `null` zurückgeben
- Fehlendes Szenario: Genau 2 Preise — **bestätigter Verhaltenshinweis:** Die Methode prüft am Eingang `Count < 2`, berechnet dann 1 Log-Return, setzt aber `validCount` auf 1, und der nachgelagerte Guard `if (validCount < 2) return null` greift → **Methode benötigt mindestens 3 Preise für ein Ergebnis**, obwohl der initiale Guard nur `< 2` prüft. Dieser Unterschied ist undokumentiert und ungetestet.
- Fehlendes Szenario: Genau 3 Preise (effektives Minimum für valides Ergebnis)
- Fehlendes Szenario: Sehr große Preisschwankungen (numerische Stabilität)

### 1.7 `CalculateTwr` — Randfälle fehlen

- Fehlendes Szenario: `null`-Eingabe → muss `null` zurückgeben
- Fehlendes Szenario: Alle Perioden haben Null-Denominator → kein valider Zeitraum → `null`
- Fehlendes Szenario: Negative externe Cashflows (Teilentnahme) – negativer `ExternalCashflow`-Wert

### 1.8 `CalculateMaxDrawdown` — Randfälle fehlen

- Fehlendes Szenario: `null`-Eingabe → muss `null` zurückgeben
- Fehlendes Szenario: Alle Werte identisch → MaxDrawdown = 0
- Fehlendes Szenario: Erster Wert = 0 (`peak = 0.0`) → Guard `peak > 0.0` verhindert Division; kein Drawdown berechnet → Ergebnis = 0m

### 1.9 `CalculateSharpeRatio` — Randfälle fehlen

- Fehlendes Szenario: Negative Volatilität als Eingabe (sollte defensiv behandelt werden)
- Fehlendes Szenario: `annualisedReturn == riskFreeRate` → Sharpe Ratio = 0

---

## 2. `FifoCostBasisCalculator`

### 2.1 Buy mit Menge 0 oder `null` — ungetestet

- Fehlendes Szenario: `Buy`-Transaktion mit `Quantity == null` → Lot wird nicht angelegt, Kostenbasis bleibt unverändert
- Fehlendes Szenario: `Buy`-Transaktion mit `Quantity == 0` → identisches Verhalten, kein Lot

### 2.2 Sell mit Menge 0 oder `null` — ungetestet

- Fehlendes Szenario: `Sell`-Transaktion mit `Quantity == null` → Sell wird übersprungen, keine Änderung an Lots und RealizedGains

### 2.3 Standalone-Fee ohne passenden Buy-Lot — ungetestet

- Fehlendes Szenario: `Fee`-Transaktion mit `GroupId`, für die kein `Buy`-Lot in `groupIdToLot` existiert → Fee wird nur geloggt, nicht der Kostenbasis zugerechnet; Endresultat muss identisch sein wie ohne diese Fee

### 2.4 Fee verknüpft mit bereits vollständig verkauftem Lot — ungetestet

- Fehlendes Szenario: Buy → Sell (vollständig) → Fee mit gleichem GroupId wie der Buy → Fee erhöht Kostenbasis des entfernten Lots im Dictionary, obwohl der Lot nicht mehr in der Queue ist → `RemainingLots` ist leer, aber `groupIdToLot` hat noch den Eintrag; kein Absturz, aber Kostenbasis-Ergebnis?

### 2.5 Mehrere Fees am selben Buy-Lot — ungetestet

- Fehlendes Szenario: Zwei `Fee`-Transaktionen mit demselben `GroupId` werden kumulativ zur `TotalCost` des Lots addiert

### 2.6 Null-Transaktionsliste — ungetestet

- Fehlendes Szenario: `Calculate(null)` → muss leeres `FifoCostBasisResult` zurückgeben (Guard `transactions is null`)

### 2.7 Unbekannter Transaktionstyp — ungetestet

- Fehlendes Szenario: Transaktion mit `Type`, der keinem bekannten `SecurityPostingSubType` entspricht → trifft `default`-Fall, wird geloggt und übersprungen; Ergebnis unverändert

### 2.8 Oversell mit partieller Lot-Konsumption — ungetestet

- Fehlendes Szenario: Buy 10 → Sell 15 → zwei Lots vorhanden → erster Lot vollständig konsumiert, zweiter Lot partiell, `remainingToSell > 0` → OversellWarning gesetzt; `costOfSold` enthält nur verfügbare Kosten; `TotalSharesHeld = 0`

### 2.9 Wiedereinstieg nach vollständigem Verkauf — ungetestet

- Fehlendes Szenario: Buy 10 → Sell 10 (alle) → Buy 5 (neuer Einstieg) → neue Lot-Erzeugung nach leerem Queue; `TotalSharesHeld = 5`, zwei separate Kostenbasis-Perioden

### 2.10 Gleichzeitige Buy- und Sell-Transaktionen am selben Tag — ungetestet

- Fehlendes Szenario: Buy und Sell am selben Datum → Sortierung nach Datum, dann Id → sicherstellen, dass Buy vor Sell verarbeitet wird wenn Buy-Id < Sell-Id

---

## 3. `MemoryReturnAnalysisCache`

### 3.1 Factory gibt `null` zurück — ungetestet

- Fehlendes Szenario: `GetOrCreateAsync` aufgerufen, Factory liefert `null` → Ergebnis wird **nicht** gecacht; zweiter Aufruf ruft Factory erneut auf

### 3.2 Cache-Ablauf (TTL-Expiry) — ungetestet

- Fehlendes Szenario: Eintrag mit sehr kurzer TTL wird gecacht, dann nach Ablauf erneut abgerufen → Factory wird erneut aufgerufen; `_keys` muss den abgelaufenen Key nicht mehr enthalten (oder `InvalidateAsync` hat keine Wirkung auf abgelaufene Keys)

### 3.3 Groß-/Kleinschreibung bei Schlüsseln — ungetestet

- Fehlendes Szenario: Schlüssel mit unterschiedlicher Groß-/Kleinschreibung werden durch `StringComparer.OrdinalIgnoreCase` als gleich behandelt → `GetOrCreateAsync("KEY")` nach `GetOrCreateAsync("key")` muss aus Cache liefern

### 3.4 Leeres Präfix bei `InvalidateAsync` — ungetestet

- Fehlendes Szenario: `InvalidateAsync("")` → leerer String ist in jedem Key enthalten → alle Einträge werden entfernt

### 3.5 `InvalidateAsync` auf leeren Cache — ungetestet

- Fehlendes Szenario: `InvalidateAsync("beliebiger-prefix")` wenn keine Einträge vorhanden → keine Exception, kein Fehler

### 3.6 Gleichzeitige Zugriffe (Thread-Safety) — ungetestet

- Fehlendes Szenario: Mehrere parallele `GetOrCreateAsync`-Aufrufe mit demselben Schlüssel → Factory darf mehrfach aufgerufen werden (keine atomare `GetOrAdd`-Semantik; der aktuelle Code hat eine Race Condition zwischen `TryGetValue` und `Set`)

---

## 4. `ReturnAnalysisService` (Infrastructure) — vollständig ungetestet

Für keine der folgenden Methoden existieren Unit- oder Integrationstests.

### 4.1 `GetReturnSummaryAsync` / `ComputeReturnSummaryAsync`

- Fehlendes Szenario: Security existiert nicht oder gehört nicht dem User → Methode gibt `null` zurück (Ownership-Prüfung S-1)
- Fehlendes Szenario: Security existiert, aber keine Transaktionen → `null`
- Fehlendes Szenario: Transaktionen vorhanden, kein aktueller Kurs verfügbar → `HasMissingPrices = true`, `MissingPricesHint = "Kein aktueller Kurs verfügbar."`
- Fehlendes Szenario: OversellWarning aktiv → `HasMissingPrices = true`, `MissingPricesHint` enthält Warnung aus `FifoCostBasisResult`
- Fehlendes Szenario: Haltedauer < 1 Jahr → CAGR ist `null`
- Fehlendes Szenario: Haltedauer ≥ 1 Jahr → CAGR wird berechnet und ist nicht `null`
- Fehlendes Szenario: IRR-Berechnung, wenn keine Verkäufe und kein positiver Marktwert → `BuildIrrCashflows` fügt keinen Terminal-Cashflow ein → IRR `null`
- Fehlendes Szenario: Ergebnis wird korrekt gecacht (zweiter Aufruf verwendet Cache, kein DB-Zugriff)

- Fehlendes Szenario: Keine Transaktionen → `null`
- Fehlendes Szenario: Weniger als 30 Preispunkte → `null`
- Fehlendes Szenario: Genau 30 Preispunkte → Sparkline wird zurückgegeben (Grenzwert)
- Fehlendes Szenario: Mehr als 30 Preispunkte mit Lücken → Forward-Fill füllt Wochenenden korrekt

### 4.3 `GetDetailedMetricsAsync` / `ComputeDetailedMetricsAsync`

- Fehlendes Szenario: User nicht gefunden → `null`
- Fehlendes Szenario: Security nicht gefunden oder nicht Eigentum des Users → `null`
- Fehlendes Szenario: `ShowSharpeRatio = false` → `SharpeRatio` ist immer `null`
- Fehlendes Szenario: `ShowSharpeRatio = true`, Volatilität = 0 → `SharpeRatio` bleibt `null` (Guard)
- Fehlendes Szenario: `ShowSharpeRatio = true`, TWR und Volatilität vorhanden → `SharpeRatio` wird berechnet
- Fehlendes Szenario: `investedCapital == 0` → `DividendYieldCurrentYear = 0` (Guard in `CalculateDividendYield`)
- Fehlendes Szenario: Steuern ohne Dividenden → `netReturn < grossReturn`, `TaxRate` = 0 wenn `grossReturn == 0`

### 4.4 `GetPeriodicReturnsAsync` / `ComputePeriodicReturnsAsync`

- Fehlendes Szenario: Security nicht gefunden → `null`
- Fehlendes Szenario: Keine Transaktionen → `null`
- Fehlendes Szenario: Startportfoliowert am Jahresanfang = 0 → `annualReturn = 0` (keine Division durch Null)
- Fehlendes Szenario: Monatlicher Startwert = 0 → `monthReturn = null`
- Fehlendes Szenario: Erster Kauf in laufendem Jahr → Jahresrendite ist YTD-Rendite
- Fehlendes Szenario: Dividenden über mehrere Jahre kumulieren korrekt in `AnnualDividendPoint.CumulativeNet`

### 4.5 `GetCashflowTimelineAsync` / `ComputeCashflowTimelineAsync`

- Fehlendes Szenario: Security nicht gefunden → `null`
- Fehlendes Szenario: Keine Transaktionen → leeres `CashflowTimelineDto` (nicht `null`; dies ist das spezielle Verhalten dieser Methode!)
- Fehlendes Szenario: Transaktionen über mehrere Jahre → jährliche Summen werden korrekt gruppiert
- Fehlendes Szenario: Alle Transaktionen im gleichen Jahr → genau ein `AnnualCashflowSummary`

### 4.6 `GetPerformanceChartDataAsync` / `ComputePerformanceChartDataAsync`

- Fehlendes Szenario: Security nicht gefunden → `null`
- Fehlendes Szenario: Keine Transaktionen → `null`
- Fehlendes Szenario: Keine Preise im angefragten Zeitbereich → `null`
- Fehlendes Szenario: `ChartTimeRange.OneMonth` → `fromDate = Today.AddMonths(-1)` (alle sechs Enum-Werte ungetestet)
- Fehlendes Szenario: `ChartTimeRange.All` → `fromDate = DateTime.MinValue`

### 4.7 `GetBenchmarkComparisonAsync` / `ComputeBenchmarkComparisonAsync`

- Fehlendes Szenario: Kein Benchmark konfiguriert (`user.BenchmarkSecurityId == null`) → `null`
- Fehlendes Szenario: Benchmark-Security gehört nicht dem User (S-3-Prüfung) → `null` (kein 403, nur `null`)
- Fehlendes Szenario: Target-Security hat weniger als 2 Preispunkte → `null`
- Fehlendes Szenario: Benchmark-Security hat weniger als 2 Preispunkte → `null`
- Fehlendes Szenario: Normalisierungsbasis des ersten Preispunkts = 0 → `null` (Guard `securityBase == 0m`)
- Fehlendes Szenario: Normalisierungsbasis des Benchmarks = 0 → `null`
- Fehlendes Szenario: Valider Benchmark → beide Serien werden auf Base 100 normalisiert

### 4.8 `GetUserSettingsAsync`

- Fehlendes Szenario: User nicht gefunden → `null`
- Fehlendes Szenario: User hat kein Benchmark konfiguriert → `BenchmarkSecurityId = null`, `BenchmarkSecurityName = null`
- Fehlendes Szenario: User hat Benchmark konfiguriert, Security existiert → `BenchmarkSecurityName` gefüllt
- Fehlendes Szenario: User hat Benchmark konfiguriert, Security wurde gelöscht → `BenchmarkSecurityName = null` (FirstOrDefault gibt null)

### 4.9 `UpdateUserSettingsAsync`

- Fehlendes Szenario: `benchmarkSecurityId` gesetzt, aber Security gehört nicht dem User → `ArgumentException` wird geworfen (S-3-Prüfung)
- Fehlendes Szenario: `benchmarkSecurityId = null` → Benchmark wird gelöscht, keine Ownership-Prüfung
- Fehlendes Szenario: User nicht gefunden → Methode kehrt lautlos zurück (kein Exception, keine Persistenz)
- Fehlendes Szenario: Valide Settings werden korrekt persistiert (DB-SaveChanges wird aufgerufen)
- Fehlendes Szenario: `riskFreeRate < 0` → kein Guard im Service (nur Validierung per `[Range]` im Controller)

### 4.10 `InvalidateCacheAsync`

- Fehlendes Szenario: Korrekte Ableitung des `SecurityUserToken` und Weiterleitung an `_cache.InvalidateAsync` → alle Einträge für die Security/User-Kombination werden invalidiert
- Fehlendes Szenario: Invalidierung betrifft nicht Einträge anderer Security/User-Kombinationen

### 4.11 Interne Hilfsmethoden — Verhaltens-Lücken bestätigt

- **`BuildTwrPeriods` — bestätigter Logikfehler (ungetestet):** In Zeile 744 wird `ComputeSharesHeldOnDate(transactions, start)` zweimal aufgerufen (einmal für `sharesAtStart`, einmal für `sharesAtEnd`), statt `end` für `sharesAtEnd` zu verwenden. `sharesAtEnd` entspricht damit immer `sharesAtStart` → End-Portfoliowert basiert auf falscher Stückzahl. Kein Test deckt diesen Fehler auf.
- **`ComputeInvestedCapitalOnDate` — Annäherung ungetestet:** Die Methode verwendet eine einfache Summen-Approximation statt FIFO-Lot-Tracking. Für Szenarien mit mehrfachen Käufen und Teilverkäufen weicht der Wert vom echten FIFO-Kapitaleinsatz ab. Kein Test prüft die Abweichung oder dokumentiert das Approximationsverhalten.
- **`ForwardFill` — Randfälle ungetestet:** Keine Tests für leere Eingabe, Preissel mit Datum vor `from`, Lücken nur am Anfang vs. in der Mitte der Serie.
- **`BuildIrrCashflows` — Randfälle ungetestet:** Kein Terminal-Cashflow wenn `currentMarketValue <= 0`; keine Tests für rein negative Cashflow-Listen nach diesem Filter.
- **`GetPortfolioValueOnDate` — Randfälle ungetestet:** Kein Preis auf oder vor dem Datum → gibt 0 zurück; kein Test prüft diesen Pfad.
- **`GetFromDateForTimeRange` — ungetestet:** Kein Test prüft die korrekte `fromDate`-Berechnung für alle sechs `ChartTimeRange`-Werte; insbesondere `ChartTimeRange.All → DateTime.MinValue` und der `default`-Fall (`→ AddYears(-1)`).

---

## 5. `SecuritiesController` — Return-Analysis-Endpunkte (Integrationstests fehlen komplett)

In `ApiClientSecuritiesTests.cs` gibt es **keine** Tests für die folgenden 9 Endpunkte:

### 5.1 `GET {id}/return-summary`

- Fehlendes Szenario: 200 OK wenn Security und Transaktionen vorhanden
- Fehlendes Szenario: 404 Not Found wenn Security dem User nicht gehört
- Fehlendes Szenario: Unauthentifizierter Zugriff → 401 Unauthorized

### 5.2 `GET {id}/return-sparkline`

- Fehlendes Szenario: 200 OK mit validen Sparkline-Daten
- Fehlendes Szenario: 404 Not Found wenn weniger als 30 Preispunkte

### 5.3 `GET {id}/return-metrics`

- Fehlendes Szenario: 200 OK mit DetailedReturnMetricsDto
- Fehlendes Szenario: 404 Not Found

### 5.4 `GET {id}/return-periodic`

- Fehlendes Szenario: 200 OK mit PeriodicReturnsDto
- Fehlendes Szenario: 404 Not Found

### 5.5 `GET {id}/return-cashflows`

- Fehlendes Szenario: 200 OK mit CashflowTimelineDto (auch für Security ohne Transaktionen → leer, nicht 404)
- Fehlendes Szenario: 404 Not Found wenn Security nicht gefunden

### 5.6 `GET {id}/return-chart`

- Fehlendes Szenario: 200 OK für alle `ChartTimeRange`-Werte (`OneMonth`, `ThreeMonths`, `SixMonths`, `OneYear`, `ThreeYears`, `All`)
- Fehlendes Szenario: 404 Not Found wenn keine Preisdaten im Zeitbereich
- Fehlendes Szenario: Ungültiger `timeRange`-Query-Parameter (unbekannter Enum-Wert)

### 5.7 `GET {id}/return-benchmark`

- Fehlendes Szenario: 404 Not Found wenn kein Benchmark konfiguriert
- Fehlendes Szenario: 404 Not Found wenn Benchmark-Security nicht dem User gehört (S-3)
- Fehlendes Szenario: 200 OK mit normalisierten Vergleichsdaten

### 5.8 `GET return-analysis/settings`

- Fehlendes Szenario: 200 OK mit Standard-Fallback-DTO wenn User nicht gefunden (`new ReturnAnalysisSettingsDto(null, null, false, 0)`)
- Fehlendes Szenario: 200 OK mit gespeicherten Settings

### 5.9 `PUT return-analysis/settings`

- Fehlendes Szenario: 204 NoContent bei valider Anfrage
- Fehlendes Szenario: 400 BadRequest wenn `benchmarkSecurityId` nicht dem User gehört (`ArgumentException`)
- Fehlendes Szenario: 400 BadRequest bei negativer `RiskFreeRate` (`[Range]`-Validation)
- Fehlendes Szenario: 500 Internal Server Error bei unerwartetem Fehler

### 5.10 `DELETE {id}/return-cache`

- Fehlendes Szenario: 204 NoContent nach Invalidierung (auch wenn Security nicht existiert)
- Fehlendes Szenario: Unauthentifizierter Zugriff → 401 Unauthorized

---

## 6. `ReturnSummaryWidget` (Blazor-Komponente) — vollständig ungetestet

- Fehlendes Szenario: `SecurityId == Guid.Empty` → Loading wird sofort beendet, kein API-Aufruf
- Fehlendes Szenario: Ladezustand während `OnParametersSetAsync` → `_loading = true`, Skeleton-UI sichtbar
- Fehlendes Szenario: Fehlerfall – `GetReturnSummaryAsync` wirft Exception → Fehlermeldung im `_error`-State wird angezeigt
- Fehlendes Szenario: `_summary == null` nach erfolgreichem Laden → "Keine Transaktionsdaten vorhanden."-Ansicht
- Fehlendes Szenario: `_summary` vorhanden, `Cagr = null` → CAGR-Zeile wird nicht gerendert
- Fehlendes Szenario: `_summary` vorhanden, `Irr = null` → IRR-Zeile wird nicht gerendert
- Fehlendes Szenario: `HasMissingPrices = true` → Warnzeile mit `MissingPricesHint` wird angezeigt
- Fehlendes Szenario: `TotalReturnPercent >= 0` → CSS-Klasse `positive` gesetzt
- Fehlendes Szenario: `TotalReturnPercent < 0` → CSS-Klasse `negative` gesetzt
- Fehlendes Szenario: Klick auf "Detaillierte Analyse →" navigiert zu `/securities/{SecurityId}/performance`
- Fehlendes Szenario: `SecurityId`-Parameter ändert sich → `OnParametersSetAsync` wird erneut aufgerufen, Widget lädt neu

---

## 7. `SecurityPerformancePage` (Blazor-Komponente) — vollständig ungetestet

- Fehlendes Szenario: `CurrentUser.IsAuthenticated == false` → Login-Hinweis wird gerendert, kein API-Aufruf
- Fehlendes Szenario: Security nicht gefunden (`SecurityService.GetAsync` gibt `null`) → `_notFound = true`, Not-Found-UI mit Zurück-Button
- Fehlendes Szenario: Security gefunden → `_securityName` aus DTO gesetzt, Seiten-Titel korrekt
- Fehlendes Szenario: Standard-Tab beim Laden ist `Overview`
- Fehlendes Szenario: Tab-Wechsel zu `TimeSeries` → `TimeSeriesTab`-Komponente sichtbar
- Fehlendes Szenario: Tab-Wechsel zu `Cashflows` → `CashflowTab`-Komponente sichtbar
- Fehlendes Szenario: Tab-Wechsel zu `Metrics` → `MetricsTab`-Komponente sichtbar
- Fehlendes Szenario: Tab-Wechsel zu `Benchmark` → `BenchmarkTab`-Komponente sichtbar
- Fehlendes Szenario: Zurück-Button navigiert zu `/card/securities/{Id}`

---

## 8. `ReturnAnalysisCacheKeys` — teilweise ungetestet

- Fehlendes Szenario: `SecurityUserToken` enthält beide IDs als Substring aller anderen Keys (Nachweis, dass Token-basierte Invalidierung alle 7 Key-Typen trifft: Summary, Sparkline, Metrics, Periodic, Cashflow, Chart, Benchmark)
- Fehlendes Szenario: Chart-Key enthält `timeRange`-Suffix korrekt für alle `ChartTimeRange`-Werte
- Fehlendes Szenario: Unterschiedliche Security-IDs erzeugen unterschiedliche Keys (keine Kollision)

---

## Zusammenfassung der Lücken

| Komponente | Lücken-Kategorie | Anzahl fehlender Szenarien (geschätzt) |
|---|---|---|
| `ReturnCalculationService` | Fehlende Methoden (`DividendYield`, `TaxRate`) + Randfälle | ~22 |
| `FifoCostBasisCalculator` | Randfälle + Edge Cases | ~10 |
| `MemoryReturnAnalysisCache` | Randfälle + Thread-Safety | ~6 |
| `ReturnAnalysisService` | **Vollständig ungetestet**, inkl. bestätigter Logikfehler in `BuildTwrPeriods` | ~42 |
| `SecuritiesController` (Return-Endpunkte) | **Keine Integrationstests** für alle 9 neuen Endpunkte | ~20 |
| `ReturnSummaryWidget` | **Vollständig ungetestet** | ~11 |
| `SecurityPerformancePage` | **Vollständig ungetestet** | ~9 |
| `ReturnAnalysisCacheKeys` | Schlüssel-Validierung | ~3 |
| **Gesamt** | | **~123** |

> ⚠️ **Kritische Befunde aus der Code-Analyse:**
> 1. `CalculateVolatility` benötigt faktisch **mindestens 3 Preise** für ein Nicht-null-Ergebnis (interner `validCount < 2`-Guard), obwohl der API-Eingangscheck nur `Count < 2` prüft — undokumentiert und ungetestet.
> 2. `BuildTwrPeriods` verwendet `start` statt `end` für die Shares-at-End-Berechnung (Zeile 744) — **bestätigter Logikfehler**, kein Test deckt ihn auf.
> 3. `ComputeInvestedCapitalOnDate` ist eine bewusste Näherung ohne FIFO-Genauigkeit — das Approximationsverhalten ist nicht durch Tests dokumentiert.
