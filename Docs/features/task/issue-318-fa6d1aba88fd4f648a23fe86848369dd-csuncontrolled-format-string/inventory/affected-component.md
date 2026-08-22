# Detail: Betroffene Komponente und Schlüsselbildung

## Datei

`FinanceManager.Infrastructure/Budget/ReportCacheService.cs`

## Relevante Stellen

- `:15`: `ReportCacheService` implementiert `IReportCacheService`.
- `:38`: Cache-Leseweg ruft `BuildKey` auf.
- `:71`: Cache-Schreibweg ruft `BuildKey` auf.
- `:98` und `:157`: Refresh-Pfade erkennen Budget-Report-Einträge über `KeyPrefix_BudgetReportRawData`.
- `:181-183`: Präfix und private Schlüsselgenerierung.

## Aktuelles Verhalten

`BuildKey` übergibt eine bereits interpolierte Zeichenfolge als erstes Argument an `string.Format`. Dadurch ist der Formatstring nicht konstant, obwohl die Methode die Eingabeparameter nur als Werte darstellen soll. Die Kultur wird über `CultureInfo.InvariantCulture` an `string.Format` übergeben.

## Erhaltenswerte Eigenschaften

- Präfix: `budgetreportraw`
- Datumsformat: `yyyyMMdd`
- Reihenfolge: Präfix, `from`, `to`, `dateBasis`, jeweils durch `-` getrennt
- deterministische Ausgabe für identische Eingaben
- Unterscheidbarkeit bei Änderung eines Datums oder der Date-Basis

## Änderungsgrenze

Nur die private Formatierungsimplementierung ist unmittelbar betroffen. Die öffentliche Signatur, der Datenbankzugriff und die Refresh-Filterung bleiben unverändert.
