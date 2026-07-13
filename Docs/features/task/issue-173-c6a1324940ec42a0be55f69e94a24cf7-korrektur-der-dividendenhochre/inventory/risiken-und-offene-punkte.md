# Detail: Umsetzungsrisiken und offene Entscheidungen

## Stichtag fuer Bestand

Offen aus `requirement.md`: Soll der Bestand exakt zum erwarteten Dividendentag oder pauschal zum `AnalysisDate` geprueft werden?

Technischer Befund:

- `ProjectionCandidate.ExpectedDate` ist bereits vorhanden.
- Eine Pruefung zum erwarteten Dividendentag ist damit praezise und lokal moeglich.
- Eine Pruefung zum `AnalysisDate` waere einfacher, kann aber erwartete Dividenden nach einem spaeteren Verkauf im Analysezeitraum anders behandeln.

Planungsentscheidung erforderlich.

## Bestandsbuchungen ohne Quantity

Offen aus `requirement.md`: Wie sollen Wertpapiere behandelt werden, deren Buy-/Sell-Postings keine `Quantity` enthalten?

Technischer Befund:

- `Quantity` ist nullable.
- Return Analysis behandelt fehlende Menge als `0`.
- Wenn fuer ein Wertpapier nur Dividendenhistorie, aber keine Mengenhistorie existiert, wuerde eine strikte Bestandspruefung alle Erwartungen entfernen.

Moegliche Entscheidungen:

- Strikt: fehlende Menge bedeutet kein positiver Bestand.
- Kompatibilitaet: wenn keine bestandsrelevanten Postings existieren, bisheriges Projektionsverhalten beibehalten.

Die Anforderung tendiert zu strikter positiver Stueckzahl, nennt den Umgang aber explizit als offene Frage.

## Rundungstoleranz

Offen aus `requirement.md`: Soll Bestand exakt `> 0` sein oder mit Toleranz?

Technischer Befund:

- Mengen sind `decimal`.
- Teilverkaeufe koennen theoretisch minimale Restwerte erzeugen.
- Return Analysis begrenzt nur auf `Math.Max(0m, shares)`, verwendet aber keine explizite Epsilon-Toleranz.

Fuer minimale Aenderung sollte zunaechst `> 0m` gelten. Eine Toleranz waere eine fachliche Zusatzregel.

## Ladefenster und Performance

Bestand muss alle Buy-/Sell-Buchungen bis zum Stichtag kennen. Ein zu enges Ladefenster, etwa `loadStartMonth`, waere fachlich falsch, weil Kaeufe weit vor dem Dividenden-Vergleichszeitraum liegen koennen.

Risiko:

- Fuer Nutzer mit vielen Wertpapierbuchungen kann ein vollstaendiger Load aller Buy/Sell-Postings bis `projectionEnd` groesser werden.

Abmilderung:

- Nur `PostingKind.Security`, eigene `ownedIds`, Subtypen `Buy`/`Sell`, `SecurityId != null`, `Quantity != null` laden.
- Nur benoetigte Felder projizieren: `SecurityId`, Datum, Subtyp, Quantity.
- In-memory nach Security gruppieren und sortieren.

## Datum: Booking vs. Valuta

`QuerySecurityDividendsNetAsync` nutzt fuer Dividenden bei `UseValutaDate` das Valutadatum mit Fallback auf BookingDate (`ReportAggregationService.cs:741`, `760-761`).

Fuer die Bestandspruefung sollte dieselbe Datumssemantik gelten, sonst kann die Projektion bei Valuta-Reports inkonsistent werden. Das bedeutet:

- `query.UseValutaDate == true`: `p.ValutaDate` mit Fallback auf `p.BookingDate`.
- sonst: `p.BookingDate`.

## Kategorie- und Type-Aggregation

Wenn erwartete Dividenden bereits auf `ProjectionCandidate`-Ebene gefiltert werden, bleiben Kategoriezeilen konsistent, weil sie aus Child-Projection-Details aufgebaut werden.

Type-Zeilen sind riskanter:

- `ApplyProjectionAmounts` kann Type-Zeilen aggregieren.
- Der spezialisierte Dividendenpfad erzeugt fuer normale reine Security-Projection-Reports aber keine Type-Zeilen.
- Multi-Kind und AllHistory deaktivieren `CompareProjection`.

Die Planungsphase sollte klaeren, ob fuer Type-Zeilen ein bestehender erreichbarer Pfad existiert oder ob die Anforderung vorsorglich aus der generischen Aggregationslogik stammt.

## Doppelte Ausfuehrung von `ApplyProjectionAmounts`

Die Funktion wird vor und nach dem Ergaenzen fehlender Latest-Period-Zeilen aufgerufen. Eine Bestandsermittlung darf deshalb nicht pro Aufruf DB-seitig wiederholt werden.

Empfehlung:

- Bestandstransaktionen einmal in `QuerySecurityDividendsNetAsync` laden.
- Eine lokale synchron auswertbare Hilfsfunktion an `ApplyProjectionAmounts` anbinden.
- `ApplyProjectionAmounts` idempotent belassen, sodass der zweite Aufruf die Projection-Felder konsistent ueberschreibt.

## Keine DTO-/API-Aenderung noetig

`ReportAggregationQuery` enthaelt bereits `CompareProjection`, `AnalysisDate`, `UseValutaDate` und Filter.

`ReportAggregatePointDto` enthaelt bereits `ProjectionAmount` und `ProjectionExpectedDividends`.

Es ist keine Migration, kein neues DTO-Feld und keine UI-Aenderung erforderlich.
