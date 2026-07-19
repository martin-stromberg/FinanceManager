# BackgroundTask- und HostedService-Muster

## Fundstellen

- `FinanceManager.Application/BackgroundTaskManager.cs`
- `FinanceManager.Application/BackgroundTaskRunner.cs`
- `FinanceManager.Web/Services/BackupRestoreTaskExecutor.cs`
- `FinanceManager.Web/Services/RebuildAggregatesTaskExecutor.cs`
- `FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs`
- `FinanceManager.Web/Services/MonthlyReminderScheduler.cs`
- `FinanceManager.Web/Services/SecurityPriceWorker.cs`
- `FinanceManager.Web/Controllers/BackgroundTasksController.cs`

## BackgroundTask-System

Das vorhandene BackgroundTask-System besteht aus:

- `IBackgroundTaskManager` fuer Enqueue, Query, Cancel, Remove und Status-Update
- `BackgroundTaskManager` als in-memory Queue mit `ConcurrentQueue`, `ConcurrentDictionary` und `SemaphoreSlim`
- `IBackgroundTaskExecutor` mit `BackgroundTaskType Type` und `ExecuteAsync(...)`
- `BackgroundTaskRunner` als `BackgroundService`, der queued Tasks sequentiell verarbeitet

Die Tasks sind benutzerbezogen (`UserId`) und verhindern standardmaessig Duplikate pro User und Task-Typ. Progress wird ueber `BackgroundTaskContext.ReportProgress(...)` gemeldet.

## Periodische Hosted Services

`MonthlyReminderScheduler` und `SecurityPriceWorker` sind die besseren Muster fuer die geforderte automatische Updatepruefung:

- beide erben von `BackgroundService`
- beide laufen in einer Schleife bis `stoppingToken`
- scoped Services werden pro Lauf ueber `IServiceScopeFactory.CreateScope()` aufgeloest
- Fehler eines Laufs werden geloggt und beenden nicht den Host
- Wartezeiten werden mit `Task.Delay(..., stoppingToken)` umgesetzt

`SecurityPriceWorker` ist zudem per Konfiguration aktivierbar (`Workers:SecurityPriceWorker:Enabled`) und nutzt Options fuer Quoten.

## Relevanz fuer Self-Update

Fuer das Feature sollten zwei verschiedene Konzepte getrennt bleiben:

- Periodischer Check und Scheduler als eigene `BackgroundService`-Klassen.
- Installationsstart als prozessweiter Update-Service mit Lock, Status und externer Skriptausfuehrung.

Das vorhandene `IBackgroundTaskManager` eignet sich nicht als alleinige Quelle fuer Update-Status, weil:

- es in-memory ist und bei Neustart verloren geht
- es UserId-zentriert ist
- es Tasks im laufenden Prozess verwaltet, waehrend das Update den Prozess beendet
- es keine persistente Lock-Datei oder prozessuebergreifende Koordination bietet

Wiederverwendbar sind dagegen:

- Status-DTO-Pattern
- Conflict bei aktivem Vorgang
- Progress-/Fehlerstatus-Konzept
- Tests fuer Queue/Status-Endpunkte

