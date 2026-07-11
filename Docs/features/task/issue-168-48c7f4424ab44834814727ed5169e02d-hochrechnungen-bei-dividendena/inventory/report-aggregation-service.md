# ReportAggregationService und Security-Dividendenpfad

## Relevante Dateien

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- `FinanceManager.Application/Reports/IReportAggregationService.cs`
- `FinanceManager.Domain/Postings/Posting.cs`
- `FinanceManager.Domain/Postings/PostingAggregate.cs`

## Allgemeiner Aggregationspfad

`QueryAsync(...)` loest `PostingKinds`, bestimmt das Quellintervall, liest `PostingAggregates` nach Booking- oder Valuta-DateKind, prueft Ownership, wendet Filter an, baut Entity-/Category-/Type-Zeilen, transformiert Intervalle, berechnet Vergleiche und trimmt auf `Take`.

Der normale Pfad arbeitet mit voraggregierten Daten. Er hat keine Informationen ueber einzelne Dividendenereignisse und eignet sich daher nicht allein fuer die geforderte Aequivalenzpruefung.

## Spezialpfad fuer Security-Dividenden

`QuerySecurityDividendsNetAsync(...)` wird aktuell verwendet, wenn:

- nur `PostingKind.Security` angefragt ist und `Filters.IncludeDividendRelated == true`, oder
- nur `PostingKind.Security`, Intervall `Ytd` und `SecuritySubTypes` enthaelt `Dividend`.

Der Spezialpfad liest `Postings` direkt, filtert auf Security-Postings des Owners, Zeitraum `startMonth` bis `endExclusive`, nicht-null `SecuritySubType`, bestimmt Dividend-Gruppen ueber `GroupId`, summiert Dividend/Fee/Tax pro Gruppe und aggregiert auf Security+Monat. Kategoriezeilen, Intervalltransformation, Vergleiche und Trim laufen danach im Spezialpfad separat.

## Wichtige Beobachtungen

- Der Spezialpfad filtert und ankert aktuell immer auf `BookingDate`; `UseValutaDate` wird hier nicht beachtet.
- Die Netto-Berechnung nutzt `GroupId` und Subtypes `Dividend`, `Fee`, `Tax`. Das passt zur offenen Frage "Netto oder Brutto" aus der Anforderung.
- Fuer die Projektion muessen aktuelle Periode und gleiche Vorjahresperiode gemeinsam verfuegbar sein. Der aktuelle Spezialpfad laedt nur den aktuellen Ergebniszeitraum `analysis - (take - 1)` bis `analysis + 1 Monat`.
- Eine Vorjahresdividende kann nur dann als "bestaetigt" erkannt werden, wenn der Spezialpfad Ereignisebene vor der Monatsaggregation beibehaelt. Nach `monthly` ist diese Information verloren.
- Kategorie- und Type-Summen muessen `ProjectionAmount` separat summieren; ein einfaches `Amount`-Mapping reicht nicht.

## Geeigneter Implementierungsansatz

Der geringste Eingriff ist, im Spezialpfad eine interne Ereignisstruktur fuer Dividendengruppen aufzubauen:

- SecurityId
- DividendDate nach Booking/Valuta-Regel
- PeriodStart
- NetAmount
- GroupId

Dann kann fuer Projektion je aktueller Ausgabeperiode die korrespondierende Vorjahresperiode bestimmt und je Security verglichen werden. Als Default-Aequivalenz bietet sich `SecurityId` plus Monat/Periodenbucket an, solange die fachliche Frage nicht anders beantwortet wird.

## Server-Regel

Das neue Flag darf nur wirksam werden, wenn die effektiven `kinds` exakt `[PostingKind.Security]` sind. Bei anderen Kombinationen sollte `ComparedProjection == false` sein und `ProjectionAmount == null` bleiben oder der Controller sollte die Anfrage ablehnen.
