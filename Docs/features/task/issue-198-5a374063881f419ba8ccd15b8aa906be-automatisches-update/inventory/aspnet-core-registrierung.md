# ASP.NET-Core-Registrierung

## Fundstellen

- `FinanceManager.Web/Program.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/appsettings.Production.json`

## Bestehendes Muster

`ProgramExtensions.RegisterAppServices` ist die zentrale Stelle fuer Web-Registrierungen. Dort werden Localization, Razor Components, Controllers, Infrastructure, `ICurrentUserService`, Options, DataProtection, BackgroundTask-Komponenten, HTTP-Clients, Hosted Services, Auth und Authorization registriert.

Wichtige bestehende Registrierungen:

- `builder.Services.AddRazorComponents().AddInteractiveServerComponents()`
- `builder.Services.AddControllers()`
- `builder.Services.AddInfrastructure(...)`
- `builder.Services.AddHttpContextAccessor()`
- `builder.Services.AddScoped<ICurrentUserService, CurrentUserService>()`
- `builder.Services.Configure<AttachmentUploadOptions>(...)`
- `builder.Services.Configure<BackupSecurityOptions>(...)`
- `builder.Services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>()`
- mehrere `IBackgroundTaskExecutor` als Singleton
- `builder.Services.AddHostedService<BackgroundTaskRunner>()` konditional ueber `BackgroundTasks:Enabled`
- `builder.Services.AddHostedService<MonthlyReminderScheduler>()`
- `builder.Services.AddHttpClient("Api", ...)`
- `builder.Services.AddHttpClient("AlphaVantage", ...)`
- `builder.Services.AddHostedService<SecurityPriceWorker>()` konditional ueber `Workers:SecurityPriceWorker:Enabled`
- JWT Bearer Auth, Identity, Antiforgery und `AddAuthorization`

`ProgramExtensions.ConfigureMiddleware` nutzt:

- `RequestLoggingMiddleware`
- `IpBlockMiddleware`
- HTTPS-Redirection in Development nur wenn nicht per E2E-Konfiguration deaktiviert
- `UseStaticFiles`, `UseAntiforgery`, `UseAuthentication`, `UseAuthorization`
- `JwtRefreshMiddleware`
- `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- `MapControllers()`

## Relevanz fuer Self-Update

Neue Update-Services sollten hier registriert werden, nicht verstreut in `Program.cs`. Fuer periodische Pruefung und Scheduler passt das vorhandene Muster:

- `services.Configure<UpdateOptions>(configuration.GetSection("Updates"))`
- `services.AddHttpClient("GitHubReleases", ...)` mit User-Agent und Timeout
- Scoped/Singleton-Registrierung fuer Update-Status, Dateiverwaltung, Scriptgenerator und Executor je nach Thread-Safety
- `AddHostedService<UpdateChecker>()` und `AddHostedService<UpdateScheduler>()` konditional ueber Konfigurationsflags

## Einschraenkungen

`appsettings` wird im Bestand nur gelesen, nicht zur Laufzeit geschrieben. Das spricht gegen eine reine `appsettings`-Persistenz fuer Admin-aenderbare Updateeinstellungen. Fuer globale Updateeinstellungen ist eine DB-Entitaet oder eine eigene persistierte JSON-Datei mit gesichertem Schreibpfad noetig.

