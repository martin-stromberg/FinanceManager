# Bestandsaufnahme: Unkontrollierte Formatzeichenfolge im Budget-Report-Cache

## Ergebnis

Die betroffene Schlüsselgenerierung ist auf eine private Hilfsmethode in `ReportCacheService` begrenzt. `BuildKey` wird von den Cache-Lese- und Schreibpfaden aufgerufen; der erzeugte Schlüssel wird anschließend ausschließlich als `ReportCacheEntry.CacheKey` persistiert und abgefragt. Die fachliche Änderung kann daher auf die Formatierung in `BuildKey` und gegebenenfalls fokussierte Tests für deren Ergebnis begrenzt werden.

## Betroffene Komponenten

- `FinanceManager.Infrastructure/Budget/ReportCacheService.cs:181-183`
  - Enthält das Präfix `budgetreportraw` und `BuildKey(DateOnly, DateOnly, BudgetReportDateBasis)`.
  - Verwendet aktuell `string.Format(CultureInfo.InvariantCulture, interpolierterString)`.
- `FinanceManager.Application/Budget/IReportCacheService.cs`
  - Öffentlicher Cache-Vertrag; keine direkte Schlüsselbildungslogik und voraussichtlich keine API-Änderung erforderlich.
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs:134`
  - Registriert `IReportCacheService` als `ReportCacheService` mit Scoped-Lebensdauer.

## Bestehende Tests

Der Testbereich liegt in `FinanceManager.Tests/Infrastructure/Budget/ReportCacheServiceTests.cs`. Er verwendet SQLite in-memory und prüft die Cache-Invalidierung für überlappende Zeiträume sowie die Präfixfilterung. Der private Test-Helfer `BuildKey` erzeugt den erwarteten Schlüssel aktuell separat mit einer konstanten Formatzeichenfolge.

Es gibt noch keinen direkten Test, der die konkrete `BuildKey`-Ausgabe über die öffentliche Service-Schnittstelle für Lesen und Schreiben absichert. Geeignet sind fokussierte Integrationstests über `SetBudgetReportRawDataAsync` und `GetBudgetReportRawDataAsync`, sofern der Zugriff auf die gespeicherte `CacheKey` benötigt wird. Alternativ kann der bestehende Test-Helfer die erwartete Struktur für Invalidierungstests dokumentieren; ein direkter Determinismus-Test bleibt vorzuziehen.

## Daten- und Ausführungsfluss

1. `BudgetReportsController.GetRawAsync` erhält `BudgetReportRequest` einschließlich `DateBasis` und berechnet den Datumsbereich.
2. Der Report-Service ruft den Cache über `IReportCacheService` auf.
3. `ReportCacheService` baut mit `BuildKey` einen Schlüssel aus Bereich und `BudgetReportDateBasis`.
4. EF Core sucht oder schreibt `ReportCacheEntry` anhand von `OwnerUserId` und `CacheKey`.
5. Refresh-Pfade filtern Cacheeinträge über das Präfix und verwenden die serialisierten `BudgetReportCacheParameter` für die fachliche Bereichsprüfung.

Die MVC-Parameter erreichen die Schlüsselbildung indirekt über den Servicevertrag. Die Änderung darf weder Parameter-Validierung noch Cache-Lebensdauer, Refresh-Logik oder Persistenzmodell verändern.

## Erwartete Schlüsselstruktur

Für `from = 2026-01-01`, `to = 2026-01-31` und `dateBasis = BookingDate` muss der Schlüssel weiterhin lauten:

`budgetreportraw-20260101-20260131-BookingDate`

Die Datumsbestandteile müssen mit `yyyyMMdd` und kulturinvariant erzeugt werden. Die beiden Datumswerte, `dateBasis` und das feste Präfix müssen weiterhin relevante Bestandteile des Schlüssels bleiben.

## Detaildokumente

- [Betroffene Komponente und Schlüsselbildung](inventory/affected-component.md)
- [Cache-Vertrag und Ausführungsfluss](inventory/cache-flow.md)
- [Tests und Verifikation](inventory/tests.md)
- [Abgrenzung, Risiken und Änderungsumfang](inventory/scope-and-risks.md)

## Empfohlener Änderungsumfang

- `ReportCacheService.BuildKey` so umformulieren, dass kein von Parametern abhängiger Ausdruck als Formatstring an `string.Format` übergeben wird.
- Die kulturinvariante Datumsformatierung und die bisherige Schlüsselstruktur beibehalten.
- Bestehende Cache-Invalidierungstests ausführen und einen fokussierten Test für Schlüsselstruktur, Determinismus und relevante Unterschiede ergänzen, falls der direkte Pfad noch nicht ausreichend abgedeckt ist.
- Keine Änderungen an Controller-Vertrag, Cache-Entity, DI-Registrierung oder Cache-Strategie.

## Offene Punkte

Keine fachlichen offenen Punkte. Die technische Implementierungsentscheidung zwischen String-Interpolation mit formatierbaren Werten und `string.Create`/expliziter Formatierung kann im Plan festgelegt werden; entscheidend ist, dass kein dynamischer Formatstring an `string.Format` übergeben wird.
