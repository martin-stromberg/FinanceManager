# Detail: Reporting-Service und Projektion

## Betroffene Datei

`FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`

## Einstieg aus `QueryAsync`

`QueryAsync` prueft die angefragten Posting-Kinds und aktiviert den spezialisierten Wertpapier-Dividendenpfad, wenn nur `PostingKind.Security` angefragt ist und entweder `IncludeDividendRelated` oder `CompareProjection` relevant ist.

Wichtige Stellen:

- `ReportAggregationService.cs:52`: `includeDividendRelated`
- `ReportAggregationService.cs:53`: `onlySecurityKind`
- `ReportAggregationService.cs:56`: widerspruechliche Security-Subtypfilter
- `ReportAggregationService.cs:57`: `compareProjection`
- `ReportAggregationService.cs:58-61`: Aufruf von `QuerySecurityDividendsNetAsync`

Damit ist die Korrektur fachlich im spezialisierten Pfad richtig verortet. Nicht-Wertpapierberichte und Mehrfachauswahlen bleiben bereits ausgeschlossen.

## `QuerySecurityDividendsNetAsync`

Der spezialisierte Pfad beginnt bei `ReportAggregationService.cs:697`.

Relevante Verarbeitung:

- `ReportAggregationService.cs:699-701`: `AnalysisDate` wird auf Monatsanfang normalisiert.
- `ReportAggregationService.cs:706-714`: Zeitraum und `compareProjection` werden bestimmt.
- `ReportAggregationService.cs:716-724`: Wertpapiere werden ueber `Securities.OwnerUserId == query.OwnerUserId` geladen. Das ist die zentrale Ownership-Grenze.
- `ReportAggregationService.cs:738-742`: `postings` umfasst Security-Postings fuer eigene Wertpapiere, im Ladezeitraum, mit Security-Subtyp.
- `ReportAggregationService.cs:744-764`: Dividendengruppen werden netto aus `Dividend`, `Fee`, `Tax` gebildet.
- `ReportAggregationService.cs:766-769`: `DividendEvent` entsteht aus den Gruppennetzen.
- `ReportAggregationService.cs:771-839`: Leaf- und Kategoriepunkte werden aus Dividendenevents gebaut.
- `ReportAggregationService.cs:927-932` und `974-977`: `ApplyProjectionAmounts` wird aktuell zweimal aufgerufen, einmal vor und einmal nach dem Ergaenzen fehlender Latest-Period-Zeilen.

## `ApplyProjectionAmounts`

Die lokale Funktion startet bei `ReportAggregationService.cs:1063`.

Wichtige Abschnitte:

- `ReportAggregationService.cs:1070-1080`: Mappt erwartete Dividendendaten in den angefragten Reportzeitraum.
- `ReportAggregationService.cs:1096-1101`: Bestimmt Projektionsfenster.
- `ReportAggregationService.cs:1103-1128`: Erstellt `expectedDetails` pro Security aus historischen Dividendenevents.
- `ReportAggregationService.cs:1113-1118`: Bildet `ProjectionCandidate` aus Vorjahresereignissen.
- `ReportAggregationService.cs:1120-1126`: Wandelt erwartete Kandidaten in `ReportProjectionExpectedDividendDto`.
- `ReportAggregationService.cs:1130-1149`: Gruppiert erwartete Details je Security und Periode.
- `ReportAggregationService.cs:1291-1304`: Setzt `ProjectionAmount = point.Amount + expected` und `ProjectionExpectedDividends`.
- `ReportAggregationService.cs:1306-1332`: Aggregiert Projektion auf Kategoriezeilen.
- `ReportAggregationService.cs:1334-1379`: Aggregiert Projektion auf Type-Zeilen, falls solche Punkte existieren.

## Aktuelle Lücke

`ProjectionCandidate` enthaelt nur `SecurityId`, `ExpectedDate`, `PriorYearDate` und `NetAmount`. Es gibt keine Bestandsermittlung und keinen Filter auf positiven Bestand. Jedes passende Vorjahresereignis kann damit eine erwartete Dividende erzeugen.

## Geeigneter Aenderungspunkt

Der sauberste Punkt ist vor der Erzeugung von `ReportProjectionExpectedDividendDto`, also zwischen `ProjectionCandidate`-Erzeugung und `FindExpectedDividends(...)`.

Dort ist `ExpectedDate` bereits bekannt. Die Bestandsermittlung kann als Dictionary oder Funktion `HasPositiveHolding(securityId, date)` in der Closure von `ApplyProjectionAmounts` verfuegbar sein.

## Vorsicht bei doppeltem Aufruf

`ApplyProjectionAmounts` wird zweimal ausgefuehrt. Eine neue Bestandsabfrage sollte nicht innerhalb dieser lokalen Funktion jedes Mal die Datenbank erneut lesen. Besser:

- Bestandstransaktionen einmal vor dem ersten Aufruf asynchron laden,
- in-memory nach Security und Stichtag auswerten,
- `ApplyProjectionAmounts` rein synchron mit bereits geladenen Daten belassen.

Alternativ kann die Funktion nur beim zweiten Aufruf laufen, falls der erste Aufruf nicht mehr notwendig ist. Das waere aber eine Verhaltensaenderung mit groesserem Risiko und sollte separat begruendet werden.
