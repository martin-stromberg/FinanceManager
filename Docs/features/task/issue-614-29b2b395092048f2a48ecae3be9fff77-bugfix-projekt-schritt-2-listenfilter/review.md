# Plan-Review – Schritt 2: Listenfilter (#599, #601, #604)

**Status:** Vollständig umgesetzt

## Offene Aufgaben

Keine. Die Review-Findings wurden in derselben Iteration behandelt:

- [x] `ArgumentOutOfRangeException`-Guard in beiden Enumerations-Schleifen (`DateTime`-Überlauf leerte die komplette Liste) — behoben.
- [x] `BuildRecords()` in `SecuritiesListViewModel.LoadAsync(ct)` ergänzt — behoben.
- [x] `IntervalMonths`-Duplikat mit Sync-Hinweis versehen; XML-Doku der public Intervall-Methoden präzisiert — behoben.
- [x] CS8629 (`plan.Interval.Value`) via `is not { } interval`-Pattern aufgelöst — behoben.
- [x] E2E-Zeilen-Counting korrigiert: `tbody tr` + `.generic-list-mobile-card` existieren beide im DOM (CSS-Sichtbarkeit) → nur sichtbare Knoten zählen — behoben.
- [x] E2E-Härtung: Retry um Filter-Interaktion mit Stabilitätsfenster (Provider-Recreation beim Circuit-Start kann gefilterten Zustand überschreiben) — behoben.
- [x] E2E-Lücke geschlossen: Users-Test verifizierte zuvor gegen `adminName` — ununterscheidbar (gefiltert/ungefiltert beide = 1 Treffer); jetzt gegen Präfix `list-filter-` — behoben.

## Prüfbericht — Planentscheidungen vs. Umsetzung

### A) `SavingsPlan.cs` — Intervall-Mathematik public — umgesetzt

`AddIntervalWithMonthEndRule` ist `public static`; das neue `SubtractIntervalWithMonthEndRule` ist das exakte Spiegelbild für die Rückwärts-Enumeration vergangener Fälligkeiten; gemeinsamer privater `IntervalMonths`-Helper (Monthly=1 … Annually=12). XML-Doku vollständig inkl. Überlauf-Exception-Doku.

### B) `SavingsPlansListViewModel.LoadPageAsync` — Suche + Zeitraum vor Projektion — umgesetzt

- `MatchesSearch`: Name + `ContractNumber` + Kategoriename, `OrdinalIgnoreCase`, Trim — wie geplant.
- `MatchesRange`: Einzeltermin-Pläne matchen `TargetDate ∈ [from,to]`; Recurring-Pläne enumerieren Fälligkeiten rückwärts (≥ `max(from, CreatedUtc)`) und vorwärts (≤ `min(to, ArchivedUtc)`) via Monatsende-Regeln — wie geplant inkl. Archiv-Schranke und Phantom-Fälligkeit-Vorab-Guard.
- Pläne ohne `TargetDate` bei gesetztem Zeitraum ausgeblendet; einseitige Grenze = offenes Ende; `Von > Bis` wird geswappt (Konvention `BudgetPurposeListViewModel`).
- Filterung erfolgt **vor** `Items`-Projektion und den N+1-Analyseaufrufen — per Test `FilteredPlans_SkipAnalysisCalls` belegt.
- Filter UND-verknüpft mit `OnlyActive` (API-Parameter).

### C) `SecuritiesListViewModel` / `UserListViewModel` — umgesetzt

- `AllowRangeFiltering => false` auf beiden (Konvention `ContactListViewModel`/`BankAccountListViewModel`/`SavingsPlanCategoryListViewModel`) → `GenericListPage` rendert keinen sinnlosen Datumsfilter mehr (#604).
- Client-seitige `Search`: Securities über Name/`Identifier`/`AlphaVantageCode`/`CategoryName`; Users über `Username` (#599/#604 — vorher lud die Suche nur neu, filterte nichts).

### D) Tests — umgesetzt

- `SavingsPlanIntervalMathTests` (Domäne, symmetrisch Add/Subtract inkl. Monatsende-Capping).
- `SavingsPlansListFilterTests` (14 Fälle: Suche Felder/Case/Leersuche, Einzeltermin-Grenzen, Recurring-Rückwärts/Vorwärts, Archiv-Schranke, Phantom-Fälligkeit vor `CreatedUtc`, Von>Bis-Swap, einseitige Grenzen, kein `TargetDate`, UND mit `OnlyActive`, Analyse-Skip).
- `SecuritiesListViewModelTests` + `UsersViewModelTests`: Suchfilter-Tests ergänzt.

### E) E2E-Abnahme — umgesetzt

`ListFilterPlaywrightTests` (4 Tests) mit Screenshots — visuell geprüft:

- `s2-savings-plans-search-filtered.png`: Suche „alphaplan" → nur AlphaPlan.
- `s2-savings-plans-range-filtered.png`: Zeitraum 01.–30.06.2026 → nur Plan mit Zieldatum 15.06.2026.
- `s2-securities-search-filtered.png`: Suche „alphasec" → nur AlphaSec, kein Datumsfilter.
- `s2-users-search-filtered.png`: Suche Admin-Name → nur Admin-Zeile, kein Datumsfilter.

### Scope-Ausschlüsse / Projektregeln — eingehalten

Keine EF-/Enum-/resx-/Backup-Änderungen; keine root-absoluten URLs; XML-Doku vollständig; keine neuen Enum-Mitglieder (`EnumCoverageTests` unberührt); `SavingsPlanDto` unverändert.

### Akzeptierte, dokumentierte Einschränkungen (aus Reviews, kein Handlungsbedarf im Scope)

- Recurring-Rekonstruktion unter `CreatedUtc`-Schranke ist Näherung (Phantom-Fälligkeit möglich) — so spezifiziert.
- Monatsende-Drift bei Rückwärts-Rekonstruktion (31.→30.→28.) nicht invertierbar — per Test festgenagelt.
- Listen-weite UX-Themen (Debounce, `LastError`-Anzeige, Filter-Reset-Ribbon, differenzierter Leertext, A11y-Labels) als Folge-Verbesserungen in `review-usability.md` vertagt — nicht schrittspezifisch.
