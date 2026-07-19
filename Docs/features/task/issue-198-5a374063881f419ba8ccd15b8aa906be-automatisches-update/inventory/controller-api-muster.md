# Controller- und API-Muster

## Fundstellen

- `FinanceManager.Web/Controllers/AdminController.cs`
- `FinanceManager.Web/Controllers/BackupsController.cs`
- `FinanceManager.Web/Controllers/BackgroundTasksController.cs`
- `FinanceManager.Web/Controllers/UsersController.cs`
- `FinanceManager.Web/Controllers/MetaHolidayProvidersController.cs`
- `FinanceManager.Shared/IApiClient.cs`
- `FinanceManager.Shared/ApiClient.Admin.cs`

## Bestehendes Muster

Controller sind klassische ASP.NET-Core-MVC-Controller mit:

- `[ApiController]`
- `[Route("api/...")]` oder `[Route("api/[controller]")]`
- `[Produces(MediaTypeNames.Application.Json)]` bei JSON-Endpunkten
- `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` fuer geschuetzte APIs
- `[ProducesResponseType(...)]` fuer dokumentierte Statuscodes
- DTOs aus `FinanceManager.Shared.Dtos...`
- `ApiErrorDto`/`ApiErrorFactory` fuer lokalisierbare Fehler in mehreren Controllern

Der `AdminController` zeigt zwei Admin-Schutzvarianten:

- Methoden mit `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`
- Methoden mit explizitem `if (!_current.IsAdmin) return Forbid();`

Der `BackupsController` ist ein gutes Muster fuer Setup-nahe Betriebsendpunkte:

- Route `api/setup/backups`
- List/Create/Upload/Download/Delete
- Start einer laenger laufenden Operation ueber `StartApplyAsync`
- Status-Endpunkt `restore/status`
- Konfliktantwort bei aktivem Restore
- Validierungsfehler als `ApiErrorDto`

Der `BackgroundTasksController` zeigt generische Queue-Endpunkte:

- `POST api/background-tasks/{type}`
- `GET api/background-tasks/active`
- `GET api/background-tasks/{id}`
- `DELETE api/background-tasks/{id}`
- spezialisierter Aggregates-Rebuild-Endpunkt mit Status

Der Shared Client ist als partielle Klasse organisiert. Neue API-Methoden sollten in einer eigenen Datei ergaenzt werden, z. B. `ApiClient.Update.cs`, und das Interface `IApiClient` erweitern.

## Relevanz fuer Self-Update

Ein dedizierter Controller ist sinnvoll:

- Route: `api/setup/update` oder `api/admin/update`
- Klassenattribut: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`
- Endpunkte:
  - `GET status`
  - `POST check`
  - `GET settings`
  - `PUT settings`
  - `POST schedule`
  - `POST install/start`
  - optional `POST lock/reset` nur bei expliziter fachlicher Freigabe

Die Installationsausloesung sollte nicht das vorhandene User-Task-System missbrauchen, weil Updateinstallation prozessweit ist und nicht einem Benutzer-Queue-Konzept folgen sollte. Das Backup-Pattern fuer Start/Status/Konflikt ist jedoch direkt wiederverwendbar.

## Fehlende Bausteine

- Kein vorhandener Update-Controller.
- Kein vorhandener Health-Controller.
- Kein vorhandener App-Version-/Build-Meta-Endpunkt.

