# Bestandsaufnahme - Hochrechnungen bei Dividendenreports

## Kontext

Die Anforderung erweitert das Report-Dashboard um eine Option "Hochrechnung" fuer reine Wertpapierberichte. Das Flag muss durch UI, Shared-DTOs/API-Client, Controller, Favoritenpersistenz, Aggregation und Tests gefuehrt werden. Der fachlich relevante Berechnungspfad liegt im vorhandenen Spezialpfad fuer Netto-Dividenden (`QuerySecurityDividendsNetAsync`).

## Detaildokumente

- [Report-Dashboard und UI-State](inventory/report-dashboard-ui.md)
- [ReportFavorite-Persistenz und Service-Mapping](inventory/report-favorites.md)
- [Shared-DTOs und API-Client](inventory/shared-api-client.md)
- [ReportsController](inventory/reports-controller.md)
- [ReportAggregationService und Security-Dividendenpfad](inventory/report-aggregation-service.md)
- [Blazor Resources](inventory/blazor-resources.md)
- [EF-Migrationen und DbContext](inventory/ef-migrations.md)
- [Bestehende Unit-, Integration- und E2E-Tests](inventory/tests.md)

## Wichtigste Befunde

- `ReportAggregatePointDto` enthaelt aktuell nur `Amount`, `PreviousAmount` und `YearAgoAmount`; fuer die neue Spalte wird ein weiteres optionales Feld benoetigt.
- `ReportAggregationResult` meldet nur `ComparedPrevious` und `ComparedYear`; eine serverseitige Ergebniskennung fuer die Projektion existiert noch nicht.
- `ReportDashboard.razor` rendert Vergleichsspalten vor `Amount`; die Anforderung verlangt "Hochrechnung" direkt nach `Amount`, also muss die Tabellenreihenfolge bewusst angepasst werden.
- Die UI verteilt Report-State zwischen Razor-Komponente und `ReportDashboardViewModel`. Ein neues Flag muss in beiden Stellen konsistent synchronisiert, in Favoriten uebernommen und beim Wechsel der Buchungsarten deaktiviert werden.
- `ReportFavorite` persistiert Flags wie `ComparePrevious`, `CompareYear` und `UseValutaDate` direkt auf der Entity. Das neue Flag passt in diese Struktur und muss zusaetzlich in Backup-DTO/Restore aufgenommen werden.
- `ReportFavoriteService` prueft Duplikate nur ueber den Favoritennamen pro Benutzer. Eine kriteriumsbasierte Duplikatspruefung existiert nicht.
- `ReportsController.QueryAsync` validiert aktuell nur `Take` und mappt Request-Felder ohne fachliche Sondervalidierung. Die Security-only-Regel fuer Hochrechnung muss hier oder im Service zentral abgesichert werden.
- `ReportAggregationService.QueryAsync` nutzt `QuerySecurityDividendsNetAsync` bereits fuer reine Security-Dividendenreports mit `IncludeDividendRelated` oder YTD+Dividend-Subtype. Das ist der geeignetste Einstiegspunkt fuer die Projektion.
- `QuerySecurityDividendsNetAsync` nutzt aktuell immer `BookingDate` fuer Periodenfilter und Dividendenanker. `UseValutaDate` wird im Spezialpfad nicht beruecksichtigt.
- Es gibt Migrationen in zwei Verzeichnissen (`FinanceManager.Infrastructure/Migrations` und `FinanceManager.Infrastructure/Data/Migrations/Identity`). Die aktuelle `AppDbContextModelSnapshot` liegt im nicht-Identity-Pfad und enthaelt `ReportFavorites`.

## Naechste Planungsschwerpunkte

- Namen und Semantik des neuen Flags festlegen, z. B. `CompareProjection`, analog zur Anforderung und zu bestehenden Vergleichsflags.
- Fachliche Aequivalenz von Dividenden konkretisieren; vorhandene Daten bieten `SecurityId`, `GroupId`, `BookingDate`, `ValutaDate`, `SecuritySubType` und Textfelder.
- Entscheiden, ob die Hochrechnung nur im Netto-Dividendenpfad aktiv ist oder ob sie bei Security-Dividenden ohne `IncludeDividendRelated` ebenfalls implizit den Spezialpfad erzwingen soll.
- `UseValutaDate` im Spezialpfad pruefen, da die bestehende normale Aggregation Valuta-Aggregate verwenden kann, der Dividendenspezialpfad aber direkt auf `Postings.BookingDate` filtert.
