# Code-Review – Schritt 2: Listenfilter (#599, #601, #604)

**Status:** Vollständig behandelt (1× medium behoben, 4× low behoben/dokumentiert, 1× low als Spezifikation akzeptiert)

## Findings und Auflösung

### 1. [medium] `AddIntervalWithMonthEndRule`/`SubtractIntervalWithMonthEndRule` konnten `ArgumentOutOfRangeException` werfen

**Problem:** Beide Enumerations-Schleifen riefen die Intervall-Methoden unconditional auf, bevor die Grenzprüfung griff. Bei `TargetDate` nahe `DateTime.MaxValue` (per UI-Datumsauswahl/Backup setzbar) oder `CreatedUtc` nahe `DateTime.MinValue` warf `AddMonths`/`SubtractMonths` `ArgumentOutOfRangeException`. Die Exception lief in den pauschalen `catch {}` von `LoadPageAsync` → `Items.Clear()` → die **gesamte** Sparplan-Liste verschwand lautlos.

**Auflösung:** Beide Schleifen fangen `ArgumentOutOfRangeException` nun pro Iteration ab und brechen die Enumeration ab — Datumsdomänen-Ende bedeutet „keine weiteren Fälligkeiten in dieser Richtung". Ein einzelner extrem terminiert Plan kann damit nicht mehr die komplette Liste leeren. Behoben in `SavingsPlansListViewModel.MatchesRange`.

### 2. [low] `IntervalMonths`-Mapping dupliziert mit abweichender Fehlersemantik

**Problem:** `SavingsPlansListViewModel.IntervalMonths` (privat, liefert `0` für unbekannte/null) dupliziert `SavingsPlan.IntervalMonths` (privat, wirft). Bei einem sechsten Enum-Mitglied fiele das VM-Mapping still auf `0` → Plan würde als Einzeltermin behandelt.

**Auflösung:** XML-Doc-Returns des VM-Helpers dokumentiert die Synchronisationspflicht mit `SavingsPlan.IntervalMonths` explizit. Der defensive `0`-Fallback bleibt bewusst (Filter darf bei unbekanntem Intervall nicht crashen).

### 3. [low] `SecuritiesListViewModel.LoadAsync(ct)` baute `Records` nicht neu

**Problem:** Der konkrete `LoadAsync(CancellationToken)` versteckt `base.LoadAsync()`; direkte Aufrufe `vm.LoadAsync(ct)` (Muster existiert in `Setup*Tab.razor`) ließen `Records` veralten. `UserListViewModel` ruft `BuildRecords()` selbst — `SecuritiesListViewModel` nicht.

**Auflösung:** `BuildRecords()` am Ende von `SecuritiesListViewModel.LoadAsync` ergänzt (doppelter Build über den `LoadPageAsync`-Pfad ist harmlos, `UserListViewModel` macht es bereits so).

### 4. [low] Dokumentation der neuen public Intervall-Methoden

**Problem:** `<returns>` von `SubtractIntervalWithMonthEndRule` verwendete unübliches Englisch („recessed"); `<exception>` dokumentierte nicht den `AddMonths`-Überlauf — jetzt `public`, also API-Contract-relevant.

**Auflösung:** Formulierung korrigiert; `ArgumentOutOfRangeException` bei Datumsdomänen-Überlauf für beide Methoden dokumentiert.

### 5. [low] Filter-Exceptions werden vom Bestands-`catch {}` verschluckt

**Problem:** Der `try`-Block in `LoadPageAsync` umfasst die Filtermathematik; Würfe → `Items.Clear()` ohne Diagnose (prä-existentes Muster).

**Auflösung:** Durch Finding 1 praktisch entschärft (der einzige realistische Wurf-Pfad ist jetzt guarded). Ein breiteres `LastError`-Exponieren über `IListProvider` ist als Usability-Finding M2 vermerkt und bewusst aus dem Schritt-Scope gehalten (betrifft alle Listen gleichermaßen).

### 6. [low] Akzeptierte Rekonstruktions-Näherung `lower = max(from, CreatedUtc)`

**Status:** Bewusst so spezifiziert (`requirement.md`): `TargetDate − n·Interval` kann Fälligkeiten zwischen `CreatedUtc` und der tatsächlichen ersten Fälligkeit erzeugen. Als bekannte Einschränkung vermerkt; die Monatsende-Drift-Nichtinvertierbarkeit ist per Test (`Range_Recurring_MonthEndDrift_ReconstructedSeries`) festgenagelt.

## Verifiziert OK (keine Findings offen)

- **Termination/Monotonie:** Beide Schleifen strikt monoton (`AddMonths(±n)`, n≥1) → kein Endlosschleifen-Risiko.
- **Off-by-one:** Grenzen inklusiv (`.Date`), `from>to`-Swap korrekt, `upper = min(to, ArchivedUtc)` / `lower = max(from, CreatedUtc)` entsprechen der Spezifikation.
- **Null-Safety:** `plan.Interval` via `is not { } interval`-Pattern abgesichert; alle Suchfelder `?.`-geschützt.
- **Nebenwirkungen:** Filter vor Analyse-Projektion reduziert N+1-Aufrufe (Test `FilteredPlans_SkipAnalysisCalls` belegt es).
- **Konventionen:** `AllowRangeFiltering => false` = Bestandsmuster; XML-Docs vollständig; keine Enum-/Backup-/resx-Änderungen.
- **Tests:** `SavingsPlansListFilterTests` (14 Fälle inkl. Grenzen, Swap, Archiv-Schranke, Phantom-Fälligkeit, UND mit Suche) und `SavingsPlanIntervalMathTests` (symmetrisch vollständig) grün.
