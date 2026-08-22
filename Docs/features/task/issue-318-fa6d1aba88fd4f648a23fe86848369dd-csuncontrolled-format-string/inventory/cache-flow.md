# Detail: Cache-Vertrag und Ausführungsfluss

## Vertrag

`FinanceManager.Application/Budget/IReportCacheService.cs` definiert die Cache-Operationen. Die Methoden nehmen `ownerUserId`, Datumsbereich und `BudgetReportDateBasis` entgegen. Es existiert keine öffentliche Methode zum Erzeugen eines Cache-Schlüssels.

## Verbraucher

- `BudgetReportsController.GetRawAsync` nimmt `BudgetReportRequest` aus dem MVC-Request entgegen und reicht `DateBasis` an den Report-Service weiter.
- Der Budget-Report-Service verwendet den Cache für rohe Reportdaten.
- `ReportCacheRefreshTaskExecutor` und die Infrastruktur-Refreshpfade nutzen die Cache-Schnittstelle für Aktualisierungen.

## Persistenz

`FinanceManager.Domain/Reports/ReportCacheEntry.cs` speichert `CacheKey`, `OwnerUserId`, JSON-Wert, Parameter und Refresh-Status. Die Schlüsseländerung darf keine Migration erfordern, solange das Format unverändert bleibt.

`BudgetReportCacheParameter` speichert den Datumsbereich und die Date-Basis zusätzlich serialisiert. Diese redundante Prüfung bleibt unverändert und dient als zweite Konsistenzprüfung beim Lesen beziehungsweise Refresh.

## Lebensdauer und Registrierung

`ServiceCollectionExtensions.AddInfrastructure` registriert `IReportCacheService` als Scoped. Die Änderung hat keinen Einfluss auf DI-Lebensdauer oder Datenbanktransaktionen.
