# Anforderung — Schritt 2: Listenfilter (Suche/Datumsfilter)

Projektschritt 2 von `task/issue-614-…-bugfix-projekt` (Issue #614).
Issues: **#599** (Sparplanübersicht), **#601** (Wertpapierübersicht), **#604** (Benutzerübersicht).

## Ausgangslage

`GenericListPage` rendert Suchfeld und Von/Bis-Datumsfilter für jeden `IListProvider`
(`AllowSearchFiltering`/`AllowRangeFiltering`, Standard `true`). `SetSearch`/`SetRange` werden
korrekt aufgerufen — die drei ViewModels werten `Search`, `RangeFrom`, `RangeTo` aber nie aus.

## Anforderungen

### #599 — Sparplanübersicht (`/list/savings-plans`)

- **Suche:** Teilstring, Case-insensitive auf `Name` + `ContractNumber` + Kategoriename
  (Entscheidung Projektplan: Vorschlagsumfang übernommen).
- **Datumsfilter** auf `TargetDate`, Grenzen inklusive, eine Grenze = offene Seite,
  Von > Bis → tauschen:
  - `OneTime`/`Open` mit `TargetDate`: Treffer bei `TargetDate ∈ [Von,Bis]`.
  - `Recurring`: Treffer bei ≥1 Fälligkeit im Zeitraum; Fälligkeiten = `TargetDate ± n·Interval`,
    rückwärts bis `CreatedUtc` als Untergrenze; Monatsend-Semantik wie
    `SavingsPlan.AddIntervalWithMonthEndRule` (nicht `AddMonths` naiv). Bei archivierten Plänen
    ist `ArchivedUtc` die obere Schranke der Fälligkeiten (Plan ist beendet).
  - Pläne ohne `TargetDate` (u. a. `Type = Open`): bei gesetztem Filter ausblenden
    (Entscheidung: Vorschlag übernommen).
  - UND-Verknüpfung mit Suche und `OnlyActive`.
- Filterung **vor** Items-Projektion, damit `SavingsPlans_AnalyzeAsync` (N+1) nur für
  sichtbare Pläne läuft.

### #601 — Wertpapierübersicht (`/list/securities`)

- Suche wirksam machen: Teilstring, Case-insensitive auf `Name`, `Identifier`,
  `AlphaVantageCode`, `CategoryName` (Entscheidung: Vorschlagsfelder übernommen).
- `AllowRangeFiltering => false` (Datumsfilter fachlich nicht sinnvoll; Bestandskonvention).

### #604 — Benutzerübersicht (`/list/users`)

- Suche wirksam machen: Teilstring, Case-insensitive auf `Username` (einzige Freitext-Spalte).
- `AllowRangeFiltering => false` (Bestandskonvention).

## Akzeptanzkriterien

- Suchbegriff schränkt die Liste sichtbar ein; Leeren zeigt wieder alle Einträge.
- Datumsbereich der Sparplanliste filtert inklusive Grenzen; Recurring trifft auch dann, wenn
  das angezeigte nächste `TargetDate` außerhalb liegt.
- Suche/Datum/OnlyActive UND-verknüpft.
- Datumsfilter auf Wertpapier- und Benutzerliste nicht mehr sichtbar; andere Listen unverändert.
- Beleg durch VM-/Domänentests.

## Akzeptierte Semantik-Einschränkung

Die rückwärts rekonstruierten Fälligkeiten (`TargetDate − n·Interval`) können nach
Monatsende-Drift (z. B. Anker 30.1. → Feb 28./29. → Mär 31.) von der tatsächlichen
Fälligkeitshistorie abweichen — der ursprüngliche Ankertag ist aus `TargetDate` allein nicht
rekonstruierbar. Maßgeblich ist die definierte Semantik `TargetDate ± n·Interval` mit
Monatsende-Regel; Abweichungen von ±1–2 Tagen an Monatsgrenzen sind unvermeidbar und
für einen Listenfilter ohne praktische Auswirkung.
