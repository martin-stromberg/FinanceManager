# Umsetzungsplan — Schritt 2: Listenfilter (#599, #601, #604)

Branch: `task/issue-614-…-bugfix-projekt-schritt-2-listenfilter` (Basis: Projekt-Basisbranch)

## Design-Entscheidung

Clientseitige Filterung in den ViewModels (Option b) — die Listen werden ohnehin ungepagt
vollständig geladen (`CanLoadMore = false`); kein API-Contract-Ausbau nötig. Suchfelder und
Datumsfilter-Semantik wie in `requirement.md` festgelegt (Issue-Vorschläge übernommen).

## Änderungen

### 1. `FinanceManager.Domain/Savings/SavingsPlan.cs`

- `AddIntervalWithMonthEndRule` → `public static` (Sichtbarkeit für Web-VM + Tests).
- Neu: `public static DateTime SubtractIntervalWithMonthEndRule(DateTime date, SavingsPlanInterval interval)`
  — gleiche Monatsend-Semantik in Rückwärtsrichtung (war Monatsende → Monatsende des Zielmonats,
  sonst Tag gedeckelt auf Monatslänge). Implementierung spiegelt die Add-Variante mit negativen
  Monatswerten.

### 2. `SavingsPlansListViewModel.LoadPageAsync`

Nach `SavingsPlans_ListAsync` + `SavingsPlanCategories_ListAsync`, vor Items-Projektion:

```
list = list.Where(MatchesSearch).Where(MatchesRange)
```

- `MatchesSearch(dto, categories)`: `Search` leer → true; sonst OrdinalIgnoreCase-Contains auf
  `Name`, `ContractNumber`, Kategoriename (`categories[dto.CategoryId]`).
- `MatchesRange(dto)`: kein `RangeFrom`/`RangeTo` → true; Von>Bis → tauschen;
  `!dto.TargetDate` → false; `Type != Recurring || !Interval` → `TargetDate ∈ [from,to]` (Date-Vergleich);
  Recurring:
  ```
  upper = min(to ?? MaxValue, ArchivedUtc ?? MaxValue)   // Archivdatum = Planende
  if (!to && !ArchivedUtc) → true                       // aktiver Plan: Fälligkeiten unbeschränkt
  L = max(from ?? floor, floor)  (floor = CreatedUtc.Date);  upper < L → false
  rückwärts ab TargetDate bis d < L; vorwärts ab TargetDate bis next > upper;
  Treffer bei Fälligkeit ∈ [L, upper]
  ```
  Guard: `Type == Recurring && IntervalMonths(Interval) > 0` (ungültige Intervalle → Einzeldatum-Fallback).
  Hinweis (Review): Die Rückrechnung ist bei Monatsende-Drift nicht exakt umkehrbar — akzeptierte
  Einschränkung, in requirement.md dokumentiert.
- Gefilterte Liste → `_intervalByPlan`/`_typeByPlan`/Items wie bisher; Analyse nur für sichtbare Pläne.
- `AllowRangeFiltering` bleibt `true` (Sparpläne haben Datumsbezug).

### 3. `SecuritiesListViewModel`

- `public override bool AllowRangeFiltering => false;`
- In `LoadAsync`: `list.Where(s => MatchesSearch(s))` vor `Items.AddRange` — Contains auf
  `Name`, `Identifier`, `AlphaVantageCode`, `CategoryName`.

### 4. `UserListViewModel`

- `public override bool AllowRangeFiltering => false;`
- In `LoadAsync`: `list.Where(u => u.Username?.Contains(Search, OrdinalIgnoreCase) == true)`.

## Tests

- `SavingsPlansViewModelTests` (oder neue `SavingsPlansListFilterTests` nach test-class-structure):
  Suche Name/ContractNumber/Kategorie, Case-insensitive, Leer-Suche; Datumsfilter OneTime in/außerhalb,
  Grenzen inklusive, nur-Von, nur-Bis, Von>Bis-Tausch, Open ohne Zieldatum ausgeblendet,
  Recurring mit Fälligkeit im Zeitraum obwohl `TargetDate` außerhalb (vorwärts + rückwärts),
  UND mit Suche.
- `SecuritiesListViewModelTests`: Suche auf allen vier Feldern + `AllowRangeFiltering == false`.
- `UsersViewModelTests`: Suche auf `Username`, Case-insensitive, Leer-Suche + `AllowRangeFiltering == false`.
- Domänentest `FinanceManager.Tests/Savings/`: `SubtractIntervalWithMonthEndRule` (Monatsende→Monatsende,
  Tag-Deckelung, alle Intervalle, Fehler bei unbekanntem Intervall) — Spiegelung der Add-Tests
  (falls vorhanden, sonst beide Seiten abdecken).

## Offene Punkte (aus Issues) — Entscheidungen

- Suchfelder wie in den Issue-Vorschlägen übernommen (s. requirement.md).
- Vergangene Zeiträume bei Recurring: ja — Fälligkeiten in beide Richtungen (Issue-Vorschlag).
- `Type = Open` ohne `TargetDate`: bei Datumsfilter ausgeblendet (Issue-Vorschlag).
- Debounce der Suche: kein Umbau (GenericListPage-Verhalten unverändert; Filterung vor Analyse-Aufrufen
  begrenzt die N+1-Kosten).
- Optionaler Ribbon `ClearSearch`: nicht umgesetzt (nicht gefordert; wäre eigenes UI-Feature).
