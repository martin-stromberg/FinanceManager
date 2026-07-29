# SoftwareSchmiede.AutoUpdate

A hosting-independent, DI-compatible self-update library for .NET applications: configurable update sources,
background checking and scheduled installation, thread-safe status tracking and cancellable lifecycle events.

## Installation

Add a project reference (or, once published, the NuGet package) to your host project and reference the
`SoftwareSchmiede.AutoUpdate` namespace.

## Getting started

Call `UseAutoUpdate` once on your `IHostApplicationBuilder` — this works with `WebApplicationBuilder`,
`HostApplicationBuilder` (worker services) and plain console hosts alike, since `WebApplicationBuilder`
implements `IHostApplicationBuilder`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.UseAutoUpdate(cfg =>
{
    cfg.EnableAutomaticDownload("./updates")
       .UseGithubSource("MyRepository", "my-org")
       .WithSourceCheck(interval: 360)
       .EnableAutomaticInstallation();
});

var app = builder.Build();
```

If no source is configured, the library falls back to `AutoUpdateLocalFolderSource` pointed at a `source`
subdirectory of the configured download path.

## Configuration

By default, `AutoUpdateOptions` binds from the `AutoUpdate` configuration section. Use
`AutoUpdateBuilder.BindConfiguration("SectionName")` to bind from a different section:

```json
{
  "AutoUpdate": {
    "Enabled": true,
    "EnableAutomaticDownload": true,
    "DownloadPath": "updates",
    "EnableAutomaticInstallation": false,
    "MaxAssetBytes": 536870912,
    "HostedServicesEnabled": true,
    "StopHostAfterScriptStart": false,
    "HealthTimeoutSeconds": 120,
    "SourceCheck": {
      "Interval": 360,
      "TimeRanges": [
        { "DayOfWeek": "Monday", "StartTime": "22:00:00", "EndTime": "06:00:00" }
      ]
    }
  }
}
```

`Source` cannot be bound from configuration (it requires code) — configure it via `UseGithubSource`,
`UseLocalFolderSource` or `UseSource` in the `UseAutoUpdate` delegate.

## Update sources

- **`AutoUpdateGithubSource`** — reads a release manifest (`update.json`) from the latest GitHub release of a
  repository and downloads its assets. Requires network access to `github.com`.
- **`AutoUpdateLocalFolderSource`** — reads the same `update.json` manifest schema from a local directory.
  Deterministic and offline-capable; used as the default source and well suited for tests.
- Implement `IAutoUpdateSource` for a custom source. Implementations must be stateless and thread-safe, since a
  single instance is shared as part of the singleton `AutoUpdateOptions`.

## Events

Subscribe to `IAutoUpdateEventAggregator` (registered as a singleton) to observe or veto steps of the update
workflow:

```csharp
var events = app.Services.GetRequiredService<IAutoUpdateEventAggregator>();
events.BeforeInstall += (_, args) =>
{
    if (!IsMaintenanceWindow())
    {
        args.Cancel = true;
    }
};
events.ErrorOccured += (_, args) => logger.LogError(args.Error, "Auto-update failed during {Phase}", args.Phase);
```

`BeforeCheckSource`, `BeforeDownload`, `BeforeInstall` and `BeforeStartUpdateScript` are cancellable.
`AfterStartUpdateScript` and `ErrorOccured` are informational. A failing subscriber is reported via
`ErrorOccured` and does not abort the operation or destabilize the library.

## Status and manual control

- `IAutoUpdateStatusProvider.GetSnapshot()` returns a consistent, immutable `AutoUpdateStatusSnapshot` at any
  time.
- `IAutoUpdateCommandHandler` (`AutoUpdateCommandService`) exposes `CheckAsync`, `DownloadAsync` and
  `InstallAsync(confirmDowntime)` for manual, UI-driven control. It is a thin facade with no update logic of
  its own; all operations are serialized through `IAutoUpdateOrchestrator`.
- `IAutoUpdateOrchestrator` coordinates the full workflow (`RunUpdateAsync`) as well as the individual steps,
  and reconciles the status after a restart triggered by the installation script.

## Background services

When `AutoUpdateOptions.HostedServicesEnabled` is `true` (the default), two hosted services are registered:

- **`AutoUpdateCheckerService`** — periodically calls `CheckForUpdateAsync`, honoring
  `SourceCheck.Interval` and `SourceCheck.TimeRanges`. Never downloads or installs.
- **`AutoUpdateSchedulerService`** — triggers installation once per day at `AutoUpdateOptions.ScheduledInstallTime`
  when a package is `ReadyToInstall`.

Use `AutoUpdateBuilder.DisableHostedServices()` to opt out, e.g. in tests.

## Supported platforms

Installation is supported on **Windows** (Windows Service or executable restart) and **Linux** (systemd unit).
**macOS is not supported**; `AutoUpdateScriptGenerator` throws `InvalidOperationException` on unsupported
platforms.

## Error handling

All update operations return a structured `AutoUpdateResult` containing:
- `Outcome` – Success, NoUpdate, Skipped, Canceled, or Failed
- `State` – Current state after the operation (Idle, Checking, Downloading, ReadyToInstall, Installing, etc.)
- `Message` – Human-readable summary of the result
- `Error` – Exception details if `Outcome` is `Failed`

The library never throws exceptions for update failures; instead, errors are reported via the `ErrorOccured` event
and persisted in the status snapshot.

## Testing

Use a local folder source (`UseLocalFolderSource`) together with `AutoUpdateBuilder.DisableHostedServices()`
in test environments to avoid network calls to GitHub and unwanted background activity. The test project
`SoftwareSchmiede.AutoUpdate.Tests` provides comprehensive xUnit test coverage for all major components.

## Project structure

- **`SoftwareSchmiede.AutoUpdate/`** – Core library (net10.0, NuGet-ready)
  - `AutoUpdateHostBuilderExtensions` – Single entry point via `UseAutoUpdate()`
  - `AutoUpdateBuilder` – Fluent configuration API
  - `AutoUpdateOptions` / `SourceCheckOptions` – Configuration models
  - `IAutoUpdateSource` implementations – GitHub and local folder sources
  - `AutoUpdateOrchestrator` / `AutoUpdateStatusService` / `AutoUpdateCommandService` – Core logic
  - `AutoUpdateCheckerService` / `AutoUpdateSchedulerService` – Background services
  - Events, DTOs and state models
- **`SoftwareSchmiede.AutoUpdate.Tests/`** – Unit test suite (xUnit v3)

## Status and roadmap

- **Current version:** 0.1.0 (development/pre-release)
- **Platform support:** Windows (Service/Executable), Linux (systemd); macOS not supported
- **Future:** NuGet package publication once API stabilizes

## License

MIT — see [LICENSE](../LICENSE).

## Questions or contributions?

This library is part of the [FinanceManager](https://github.com/martin-stromberg/FinanceManager) project.
Issues, questions and contributions are welcome via the main repository.
