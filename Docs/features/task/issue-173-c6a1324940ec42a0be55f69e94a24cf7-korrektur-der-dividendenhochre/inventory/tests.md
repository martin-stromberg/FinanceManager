# Detail: Tests und Testluecken

## Vorhandene Projektionstests

`FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs` ist der zentrale Testort.

Vorhandene Abdeckung:

- `ReportAggregationProjectionTests.cs:64`: fehlende aktuelle Dividende wird aus Vorjahres-Nettoereignis hochgerechnet.
- `ReportAggregationProjectionTests.cs:109`: Vorjahresereignisse werden einzeln abgeglichen.
- `ReportAggregationProjectionTests.cs:141`: fruehere aktuelle Jahresdividende gilt als bestaetigt.
- `ReportAggregationProjectionTests.cs:172`: monatliche Muster erwarten keine verpassten vergangenen Monate.
- `ReportAggregationProjectionTests.cs:217` und `270`: Korrektur-/Gegenbuchungen in monatlichen Mustern.
- `ReportAggregationProjectionTests.cs:351` und `389`: Quartalslogik.
- `ReportAggregationProjectionTests.cs:419` und `451`: unregelmaessige Dividenden.
- `ReportAggregationProjectionTests.cs:482`: `UseValutaDate`.
- `ReportAggregationProjectionTests.cs:513` und `537`: Projektion wird fuer nicht unterstuetzte Faelle deaktiviert.
- `ReportAggregationProjectionTests.cs:578`: Kategorieaggregation mit Projektion.
- `ReportAggregationProjectionTests.cs:609`: YTD und Quartalsintervall.

## Test-Helfer

Aktuelle Helfer:

- `CreateDb()` nutzt SQLite in-memory.
- `AddUserAsync(...)` legt Benutzer an.
- `AddSecurity(...)` legt eigene Wertpapiere an.
- `AddDividendGroup(...)` legt Dividend/Fee/Tax-Postings mit gemeinsamem `GroupId` an.

Es fehlt ein Helfer fuer Bestandsbuchungen, zum Beispiel:

- `AddTrade(AppDbContext db, Security security, DateTime date, SecurityPostingSubType subtype, decimal quantity, decimal amount = 0m, DateTime? valutaDate = null)`

Dabei sollte `Buy` positive Quantity und `Sell` negative Quantity verwenden, passend zur Buchungskonvention.

## Fehlende Szenarien

Die aktuelle Testsuite erzeugt in `ReportAggregationProjectionTests` keine Buy-/Sell-Postings. Deshalb wuerde eine Projektion fuer vollstaendig verkaufte Wertpapiere aktuell nicht auffallen.

Erforderliche neue Tests:

1. Vollstaendig verkauft:
   - Vorjahresdividende vorhanden.
   - Kauf vor Vorjahres-/Analysezeitraum.
   - Verkauf der gesamten Menge vor oder bis zum relevanten Stichtag.
   - Erwartung: `ProjectionAmount` bleibt `0m` oder nur gebuchte aktuelle Dividenden; `ProjectionExpectedDividends` ist `null`.

2. Teilverkauft:
   - Kauf zum Beispiel `10`, Verkauf `-4`.
   - Vorjahresdividende vorhanden, keine aktuelle Dividende.
   - Erwartung: erwartete Dividende bleibt vorhanden, weil Bestand `6 > 0`.

3. Gebuchte aktuelle Dividende trotz geschlossenem Bestand:
   - Aktuelle Dividende ist bereits gebucht.
   - Bestand zum Projektionsstichtag ist `0`.
   - Erwartung: `Amount` bleibt erhalten; nur zusaetzliche erwartete Dividenden werden ausgeschlossen.

4. Kategorieaggregation:
   - Eine Kategorie mit mindestens einem gehaltenen und einem vollstaendig verkauften Wertpapier.
   - Erwartung: Kategorie-`ProjectionAmount` und `ProjectionExpectedDividends` enthalten nur gehaltene Wertpapiere.

5. Benutzerisolation:
   - Gleiches oder anderes Wertpapier eines anderen Benutzers mit Buy/Sell-Buchungen.
   - Erwartung: fremde Buchungen beeinflussen Bestand nicht.

6. `UseValutaDate`:
   - Wenn die Bestandspruefung analog zum Reportdatum laufen soll, muss ein Test klaeren, ob Buy/Sell nach Valuta oder Booking Date bewertet werden.

## Type-Zeilen

Die lokale Projektion kann Type-Zeilen aggregieren (`ReportAggregationService.cs:1334-1379`). Im spezialisierten `QuerySecurityDividendsNetAsync` werden Type-Zeilen aber nicht allgemein aufgebaut; im normalen Pfad entstehen sie bei Multi-Kind oder AllHistory.

Da `CompareProjection` fuer Multi-Kind und AllHistory deaktiviert ist, ist ein direkter Type-Zeilen-Test eventuell nur moeglich, wenn ein konkreter Reportmodus im UI/API Type-Zeilen fuer reine Security-Dividenden liefert. Vor der Implementierung sollte geprueft werden, ob die Akzeptanzanforderung zu Type-Zeilen mit dem aktuellen spezialisierten Pfad tatsaechlich erreichbar ist oder ob nur die bereits vorhandene Aggregationslogik gegen Regression abgesichert werden soll.

## Empfohlener Testlauf

Nach Implementierung:

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~FinanceManager.Tests.Reports.ReportAggregationProjectionTests`

Bei breiterem Risiko:

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter FullyQualifiedName~FinanceManager.Tests.Reports`
