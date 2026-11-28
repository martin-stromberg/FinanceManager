# Abnahme Schritt 2 — Suche/Datumsfilter (#599, #601, #604)

Datum: 2026-11-28
Schritt-Branch: `task/issue-614-29b2b395092048f2a48ecae3be9fff77-bugfix-projekt-schritt-2-listenfilter`

## Umgesetzte Änderungen

- `SavingsPlan.cs`: `AddIntervalWithMonthEndRule` → `public static`; neuer `SubtractIntervalWithMonthEndRule`
  (Spiegelbild zur Rückwärts-Enumeration vergangener Fälligkeiten); gemeinsamer `IntervalMonths`-Helper.
- `SavingsPlansListViewModel.LoadPageAsync`: `Search` (Name + `ContractNumber` + Kategoriename,
  OrdinalIgnoreCase) und Datumsbereich vor der `Items`-Projektion/den N+1-Analyseaufrufen angewendet.
  Recurring-Pläne: Fälligkeits-Rekonstruktion rückwärts (≥ `max(Von, CreatedUtc)`) und vorwärts
  (≤ `min(Bis, ArchivedUtc)`) via Monatsende-Regeln; Pläne ohne `TargetDate` bei gesetztem Zeitraum
  ausgeblendet; einseitige Grenze = offenes Ende; `Von > Bis` geswappt; `ArgumentOutOfRangeException`-Guard
  gegen Datumsdomänen-Überlauf.
- `SecuritiesListViewModel`: `AllowRangeFiltering => false` + client-seitige `Search` über
  Name/`Identifier`/`AlphaVantageCode`/`CategoryName`; `BuildRecords()` im konkreten `LoadAsync`.
- `UserListViewModel`: `AllowRangeFiltering => false` + client-seitige `Search` über `Username`.

## Reviews

- `plan-check.md`: bestanden (3 Lücken gemeldet → Archiv-Schranke, Intervall-Guard, Semantik-Doku geschlossen)
- `review-code.md`: 1× medium behoben (DateTime-Überlauf → ganzes Listen-Leeren), 4× low behoben/dokumentiert
- `review-usability.md`: 4× medium + 6× low bewertet — Kernfindings sind listen-weite Bestandsthemen
  (Debounce, LastError-Anzeige, Filter-Reset, Leertext, A11y) und als Folge-Verbesserungen vertagt;
  schrittspezifische Semantik (Recurring-Match, unsichtbare Suchfelder) fachlich begründet akzeptiert.

## Tests

- Unit: `SavingsPlanIntervalMathTests` + `SavingsPlansListFilterTests` + Ergänzungen in
  `SecuritiesListViewModelTests`/`UsersViewModelTests` — 43 Tests, grün.
- E2E (Playwright): `FinanceManager.Tests.E2E/Tests/Lists/ListFilterPlaywrightTests.cs` — 4 Tests, grün.
  Test-Härtung: nur sichtbare Zeilen gezählt (`offsetParent`; `tbody tr` + Mobile-Cards koexistieren im DOM),
  Retry+Settle um Filter-Interaktion (Provider-Recreation beim Circuit-Start), Users-Test gegen
  Präfix verifiziert (vorher ununterscheidbar).

## E2E-Abnahme-Screenshots (sichtgeprüft)

| Datei | Inhalt |
|-------|--------|
| `s2-savings-plans-search-filtered.png` | Suche „alphaplan" → nur AlphaPlan-Zeile; Datumsfilter sichtbar, leer |
| `s2-savings-plans-range-filtered.png` | Von 01.06.2026 / Bis 30.06.2026 → nur Plan mit Zieldatum 15.06.2026 |
| `s2-securities-search-filtered.png` | Suche „alphasec" → nur AlphaSec-Zeile; **kein** Datumsfilter gerendert |
| `s2-users-search-filtered.png` | Suche Admin-Name → nur Admin-Zeile; **kein** Datumsfilter gerendert |

Ablage: SCM-attachmentsPath `138706f7-…/screenshots/`.

## Merge

`git merge --no-ff` auf Basisbranch (Merge `merge: Schritt 2 Listenfilter (#599 #601 #604)`).
