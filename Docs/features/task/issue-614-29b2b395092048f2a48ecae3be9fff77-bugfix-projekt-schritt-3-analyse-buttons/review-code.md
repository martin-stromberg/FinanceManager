# Code-Review — Schritt 3 (analyse-buttons, #603)

**Reviewer:** Review-Subagent (frischer Kontext, read-only)
**Ergebnis:** Alle gemeldeten Befunde wurden behoben bzw. dokumentiert akzeptiert.

## Befunde und Auflösung

| # | Schwere | Befund | Auflösung |
|---|---|---|---|
| 1 | Hoch | CS1572: `<param name="From">` dokumentierte Tuple-Elementnamen als Parameter (konflikt mit Doc-Hook, der den Namen verlangt hatte) → CI-Buildfehler bei TreatWarningsAsErrors | **Behoben:** `WaitForRangeAsync`/`TryWaitForRangeAsync` auf explizite Parameter `expectedFrom`/`expectedTo` umgestellt; Phantom-`<param>`-Tags entfernt |
| 2 | Mittel | `AddYears`/`AddMonths` auf nutzergesteuertem Range → `ArgumentOutOfRangeException` bei Jahr 0001/9999 → Circuit-Abbruch | **Behoben:** try/catch `ArgumentOutOfRangeException` in `ShiftRange` (No-Op, Range bleibt) + Test `NextYear_AtDateTimeBoundary_KeepsRange`. Gleiche Klasse zusätzlich im Bericht abgesichert (`ShiftAsOfMonthAsync` try/catch, `ShiftAsOfYearAsync` Jahr-Range-Check) |
| 3 | Mittel | Fehlender Load-Versions-Guard → überlappende Reloads bei Schnellklicks können Items mischen/duplizieren | **Akzeptiert/dokumentiert:** pre-existing Schwäche des gesamten Listen-Reload-Pfads (betrifft Reload-Button und Suche identisch); architektureller Eingriff über `BaseListViewModel`/`ListPage` als Folge-Thema, nicht Scope dieses Schritts |
| 4 | Niedrig | Monatsende-Snap nur für `to`, nicht für `from` | **Akzeptiert:** dokumentierte Semantik ist „range ending on a month end stays month-aligned"; symmetrisches Snap würde Breitenerhalt für Monatsende-Starts verändern — bewusste Entscheidung |
| 5 | Niedrig | Leer-Grenzen-Semantik divergiert zwischen `ShiftRange` (Monatsanker der gesetzten Grenze) und `GetEffectiveMonthlyRange` (heute/from) | **Akzeptiert:** bewusste UX-Entscheidung, im PR-Text genannt |
| 6 | Niedrig | `ClickUntilRangeAsync` klickt pro Versuch bedingungslos → Overshoot-Flake bei verspätetem Rendering | **Behoben:** Baseline-Guard — Re-Click nur solange Range noch dem Ausgangswert entspricht; Report-Test wartet zusätzlich auf Periodentext-Änderung |
| 7 | Niedrig | E2E-Kulturannahme de/en im Period-Assert | **Akzeptiert:** Alternation deckt de-DE/en-US ab; Seeder-Sprache ist kontrolliert |

## Zusätzlich behobene Testlücken

- `PrevMonth_OnlyToSet_AnchorsToBoundsMonth` (nur-Bis-gesetzt)
- `PrevMonth_InvertedRange_Normalizes` + `ShiftRange` normalisiert jetzt invertierte Ranges
- `NextMonth_LoadAsync_QueriesApiWithShiftedRange` (Verify auf `Budgets_ListPurposesAsync` mit verschobenem `DateOnly`-Range)
- `NextMonth_OnDecember_RollsIntoJanuary`, `PrevYear_OnLeapDayRange_ClampsToFebruary28` (aus Plan-Check)

## Positiv vermerkt

Ankerableitung inversionsfrei, Monatsende-Snap auf Ausgangs-`toDate` geprüft, Reload-Verdrahtung (`UiActionRequested` → `ListPage` → `LoadAsync` → `GetEffectiveMonthlyRange` → API) verifiziert, resx/Icons/Button-Reihenfolge konsistent.

**Status:** 24 Unit-Tests + 2 E2E-Tests grün; E2E-Projekt baut warnungsfrei.
