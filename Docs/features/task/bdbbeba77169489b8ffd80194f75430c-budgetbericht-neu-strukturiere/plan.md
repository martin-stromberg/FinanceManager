# Umsetzungsplan: Budgetbericht neu strukturieren (OO-Refactoring)

## Übersicht

Der Budgetbericht wird von einer prozeduralen (1500+ Zeilen `BudgetReportService`) zu einer objektorientierten Implementierung mit spezialisierten Domänenklassen umstrukturiert. Das neue Modell bildet die fünf Phasen der Berechnung (Initialization, Planning, Posting Assignment, Finish, Output) durch dedizierte Klassen ab und ersetzt damit die monolithische Service-Logik. Die Schnittstelle `IBudgetReportService` bleibt erhalten, wird aber durch die neue Domänenlogik implementiert. Das neue Modell gilt als fachliche Referenz.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Namespace für neue Domänenklassen** | `FinanceManager.Domain.Budget.ReportCalculation` | Klare Trennung zwischen Entity-Modell (BudgetCategory, BudgetPurpose, BudgetRule) und Berechnung. Zukünftige Erweiterungen (z. B. andere Report-Typen) nutzen ähnliche Struktur. **[GEKLÄRT]** |
| **Fehlerbehandlung** | Exceptions für ungültige Zustände (z. B. ungültige Rule-Konfiguration in `SetPlanung()`). Exception-Klasse: `BudgetReportCalculationException`. | Domänenklassen sollten es unmöglich machen, in einem ungültigen Zustand zu existieren (Ubiquitous Language). Exceptions signalisieren kritische Fehler; Validierungen erfolgen beim Aufbau. Ungültige Konfiguration ist ein Development-Fehler und sollte früh sichtbar werden. **[GEKLÄRT]** |
| **Serialisierung für Caching** | Nur Output-DTOs (`BudgetReportRawDataDto`, `BudgetReportCumulativeEntry`, `BudgetReportEntry`) werden gecacht, nicht das Domänenmodell. | Domänenmodell ist für Berechnung, nicht für Persistierung. Nur Output-DTOs serialisieren (via `ReportCacheService` wie bisher). Effizient und klar. **[GEKLÄRT]** |
| **Rollout-Strategie** | Expliziter, direkter Cutover ohne parallelen Betrieb oder Feature-Flag. Beide `IBudgetReportService`-Methoden (`GetRawDataAsync`, `GetMonthlyKpiAsync`) werden in einem Schritt vollständig auf neue Implementierung migriert. | Schrittweise Migration würde parallele Codepfade erzeugen (Komplexität). Domänenmodell ist in sich konsistent und gut getestet; Abkehr vom alten Service ist sauber und findet in einem Schritt statt. **[GEKLÄRT]** |
| **Konstruktor-Parameter für `Budgetbericht`** | `DateOnly betrachtungsDatum`, `int anzahlMonate`, `BudgetReportInterval intervall`, `BudgetReportDateBasis dateBasis` | Entspricht bestehender API (`BudgetReportRequest`). Diese vier Parameter definieren den Berichtszeitraum eindeutig. |
| **Muster für Zuordnungslogik** | Specification Pattern (Rule als Specification, die `MatchesPosting()` bewertet) + Sortierstrategie | Macht komplexe Bedingungen deklarativ: Regex-Pattern, Datum-Range, SourceType. Sortierstrategie (StartDate, dann Erstellungsreihenfolge) ist zentral dokumentiert. |
| **Umgang mit Kategorielosen Zwecken** | Virtuelle Kategorie mit spezieller ID (`Guid.Empty`), Name „Uncategorized" | Konsistenz mit bestehender DTO-Struktur (`BudgetReportRawDataDto.UncategorizedPurposes`). UI kann diese filtern, wenn gewünscht. |
| **Mehrere Gesamtbudgets (mehrere Rules für einen Zweck)** | **Sequenzielle Zuweisung nach Priorität.** (1) Sortiere Rules nach `BudgetRule.StartDate` aufsteigend (Gleichstand: Erstellungsreihenfolge). (2) Beim `AddPosting()`: Weise Posting der höchstpriorisierten (frühesten) erwartung zu, bis deren Betrag ausgeschöpft ist; Überschuss zur nächsten Erwartung usw. (3) Beim `Finish()`: Zusätzlich nach `PostingDate` sortieren, bevor Postings zugewiesen werden. Ist der Gesamtbetrag aller Erwartungen für einen Zweck unzureichend, wird der Rest unbudgetiert erfasst. | **[GEKLÄRT]** Dies ist nicht der Pool-Ansatz, sondern exakt wie in `issue.md`/`requirement.md` spezifiziert: sequenzielle Verarbeitung mit klarer Prioritätsordnung. Wirtschaftlich sinnvoll und transparent für den Benutzer. |

---

## Programmabläufe

### Initialization Phase

1. Benutzer/Controller ruft `new Budgetbericht(betrachtungsDatum, anzahlMonate, intervall, dateBasis)` auf.
2. `Budgetbericht`-Konstruktor erzeugt für jeden Monat des Berichtszeitraums einen `MonthlyBudgetResult`-Eintrag.
3. Jeder `MonthlyBudgetResult` initialisiert leere Listen für `ExpectationGroups[]`, `UnbudgetedPostings[]`, `CostNeutralPostings[]`.

Beteiligte Klassen/Komponenten: `Budgetbericht`, `MonthlyBudgetResult`, `BudgetReportInterval`, `BudgetReportDateBasis`

### Planning Phase (via `SetPlanung()`)

1. Benutzer/Service ruft `budgetbericht.SetPlanung(categories[], purposes[], rules[])` auf.
2. Für jede `BudgetRule`:
   - Berechne alle Vorkommen (Interval-Expansion: Monatlich, Quartalsweise, Jährlich, Custom) im Berichtszeitraum (statische Methode oder Hilfslogik).
   - Erstelle pro Vorkommen einen `MonthlyBudgetExpectationPosting` (Amount, BudgetType=ExactPosting|TotalBudget, StartDate aus Rule).
3. Für jede `BudgetPurpose` in jeder Kategorie:
   - Erstelle einen `MonthlyBudgetExpectation` pro Monat/Kategorie (oder nutze Caching, wenn Multiple Rules denselben Zweck haben).
   - Weisen Sie Postings zu basierend auf `BudgetRule.BudgetPurposeId` oder `BudgetRule.BudgetCategoryId`.
4. Für Kategorien: Erstelle pro Kategorie/Monat einen `MonthlyBudgetExpectationGroup` mit `DirectExpectations` (Kategorie-Ebene) und `Purposes` (Zweck-Ebene mit deren Expectations).
5. Für kategorielose Zwecke: Erstelle virtuelle Kategorie „Uncategorized" (ID=Guid.Empty).
6. Füge alle `MonthlyBudgetExpectationGroup` in die entsprechenden `MonthlyBudgetResult.ExpectationGroups[]` ein.

Beteiligte Klassen/Komponenten: `Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationPosting`, `BudgetCategory`, `BudgetPurpose`, `BudgetRule`, `BudgetSourceType`, `BudgetValuationType`, `BudgetIntervalType`

### Posting Assignment Phase (via `AddPosting()`)

1. Benutzer/Service ruft `budgetbericht.AddPosting(posting, datumBasis)` für jeden Posting auf.
2. Bestimme `PostingMonth` basierend auf `datumBasis` (BookingDate oder ValutaDate).
3. Finde entsprechenden `MonthlyBudgetResult` für diesen Monat.
4. **Zuordnungslogik (Priorität):**
   a. Finde alle `MonthlyBudgetExpectation` in diesem Monat, deren `Zweck.SourceType` + `SourceId` passen (Contact/ContactGroup/SavingsPlan-Match).
   b. Wende `BudgetRulePatternMatcher.MatchesPosting()` an (für `PurposePattern`, falls vorhanden).
   c. Unterscheide nach `BudgetValuationType`:
      - **ExactPostings**: Nur Postings mit **gleicher Vorzeichen** (Richtung) wie Erwartung matchen. **[GEKLÄRT: Vorzeichen muss exakt passen]**
      - **TotalBudget**: Alle Beträge (unabhängig Vorzeichen).
   d. **Mehrere passende Expectations: Sequenzielle Zuweisung nach Priorität (nicht Pool-Ansatz).** Sortiere nach `BudgetRule.StartDate` (aufsteigend), dann Erstellungsreihenfolge.
   e. Weise Posting der höchstpriorisierten (frühesten) `MonthlyBudgetExpectationPosting` zu, bis deren `Amount` aufgebraucht ist. Ist der Betrag größer als der Restbetrag der aktuellen Expectation, wird dieser überschritten und der Rest zur nächsten Expectation weitergeleitet. Sind alle Expectations aufgebraucht, wird der Rest unbudgetiert erfasst.
5. Falls kein Match gefunden wird: Prüfe `posting.GroupId` (kostenneutrale Transfers — Spiegelgruppe).
   - Falls `GroupId` gesetzt: Füge zu `CostNeutralPostings[]` hinzu.
   - Sonst: Füge zu `UnbudgetedPostings[]` hinzu.

Beteiligte Klassen/Komponenten: `Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationPosting`, `BudgetRulePatternMatcher`, `BudgetValuationType`, `Posting` (Entity), `MonthlyBudgetRealization` (Input-DTO)

### Finish Phase (via `Finish()`)

1. Benutzer/Service ruft `budgetbericht.Finish()` auf nach allen `AddPosting()`-Aufrufen.
2. Pro `MonthlyBudgetResult` und je `MonthlyBudgetExpectation` (für jeden Zweck/jede Kategorie):
   a. **Für ExactPosting-Expectations**: Prüfe Übersteigerungen. Wenn zugeordnete Posting-Summe > Erwartung: Aufteilen in budgetiert (bis Erwartung) und unbudgetiert (Rest). Unbudgetierter Rest wird `UnbudgetedPostings` hinzugefügt.
   b. **Für TotalBudget-Expectations (mehrere Rules für einen Zweck):** 
      - Sortiere **alle zugeordneten Postings nach ihrem `PostingDate`** (aufsteigend).
      - Sortiere die assoziierten `MonthlyBudgetExpectationPosting` nach `BudgetRule.StartDate` (aufsteigend), dann Erstellungsreihenfolge.
      - Führe sequenzielle Zuweisung durch: Postings werden der Reihe nach der ersten Expectation zugewiesen, bis deren Betrag ausgeschöpft ist; Überschuss zur nächsten usw. Reicht der Gesamtbetrag nicht aus, wird der Rest unbudgetiert.
3. Berechne pro Expectation/Posting die Summenwerte:
   - `SumExpectedAmount` (sum der `MonthlyBudgetExpectationPosting.Amount`).
   - `SumActualAmount` (sum der zugeordneten Posting-Beträge).
   - `Variance` = ActualAmount - ExpectedAmount.

Beteiligte Klassen/Komponenten: `Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationPosting`

### Output Phase (via `GetCurrentResult()` und `GetCumulativeResult()`)

**`GetCurrentResult()`** (für einen Monat, oder aktuelle Aggregation):
1. Iteriere über `MonthlyBudgetResult` (oder alle, falls Aggregation gewünscht).
2. Für jede `MonthlyBudgetExpectationGroup`:
   a. Erstelle `BudgetReportEntry` mit `RowKind=Category`, Name, `BudgetedAmount` (sum der Expectations), `ActualAmount` (sum der zugeordneten Postings).
   b. Für jede `MonthlyBudgetExpectation` in der Gruppe:
      - Erstelle `BudgetReportEntry` mit `RowKind=Purpose`, Name, Amounts, Postings-Liste.
   c. Erstelle `BudgetReportEntry` mit `RowKind=Subtotal` für Kategoriensumme.
3. Falls `ExpectationGroups.Count > 1` oder „Uncategorized" nicht einzig: Zeige Kategorie-Zeile. Sonst: Blende aus.
4. Erstelle Zusatzzeilen:
   - `BudgetReportEntry` mit `RowKind=Unbudgeted`, Amounts aus `UnbudgetedPostings[]`.
   - `BudgetReportEntry` mit `RowKind=CostNeutral`, Amounts aus `CostNeutralPostings[]`.
   - `BudgetReportEntry` mit `RowKind=Total`, Gesamtsummen.
5. Gebe Array von `BudgetReportEntry[]` zurück.

**`GetCumulativeResult()`** (aggregiert nach Intervall):
1. Iteriere über alle `MonthlyBudgetResult` und aggregiere nach `Intervall` (Monat/Quartal/Jahr).
2. Pro Intervall-Bucket: Summiere Budgets und Istwerte über alle Kategorien und Zwecke.
3. Berechne Abweichung und prozentuale Abweichung.
4. Gebe Array von `BudgetReportCumulativeEntry[]` zurück.

Beteiligte Klassen/Komponenten: `Budgetbericht`, `MonthlyBudgetResult`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetExpectation`, `BudgetReportEntry`, `BudgetReportCumulativeEntry`, `BudgetReportInterval`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Budgetbericht` | Domänenklasse (Aggregate Root) | Zentrale Berichtsberechnung, verwaltet Lifecycle (Initialization → Planning → PostingAssignment → Finish → Output) |
| `MonthlyBudgetResult` | Domänenklasse (Value Object) | Ein Eintrag pro Monat mit Expectations und tatsächlichen Buchungen |
| `MonthlyBudgetExpectationGroup` | Domänenklasse (Value Object) | Gruppierung eines Monats nach Budgetkategorie mit kategorialen und Zweck-Expectations |
| `MonthlyBudgetExpectation` | Domänenklasse (Value Object) | Einzelne Erwartung auf Kategorie- oder Zweck-Ebene mit assozierten Postings |
| `MonthlyBudgetExpectationPosting` | Domänenklasse (Value Object) | Einzelne erwartete Buchung (aus BudgetRule-Expansion) mit AssignedPostings-Liste |
| `BudgetReportEntry` | DTO (Output) | Eine Zeile in der Detailtabelle für einen Monat mit RowKind (Category/Purpose/Subtotal/Unbudgeted/CostNeutral/Total) |
| `BudgetReportCumulativeEntry` | DTO (Output) | Eine Zeile in der Intervall-Zusammenfassungstabelle (Monat/Quartal/Jahr) |
| `MonthlyBudgetRealization` | DTO (Input) | Ein Buchungsposten mit allen relevanten Metadaten (BookingDate, ValutaDate, ContactId, Amount, Purpose, Description, GroupId) |
| `BudgetReportCalculationException` | Exception | Für kritische Fehler während Berechnung (z. B. ungültiger Zeitraum, ungültige Rule) |

---

## Änderungen an bestehenden Klassen

### `IBudgetReportService` (Interface)

- **Keine neuen Methoden.** Das Interface bleibt wie es ist: `GetRawDataAsync()` und `GetMonthlyKpiAsync()`.
- **Implementierung wechselt:** Von `BudgetReportService` zu neuer Klasse `BudgetReportService` (gleicher Name, neue Implementierung basierend auf `Budgetbericht`-Domänenmodell).

### `BudgetReportService` (neue Implementierung in Infrastructure/Budget/)

- **Gesamtzweck:** Adapter zwischen `IBudgetReportService`-Schnittstelle und `Budgetbericht`-Domänenmodell.
- **Neue Methoden:**
  - `GetRawDataAsync(ownerUserId, from, to, dateBasis, ct, ignoreCache)` — **Implementierung wechselt komplett:**
    1. Erstelle neue `Budgetbericht(from, to - from + 1 month, BudgetReportInterval.Month, dateBasis)` Instanz.
    2. Lade `BudgetCategory[]`, `BudgetPurpose[]`, `BudgetRule[]` für Benutzer aus Repositories.
    3. Rufe `budgetbericht.SetPlanung(categories, purposes, rules)` auf.
    4. Lade alle `Posting` aus `IPostingsQueryService` für Zeitraum.
    5. Rufe für jeden Posting `budgetbericht.AddPosting(posting, dateBasis)` auf.
    6. Rufe `budgetbericht.Finish()` auf.
    7. Rufe `budgetbericht.GetCurrentResult()` auf → konvertiere zu `BudgetReportRawDataDto` (Mapper).
    8. Optionale Caching via `IReportCacheService` (nur DTOs, nicht Domänenmodell).
  - `GetMonthlyKpiAsync(userId, date, dateBasis, ct)` — **Implementierung wechselt komplett:**
    1. Erstelle neue `Budgetbericht(date, 1, BudgetReportInterval.Month, dateBasis)` Instanz.
    2. Lade Budgets, Rules für einen Monat.
    3. Führe Planning und Posting Assignment durch.
    4. Rufe `budgetbericht.GetCurrentResult()` auf.
    5. Konvertiere zu `MonthlyBudgetKpiDto` (Mapper).
- **Abhängigkeiten (Konstruktor):** Keine neuen; bestehende bleiben:
  - `IBudgetPurposeService`, `IBudgetCategoryService`, `IBudgetRuleService`
  - `IPostingsQueryService`, `IContactService`, `ISavingsPlanService`, `ISecurityService`
  - `IReportCacheService`
- **Entfernte Methoden:** Alle bisherigen privaten Methoden (`BuildPostingDtosAsync`, `BuildUncategorizedPurposeDtosAsync` usw.) entfallen; Logik zieht in `Budgetbericht`.

### `ReportCacheService` (Infrastructure/Budget/)

- **Keine strukturellen Änderungen an der Klasse selbst.**
- **Serialisierung-Verhalten:** Weiterhin `BudgetReportRawDataDto` serialisieren/deserializieren (DTO-Level, nicht Domänenmodell).
- **Cache-Key-Strategie:** Bleibt unverändert (`"budgetreportraw-YYYYMMDD-YYYYMMDD-DateBasis"`).

### `BudgetReportRawDataDto` (und untergeordnete DTOs)

- **Keine Änderungen.** DTO-Struktur bleibt kompatibel.
- **Erzeugung:** Nicht mehr direkt vom `BudgetReportService` aufgebaut, sondern als Ergebnis der Mapper-Funktion vom `BudgetReportEntry`-Array.

### `BudgetCategory`, `BudgetPurpose`, `BudgetRule` (Entities)

- **Keine Änderungen erforderlich.** Diese werden weiterhin gelesen und als Input für `SetPlanung()` verwendet.

---

## Datenbankmigrationen

**Keine.** Alle neuen Domänenklassen sind In-Memory-Objekte (Aggregates), keine neuen Tabellen oder Spalten erforderlich.

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Budgetbericht` Konstruktor: `anzahlMonate` | Muss > 0 sein | Throw `BudgetReportCalculationException` |
| `Budgetbericht` Konstruktor: `betrachtungsDatum` | Muss gültiges Datum sein | Throw `BudgetReportCalculationException` |
| `SetPlanung()`: `BudgetRule` mit ungültiger Konfiguration | Rule ohne `Amount`, oder ungültiger `Interval`, oder fehlende `CustomIntervalMonths` bei Custom-Interval | Throw `BudgetReportCalculationException` mit aussagekräftiger Fehlermeldung. **[GEKLÄRT: Exception werfen]** |
| `SetPlanung()`: `BudgetRule.Interval` | Muss gültig sein (Monatlich, Quartalsweise, Jährlich, Custom) | Throw `BudgetReportCalculationException` |
| `AddPosting()`: `posting` null | Darf nicht null sein | Throw `ArgumentNullException` |
| `MonthlyBudgetRealization` (Input-DTO): `Amount` | Muss ungleich 0 sein (oder nullable) | Validation-Fehler; wird von Service-Layer (Application/Budget) behandelt |

---

## Konfigurationsänderungen

**Keine.** Die Parameter für Berichtserstellung (`BetrachtungsDatum`, `AnzahlMonate`, `Intervall`, `DateBasis`) werden bereits über `BudgetReportRequest` (API) oder UI gesteuert und an die neue `Budgetbericht`-Klasse weitergegeben.

Optionale zukünftige Konfiguration (außerhalb dieses Plans):
- Feature-Flag für parallele Rollout-Phase (alt vs. neu), falls nötig.

---

## Seiteneffekte und Risiken

- **`BudgetReportService` wird vollständig neu implementiert (Cutover):** Alte Logik entfällt komplett. Beide Methoden (`GetRawDataAsync`, `GetMonthlyKpiAsync`) werden in einem Schritt migriert — **kein Feature-Flag, keine parallele Codepath.** **[GEKLÄRT: Expliziter Cutover]** Risiko: Verhalten muss 100% kompatibel sein mit alten Ausgaben (`BudgetReportRawDataDto`). Mitigation: Umfangreiche Integrationstests gegen bestehende Testdaten. Cache-Einträge von Alt-Implementierung sollten invalidiert werden beim Deployment (via `MarkAllReportCacheEntriesForUpdateAsync()`).

- **Test-Abdeckung der Zuordnungslogik muss sehr vollständig sein:** Komplexe Prioritäten (Contact-Match, Pattern-Match, StartDate-Sortierung, sequenzielle Zuweisung bei mehreren Gesamtbudgets) sind schwer zu debuggen. Mitigation: Unit-Tests für `Budgetbericht.AddPosting()` und `Finish()` müssen alle bekannten Edge Cases abdecken (siehe Bestandsaufnahme: Mixed Income/Expense, Übererfüllung, mehrere Gesamtbudgets, kostenneutrale Transfers, Vorzeichen-Matching).

- **Serialisierung bei Caching:** Nur Output-DTOs werden gecacht (nicht das Domänenmodell). **[GEKLÄRT: Nur DTOs cachen]** `ReportCacheService` wird weiterhin `BudgetReportRawDataDto` serialisieren (keine Änderungen nötig, wenn Serialisierung stabil bleibt).

- **Mehrere Gesamtbudgets für einen Zweck:** Neue Finish-Phase führt sequenzielle Zuweisung durch (mit Postings-Sortierung nach Datum) — nicht den Pool-Ansatz. **[GEKLÄRT: Sequenzielle Zuweisung]** Risiko: Behavior könnte von Alt-Implementierung abweichen. Mitigation: Tests mit Szenarien aus `BudgetReportServiceTests` (z. B. „Streaming Provider" mit mehreren Expectations).

- **Namespace-Struktur:** Neue Klassen in `FinanceManager.Domain.Budget.ReportCalculation` — könnten Circular Dependencies mit bestehenden `FinanceManager.Domain.Budget.*` entstehen? Mitigation: Compilation-Test; Namespaces sind gut getrennt (Value Objects referenzieren nur Entities, nicht umgekehrt).

- **Bestehende Tests werden ersetzt:** `BudgetReportServiceTests` und `BudgetReportServiceRawDataTests` werden gelöscht und durch neue Tests ersetzt. **[GEKLÄRT: Neue Tests schreiben, alte ersetzen]** Risiko: Test-Setup-Logik könnte verloren gehen. Mitigation: Setup-Szenarien aus alten Tests können als Vorlage dienen.

---

## Umsetzungsreihenfolge

1. **Neue Domänenklassen: Value Objects für Monatsergebnisse**
   - Voraussetzungen: Keine (bestehende Domain-Entities können referenziert werden).
   - Beschreibung: Erstelle `MonthlyBudgetExpectationPosting`, `MonthlyBudgetExpectation`, `MonthlyBudgetExpectationGroup`, `MonthlyBudgetResult` in `FinanceManager.Domain/Budget/ReportCalculation/`. Keine Persistierung (in-memory). Konstruktoren validieren Constraints. Collections sind ImmutableList oder List (interne Verwaltung durch `Budgetbericht`). ~200–300 Zeilen Code.

2. **Neue Domänenklasse: Budgetbericht (Aggregate Root)**
   - Voraussetzungen: Schritt 1 (Value Objects) muss abgeschlossen sein.
   - Beschreibung: Erstelle `Budgetbericht` in `FinanceManager.Domain/Budget/ReportCalculation/`. Implementiere Konstruktor (Initialization), `SetPlanung()`, `AddPosting()`, `Finish()`. `GetCurrentResult()` und `GetCumulativeResult()` rufen Hilfsmethoden auf. ~600–800 Zeilen Code (Umfang der Zuordnungslogik).

3. **Exception-Klasse für Berechnungsfehler**
   - Voraussetzungen: Keine.
   - Beschreibung: Erstelle `BudgetReportCalculationException` in `FinanceManager.Domain/Budget/ReportCalculation/`. Erbe von `DomainException` oder `Exception`. ~20 Zeilen.

4. **Neue Output-DTOs: BudgetReportEntry und BudgetReportCumulativeEntry**
   - Voraussetzungen: Keine (neue, separate DTOs).
   - Beschreibung: Erstelle in `FinanceManager.Shared/Dtos/Budget/`. `BudgetReportEntry` mit `RowKind` enum (Category/Purpose/Subtotal/Unbudgeted/CostNeutral/Total), Name, Amounts, Postings-Array. `BudgetReportCumulativeEntry` mit IntervalStartDate, IntervalLabel, Amounts, Deviation. ~80–120 Zeilen.

5. **Neuer Input-DTO: MonthlyBudgetRealization**
   - Voraussetzungen: Keine.
   - Beschreibung: Erstelle in `FinanceManager.Shared/Dtos/Budget/`. Properties: BookingDate, ValutaDate, ContactId, ContactGroupId, SavingsPlanId, Amount, Purpose, Description, GroupId. ~40–60 Zeilen.

6. **Mapper-Funktionen: Budgetbericht → BudgetReportRawDataDto**
   - Voraussetzungen: Schritte 1–4 (Budgetbericht, Output-DTOs).
   - Beschreibung: Erstelle statische Mapper-Klasse oder Extension-Methoden in `FinanceManager.Infrastructure/Budget/Mapping/` (z. B. `BudgetberichtMapper.cs`). Konvertiere `BudgetReportEntry[]` zu `BudgetReportRawDataDto` (Kategoriestruktur, UncategorizedPurposes, UnbudgetedPostings). Konvertiere `BudgetReportCumulativeEntry[]` zu `MonthlyBudgetKpiDto` (für KPI-Ausgabe). ~150–200 Zeilen.

7. **Neue Implementierung von BudgetReportService**
   - Voraussetzungen: Schritte 1–6 (Budgetbericht, DTOs, Mapper). Existierende Services (IBudgetPurposeService, IBudgetCategoryService, IBudgetRuleService, IPostingsQueryService, IContactService, ISavingsPlanService, ISecurityService, IReportCacheService) müssen bereits im Projekt vorhanden sein (sind sie).
   - Beschreibung: Ersetze Implementierung von `BudgetReportService` (Infrastructure/Budget/). Behalte Klassennamen und Interface-Implementierung (`IBudgetReportService`). Implementiere `GetRawDataAsync()` und `GetMonthlyKpiAsync()` neu, basierend auf `Budgetbericht`. Rufe Mapper auf. Implementiere Caching-Integration. **Lösche die alten privaten Methoden** (`BuildPostingDtosAsync`, `BuildUncategorizedPurposeDtosAsync` usw.). ~300–400 Zeilen neue Logik.

8. **Tests: Unit-Tests für Budgetbericht-Domänenmodell**
   - Voraussetzungen: Schritte 1–2 (Domänenklassen).
   - Beschreibung: Erstelle `BudgetberichtTests.cs` in `FinanceManager.Tests/Budget/Domain/`. Teste Initialization, Planning (Rule-Expansion, Kategorie/Zweck-Gruppierung), PostingAssignment (alle bekannten Szenarien aus `BudgetReportServiceTests`), Finish (Übererfüllung, mehrere Gesamtbudgets), Output. ~1000–1500 Zeilen Tests (30–50 Test-Methoden).

9. **Tests: Ersetze alte Service-Tests**
   - Voraussetzungen: Schritt 8 (neue Unit-Tests). Alter Test: `BudgetReportServiceTests`.
   - Beschreibung: Lösche oder markiere alte `BudgetReportServiceTests` als deprecated. Ersetze durch neue `BudgetberichtTests` (oben). Teste auch `BudgetReportServiceTests` für die Adapter-Funktion (GetRawDataAsync → Budgetbericht → Mapper → DTO). ~200–300 Zeilen Adapter-Tests.

10. **Tests: Anpassung von ReportCacheServiceTests**
    - Voraussetzungen: Schritt 7 (neue BudgetReportService).
    - Beschreibung: Bestehende `ReportCacheServiceTests` sollten weiterhin laufen. Überprüfe, dass die Cache-DTOs (BudgetReportRawDataDto) korrekt serialisiert/deserialisiert werden. Keine Änderungen nötig, wenn Serialisierung unverändert bleibt.

11. **Integrationstests: Anpassung bestehender E2E-Tests**
    - Voraussetzungen: Schritt 7 (neue BudgetReportService).
    - Beschreibung: Laufe alle bestehenden Integrationstests (z. B. `ApiClientBudgetReportUnbudgetedMirrorTests`, `BudgetReportViewModelIntegrationTests`). Falls Behavior sich ändert (z. B. Reihenfolge, Aggregation), passe Assertions an. Falls Output-DTO-Struktur unverändert, sollten Tests laufen.

12. **Dokumentation: Ubiquitous Language und Architektur-Übersicht**
    - Voraussetzungen: Alle Schritte oben.
    - Beschreibung: Erstelle kurze Dokumentation in `docs/features/task/bdbbeba7.../architecture.md` oder als Kommentare in den Klassen. Erkläre die fünf Phasen, Zuordnungslogik, Sonderfälle (kostenneutrale Transfers, mehrere Gesamtbudgets). ~300–500 Zeilen Dokumentation.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TestInitialization_CreatesMonthlyResultsForTimeRange` | `BudgetberichtTests` | Konstruktor erstellt korrekte Anzahl MonthlyBudgetResult für Berichtszeitraum |
| `TestSetPlanung_ExpandsRulesIntoExpectations` | `BudgetberichtTests` | Monatliche, quartalsweise, jährliche, custom Regel-Expansion funktioniert |
| `TestSetPlanung_CreatesExpectationGroups_PerCategory` | `BudgetberichtTests` | MonthlyBudgetExpectationGroup pro Kategorie/Monat erstellt |
| `TestSetPlanung_CreatesUncategorizedVirtualCategory` | `BudgetberichtTests` | Kategorielose Zwecke werden in virtuelle Kategorie (Guid.Empty) gruppiert |
| `TestAddPosting_AssignsToMatchingExpectation` | `BudgetberichtTests` | Posting wird dem korrekten Expectation zugeordnet (Contact/ContactGroup/SavingsPlan-Match) |
| `TestAddPosting_AppliesPatternMatching` | `BudgetberichtTests` | BudgetRule.PurposePattern (Substring/Regex) wird korrekt angewendet |
| `TestAddPosting_PrioritizesExactPostingsBeforeTotalBudgets` | `BudgetberichtTests` | ExactPosting-Expectations haben Vorrang vor TotalBudget |
| `TestAddPosting_SortsMultipleTotalBudgetsByStartDate` | `BudgetberichtTests` | Mehrere Gesamtbudgets (Rules) werden nach StartDate und Erstellungsreihenfolge verarbeitet |
| `TestAddPosting_AppliesValuationTypeFilter` | `BudgetberichtTests` | ExactPostings: nur Vorzeichen-Matches. TotalBudget: alle Beträge |
| `TestAddPosting_CostNeutralPostings_WithGroupId` | `BudgetberichtTests` | Posting mit GroupId → CostNeutralPostings[], nicht Unbudgeted |
| `TestAddPosting_UnbudgetedPostings_NoMatch` | `BudgetberichtTests` | Posting ohne Match → UnbudgetedPostings[] |
| `TestFinish_SplitsOverflowIntoUnbudgeted` | `BudgetberichtTests` | Übersteigende Buchungen: budgetiert bis Expectation, Rest unbudgetiert |
| `TestFinish_CombinesMultipleBudgetsPerPurpose` | `BudgetberichtTests` | Mehrere Gesamtbudgets werden zusammengefasst; Überschuss unbudgetiert |
| `TestGetCurrentResult_ReturnsCorrectRowKinds` | `BudgetberichtTests` | Output RowKind-Struktur: Category, Purpose, Subtotal, Unbudgeted, CostNeutral, Total |
| `TestGetCurrentResult_HidesUncategorizedIfOnly` | `BudgetberichtTests` | Virtuelle Kategorie wird ausgeblendet, wenn sie einzig ist |
| `TestGetCumulativeResult_AggregatesByMonth` | `BudgetberichtTests` | Aggregation nach Monat funktioniert (Intervall=Month) |
| `TestGetCumulativeResult_AggregatesByQuarter` | `BudgetberichtTests` | Aggregation nach Quartal funktioniert |
| `TestGetCumulativeResult_AggregatesByYear` | `BudgetberichtTests` | Aggregation nach Jahr funktioniert |
| `TestGetCumulativeResult_CalculatesDeviations` | `BudgetberichtTests` | Deviation und DeviationPercentage korrekt berechnet |
| `TestScenario_ShoppingAndFood_CategorizedWithMultiplePurposes` | `BudgetberichtTests` | Komplexes Szenario aus Bestandsaufnahme: „Shopping & Food" mit „Food" und „Bakeries" |
| `TestScenario_MixedIncomeExpense_Recurring` | `BudgetberichtTests` | Szenario: monatliche negative + jährliche positive Regel |
| `TestScenario_Overrun_StreamingProvider` | `BudgetberichtTests` | Szenario: Erwartung -10, tatsächlich -4.99/-4.99/-6.00 (Overrun) |
| `TestScenario_Salary_Income_Overrun` | `BudgetberichtTests` | Szenario: Income mit Übererfüllung |
| `TestBudgetReportServiceAdapter_GetRawDataAsync` | `BudgetReportServiceAdapterTests` | Adapter-Logik: GetRawDataAsync() erstellt Budgetbericht, erzeugt Output-DTO |
| `TestBudgetReportServiceAdapter_GetMonthlyKpiAsync` | `BudgetReportServiceAdapterTests` | Adapter-Logik: GetMonthlyKpiAsync() für einen Monat |
| `SetupBudgetrerichtWithCategories` (Hilfsmethode) | `BudgetberichtTests` | Test-Fixture: Budgetbericht mit Beispiel-Kategorien initialisieren |
| `SetupBudgetberichtWithRules` (Hilfsmethode) | `BudgetberichtTests` | Test-Fixture: Regeln mit verschiedenen Intervallen hinzufügen |
| `CreateTestPosting` (Hilfsmethode) | `BudgetberichtTests` | Test-Factory: MonthlyBudgetRealization mit Testdaten |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `BudgetReportServiceTests` | **Zu ersetzen oder komplett umschreiben:** Alte Tests prüfen `BudgetReportService` direkt mit async-Calls. Neue Tests sollen das `Budgetbericht`-Modell direkt prüfen. Option: Einige Tests in `BudgetberichtTests` verschieben, `BudgetReportServiceTests` als Adapter-Test-Suite behalten (nur GetRawDataAsync, GetMonthlyKpiAsync testen). |
| `BudgetReportServiceRawDataTests` | **Zu löschen oder komplett umschreiben:** Alte Tests für Raw-Data-DTO-Struktur. Wenn DTOs unverändert bleiben, können Tests weiterhin auf höherer Ebene (API-Tests) bestehen; Unit-Tests auf Mapper-Ebene sind ausreichend. |
| `ReportCacheServiceTests` | **Anpassung wahrscheinlich nicht erforderlich:** Wenn Cache weiterhin BudgetReportRawDataDto serialisiert und Serialisierung unverändert bleibt, sollten Tests unverändert laufen. Überprüfung erforderlich. |
| `MonthlyBudgetKpiViewModelTests` | **Anpassung wahrscheinlich nicht erforderlich:** Tests verwenden `IBudgetReportService.GetMonthlyKpiAsync()`, die Signatur ändert sich nicht. Interne Implementierung ändert sich, aber Output sollte kompatibel sein. |
| `BudgetReportViewModelIntegrationTests` (oder ähnlich) | **Überprüfung erforderlich:** Falls bestehende Integrationstests das Verhalten von `BudgetReportService` prüfen, müssen Assertions überprüft werden; Reihenfolge, Aggregation, Amounts sollten gleich bleiben. |
| `ApiClientBudgetReportUnbudgetedMirrorTests` (oder ähnlich) | **Überprüfung erforderlich:** Spiegelbuchungslogik (GroupId) muss in neuem Modell erhalten bleiben. Tests sollten laufen, wenn Verhalten unverändert. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Budgetbericht für vollständigen Monat mit kategorisierten Zwecken | `BudgetReportE2ETests.cs` | User ruft GetRawDataAsync() auf; API liefert BudgetReportRawDataDto mit korrekten Kategorien, Zwecken, Budgets, Istwerten |
| Budgetbericht für Quartal, aggregiert nach Intervall | `BudgetReportE2ETests.cs` | User ruft mit BudgetReportInterval.Quarter auf; GetCumulativeResult() liefert 1 Eintrag pro Quartal mit korrekten Summen |
| Unbudgetierte Buchungen erscheinen in Unbudgeted-Zeile | `BudgetReportE2ETests.cs` | User hat Postings ohne Budgetzuordnung; sie erscheinen in BudgetReportRawDataDto.UnbudgetedPostings[] |
| Kostenneutrale Transfers (Spiegelgruppen) werden korrekt behandelt | `BudgetReportE2ETests.cs` | User hat Spiegelbuchungen mit GroupId; sie werden nicht als Unbudgeted gezählt, sondern in CostNeutral-Struktur |
| Mehrere Budgetregeln für einen Zweck (Overrun-Handling) | `BudgetReportE2ETests.cs` | User mit mehreren Gesamtbudgets für einen Zweck; Finish-Phase kombiniert sie; Überschuss wird unbudgetiert |
| DateBasis-Filterung: BookingDate vs. ValutaDate | `BudgetReportE2ETests.cs` | Posting mit BookingDate und ValutaDate in unterschiedlichen Monaten; Zuordnung respektiert DateBasis-Parameter |

**Bestehende E2E-Tests, die angepasst werden müssen:**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle existierenden Budgetbericht-API-Tests (z. B. in `ApiClientBudgetReportsTests` oder ähnlich) | Überprüfung erforderlich: Falls Tests das Output-Format (`BudgetReportRawDataDto`) validieren, müssen sie weiterhin passen. Falls interne Logik-Tests vorhanden (z. B. mit Mocking von `BudgetReportService`), müssen Mocks angepasst werden. |

---

## Offene Punkte

Keine. Alle 9 technischen/fachlichen Fragen wurden mit konkreten Entscheidungen beantwortet (siehe Designentscheidungen, Programmabläufe und Validierungsregeln oben).

---

**Signatur der Dokumentation:** Dieser Plan behandelt die Refactoring-Strategie, Klassen-Struktur, Abläufe und Testanforderungen. Implementierungsdetails (Codezeilen, Parameternamen in Methoden) folgen in der Implementierungsphase. Alle Designentscheidungen basieren auf Anforderung und Bestandsaufnahme.
