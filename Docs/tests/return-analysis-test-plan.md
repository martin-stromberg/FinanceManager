# Testplan: Renditeanalyse-Feature

> **Basis:** `docs/tests/return-analysis-coverage-gaps.md`  
> **Stand:** 2026-04-25  
> **Ziel:** ~123 identifizierte Testlücken systematisch schließen  
> **Framework:** xUnit · FluentAssertions · Moq · NullLogger  
> **Naming:** `<MethodName>_Should<Erwartung>_When<Umstand>()`  
> **Muster:** Arrange / Act / Assert (je Abschnitt leer Zeile)  
> **Prioritäten:** Prio 1 = kritisch/blockierend (Bugs, Division-by-Zero, Security) · Prio 2 = wichtig (Business Logic) · Prio 3 = nice-to-have (UI, Edge Cases)

---

## Übersicht und Prioritäten

| # | Testklasse | Datei | Prio | ~Tests |
|---|---|---|---|---|
| 1 | `ReturnCalculationServiceTests` (Erweiterung) | `FinanceManager.Tests/Securities/ReturnCalculationServiceTests.cs` | **1** | 20 |
| 2 | `FifoCostBasisCalculatorTests` (Erweiterung) | `FinanceManager.Tests/Securities/FifoCostBasisCalculatorTests.cs` | **1** | 10 |
| 3 | `ReturnAnalysisCacheTests` (Erweiterung) | `FinanceManager.Tests/Securities/ReturnAnalysisCacheTests.cs` | 2 | 6 |
| 4 | `ReturnAnalysisServiceTests` (neu) | `FinanceManager.Tests/Securities/ReturnAnalysisServiceTests.cs` | **1** | 42 |
| 5 | `ReturnAnalysisCacheKeysTests` (neu) | `FinanceManager.Tests/Securities/ReturnAnalysisCacheKeysTests.cs` | 3 | 3 |
| 6 | `ApiClientReturnAnalysisTests` (neu) | `FinanceManager.Tests.Integration/ApiClient/ApiClientReturnAnalysisTests.cs` | **1** | 20 |
| 7 | `ReturnSummaryWidgetTests` (neu, optional) | `FinanceManager.Tests/Components/ReturnSummaryWidgetTests.cs` | 3 | 11 |
| 8 | `SecurityPerformancePageTests` (neu, optional) | `FinanceManager.Tests/Components/SecurityPerformancePageTests.cs` | 3 | 9 |
| **Gesamt** | | | | **~121** |

---

## ⚠️ Bestätigte Bugs und kritische Logikfehler

> Diese Bugs wurden bei der Code-Analyse (ohne Tests) entdeckt. Jeder dieser Punkte muss durch einen Prio-1-Test **zuerst rot** werden und dann mit dem Fix **grün** gemacht werden.

### BUG-1: `BuildTwrPeriods` — `sharesAtEnd` verwendet `start` statt `end` (Zeile 744)

**Datei:** `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs`, Zeile 744  
**Fehler:** `ComputeSharesHeldOnDate(transactions, start)` wird zweimal aufgerufen — einmal für `sharesAtStart` (korrekt) und einmal für `sharesAtEnd` (falsch, sollte `end` sein). Dadurch basiert der End-Portfoliowert auf der falschen Stückzahl, und der TWR jedes Zeitraums ist verfälscht, sofern zwischen `start` und `end` ein Kauf oder Verkauf stattfand.

```csharp
// Zeile 743–744 (aktueller Code):
decimal sharesAtStart = ComputeSharesHeldOnDate(transactions, start);
decimal sharesAtEnd = ComputeSharesHeldOnDate(transactions, start); // BUG: sollte 'end' sein
```

**Auswirkung:** TWR-Berechnung ist für alle Portfolios mit mehreren Transaktionen fehlerhaft.  
**Testfall:** `BuildTwrPeriods_Should_UseSharesOnEndDate_ForEndValue_When_SharesChangeWithinPeriod`  
**Zuordnung:** → Abschnitt 4.11 dieses Plans

---

### BUG-2: `CalculateVolatility` — faktisch 3 Preise Minimum, Guard prüft nur `< 2`

**Datei:** `FinanceManager.Application/Securities/ReturnAnalysis/ReturnCalculationService.cs`  
**Fehler:** Eingangs-Guard ist `if (dailyPrices.Count < 2) return null`, aber die Methode berechnet `n - 1 = 1` Log-Return, setzt `validCount = 1` und trifft dann den internen Guard `if (validCount < 2) return null` → Ergebnis ist `null`. Für ein sinnvolles Ergebnis sind mindestens 3 Preise nötig. Dieses Verhalten ist undokumentiert.

**Auswirkung:** API-Konsumenten können davon ausgehen, dass 2 Preise ausreichen. Die Diskrepanz kann zu schwer debuggbaren `null`-Ergebnissen führen.  
**Testfall:** `CalculateVolatility_Should_ReturnNull_When_ExactlyTwoPricesProvided` (dokumentiert das Verhalten)  
**Zuordnung:** → Abschnitt 1.6 dieses Plans  
**Empfehlung:** Entweder Guard auf `< 3` korrigieren (Breaking Change der API) oder mit `<remarks>`-XML dokumentieren.

---

### BUG-3: `MemoryReturnAnalysisCache` — Race Condition bei gleichzeitigen Zugriffen

**Datei:** `FinanceManager.Infrastructure/Securities/ReturnAnalysis/MemoryReturnAnalysisCache.cs`  
**Fehler:** Kein atomares `GetOrAdd` zwischen `TryGetValue` und `Set` → bei parallelen Aufrufen kann die Factory mehrfach aufgerufen werden.  
**Auswirkung:** Doppelte DB-Abfragen möglich; kein Datenverlust, aber unnötige Last.  
**Testfall:** `GetOrCreateAsync_Should_NotThrow_When_CalledConcurrently` (→ Abschnitt 3.6)  
**Empfehlung:** Separates Issue für Fix mit `SemaphoreSlim` oder `Lazy<Task<T>>` erstellen.

---

### BUG-4: `ComputeInvestedCapitalOnDate` — Näherung statt FIFO-Genauigkeit

**Datei:** `ReturnAnalysisService` (interne Hilfsmethode)  
**Fehler:** Einfache Summation statt FIFO-Lot-Tracking. Bei Portfolios mit mehreren Käufen und Teilverkäufen weicht der berechnete investierte Kapitalbetrag vom echten FIFO-Wert ab.  
**Auswirkung:** Sparkline-Datenpunkte (`InvestedCapital`) sind für komplexe Portfolios ungenau.  
**Testfall:** `ComputeInvestedCapitalOnDate_Should_ReturnApproximation_When_PartialSellOccurs` (→ Abschnitt 4.11)

---

---

## 1. `ReturnCalculationServiceTests` — Erweiterung bestehender Datei

**Pfad:** `FinanceManager.Tests/Securities/ReturnCalculationServiceTests.cs`  
**SUT:** `new ReturnCalculationService(NullLogger<ReturnCalculationService>.Instance)`  
**Arrange-Hinweis:** Kein Mock nötig; der Service ist stateless.

### 1.1 `CalculateDividendYield` (vollständig ungetestet)

| Prio | Testfall |
|---|---|
| **1** | `CalculateDividendYield_Should_ReturnCorrectYield_When_DividendsAndCapitalArePositive` |
| **1** | `CalculateDividendYield_Should_ReturnNull_When_InvestedCapitalIsZero` |
| 2 | `CalculateDividendYield_Should_ReturnZero_When_TotalDividendsIsZero` |
| 2 | `CalculateDividendYield_Should_HandleNegativeDividends_When_DividendsAreNegative` |

```
// Arrange – Normalfall
decimal totalDividends = 50m;
decimal investedCapital = 1_000m;
// Act
decimal? result = _sut.CalculateDividendYield(totalDividends, investedCapital);
// Assert
result.Should().Be(0.05m);
```

### 1.2 `CalculateTaxRate` (vollständig ungetestet)

| Prio | Testfall |
|---|---|
| **1** | `CalculateTaxRate_Should_ReturnCorrectRate_When_TaxesAndGrossReturnArePositive` |
| **1** | `CalculateTaxRate_Should_ReturnNull_When_GrossReturnIsZero` |
| 2 | `CalculateTaxRate_Should_UseAbsoluteValue_When_GrossReturnIsNegative` |
| 2 | `CalculateTaxRate_Should_ReturnZero_When_TotalTaxesIsZero` |

```
// Arrange – Division-by-Zero-Guard
// Act
decimal? result = _sut.CalculateTaxRate(totalTaxes: 20m, grossReturn: 0m);
// Assert
result.Should().BeNull();
```

### 1.3 `CalculateTotalReturn` — fehlender Randfall

| Prio | Testfall |
|---|---|
| 2 | `CalculateTotalReturn_Should_ReduceReturn_When_NetDividendsAreNegative` |

```
// Arrange – negative Nettodividende (hohe Quellensteuer)
decimal invested = 1_000m; decimal marketValue = 1_100m; decimal netDividends = -20m;
// Act
decimal? result = _sut.CalculateTotalReturn(invested, marketValue, netDividends);
// Assert
result.Should().Be(0.08m); // (1100 - 20 - 1000) / 1000
```

### 1.4 `CalculateIrr` — Randfälle

| Prio | Testfall |
|---|---|
| **1** | `CalculateIrr_Should_ReturnNull_When_ListHasExactlyOneCashflow` |
| **1** | `CalculateIrr_Should_ReturnNull_When_ListIsEmpty` |
| **1** | `CalculateIrr_Should_ReturnNull_When_InputIsNull` |
| 2 | `CalculateIrr_Should_ConvergeViaBisection_When_AllCashflowsOnSameDay` |
| 2 | `CalculateIrr_Should_ReturnNull_When_CashflowsHaveNoSignChange` |

```
// Arrange – Bisektions-Fallback testen
// Alle Cashflows am selben Tag → years[i] == 0.0 → NpvDerivative == 0 → Bisection
var date = new DateTime(2024, 1, 1);
var cashflows = new[]
{
    new CashflowPoint(date, -1000m),
    new CashflowPoint(date,  1100m),
};
// Act
decimal? result = _sut.CalculateIrr(cashflows);
// Assert
// Ergebnis hängt von Bisection ab; zumindest kein Exception
result.Should().NotBeNull(); // oder .BeNull() je nach Guard-Verhalten prüfen
```

### 1.5 `CalculateCagr` — Randfälle

| Prio | Testfall |
|---|---|
| **1** | `CalculateCagr_Should_ReturnNull_When_YearsIsNegative` |
| 2 | `CalculateCagr_Should_ReturnNegativeOne_When_EndValueIsZero` |
| 2 | `CalculateCagr_Should_ReturnNull_When_EndValueIsNegativeAndYearsIsNonInteger` |

```
// Arrange – negativer Zeitraum
decimal startValue = 1_000m; decimal endValue = 1_500m; double years = -1.0;
// Act
decimal? result = _sut.CalculateCagr(startValue, endValue, years);
// Assert
result.Should().BeNull();
```

### 1.6 `CalculateVolatility` — Randfälle

> ⚠️ **Siehe BUG-2** (oben): Genau 2 Preise liefern `null`, nicht ein Ergebnis — undokumentiertes Verhalten!

| Prio | Testfall |
|---|---|
| **1** | `CalculateVolatility_Should_ReturnNull_When_InputIsNull` |
| **1** | `CalculateVolatility_Should_ReturnNull_When_AllPricesAreZeroOrNegative` |
| **1** | `CalculateVolatility_Should_ReturnNull_When_ValidCountIsLessThanTwo` |
| **1** | `CalculateVolatility_Should_ReturnNull_When_ExactlyTwoPricesProvided` _(BUG-2: dokumentiert undokumentiertes Verhalten)_ |
| 2 | `CalculateVolatility_Should_ReturnResult_When_ExactlyThreePricesProvided` _(effektives Minimum)_ |
| 3 | `CalculateVolatility_Should_HandleLargePriceSwings_When_PricesAreExtreme` |

```
// Arrange – null-Eingabe
// Act
decimal? result = _sut.CalculateVolatility(null);
// Assert
result.Should().BeNull();
```

### 1.7 `CalculateTwr` — Randfälle

| Prio | Testfall |
|---|---|
| **1** | `CalculateTwr_Should_ReturnNull_When_InputIsNull` |
| **1** | `CalculateTwr_Should_ReturnNull_When_AllPeriodsHaveZeroDenominator` |
| 2 | `CalculateTwr_Should_HandleNegativeExternalCashflow_When_PartialWithdrawal` |

### 1.8 `CalculateMaxDrawdown` — Randfälle

| Prio | Testfall |
|---|---|
| **1** | `CalculateMaxDrawdown_Should_ReturnNull_When_InputIsNull` |
| 2 | `CalculateMaxDrawdown_Should_ReturnZero_When_AllValuesAreIdentical` |
| 2 | `CalculateMaxDrawdown_Should_ReturnZero_When_FirstValueIsZero` |

### 1.9 `CalculateSharpeRatio` — Randfälle

| Prio | Testfall |
|---|---|
| 2 | `CalculateSharpeRatio_Should_ReturnZero_When_AnnualisedReturnEqualsRiskFreeRate` |
| 3 | `CalculateSharpeRatio_Should_HandleNegativeVolatility_Defensively` |

---

## 2. `FifoCostBasisCalculatorTests` — Erweiterung bestehender Datei

**Pfad:** `FinanceManager.Tests/Securities/FifoCostBasisCalculatorTests.cs`  
**SUT:** `new FifoCostBasisCalculator(NullLogger<FifoCostBasisCalculator>.Instance)`  
**Arrange-Hinweis:** Helper-Methoden `Buy()`, `Sell()`, `Fee()` aus bestehender Datei wiederverwenden.

### 2.1 Buy mit Menge 0 oder null

| Prio | Testfall |
|---|---|
| **1** | `Calculate_Should_NotCreateLot_When_BuyQuantityIsNull` |
| **1** | `Calculate_Should_NotCreateLot_When_BuyQuantityIsZero` |

```
// Arrange
var tx = new SecurityTransaction(Guid.NewGuid(), DateTime.Today,
    SecurityPostingSubType.Buy, -1000m, quantity: null, Guid.NewGuid());
// Act
var result = _sut.Calculate([tx]);
// Assert
result.RemainingLots.Should().BeEmpty();
result.TotalSharesHeld.Should().Be(0m);
```

### 2.2 Sell mit Menge null

| Prio | Testfall |
|---|---|
| **1** | `Calculate_Should_SkipSell_When_SellQuantityIsNull` |

### 2.3 Standalone-Fee ohne passenden Buy-Lot

| Prio | Testfall |
|---|---|
| 2 | `Calculate_Should_LogAndSkipFee_When_NoMatchingBuyLotExists` |

```
// Arrange – Fee mit zufälliger GroupId, kein passender Buy
var fee = Fee(DateTime.Today, 10m, groupId: Guid.NewGuid());
// Act
var result = _sut.Calculate([fee]);
// Assert – kein Absturz, Kostenbasis unverändert
result.TotalCostBasis.Should().Be(0m);
```

### 2.4 Fee nach vollständigem Verkauf des zugehörigen Lots

| Prio | Testfall |
|---|---|
| 2 | `Calculate_Should_NotCrash_When_FeeLinkedToFullySoldLot` |

```
// Arrange: Buy 10 → Sell 10 → Fee mit derselben GroupId wie der Buy
var groupId = Guid.NewGuid();
var buy  = Buy(DateTime.Today.AddDays(-2), 1000m, 10m, groupId);
var sell = Sell(DateTime.Today.AddDays(-1), 1000m, 10m);
var fee  = Fee(DateTime.Today, 5m, groupId);
// Act
var result = _sut.Calculate([buy, sell, fee]);
// Assert
result.RemainingLots.Should().BeEmpty();
result.HasOversellWarning.Should().BeFalse();
```

### 2.5 Mehrere Fees am selben Buy-Lot

| Prio | Testfall |
|---|---|
| 2 | `Calculate_Should_AccumulateFees_When_MultipleFeesShareSameGroupId` |

```
// Arrange
var groupId = Guid.NewGuid();
var buy  = Buy(DateTime.Today, 1000m, 10m, groupId);
var fee1 = Fee(DateTime.Today, 5m, groupId);
var fee2 = Fee(DateTime.Today, 3m, groupId);
// Act
var result = _sut.Calculate([buy, fee1, fee2]);
// Assert – Kostenbasis = 1000 + 5 + 3 = 1008
result.TotalCostBasis.Should().Be(1008m);
```

### 2.6 Null-Transaktionsliste

| Prio | Testfall |
|---|---|
| **1** | `Calculate_Should_ReturnEmpty_When_TransactionListIsNull` |

```
// Act
var result = _sut.Calculate(null!);
// Assert
result.TotalCostBasis.Should().Be(0m);
result.RemainingLots.Should().BeEmpty();
```

### 2.7 Unbekannter Transaktionstyp

| Prio | Testfall |
|---|---|
| 3 | `Calculate_Should_SkipUnknownTransactionType_Without_ChangingResult` |

```
// Arrange – cast auf nicht existierenden Wert
var tx = new SecurityTransaction(Guid.NewGuid(), DateTime.Today,
    (SecurityPostingSubType)999, 0m, null, Guid.NewGuid());
// Act
var result = _sut.Calculate([tx]);
// Assert
result.TotalCostBasis.Should().Be(0m);
```

### 2.8 Oversell mit partieller Lot-Konsumption

| Prio | Testfall |
|---|---|
| **1** | `Calculate_Should_SetOversellWarning_When_SellExceedsAllLots` |

```
// Arrange: Buy 10 → Buy 5 → Sell 20 (mehr als alle Lots)
var buy1 = Buy(DateTime.Today.AddDays(-2), 1000m, 10m);
var buy2 = Buy(DateTime.Today.AddDays(-1), 500m, 5m);
var sell = Sell(DateTime.Today, 2000m, 20m);
// Act
var result = _sut.Calculate([buy1, buy2, sell]);
// Assert
result.HasOversellWarning.Should().BeTrue();
result.OversellWarningMessage.Should().NotBeNullOrEmpty();
result.TotalSharesHeld.Should().Be(0m);
```

### 2.9 Wiedereinstieg nach vollständigem Verkauf

| Prio | Testfall |
|---|---|
| **1** | `Calculate_Should_CreateNewLot_When_BuyAfterFullSell` |

```
// Arrange: Buy 10 → Sell 10 → Buy 5
var buy1 = Buy(DateTime.Today.AddDays(-2), 1000m, 10m);
var sell = Sell(DateTime.Today.AddDays(-1), 1000m, 10m);
var buy2 = Buy(DateTime.Today, 600m, 5m);
// Act
var result = _sut.Calculate([buy1, sell, buy2]);
// Assert
result.TotalSharesHeld.Should().Be(5m);
result.RemainingLots.Should().HaveCount(1);
```

### 2.10 Buy und Sell am selben Tag (Sortierung)

| Prio | Testfall |
|---|---|
| 2 | `Calculate_Should_ProcessBuyBeforeSell_When_SameDate_And_BuyIdIsSmaller` |

```
// Arrange: Buy und Sell an identischem Datum; Buy-Id < Sell-Id → Buy zuerst
var buyId  = new Guid("00000000-0000-0000-0000-000000000001");
var sellId = new Guid("00000000-0000-0000-0000-000000000002");
var date   = new DateTime(2024, 6, 1);
var buy  = new SecurityTransaction(buyId,  date, SecurityPostingSubType.Buy,  -1000m, 10m, Guid.NewGuid());
var sell = new SecurityTransaction(sellId, date, SecurityPostingSubType.Sell,  900m,  8m,  Guid.NewGuid());
// Act
var result = _sut.Calculate([sell, buy]); // bewusst falsche Reihenfolge → Sorter korrigiert
// Assert
result.TotalSharesHeld.Should().Be(2m);
result.HasOversellWarning.Should().BeFalse();
```

---

## 3. `ReturnAnalysisCacheTests` — Erweiterung bestehender Datei

**Pfad:** `FinanceManager.Tests/Securities/ReturnAnalysisCacheTests.cs`  
**SUT:** `new MemoryReturnAnalysisCache(new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000 }))`  
**Arrange-Hinweis:** `IDisposable`-Fixture bereits vorhanden; Muster aus bestehenden Tests übernehmen.

### 3.1 Factory gibt null zurück

| Prio | Testfall |
|---|---|
| **1** | `GetOrCreateAsync_Should_NotCacheResult_When_FactoryReturnsNull` |

```
// Arrange
int callCount = 0;
Task<string?> Factory() { callCount++; return Task.FromResult<string?>(null); }
// Act – zwei Aufrufe
await _sut.GetOrCreateAsync("key-null", Factory, TimeSpan.FromMinutes(5));
await _sut.GetOrCreateAsync("key-null", Factory, TimeSpan.FromMinutes(5));
// Assert – Factory beide Male aufgerufen
callCount.Should().Be(2);
```

### 3.2 Cache-Ablauf (TTL-Expiry)

| Prio | Testfall |
|---|---|
| 2 | `GetOrCreateAsync_Should_CallFactoryAgain_When_EntryHasExpired` |

```
// Arrange – sehr kurze TTL
await _sut.GetOrCreateAsync("ttl-key", () => Task.FromResult<string?>("v1"), TimeSpan.FromMilliseconds(1));
await Task.Delay(50); // warte auf Ablauf
int callCount = 0;
// Act
await _sut.GetOrCreateAsync("ttl-key", () => { callCount++; return Task.FromResult<string?>("v2"); }, TimeSpan.FromMinutes(5));
// Assert
callCount.Should().Be(1);
```

### 3.3 Groß-/Kleinschreibung bei Schlüsseln

| Prio | Testfall |
|---|---|
| 2 | `GetOrCreateAsync_Should_ReturnCachedValue_When_KeyDiffersOnlyInCase` |

```
// Arrange
await _sut.GetOrCreateAsync("MyKey", () => Task.FromResult<string?>("val"), TimeSpan.FromMinutes(5));
int callCount = 0;
// Act
var result = await _sut.GetOrCreateAsync("mykey", () => { callCount++; return Task.FromResult<string?>("other"); }, TimeSpan.FromMinutes(5));
// Assert
result.Should().Be("val");
callCount.Should().Be(0);
```

### 3.4 Leeres Präfix bei `InvalidateAsync`

| Prio | Testfall |
|---|---|
| 2 | `InvalidateAsync_Should_RemoveAllEntries_When_PrefixIsEmpty` |

```
// Arrange – zwei Einträge cachen
await _sut.GetOrCreateAsync("ra:k1", () => Task.FromResult<string?>("a"), TimeSpan.FromMinutes(5));
await _sut.GetOrCreateAsync("ra:k2", () => Task.FromResult<string?>("b"), TimeSpan.FromMinutes(5));
// Act
await _sut.InvalidateAsync("");
// Assert – Factory muss wieder aufgerufen werden
int calls = 0;
await _sut.GetOrCreateAsync("ra:k1", () => { calls++; return Task.FromResult<string?>("a"); }, TimeSpan.FromMinutes(5));
calls.Should().Be(1);
```

### 3.5 `InvalidateAsync` auf leerem Cache

| Prio | Testfall |
|---|---|
| 3 | `InvalidateAsync_Should_NotThrow_When_CacheIsEmpty` |

```
// Act & Assert
var act = async () => await _sut.InvalidateAsync("any-prefix");
await act.Should().NotThrowAsync();
```

### 3.6 Thread-Safety

> ⚠️ **Siehe BUG-3** (oben): Race Condition zwischen `TryGetValue` und `Set` ist bekannt und dokumentiert.

| Prio | Testfall |
|---|---|
| 3 | `GetOrCreateAsync_Should_NotThrow_When_CalledConcurrently` |

```
// Arrange – 20 parallele Tasks mit demselben Schlüssel
int callCount = 0;
Task<string?> Factory() { Interlocked.Increment(ref callCount); return Task.FromResult<string?>("v"); }
var tasks = Enumerable.Range(0, 20)
    .Select(_ => _sut.GetOrCreateAsync("concurrent-key", Factory, TimeSpan.FromMinutes(5)));
// Act
var act = async () => await Task.WhenAll(tasks);
// Assert – kein Exception; Race Condition ist dokumentiert, mindestens 1 Call
await act.Should().NotThrowAsync();
callCount.Should().BeGreaterThanOrEqualTo(1);
```

---

## 4. `ReturnAnalysisServiceTests` — Neue Datei

**Pfad:** `FinanceManager.Tests/Securities/ReturnAnalysisServiceTests.cs`  
**Namespace:** `FinanceManager.Tests.Securities`

### Arrange-Grundstruktur

```csharp
public sealed class ReturnAnalysisServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IReturnCalculationService> _calcMock = new();
    private readonly Mock<IFifoCostBasisCalculator> _fifoMock = new();
    private readonly Mock<IReturnAnalysisCache> _cacheMock  = new();
    private readonly ReturnAnalysisService _sut;

    public ReturnAnalysisServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sut = new ReturnAnalysisService(
            _db,
            _calcMock.Object,
            _fifoMock.Object,
            _cacheMock.Object,
            NullLogger<ReturnAnalysisService>.Instance);
    }

    public void Dispose() => _db.Dispose();
}
```

**Cache-Mock-Standardsetup:**  
Den Cache-Mock so konfigurieren, dass er stets die Factory aufruft (kein echtes Caching):

```csharp
_cacheMock
    .Setup(c => c.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<T?>>>(), It.IsAny<TimeSpan>()))
    .Returns<string, Func<Task<T?>>, TimeSpan>((_, factory, __) => factory());
```

> **Hinweis:** Für Cache-spezifische Tests (`GetReturnSummaryAsync_Should_UseCachedValue_…`) stattdessen einen `MemoryReturnAnalysisCache` mit echtem `IMemoryCache` verwenden und sicherstellen, dass DB-Methoden nur einmal aufgerufen werden.

---

### 4.1 `GetReturnSummaryAsync` / `ComputeReturnSummaryAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetReturnSummaryAsync_Should_ReturnNull_When_SecurityDoesNotBelongToUser` |
| **1** | `GetReturnSummaryAsync_Should_ReturnNull_When_NoTransactionsExist` |
| **1** | `GetReturnSummaryAsync_Should_SetHasMissingPrices_When_NoCurrentPriceAvailable` |
| **1** | `GetReturnSummaryAsync_Should_SetOversellHint_When_OversellWarningActive` |
| 2 | `GetReturnSummaryAsync_Should_ReturnNullCagr_When_HoldingPeriodLessThanOneYear` |
| 2 | `GetReturnSummaryAsync_Should_ReturnCagr_When_HoldingPeriodAtLeastOneYear` |
| 2 | `GetReturnSummaryAsync_Should_ReturnNullIrr_When_NoSalesAndNoMarketValue` |
| 2 | `GetReturnSummaryAsync_Should_UseCachedValue_When_CalledTwice` |

**Arrange-Hinweis:**
- Security und User in InMemory-DB anlegen (`_db.Securities.Add(...)`, `_db.Users.Add(...)`, `_db.SaveChanges()`).
- Für `NoCurrentPriceAvailable`: Transaktionen vorhanden, aber keine Security-Preise in DB.
- Für Cache-Test: `_cacheMock` einmalig aufsetzen und dann `Times.Once` prüfen.

---

### 4.2 `GetSparklineDataAsync` / `ComputeSparklineDataAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetSparklineDataAsync_Should_ReturnNull_When_NoTransactionsExist` |
| **1** | `GetSparklineDataAsync_Should_ReturnNull_When_LessThan30PricePoints` |
| **1** | `GetSparklineDataAsync_Should_ReturnSparkline_When_Exactly30PricePoints` |
| 2 | `GetSparklineDataAsync_Should_ForwardFillGaps_When_PricesMissingOnWeekends` |

**Arrange-Hinweis:**
- 30 Preispunkte mit `SecurityPrice`-Entities an aufeinanderfolgenden Tagen anlegen.
- Für Forward-Fill: gezielt Lücken lassen und prüfen, dass `SparklineDataDto.Prices` an diesen Tagen den letzten bekannten Preis enthält.

---

### 4.3 `GetDetailedMetricsAsync` / `ComputeDetailedMetricsAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetDetailedMetricsAsync_Should_ReturnNull_When_UserNotFound` |
| **1** | `GetDetailedMetricsAsync_Should_ReturnNull_When_SecurityNotOwnedByUser` |
| **1** | `GetDetailedMetricsAsync_Should_ReturnNullSharpeRatio_When_ShowSharpeRatioIsFalse` |
| **1** | `GetDetailedMetricsAsync_Should_ReturnNullSharpeRatio_When_VolatilityIsZero` |
| 2 | `GetDetailedMetricsAsync_Should_ReturnSharpeRatio_When_TwrAndVolatilityPresent` |
| 2 | `GetDetailedMetricsAsync_Should_ReturnZeroDividendYield_When_InvestedCapitalIsZero` |
| 2 | `GetDetailedMetricsAsync_Should_ReturnZeroTaxRate_When_GrossReturnIsZero` |

**Arrange-Hinweis:**
- User mit `ShowSharpeRatio = false/true` und `RiskFreeRate` in DB anlegen.
- `_calcMock.Setup(c => c.CalculateTwr(...)).Returns(...)` für die jeweiligen Szenarien.
- `_fifoMock.Setup(f => f.Calculate(...)).Returns(new FifoCostBasisResult(...))`.

---

### 4.4 `GetPeriodicReturnsAsync` / `ComputePeriodicReturnsAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetPeriodicReturnsAsync_Should_ReturnNull_When_SecurityNotFound` |
| **1** | `GetPeriodicReturnsAsync_Should_ReturnNull_When_NoTransactionsExist` |
| **1** | `GetPeriodicReturnsAsync_Should_ReturnZeroAnnualReturn_When_StartValueIsZero` |
| 2 | `GetPeriodicReturnsAsync_Should_ReturnNullMonthReturn_When_MonthStartValueIsZero` |
| 2 | `GetPeriodicReturnsAsync_Should_CalculateYtdReturn_When_FirstBuyInCurrentYear` |
| 2 | `GetPeriodicReturnsAsync_Should_AccumulateDividends_When_DividendsSpanMultipleYears` |

**Arrange-Hinweis:**
- Preise und Transaktionen für mehrere Jahre anlegen.
- Jahresanfangs-/Monatsanfangs-Preise gezielt setzen oder weglassen.

---

### 4.5 `GetCashflowTimelineAsync` / `ComputeCashflowTimelineAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetCashflowTimelineAsync_Should_ReturnNull_When_SecurityNotFound` |
| **1** | `GetCashflowTimelineAsync_Should_ReturnEmptyDto_When_NoTransactions` _(nicht null!)_ |
| **1** | `GetCashflowTimelineAsync_Should_GroupCorrectly_When_TransactionsSpanMultipleYears` |
| 2 | `GetCashflowTimelineAsync_Should_ReturnSingleSummary_When_AllTransactionsInSameYear` |

```
// WICHTIG: Bei "keine Transaktionen" gibt ComputeCashflowTimelineAsync ein leeres
// CashflowTimelineDto zurück (nicht null) – das unterscheidet diese Methode von allen anderen!
result.Should().NotBeNull();
result!.AnnualSummaries.Should().BeEmpty();
```

---

### 4.6 `GetPerformanceChartDataAsync` / `ComputePerformanceChartDataAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetPerformanceChartDataAsync_Should_ReturnNull_When_SecurityNotFound` |
| **1** | `GetPerformanceChartDataAsync_Should_ReturnNull_When_NoTransactions` |
| **1** | `GetPerformanceChartDataAsync_Should_ReturnNull_When_NoPricesInTimeRange` |
| 2 | `GetPerformanceChartDataAsync_Should_UseOneMonthRange_When_TimeRangeIsOneMonth` |
| 2 | `GetPerformanceChartDataAsync_Should_UseMinDateAsFrom_When_TimeRangeIsAll` |

**Arrange-Hinweis:**
- Pro `ChartTimeRange`-Wert: Preise nur innerhalb des erwarteten Zeitfensters anlegen und prüfen, dass sie im Ergebnis-DTO erscheinen.
- Für `ChartTimeRange.All`: `fromDate == DateTime.MinValue` (oder sehr weit in der Vergangenheit).

---

### 4.7 `GetBenchmarkComparisonAsync` / `ComputeBenchmarkComparisonAsync`

| Prio | Testfall |
|---|---|
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_NoBenchmarkConfigured` |
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_BenchmarkSecurityNotOwnedByUser` |
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_TargetHasLessThan2PricePoints` |
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_BenchmarkHasLessThan2PricePoints` |
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_SecurityBasePriceIsZero` |
| **1** | `GetBenchmarkComparisonAsync_Should_ReturnNull_When_BenchmarkBasePriceIsZero` |
| 2 | `GetBenchmarkComparisonAsync_Should_NormalizeToBase100_When_ValidData` |

**Arrange-Hinweis:**
- User mit `BenchmarkSecurityId` anlegen.
- Zwei Securities: Target und Benchmark, jeweils mit Preishistorie.
- Für Normalisierung: erster Preis der Target-Security = 200m → nach Normalisierung = 100m; zweiter Preis = 220m → normalisiert = 110m prüfen.

---

### 4.8 `GetUserSettingsAsync`

| Prio | Testfall |
|---|---|
| 2 | `GetUserSettingsAsync_Should_ReturnNull_When_UserNotFound` |
| 2 | `GetUserSettingsAsync_Should_ReturnNullBenchmark_When_NoBenchmarkConfigured` |
| 2 | `GetUserSettingsAsync_Should_ReturnBenchmarkName_When_BenchmarkSecurityExists` |
| 2 | `GetUserSettingsAsync_Should_ReturnNullBenchmarkName_When_BenchmarkSecurityWasDeleted` |

---

### 4.9 `UpdateUserSettingsAsync`

| Prio | Testfall |
|---|---|
| **1** | `UpdateUserSettingsAsync_Should_ThrowArgumentException_When_BenchmarkSecurityNotOwnedByUser` |
| **1** | `UpdateUserSettingsAsync_Should_ClearBenchmark_When_BenchmarkIdIsNull` |
| 2 | `UpdateUserSettingsAsync_Should_ReturnSilently_When_UserNotFound` |
| 2 | `UpdateUserSettingsAsync_Should_PersistSettings_When_ValidRequest` |
| 3 | `UpdateUserSettingsAsync_Should_AcceptNegativeRiskFreeRate_When_NoServiceGuardExists` |

```
// Arrange – SecurityNotOwnedByUser
var otherUserId = Guid.NewGuid();
var security = new Security { Id = Guid.NewGuid(), OwnerId = otherUserId, ... };
_db.Securities.Add(security);
_db.SaveChanges();
// Act & Assert
var act = async () => await _sut.UpdateUserSettingsAsync(userId, security.Id, showSharpe: false, riskFreeRate: 0.02m, ct: default);
await act.Should().ThrowAsync<ArgumentException>();
```

---

### 4.10 `InvalidateCacheAsync`

| Prio | Testfall |
|---|---|
| 2 | `InvalidateCacheAsync_Should_InvalidateCorrectToken_When_CalledWithValidIds` |
| 2 | `InvalidateCacheAsync_Should_NotInvalidateOtherSecurityEntries_When_TokenIsSpecific` |

```
// Arrange
_cacheMock.Setup(c => c.InvalidateAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
var secId  = Guid.NewGuid();
var userId = Guid.NewGuid();
// Act
await _sut.InvalidateCacheAsync(secId, userId, default);
// Assert – Token muss beide IDs als Substring enthalten
_cacheMock.Verify(c => c.InvalidateAsync(
    It.Is<string>(s => s.Contains(secId.ToString()) && s.Contains(userId.ToString()))),
    Times.Once);
```

---

## 5. `ReturnAnalysisCacheKeysTests` — Neue Datei

**Pfad:** `FinanceManager.Tests/Securities/ReturnAnalysisCacheKeysTests.cs`  
**Hinweis:** `ReturnAnalysisCacheKeys` ist `internal` → Testprojekt muss über `InternalsVisibleTo` Zugriff haben oder Reflection verwenden.  
**Alternative:** Tests in `FinanceManager.Infrastructure.Tests` auslagern oder `InternalsVisibleTo` in `FinanceManager.Infrastructure.csproj` ergänzen.

| Prio | Testfall |
|---|---|
| 2 | `SecurityUserToken_Should_BeSubstringOfAllKeyTypes` |
| 2 | `Chart_Should_ContainTimeRangeSuffix_For_AllEnumValues` |
| 3 | `AllKeyMethods_Should_ProduceDifferentKeys_For_DifferentSecurityIds` |

```csharp
[Fact]
public void SecurityUserToken_Should_BeSubstringOfAllKeyTypes()
{
    // Arrange
    var secId  = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var token  = ReturnAnalysisCacheKeys.SecurityUserToken(secId, userId);

    // Act
    var keys = new[]
    {
        ReturnAnalysisCacheKeys.Summary(secId, userId),
        ReturnAnalysisCacheKeys.Sparkline(secId, userId),
        ReturnAnalysisCacheKeys.Metrics(secId, userId),
        ReturnAnalysisCacheKeys.Periodic(secId, userId),
        ReturnAnalysisCacheKeys.Cashflow(secId, userId),
        ReturnAnalysisCacheKeys.Chart(secId, userId, "OneYear"),
        ReturnAnalysisCacheKeys.Benchmark(secId, userId),
    };

    // Assert
    keys.Should().AllSatisfy(k => k.Should().Contain(token));
}
```

---

## 6. `ApiClientReturnAnalysisTests` — Neue Integrationstestdatei

**Pfad:** `FinanceManager.Tests.Integration/ApiClient/ApiClientReturnAnalysisTests.cs`  
**Fixture:** `IClassFixture<TestWebApplicationFactory>`  
**Pattern:** Wie `ApiClientSecuritiesTests` / `ApiClientUserSettingsTests` (bestehende Dateien als Vorlage).

### Setup-Hilfsmethoden (privat in der Testklasse)

```csharp
private async Task<(ApiClient api, SecurityDto security)> SetupSecurityWithTransactionsAsync()
{
    var api = CreateClient();
    await EnsureAuthenticatedAsync(api);

    var sec = await api.Securities_CreateAsync(new SecurityRequest { Name = "Test", Identifier = "TST", CurrencyCode = "USD", ... });

    // Preis anlegen (nötig für die meisten Endpunkte)
    await api.Securities_AddPriceAsync(sec!.Id, new SecurityPriceRequest(DateTime.UtcNow.Date, 100m));

    // Posting / Transaktion anlegen
    // (konkrete ApiClient-Methode je nach vorhandener API-Route einsetzen)

    return (api, sec!);
}
```

---

### 6.1 `GET {id}/return-summary`

| Prio | Testfall |
|---|---|
| **1** | `ReturnSummary_Should_Return200_When_SecurityHasTransactions` |
| **1** | `ReturnSummary_Should_Return404_When_SecurityBelongsToOtherUser` |
| **1** | `ReturnSummary_Should_Return401_When_Unauthenticated` |

```csharp
[Fact]
public async Task ReturnSummary_Should_Return401_When_Unauthenticated()
{
    var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    var response = await http.GetAsync($"/api/securities/{Guid.NewGuid()}/return-summary");
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

---

### 6.2 `GET {id}/return-sparkline`

| Prio | Testfall |
|---|---|
| **1** | `ReturnSparkline_Should_Return200_When_Enough30PricePoints` |
| **1** | `ReturnSparkline_Should_Return404_When_LessThan30PricePoints` |

**Arrange:** 30 Preispunkte über die API (oder direkt via DB) anlegen.

---

### 6.3 `GET {id}/return-metrics`

| Prio | Testfall |
|---|---|
| **1** | `ReturnMetrics_Should_Return200_When_SecurityExists` |
| **1** | `ReturnMetrics_Should_Return404_When_SecurityNotFound` |

---

### 6.4 `GET {id}/return-periodic`

| Prio | Testfall |
|---|---|
| **1** | `ReturnPeriodic_Should_Return200_When_TransactionsAndPricesPresent` |
| **1** | `ReturnPeriodic_Should_Return404_When_NoTransactions` |

---

### 6.5 `GET {id}/return-cashflows`

| Prio | Testfall |
|---|---|
| **1** | `ReturnCashflows_Should_Return200WithEmptyDto_When_NoTransactions` _(nicht 404!)_ |
| **1** | `ReturnCashflows_Should_Return404_When_SecurityNotFound` |

```csharp
[Fact]
public async Task ReturnCashflows_Should_Return200WithEmptyDto_When_NoTransactions()
{
    var (api, sec) = await SetupSecurityWithoutTransactionsAsync();
    var result = await api.Securities_GetCashflowTimelineAsync(sec.Id);
    result.Should().NotBeNull(); // 200 OK, leeres DTO
    result!.AnnualSummaries.Should().BeEmpty();
}
```

---

### 6.6 `GET {id}/return-chart`

| Prio | Testfall |
|---|---|
| **1** | `ReturnChart_Should_Return200_For_All_ChartTimeRange_Values` _(parameterisiert via `[Theory]`)_ |
| **1** | `ReturnChart_Should_Return404_When_NoPricesInTimeRange` |
| 2 | `ReturnChart_Should_Return400_When_InvalidTimeRangeValue` |

```csharp
[Theory]
[InlineData("OneMonth")]
[InlineData("ThreeMonths")]
[InlineData("SixMonths")]
[InlineData("OneYear")]
[InlineData("ThreeYears")]
[InlineData("All")]
public async Task ReturnChart_Should_Return200_For_TimeRange(string timeRange)
{
    var (api, sec) = await SetupSecurityWithPricesAsync(priceCount: 100);
    var result = await api.Securities_GetPerformanceChartAsync(sec.Id, timeRange);
    result.Should().NotBeNull();
}
```

---

### 6.7 `GET {id}/return-benchmark`

| Prio | Testfall |
|---|---|
| **1** | `ReturnBenchmark_Should_Return404_When_NoBenchmarkConfigured` |
| **1** | `ReturnBenchmark_Should_Return404_When_BenchmarkNotOwnedByUser` |
| **1** | `ReturnBenchmark_Should_Return200_When_ValidBenchmarkConfigured` |

**Arrange:** User-Settings via `PUT return-analysis/settings` mit gültiger `BenchmarkSecurityId` setzen.

---

### 6.8 `GET return-analysis/settings`

| Prio | Testfall |
|---|---|
| 2 | `ReturnSettings_Should_Return200WithFallback_When_SettingsNotYetSaved` |
| 2 | `ReturnSettings_Should_Return200_When_SettingsWerePreviouslySaved` |

---

### 6.9 `PUT return-analysis/settings`

| Prio | Testfall |
|---|---|
| **1** | `UpdateReturnSettings_Should_Return204_When_ValidRequest` |
| **1** | `UpdateReturnSettings_Should_Return400_When_BenchmarkSecurityNotOwnedByUser` |
| **1** | `UpdateReturnSettings_Should_Return400_When_RiskFreeRateIsNegative` |

```csharp
[Fact]
public async Task UpdateReturnSettings_Should_Return400_When_RiskFreeRateIsNegative()
{
    var api = CreateClient();
    await EnsureAuthenticatedAsync(api);

    var ok = await api.Securities_UpdateReturnAnalysisSettingsAsync(
        new ReturnAnalysisSettingsUpdateRequest(BenchmarkSecurityId: null, ShowSharpeRatio: false, RiskFreeRate: -0.01m));
    // Assert: 400 BadRequest wegen [Range]-Validation
    ok.Should().BeFalse(); // oder StatusCode direkt prüfen
}
```

---

### 6.10 `DELETE {id}/return-cache`

| Prio | Testfall |
|---|---|
| **1** | `DeleteReturnCache_Should_Return204_When_SecurityExistsOrNot` |
| **1** | `DeleteReturnCache_Should_Return401_When_Unauthenticated` |

---

## 7. `ReturnSummaryWidgetTests` — Neue Datei (optional)

**Pfad:** `FinanceManager.Tests/Components/ReturnSummaryWidgetTests.cs`  
**Framework:** bUnit (muss als NuGet-Paket ergänzt werden: `Bunit`)  
**Hinweis:** Blazor-Component-Tests erfordern `bUnit`. Falls noch nicht installiert, zuerst `dotnet add package bunit` ausführen.

| Prio | Testfall |
|---|---|
| 2 | `ReturnSummaryWidget_Should_NotCallApi_When_SecurityIdIsEmpty` |
| 2 | `ReturnSummaryWidget_Should_ShowSkeleton_When_Loading` |
| 2 | `ReturnSummaryWidget_Should_ShowError_When_ApiThrowsException` |
| 2 | `ReturnSummaryWidget_Should_ShowNoDataMessage_When_SummaryIsNull` |
| 2 | `ReturnSummaryWidget_Should_HideCagrLine_When_CagrIsNull` |
| 2 | `ReturnSummaryWidget_Should_HideIrrLine_When_IrrIsNull` |
| 2 | `ReturnSummaryWidget_Should_ShowWarning_When_HasMissingPrices` |
| 2 | `ReturnSummaryWidget_Should_SetPositiveCssClass_When_TotalReturnIsNonNegative` |
| 2 | `ReturnSummaryWidget_Should_SetNegativeCssClass_When_TotalReturnIsNegative` |
| 3 | `ReturnSummaryWidget_Should_NavigateToPerformancePage_When_DetailLinkClicked` |
| 3 | `ReturnSummaryWidget_Should_ReloadData_When_SecurityIdParameterChanges` |

---

## 8. `SecurityPerformancePageTests` — Neue Datei (optional)

**Pfad:** `FinanceManager.Tests/Components/SecurityPerformancePageTests.cs`  
**Framework:** bUnit

| Priorität | Testfall |
|---|---|
| Mittel | `SecurityPerformancePage_Should_ShowLoginHint_When_UserIsNotAuthenticated` |
| Mittel | `SecurityPerformancePage_Should_ShowNotFoundUi_When_SecurityReturnsNull` |
| Mittel | `SecurityPerformancePage_Should_SetSecurityName_When_SecurityFound` |
| Mittel | `SecurityPerformancePage_Should_ShowOverviewTab_By_Default` |
| Niedrig | `SecurityPerformancePage_Should_ShowTimeSeriesTab_When_TabSwitched` |
| Niedrig | `SecurityPerformancePage_Should_ShowCashflowTab_When_TabSwitched` |
| Niedrig | `SecurityPerformancePage_Should_ShowMetricsTab_When_TabSwitched` |
| Niedrig | `SecurityPerformancePage_Should_ShowBenchmarkTab_When_TabSwitched` |
| Niedrig | `SecurityPerformancePage_Should_NavigateBack_When_BackButtonClicked` |

---

## 8. `SecurityPerformancePageTests` — Neue Datei (optional)

**Pfad:** `FinanceManager.Tests/Components/SecurityPerformancePageTests.cs`  
**Framework:** bUnit

| Prio | Testfall |
|---|---|
| 2 | `SecurityPerformancePage_Should_ShowLoginHint_When_UserIsNotAuthenticated` |
| 2 | `SecurityPerformancePage_Should_ShowNotFoundUi_When_SecurityReturnsNull` |
| 2 | `SecurityPerformancePage_Should_SetSecurityName_When_SecurityFound` |
| 2 | `SecurityPerformancePage_Should_ShowOverviewTab_By_Default` |
| 3 | `SecurityPerformancePage_Should_ShowTimeSeriesTab_When_TabSwitched` |
| 3 | `SecurityPerformancePage_Should_ShowCashflowTab_When_TabSwitched` |
| 3 | `SecurityPerformancePage_Should_ShowMetricsTab_When_TabSwitched` |
| 3 | `SecurityPerformancePage_Should_ShowBenchmarkTab_When_TabSwitched` |
| 3 | `SecurityPerformancePage_Should_NavigateBack_When_BackButtonClicked` |

---

## 4.11 `ReturnAnalysisService` — Interne Hilfsmethoden (Neue Testmethoden in `ReturnAnalysisServiceTests`)

> Diese Methoden sind `private static` und werden indirekt durch die `GetXxxAsync`-Methoden ausgeführt.  
> Tests müssen über die öffentlichen Methoden (mit InMemory-DB) oder durch Refactoring zu `internal` zugänglich gemacht werden.

### `BuildTwrPeriods` — bestätigter Logikfehler (BUG-1)

> ⚠️ **KRITISCH**: Dieser Test wird **rot** sein bis der Bug in Zeile 744 behoben wird (`start` → `end`).

| Prio | Testfall |
|---|---|
| **1** | `GetReturnSummaryAsync_Should_UseSharesOnEndDate_ForEndValue_When_SharesChangeWithinPeriod` |
| **1** | `GetReturnSummaryAsync_Should_ComputeCorrectTwr_When_MultipleTransactionsExist` |

**Arrange für den BUG-1-Regressionstest:**
```csharp
// Arrange: 2 Käufe und Preise, so dass sharesAtEnd ≠ sharesAtStart
// Buy 10 Stück am 2024-01-01 zu 100 EUR
// Buy weitere 5 Stück am 2024-06-01 zu 120 EUR
// Preis am 2024-01-01 = 100, am 2024-06-01 = 120, am 2024-12-31 = 130
// 
// Erwartung mit korrektem Code: endValue für Periode Jan→Jun = 10 Stück × 120 = 1200
// Mit BUG (start statt end): endValue = 10 × 120 = 1200 (zufällig korrekt für Periode 1)
//
// Für Periode Jun→Dez:
// Korrekt: endValue = 15 Stück × 130 = 1950
// Mit BUG: endValue = 15 × 130 = 1950 (erst nach 2. Buy identisch)
//
// Der Bug tritt auf wenn shares sich INNERHALB der Periode ändern:
// Buy 10 Stück am 2024-01-01, Buy 5 Stück am 2024-06-15 (innerhalb Periode)
// Periode: 2024-01-01 → 2024-06-15
// Korrekt:   sharesAtEnd  = ComputeSharesHeldOnDate(tx, 2024-06-15) = 15
// Mit BUG: sharesAtEnd = ComputeSharesHeldOnDate(tx, 2024-01-01) = 10
```

---

### `ForwardFill` — Randfälle

| Prio | Testfall |
|---|---|
| 2 | `GetSparklineDataAsync_Should_ReturnNull_When_PriceListIsEmpty` |
| 2 | `GetSparklineDataAsync_Should_ForwardFillFromLastKnownPrice_When_GapsExistInMiddle` |
| 3 | `GetSparklineDataAsync_Should_HandlePricesOnlyAtStart_When_NoRecentPricesExist` |

---

### `BuildIrrCashflows` — Randfälle

| Prio | Testfall |
|---|---|
| **1** | `GetReturnSummaryAsync_Should_ReturnNullIrr_When_CurrentMarketValueIsZeroOrNegative` |
| 2 | `GetReturnSummaryAsync_Should_IncludeTerminalCashflow_When_CurrentMarketValueIsPositive` |

```csharp
// Arrange: Nur Buy-Transaktionen, keine Sells, aktueller Kurs = 0m
// → BuildIrrCashflows fügt keinen Terminal-Cashflow ein
// → Alle Cashflows sind negativ (Buy-Outflows) → kein Sign-Change → IRR = null
```

---

### `GetPortfolioValueOnDate` — kein Preis verfügbar

| Prio | Testfall |
|---|---|
| 2 | `GetPeriodicReturnsAsync_Should_UseZeroAsStartValue_When_NoPriceExistsBeforeDate` |

---

### `GetFromDateForTimeRange` — alle 6 Enum-Werte

| Prio | Testfall |
|---|---|
| 2 | `GetPerformanceChartDataAsync_Should_MapAllTimeRangeValues_ToCorrectFromDate` |

```csharp
// [Theory] mit allen 6 ChartTimeRange-Werten:
// OneMonth     → fromDate ≈ Today.AddMonths(-1)
// ThreeMonths  → fromDate ≈ Today.AddMonths(-3)
// SixMonths    → fromDate ≈ Today.AddMonths(-6)
// OneYear      → fromDate ≈ Today.AddYears(-1)
// ThreeYears   → fromDate ≈ Today.AddYears(-3)
// All          → fromDate == DateTime.MinValue (oder sehr weit in der Vergangenheit)
```

---

### `ComputeInvestedCapitalOnDate` — Näherung vs. FIFO (BUG-4)

| Prio | Testfall |
|---|---|
| 2 | `GetSparklineDataAsync_Should_ApproximateInvestedCapital_When_PartialSellOccurs` |

**Arrange-Hinweis:**
```csharp
// Buy 10 Stück à 100 EUR = 1000 EUR
// Sell 5 Stück (Hälfte) → FIFO-Kostenbasis = 500 EUR
// ComputeInvestedCapitalOnDate = Summe aller Buy-Beträge - Summe aller Sell-Beträge (Näherung)
// Bei Kursgewinnen: approximierter Wert weicht von FIFO ab → Test dokumentiert die Abweichung
```

---

```
Phase 1 – Prio 1: Bugs, Guards, Security (~42 Tests)
  ├── ReturnCalculationServiceTests: §1.1–1.9 (Division-by-Zero, null-Guards, BUG-2 Volatility)
  ├── FifoCostBasisCalculatorTests: §2.1–2.10 (null-Guards, Oversell)
  ├── ReturnAnalysisCacheTests: §3.1 (null-Factory)
  ├── ReturnAnalysisServiceTests: §4.1 (Ownership/Security), §4.9 (ArgumentException), §4.11 (BUG-1 BuildTwrPeriods!)
  └── ApiClientReturnAnalysisTests: §6.1/6.3/6.9 (401, 404 Security, 400 Validation)

Phase 2 – Prio 2: Business Logic (~47 Tests)
  ├── ReturnAnalysisServiceTests: §4.2–4.8, 4.10, 4.11 restliche Hilfsmethoden
  ├── ReturnAnalysisCacheTests: §3.2–3.4
  └── ApiClientReturnAnalysisTests: §6.2, 6.4–6.10

Phase 3 – Prio 3: UI + Nice-to-Have (~23 Tests)
  ├── ReturnAnalysisCacheKeysTests (§5)
  ├── ReturnAnalysisCacheTests: §3.5–3.6
  ├── ReturnSummaryWidgetTests (§7, bUnit)
  └── SecurityPerformancePageTests (§8, bUnit)
```

---

## Offene Fragen / Risiken

| # | Frage / Risiko | Empfehlung |
|---|---|---|
| R-1 | **BUG-1**: `BuildTwrPeriods` Zeile 744 verwendet `start` statt `end` für `sharesAtEnd` | Prio-1-Regressionstest schreiben → rot laufen lassen → Fix deployen → grün |
| R-2 | **BUG-2**: `CalculateVolatility` mit genau 2 Preisen liefert `null` (undokumentiert) | Test schreiben (`Should_ReturnNull_When_ExactlyTwoPrices`) → Verhalten entweder dokumentieren oder Guard auf `< 3` korrigieren |
| R-3 | **BUG-3**: Race Condition in `MemoryReturnAnalysisCache` zwischen `TryGetValue` und `Set` | Thread-Safety-Test (§3.6) als Nicht-Regression einbauen; separates Issue für Fix mit `SemaphoreSlim` |
| R-4 | `ReturnAnalysisCacheKeys` ist `internal` → kein direkter Zugriff aus Test-Assembly | `[assembly: InternalsVisibleTo("FinanceManager.Tests")]` in `FinanceManager.Infrastructure.csproj` ergänzen |
| R-5 | bUnit noch nicht als Abhängigkeit vorhanden | `dotnet add FinanceManager.Tests package bunit` vor Phase 3 ausführen |
| R-6 | `ApiClient`-Methoden für neue Return-Endpunkte evtl. noch nicht generiert | Prüfen ob `Securities_GetReturnSummaryAsync(...)` o.ä. in `FinanceManager.Shared/ApiClient` existiert; ggf. OpenAPI-Client neu generieren |
| R-7 | `BuildTwrPeriods` ist `private static` → kein direkter Zugriff für Tests | Methode zu `internal static` machen und `InternalsVisibleTo` setzen, ODER Tests ausschließlich über `GetReturnSummaryAsync`/`GetDetailedMetricsAsync` treiben |
