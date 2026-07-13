# Umsetzungsplan: Korrektur der Dividendenhochrechnung

## Ziel

Die Dividendenhochrechnung fuer Wertpapierberichte mit `CompareProjection` soll erwartete Dividenden nur noch fuer Wertpapiere erzeugen, die zum erwarteten Dividendentag einen positiven Bestand haben. Gebuchte Dividenden bleiben unveraendert erhalten; ausgeschlossen werden nur zusaetzliche erwartete Dividenden aus der Projektion.

## Geklaerte Entscheidungen

- Der Bestand wird exakt zum erwarteten Dividendentag des jeweiligen `ProjectionCandidate` geprueft.
- Bestandsbuchungen ohne `Quantity` begruenden keinen Bestand.
- Ein Bestand von exakt `0` ist kein positiver Bestand.
- Es wird keine zusaetzliche Rundungstoleranz eingefuehrt; die Pruefung lautet strikt `> 0m`.

## Betroffene Dateien

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
  - Bestandstransaktionen fuer eigene Wertpapiere laden.
  - Positive Bestandspruefung in die Projektion einbinden.
  - `ProjectionCandidate` vor der Erzeugung von `ReportProjectionExpectedDividendDto` filtern.
- `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs`
  - Testhelfer fuer Buy-/Sell-Buchungen ergaenzen.
  - Bestandsszenarien fuer Leaf- und Kategorieprojektionen absichern.

Nicht betroffen:

- Keine Aenderung an `ReportAggregationQuery`.
- Keine Aenderung an `ReportAggregatePointDto`.
- Keine Migration oder Datenmodellaenderung.
- Keine UI-Aenderung.

## Implementierungsschritte

### 1. Lokales Modell fuer Bestandstransaktionen einfuehren

In `ReportAggregationService` wird ein kleines internes Record fuer die Projektion ergaenzt, zum Beispiel:

```csharp
private sealed record HoldingTransaction(Guid SecurityId, DateTime Date, SecurityPostingSubType SubType, decimal Quantity);
```

Dieses Record bleibt service-intern und wird nur fuer die Bestandspruefung der Dividendenprojektion verwendet.

### 2. Bestandstransaktionen einmalig laden

In `QuerySecurityDividendsNetAsync` werden vor dem ersten Aufruf von `ApplyProjectionAmounts` alle bestandsrelevanten Wertpapierbuchungen geladen:

- `PostingKind.Security`
- `SecurityId != null`
- `SecurityId` liegt in `ownedIds`
- `SecuritySubType` ist `Buy` oder `Sell`
- `Quantity != null`
- Datum ist kleiner oder gleich dem maximal relevanten Projektionsstichtag

Die Ownership-Grenze bleibt wie bisher ueber `ownedIds` aus `Securities.OwnerUserId == query.OwnerUserId` abgesichert. Fremde Buchungen duerfen nicht in die Berechnung gelangen.

Fuer die Datumssemantik wird dieselbe Logik wie bei Dividendenevents verwendet:

- Bei `query.UseValutaDate == true`: `p.ValutaDate` mit Fallback auf `p.BookingDate`
- Sonst: `p.BookingDate`

Als oberes Ladeende reicht der groesste moegliche erwartete Dividendentag im aktuellen Projektionsfenster. Praktisch kann dafuer das bereits vorhandene Projektionsende aus der Projektion verwendet oder vorab analog berechnet werden. Ein unteres Ladeende darf nicht gesetzt werden, weil Kauefe lange vor dem Dividendenvergleichszeitraum liegen koennen.

### 3. Bestandsfunktion in-memory bereitstellen

Die geladenen Transaktionen werden nach `SecurityId` gruppiert und nach Datum sortiert. Eine lokale Hilfsfunktion prueft den Bestand zu einem Stichtag:

```csharp
bool HasPositiveHolding(Guid securityId, DateTime date)
```

Berechnungsregeln:

- Nur Transaktionen mit `Date.Date <= date.Date` zaehlen.
- `Buy`: `shares += quantity`
- `Sell`: `shares -= Math.Abs(quantity)`
- Fehlende `Quantity` kommt durch den Ladefilter nicht in die Funktion.
- Rueckgabe ist `shares > 0m`.

Eine Kappung auf `Math.Max(0m, shares)` ist fuer die reine Positivpruefung nicht erforderlich, kann aber zur Lesbarkeit analog zur Return-Analysis genutzt werden. Eine Toleranz wird nicht eingebaut.

### 4. `ProjectionCandidate` nach Bestand filtern

In `ApplyProjectionAmounts` wird der bestehende Candidate-Aufbau erweitert. Direkt nachdem `ExpectedDate` und der Reportzeitraum bekannt sind, wird gefiltert:

```csharp
.Where(candidate => HasPositiveHolding(candidate.SecurityId, candidate.ExpectedDate))
```

Der Filter muss vor der Umwandlung in `ReportProjectionExpectedDividendDto` liegen. Dadurch wirken alle bestehenden Mechanismen weiter:

- Leaf-Zeilen erhalten keine erwarteten Dividenden fuer Wertpapiere ohne positiven Bestand.
- Kategoriezeilen aggregieren automatisch nur noch bereinigte Child-Details.
- Type-Zeilen bleiben konsistent, falls sie in einem erreichbaren Pfad vorhanden sind.

Die bestehende Mustererkennung fuer `Monthly`, `Quarterly`, `Annual` und `Irregular` bleibt unveraendert.

### 5. Doppelte Ausfuehrung von `ApplyProjectionAmounts` beibehalten

`ApplyProjectionAmounts` wird aktuell vor und nach dem Ergaenzen fehlender Latest-Period-Zeilen aufgerufen. Dieses Verhalten wird beibehalten, um keine Nebenwirkungen an der bestehenden Reportstruktur zu erzeugen.

Wichtig ist nur, dass die Datenbankabfrage fuer Bestandstransaktionen nicht innerhalb von `ApplyProjectionAmounts` liegt. Die Funktion arbeitet synchron auf bereits geladenen Daten.

### 6. Bestehendes Verhalten ausserhalb der Projektion unveraendert lassen

Die bestehenden Ausschlussregeln bleiben bestehen:

- `AllHistory` aktiviert keine Projektion.
- Nicht-Wertpapierberichte aktivieren keine Projektion.
- Mehrfachauswahlen mit nicht unterstuetzten Posting-Arten aktivieren keine Projektion.
- Widerspruechliche Security-Subtypfilter aktivieren keine Projektion.

Gebuchte Dividenden im aktuellen Zeitraum bleiben in `Amount` erhalten. Wenn ein Wertpapier zum erwarteten Dividendentag keinen Bestand hat, wird nur die zusaetzliche Projektion entfernt.

## Testplan

### Testhelfer

In `ReportAggregationProjectionTests` wird ein Helfer fuer Wertpapiertransaktionen ergaenzt:

```csharp
private static void AddTrade(
    AppDbContext db,
    Security security,
    DateTime bookingDate,
    SecurityPostingSubType subType,
    decimal? quantity,
    DateTime? valutaDate = null)
```

Der Helfer legt `PostingKind.Security`-Postings mit Subtyp `Buy` oder `Sell` an. Sell-Mengen sollten in Tests negativ uebergeben werden, passend zur Buchungskonvention; die Implementierung soll dennoch `Math.Abs` fuer Sell verwenden.

### Neue Tests

1. Vollstaendig verkauftes Wertpapier erzeugt keine erwartete Dividende
   - Vorjahresdividende vorhanden.
   - Kauf vor dem Vergleichszeitraum.
   - Vollstaendiger Verkauf vor oder am erwarteten Dividendentag.
   - Erwartung: `ProjectionExpectedDividends` ist `null`, `ProjectionAmount` bleibt `0m` oder entspricht nur gebuchten aktuellen Dividenden.

2. Teilverkauftes Wertpapier erzeugt weiterhin erwartete Dividende
   - Kauf `10`, Verkauf `-4`.
   - Vorjahresdividende vorhanden, aktuelle Dividende fehlt.
   - Erwartung: erwartete Dividende bleibt vorhanden, weil Bestand `6 > 0`.

3. Aktuell gebuchte Dividende bleibt trotz geschlossenem Bestand erhalten
   - Vorjahresdividende und aktuelle gebuchte Dividende vorhanden.
   - Bestand zum erwarteten Dividendentag ist `0`.
   - Erwartung: `Amount` und `ProjectionAmount` enthalten die gebuchte aktuelle Dividende; keine zusaetzlichen `ProjectionExpectedDividends`.

4. Kategorieaggregation entfernt erwartete Dividenden verkaufter Wertpapiere
   - Eine Kategorie enthaelt ein gehaltenes und ein vollstaendig verkauftes Wertpapier.
   - Beide haben passende Vorjahresdividenden.
   - Erwartung: Kategorie-`ProjectionAmount` und `ProjectionExpectedDividends` enthalten nur das gehaltene Wertpapier.

5. Buchungen anderer Benutzer beeinflussen Bestand nicht
   - Eigenes Wertpapier ohne positiven Bestand.
   - Fremdes Wertpapier oder fremde Security-Buchungen mit positiver Menge.
   - Erwartung: fremde Buchungen erzeugen keinen positiven Bestand fuer die eigene Projektion.

6. `UseValutaDate` gilt auch fuer Bestand
   - Buy/Sell mit abweichendem Booking- und Valutadatum.
   - Query mit `UseValutaDate: true`.
   - Erwartung: die Bestandsentscheidung richtet sich nach Valutadatum mit BookingDate-Fallback.

7. Buchungen ohne `Quantity` begruenden keinen Bestand
   - Buy-Posting ohne `Quantity` und Vorjahresdividende.
   - Erwartung: keine erwartete Dividende, weil kein positiver Bestand nachweisbar ist.

### Regressionstests

Die vorhandenen Projektionstests muessen ggf. um explizite Buy-Bestandsbuchungen ergaenzt werden, wenn die neue strikte Bestandsregel sonst bisherige positive Projektionsszenarien entfernt. Das ist erwartbar und fachlich korrekt: Tests, die weiterhin Projektionen erwarten, muessen einen positiven Bestand modellieren.

Empfohlene Testlaeufe:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~FinanceManager.Tests.Reports.ReportAggregationProjectionTests
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~FinanceManager.Tests.Reports
```

## Risiken und Gegenmassnahmen

- Bestandshistorie vor dem bisherigen `loadStartMonth`: Fuer Bestand darf kein unteres Datumslimit verwendet werden, sonst fehlen alte Kauefe.
- Performance bei vielen Buchungen: Nur eigene Securities, Buy/Sell, gesetzte `Quantity` und benoetigte Spalten laden.
- Doppelte Projektion: Bestand einmal laden und in-memory auswerten, damit beide `ApplyProjectionAmounts`-Aufrufe keine zusaetzliche DB-Last erzeugen.
- Datumsinkonsistenz: Fuer Bestand dieselbe Booking-/Valuta-Entscheidung verwenden wie fuer Dividendenevents.
- Type-Zeilen: Kein separater Eingriff noetig, da die vorhandene Aggregation aus bereinigten Projektiondetails arbeitet.

## Abnahmekriterien

- Vollstaendig verkaufte Wertpapiere erzeugen keine erwarteten Dividenden mehr.
- Teilverkaufte Wertpapiere mit Restbestand `> 0m` erzeugen weiterhin erwartete Dividenden.
- Wertpapiere mit Bestand exakt `0m` werden nicht projiziert.
- Postings ohne `Quantity` begruenden keinen Bestand.
- Der Bestand wird zum erwarteten Dividendentag geprueft.
- Kategorieprojektionen enthalten keine erwarteten Dividenden vollstaendig verkaufter Wertpapiere.
- Fremde Wertpapierbuchungen beeinflussen den Bestand nicht.
- Bestehende deaktivierende Regeln fuer nicht unterstuetzte Projektionen bleiben unveraendert.

## Offene Punkte

Keine.
