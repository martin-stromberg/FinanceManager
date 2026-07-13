# Bestandsaufnahme: Korrektur der Dividendenhochrechnung

## Ziel der Untersuchung

Die Bestandsaufnahme untersucht gezielt die Dividendenhochrechnung mit `CompareProjection` in Wertpapierberichten. Fokus sind Berichtserzeugung, Wertpapierbestand, Reporting-Service und Tests. Grundlage ist `requirement.md` in diesem Feature-Verzeichnis.

## Kurzbefund

Die Hochrechnung ist zentral in `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` implementiert. Der spezialisierte Pfad `QuerySecurityDividendsNetAsync` erzeugt Netto-Dividendenereignisse aus gruppierten Wertpapier-Postings und berechnet daraus erwartete Dividenden ueber die lokale Funktion `ApplyProjectionAmounts`.

Aktuell prueft die Projektion keinen Wertpapierbestand. `ProjectionCandidate` wird allein aus Vorjahres-Dividendenereignissen gebildet. Dadurch koennen erwartete Dividenden fuer vollstaendig verkaufte Wertpapiere entstehen, sofern historische Dividenden vorhanden sind.

Die Datenbasis fuer Bestand ist vorhanden: `Posting.Quantity`, `SecurityPostingSubType.Buy` und `SecurityPostingSubType.Sell`. Der Buchungsservice speichert Kaeufe mit positiver Menge und Verkaeufe mit negativer Menge. Eine aehnliche Bestandsableitung existiert bereits in der Return-Analysis, ist aber nicht in die Berichtshochrechnung integriert.

Die vorhandenen Projektionstests decken Monats-, Quartals-, Jahres-, YTD-, Valuta- und Kategorie-Szenarien ab, aber keine Buy-/Sell-Bestaende. Neue Tests sollten im bestehenden `ReportAggregationProjectionTests` angesiedelt werden.

## Relevante Detaildokumente

- [Reporting-Service und Projektion](inventory/reporting-service.md)
- [Wertpapierbestand und Domänenmodell](inventory/wertpapierbestand.md)
- [Tests und Testlücken](inventory/tests.md)
- [Umsetzungsrisiken und offene Entscheidungen](inventory/risiken-und-offene-punkte.md)

## Zentrale Code-Einstiegspunkte

| Bereich | Datei | Relevanz |
|---|---|---|
| Projektion aktivieren | `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs:41` | `QueryAsync` leitet reine Security-Reports mit `CompareProjection` in den spezialisierten Dividendenpfad. |
| Dividenden-Nettoauswertung | `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs:697` | `QuerySecurityDividendsNetAsync` laedt Security-Postings, bildet Dividendengruppen und aggregiert Punkte. |
| Hochrechnung | `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs:1063` | `ApplyProjectionAmounts` erkennt Muster, erzeugt `ProjectionCandidate` und setzt `ProjectionAmount`/`ProjectionExpectedDividends`. |
| Projektion DTO | `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs:16` | DTO enthaelt bereits `ProjectionAmount` und `ProjectionExpectedDividends`; keine neue Eigenschaft erforderlich. |
| Query DTO | `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs:18` | `CompareProjection`, `AnalysisDate`, `UseValutaDate` und Filter sind bereits vorhanden. |
| Posting-Daten | `FinanceManager.Domain/Postings/Posting.cs:108` | Konstruktor und Property `Quantity` liefern die benoetigte Bestandsdatenbasis. |
| Subtypen | `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs:6` | `Buy = 0`, `Sell = 1`, `Dividend = 2`, `Fee = 3`, `Tax = 4`. |
| Buchungskonvention | `FinanceManager.Infrastructure/Statements/StatementDraftService.cs:2205` | Kaeufe werden positiv, Verkaeufe negativ als `Quantity` gespeichert; Dividenden ohne Menge. |
| Vergleichbare Bestandsermittlung | `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs:1600` | Bestehende Berechnung summiert Kaeufe und reduziert Verkaeufe. |
| Projektionstests | `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs:10` | Passender Ort fuer neue Bestandsszenarien zur Dividendenhochrechnung. |

## Beobachtete fachliche Lücke

In `ApplyProjectionAmounts` entsteht die erwartete Dividende ueber:

1. historische `DividendEvent`-Eintraege,
2. daraus abgeleitete `ProjectionCandidate`-Eintraege mit `ExpectedDate = PriorYearDate.AddYears(1)`,
3. Musterlogik fuer monatliche, quartalsweise, jaehrliche und unregelmaessige Dividenden,
4. Aggregation der erwarteten Details auf Leaf-, Kategorie- und Type-Zeilen.

Zwischen Schritt 2 und 4 gibt es keine Pruefung, ob zum erwarteten Datum oder zum Analysezeitpunkt noch ein positiver Bestand fuer `SecurityId` existiert.

## Naheliegende Implementierungsstelle

Der kleinste Eingriff liegt in `QuerySecurityDividendsNetAsync` vor oder innerhalb von `ApplyProjectionAmounts`:

- Bestand pro `SecurityId` aus `Postings` mit `SecuritySubType` `Buy`/`Sell` und `Quantity` berechnen.
- Nur Buchungen des aktuellen Benutzers beruecksichtigen. Da `Posting` keinen `OwnerUserId` hat, muss die bestehende Ownership ueber `Securities.OwnerUserId` beziehungsweise die bereits ermittelten `ownedIds` abgesichert bleiben.
- `ProjectionCandidate` vor der DTO-Erzeugung nach positivem Bestand filtern.
- Dadurch werden Leaf-, Kategorie- und Type-Aggregate automatisch bereinigt, weil sie aus `ProjectionExpectedDividends` und `ProjectionAmount` der Child-Zeilen aufgebaut werden.

## Testbedarf

Minimal benoetigt werden neue Tests fuer:

- vollstaendig verkauftes Wertpapier mit Vorjahresdividende erzeugt keine erwartete Dividende,
- teilverkauftes Wertpapier mit positivem Restbestand erzeugt weiterhin erwartete Dividende,
- Kategoriezeilen enthalten keine erwarteten Dividenden vollstaendig verkaufter Wertpapiere,
- Type-Zeilen beziehungsweise Multi-/Hierarchie-Szenario, sofern der spezialisierte Dividendenpfad Type-Zeilen erzeugt oder erwartet.

Details stehen in [Tests und Testlücken](inventory/tests.md).
