# Logik und Services: Bestehende Implementierungen

## Hauptservices

### `BudgetReportService`
Datei: `FinanceManager.Infrastructure/Budget/BudgetReportService.cs` (1500+ Zeilen)

Implementierung von `IBudgetReportService` mit der komplexen prozeduralen Logik zur Budgetbericht-Generierung. **Diese Klasse wird durch das neue Domänenmodell ersetzt.**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetRawDataAsync | Public | Generiert Rohdaten für einen Zeitraum (Interface-Implementierung) |
| GetMonthlyKpiAsync | Public | Generiert KPI-Daten für einen einzelnen Monat (Interface-Implementierung) |
| BuildPostingDtosAsync | Private | Lädt und annotiert Buchungen mit Budget-Metadaten |
| BuildUncategorizedPurposeDtosAsync | Private | Verarbeitet unkategorisierte Zwecke und ordnet Buchungen zu |
| BuildUnbudgetedPostingsAsync | Private | Identifiziert Buchungen ohne Budgetzuordnung |
| BuildCategoriesAsync | Private | Aggregiert kategorisierte Daten |
| BuildUncategorizedPurposesAsync | Private | Aggregiert unkategorisierte Zwecke |
| BuildPurposesAsync | Private | Baut Zweck-DTOs mit Buchungen |
| GetBudgetedAmountForPurposeAsync | Private | Berechnet Gesamtbudget für Zweck |
| GetBudgetedAmountForCategoryAsync | Private | Berechnet Gesamtbudget für Kategorie |
| GetBudgetedIncomeForPurposeAsync | Private | Berechnet budgetiertes Einkommen |
| GetBudgetedExpenseForPurposeAsync | Private | Berechnet budgetierte Ausgaben |
| GetBudgetedIncomeForCategoryAsync | Private | Berechnet Einkommensbudget für Kategorie |
| GetBudgetedExpenseForCategoryAsync | Private | Berechnet Ausgabenbudget für Kategorie |
| GetActualPostingsAsync | Private | Ruft tatsächliche Buchungen ab |
| ComputeBudgetedOccurrences | Private/Static | Berechnet alle Vorkommen von Regeln in einem Zeitraum |
| CountOccurrencesInRange | Private/Static | Zählt Regel-Vorkommen in einem Bereich |
| EnumerateRulePeriods | Private/Static | Iteriert über Zeiträume einer Regel |
| ComputeBudgetedAmountForPeriod | Private/Static | Berechnet Budgetierter Betrag für Periode |
| GetPostingDate | Private/Static | Gibt Buchungsdatum basierend auf DateBasis zurück |

**Komplexe Zuordnungslogik:**
- Buchungen werden pro Monat nach verschiedenen Kriterien gefiltert (DateBasis: BookingDate oder ValutaDate)
- Zuordnung nach Contact/ContactGroup/SavingsPlan-Match mit optionalem Regex-Pattern-Matching
- Unterscheidung zwischen ExactPostings (Sign-Match) und TotalBudget (alle Beträge)
- Mehrere Gesamtbudgets werden nach StartDate und Erstellungsreihenfolge priorisiert
- Übersteigende Beträge werden aufgeteilt zwischen budgetiert und unbudgetiert
- Kostenneutrale Buchungen (GroupId) erhalten spezielle Behandlung

**Abhängigkeiten (Konstruktor):**
- `IBudgetPurposeService` — Zweck-Overviews
- `IBudgetCategoryService` — Kategorie-Overviews
- `IBudgetRuleService` — Budgetregel-Lookup
- `IPostingsQueryService` — Buchungsabruf
- `IContactService` — Kontakt-Daten
- `ISavingsPlanService` — Sparplan-Daten
- `ISecurityService` — Wertpapier-Daten
- `IReportCacheService` — Caching

### `IBudgetReportService`
Datei: `FinanceManager.Application/Budget/IBudgetReportService.cs`

Service-Interface mit zwei Hauptmethoden.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GetRawDataAsync | ownerUserId, from, to, dateBasis, ct, ignoreCache | Task<BudgetReportRawDataDto> | Rohdaten für Zeitraum abrufen |
| GetMonthlyKpiAsync | userId, date, dateBasis, ct | Task<MonthlyBudgetKpiDto> | KPI-Daten für Monat abrufen |

## Cache und Persistierung

### `ReportCacheService`
Datei: `FinanceManager.Infrastructure/Budget/ReportCacheService.cs`

Implementierung von `IReportCacheService` für datenbankgestützte Caching von Budgetbericht-Rohdaten.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetBudgetReportRawDataAsync | Public | Ruft gecachte Rohdaten ab |
| SetBudgetReportRawDataAsync | Public | Speichert Rohdaten im Cache |
| MarkAllReportCacheEntriesForUpdateAsync | Public | Markiert alle Einträge als aktualisierungsbedürftig |
| ClearReportCacheAsync | Public | Löscht alle Cache-Einträge |
| EnqueueBudgetReportCacheRefresh | Public | Queued Cache-Refresh-Task |
| GetNextBudgetReportCacheToUpdateAsync | Public | Ruft nächsten zu aktualisierenden Cache-Eintrag ab |
| MarkBudgetReportCacheEntriesForUpdateAsync | Public | Markiert Cache-Einträge für Zeitraum als aktualisierungsbedürftig |
| BuildKey | Private/Static | Erstellt Cache-Key aus Parametern (Format: "budgetreportraw-YYYYMMDD-YYYYMMDD-DateBasis") |

**Serialisierung:** Verwendet JsonSerializer für vollständiges Objekt-Graph-Serialisierung von `BudgetReportRawDataDto`

**Abhängigkeiten:**
- `AppDbContext` — Datenbankzugriff
- `IBackgroundTaskManager` — Asynchrone Task-Verwaltung

### `IReportCacheService`
Datei: `FinanceManager.Application/Budget/IReportCacheService.cs`

Service-Interface für Report-Caching-Operationen.

## Hilfsfunktionalität

### `BudgetRulePatternMatcher`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetRulePatternMatcher.cs`

Statische Klasse zum Pattern-Matching gegen Buchungstexte.

| Methode | Zweck |
|---------|--------|
| MatchesPosting(subject, description, pattern, useRegex, timeout) | Pattern-Match mit Substring- oder Regex-Unterstützung |

**Implementierungsdetails:**
- Einfache Pattern: Case-insensitive Substring-Match
- Regex-Pattern: Case-insensitive, Kultur-invariant, mit 1 Sekunde Timeout
- Leere Patterns matchen immer
- Fehlerbehandlung: Exception-Fangung bei Invalid Regex → false zurückgeben

## Abhängigkeitsbeziehungen

```
BudgetReportService (zu ersetzen)
├─ IBudgetPurposeService
│  └─ BudgetPurposeOverviewDto
├─ IBudgetCategoryService
│  └─ BudgetCategoryOverviewDto
├─ IBudgetRuleService
│  └─ BudgetRuleDto
├─ IPostingsQueryService
│  └─ PostingServiceDto
├─ IContactService
│  └─ ContactDto
├─ ISavingsPlanService
│  └─ SavingsPlanDto
├─ ISecurityService
│  └─ SecurityDto
└─ IReportCacheService
   └─ ReportCacheEntry (Domain Entity)

ReportCacheService
├─ AppDbContext
│  └─ ReportCacheEntry (DbSet)
└─ IBackgroundTaskManager
```

## Test-Infrastruktur

### Bestehende Testklassen

**`BudgetReportServiceTests`** (FinanceManager.Tests/Budget/)
- Umfassende Unit-Tests für volle Monate mit in-memory Setup
- Testabdeckung: 14+ verschiedene Budget-Konstellationen
- Szenarien: Gruppierte Zwecke, Mixed Income/Expense, Overruns, Unbudgeted, etc.

**`BudgetReportServiceRawDataTests`** (FinanceManager.Tests/Infrastructure/Budget/)
- Tests für Raw-Data-Generierung

**`ReportCacheServiceTests`** (FinanceManager.Tests/Infrastructure/Budget/)
- Tests für Caching-Funktionalität

**`MonthlyBudgetKpiViewModelTests`** (FinanceManager.Tests/ViewModels/)
- Tests für KPI-View-Model
