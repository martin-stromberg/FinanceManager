### Fachliche Zusammenfassung

Die Dividendenhochrechnung in Wertpapierberichten darf keine erwarteten Dividenden für Wertpapiere ausweisen, für die zum relevanten Hochrechnungszeitpunkt kein Bestand mehr vorhanden ist. Ein Wertpapier ohne Bestand kann keine zukünftige Dividende mehr erzeugen; deshalb müssen solche Wertpapiere bei der Berechnung von `ProjectionAmount` und den zugehörigen erwarteten Dividenden ausgeschlossen werden.

Die bestehende Hochrechnung bleibt grundsätzlich erhalten: Sie ergänzt gebuchte Netto-Dividenden des aktuellen Betrachtungszeitraums um erwartete Netto-Dividenden aus vergleichbaren Vorjahresereignissen. Neu ist, dass ein erwartetes Dividendenereignis nur dann berücksichtigt wird, wenn das jeweilige Wertpapier im aktuellen Bestand des Benutzers noch eine positive Stückzahl hat.

---

### Betroffene Klassen und Komponenten

#### Berichtswesen

- **`ReportAggregationService`** (`FinanceManager.Infrastructure.Reports.ReportAggregationService`)
  - Die spezialisierte Dividendenauswertung `QuerySecurityDividendsNetAsync` muss die Bestandsprüfung für Wertpapiere einbeziehen.
  - Die interne Hochrechnungslogik `ApplyProjectionAmounts` darf `ProjectionCandidate`-Einträge nur für Wertpapiere verwenden, deren Bestand zum relevanten Stichtag größer als `0` ist.
  - Kategorie- und Typzeilen dürfen ausgeschlossene erwartete Dividenden nicht aggregieren.

- **`ReportAggregationQuery`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregationQuery`)
  - Keine neue Eingabe erforderlich; die Korrektur ist Teil der bestehenden Option `CompareProjection`.
  - Die bestehende Auswahl nach `PostingKind.Security`, Intervall, Filtern, `AnalysisDate` und `UseValutaDate` bleibt maßgeblich.

- **`ReportAggregatePointDto`** (`FinanceManager.Shared.Dtos.Reports.ReportAggregatePointDto`)
  - Keine neue Eigenschaft erforderlich.
  - `ProjectionAmount` und `ProjectionExpectedDividends` müssen nach der Korrektur bereits bereinigte Werte enthalten.

#### Datenbasis für den Bestand

- **`Posting`** (`FinanceManager.Domain.Postings.Posting`)
  - Der Wertpapierbestand ist aus Wertpapierbuchungen mit `SecurityId`, `SecuritySubType` und `Quantity` abzuleiten.
  - Käufe erhöhen den Bestand; Verkäufe reduzieren den Bestand. In der bestehenden Domänenkonvention werden Verkäufe mit negativer `Quantity` gespeichert.
  - Dividendenbuchungen ohne Stückzahl dürfen den Bestand nicht erhöhen.

- **`SecurityPostingSubType`** (`FinanceManager.Domain.Postings.SecurityPostingSubType`)
  - Relevante Subtypen für den Bestand sind insbesondere `Buy` und `Sell`.
  - `Dividend`, `Fee` und `Tax` sind für die Dividendenhöhe relevant, dürfen aber keinen offenen Bestand begründen.

#### Tests

- **`ReportAggregationProjectionTests`** (`FinanceManager.Tests.Reports.ReportAggregationProjectionTests`)
  - Ergänzung um Tests für vollständig verkaufte Wertpapiere.
  - Ergänzung um Tests für teilweise verkaufte Wertpapiere mit weiterhin positivem Bestand.
  - Ergänzung um Tests, dass Kategorie- und Typzeilen keine erwarteten Dividenden vollständig verkaufter Wertpapiere enthalten.

---

### Funktionale Anforderungen

1. Wenn ein Wertpapier zum relevanten Hochrechnungszeitpunkt keinen positiven Bestand mehr hat, darf es keine erwartete Dividende in `ProjectionExpectedDividends` erzeugen.

2. Wenn ein Berichtspunkt eines vollständig verkauften Wertpapiers im aktuellen Zeitraum keine gebuchte Dividende enthält, bleibt `ProjectionAmount` für dieses Wertpapier `0` oder leer gemäß bestehender Berichtskonvention; es darf nicht durch Vorjahresdividenden erhöht werden.

3. Wenn ein Wertpapier im aktuellen Zeitraum bereits gebuchte Dividenden enthält, bleiben diese gebuchten Beträge weiterhin im normalen `Amount` und in der Hochrechnung erhalten. Die Korrektur betrifft nur zusätzlich erwartete, noch nicht gebuchte Dividenden.

4. Wenn ein Wertpapier nur teilweise verkauft wurde und der berechnete Bestand weiterhin größer als `0` ist, darf die bestehende Hochrechnung weiterhin erwartete Dividenden ausweisen.

5. Wenn Wertpapiere in Kategorien zusammengefasst werden, dürfen Kategoriezeilen nur erwartete Dividenden von Wertpapieren mit positivem Bestand aggregieren.

6. Wenn Typzeilen für Wertpapiere angezeigt werden, dürfen Typzeilen nur erwartete Dividenden von Wertpapieren mit positivem Bestand aggregieren.

7. Die Bestandsprüfung muss benutzerspezifisch erfolgen; Buchungen anderer Benutzer dürfen den Bestand nicht beeinflussen.

8. Die Korrektur gilt für alle von der Hochrechnung unterstützten Intervalle (`Month`, `Quarter`, `HalfYear`, `Year`, `Ytd`) und für die bestehende Behandlung von `AnalysisDate`.

9. Für `AllHistory`, Nicht-Wertpapierberichte, Mehrfachauswahlen mit nicht unterstützten Posting-Arten oder widersprüchliche Wertpapier-Subtypfilter bleibt die Hochrechnung wie bisher deaktiviert.

---

### Akzeptanzkriterien

1. Ein Wertpapier mit Vorjahresdividende und vollständigem Verkauf vor oder bis zum Analysezeitpunkt erzeugt keine erwartete Dividende.

2. Ein Wertpapier mit Vorjahresdividende und positivem aktuellem Bestand erzeugt weiterhin eine erwartete Dividende nach der bestehenden Projektionslogik.

3. Ein Wertpapier mit Kauf und anschließendem Teilverkauf erzeugt weiterhin eine erwartete Dividende, solange die Summe der bestandsrelevanten Stückzahlen größer als `0` ist.

4. Ein Wertpapier mit Kauf und anschließendem vollständigem Verkauf erzeugt keine erwartete Dividende, auch wenn im Vorjahr eine vergleichbare Dividende gebucht wurde.

5. Kategorieberichte enthalten in `ProjectionAmount` und `ProjectionExpectedDividends` keine erwarteten Dividenden vollständig verkaufter Wertpapiere.

6. Typberichte enthalten in `ProjectionAmount` und `ProjectionExpectedDividends` keine erwarteten Dividenden vollständig verkaufter Wertpapiere.

7. Die bestehenden Tests zur Dividendenhochrechnung für monatliche, quartalsweise, jährliche, unregelmäßige und YTD-Szenarien bleiben gültig, sofern ein positiver Bestand vorhanden ist.

---

### Implementierungsansatz

1. In `ReportAggregationService` wird für die Hochrechnung ein Bestand je `SecurityId` ermittelt. Grundlage sind Wertpapier-Postings des aktuellen Benutzers mit bestandsrelevanter `Quantity`.

2. Die Bestandsberechnung summiert `Quantity` für Käufe und Verkäufe gemäß bestehender Domänenkonvention. Verkäufe sind bereits negativ gespeichert und reduzieren dadurch den Bestand.

3. Der relevante Stichtag ist der Analysezeitpunkt beziehungsweise der Zeitraum, für den die erwartete Dividende geprüft wird. Erwartete Dividenden dürfen nur erzeugt werden, wenn der Bestand zu diesem Zeitpunkt positiv ist.

4. Die Liste der `ProjectionCandidate`-Einträge wird vor der Aggregation nach Bestand gefiltert. Dadurch bleiben Leaf-, Kategorie- und Typzeilen automatisch konsistent.

5. Die bestehende Erkennung von Dividendenmustern (`Monthly`, `Quarterly`, `Annual`, `Irregular`) bleibt unverändert. Die neue Regel ist eine zusätzliche fachliche Ausschlussbedingung.

6. Die Tests erzeugen Wertpapierbuchungen mit `SecurityPostingSubType.Buy` und `SecurityPostingSubType.Sell` sowie passenden `Quantity`-Werten, um vollständigen Verkauf, Teilverkauf und weiterhin offenen Bestand abzudecken.

---

### Nicht-Ziele

- Keine Änderung der Benutzeroberfläche für Berichte.
- Keine neue Konfigurationsoption für die Dividendenhochrechnung.
- Keine Änderung am Datenmodell oder an Datenbankmigrationen, sofern die vorhandenen Buchungsdaten mit `Quantity` ausreichen.
- Keine Änderung an der bestehenden Logik zur Erkennung von Dividendenmustern.
- Keine Neuberechnung historischer Buchungen außerhalb der Berichtsauswertung.

---

### Offene Fragen

1. Soll der Bestand für erwartete Dividenden exakt zum erwarteten Dividendentag geprüft werden oder pauschal zum `AnalysisDate`?
2. Wie sollen Wertpapiere behandelt werden, deren Bestandsbuchungen keine `Quantity` enthalten, obwohl Dividendenhistorie vorhanden ist?
3. Soll ein Bestand von exakt `0` mit Rundungstoleranz behandelt werden, um Dezimalreste aus Teilverkäufen zu ignorieren?
