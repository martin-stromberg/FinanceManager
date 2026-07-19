# Auth, Rollen, Health/Meta und Tests

## Fundstellen

- `FinanceManager.Web/Services/CurrentUserService.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Controllers/AdminController.cs`
- `FinanceManager.Web/Controllers/MetaHolidayProvidersController.cs`
- `FinanceManager.Web/Controllers/UsersController.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackupsTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientBackgroundTasksTests.cs`
- `FinanceManager.Tests/ViewModels/SetupBackupsViewModelTests.cs`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Tests/Notifications/MonthlyReminderSchedulerTests.cs`
- `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`

## Auth und Rollen

JWT Bearer ist das zentrale API-Auth-Schema. Tokens koennen aus dem `FinanceManager.Auth`-Cookie gelesen werden. Beim Token-Validieren wird geprueft:

- UserId/Subject vorhanden und gueltig.
- SecurityStamp stimmt mit aktuellem Benutzer ueberein.
- Benutzer ist aktiv.
- Admin-Rolle im Token stimmt mit aktueller Identity-Rolle ueberein.

`CurrentUserService` stellt `UserId`, `PreferredLanguage`, `IsAuthenticated` und `IsAdmin` bereit. `IsAdmin` basiert auf `User.IsInRole("Admin")`.

`ProgramExtensions.ApplyMigrationsAndSeed` synchronisiert Domain-Feld `User.IsAdmin` mit Identity-Rolle `Admin`.

## Health und Meta

Ein dedizierter Health-Endpunkt existiert nicht. Vorhanden sind:

- `UsersController` mit anonym erreichbarem `GET api/users/exists`
- `MetaHolidayProvidersController` unter `api/meta`, aber authentifiziert

Fuer die Warteseite nach Update-Start braucht es einen anonym erreichbaren, sehr einfachen Health-Endpunkt, z. B. `GET /health` oder `GET /api/health`, der bei laufender Anwendung 200 liefert. Er sollte vor dem Blazor-Fallback erreichbar sein und keine DB-Abhaengigkeit benoetigen, wenn nur Prozessverfuegbarkeit relevant ist.

## Tests

Vorhandene Testebenen:

- `FinanceManager.Tests`: Unit-/ViewModel-/Component-Tests, inkl. bUnit.
- `FinanceManager.Tests.Integration`: API-Client-Tests mit `WebApplicationFactory`.
- `FinanceManager.Tests.E2E`: Playwright-Tests mit echtem Webprozess.
- Node-Tests fuer Release-Skripte unter `scripts/*.test.mjs`.

`TestWebApplicationFactory` deaktiviert Hosted Services per Konfiguration und entfernt sie aus DI. Es seeded einen Bootstrap-Admin, damit Testregistrierungen nicht automatisch Erstbenutzer-Admin werden. Fuer neue Update-Hosted-Services muss die Factory erweitert werden, sonst laufen periodische Updater in Integrationstests.

Bestehende relevante Testmuster:

- `ApiClientBackupsTests`: Upload/Download/Status/Cancel-Flows.
- `ApiClientBackgroundTasksTests`: Queue-/Status-/Cancel-Endpunkte.
- `SetupBackupsViewModelTests`: ViewModel-Interaktion gegen `IApiClient`.
- `MonthlyReminderSchedulerTests` und `SecurityPriceErrorRecoveryTests`: Hosted-Service-Logik isoliert testen.
- `resolve-release-version.test.mjs`: Release-Asset- und GitHub-Release-Logik ohne echten GitHub-Zugriff testen.

## Relevanz fuer Self-Update

Empfohlene Tests:

- Unit-Tests fuer `UpdateMetadata`-Parsing, Versionvergleich, Hash-Pruefung und Dateigroessenlimits.
- Unit-Tests fuer Lock-Datei und Statusuebergaenge.
- Unit-Tests fuer Windows- und Linux-Skripterzeugung mit stabilen erwarteten Fragmenten.
- Controller-Integrationstests fuer Admin-only Zugriff, Status, Einstellungen, Check, Schedule und Install-Konflikt.
- Hosted-Service-Tests fuer Check-Intervall und Scheduler-Entscheidungen.
- ApiClient-Tests fuer neue Update-Methoden.
- ViewModel/bUnit-Tests fuer Setup-Update-Tab.
- Release-Skript-Tests fuer `update.json`-Erzeugung, SHA-256 und plattformspezifische Assetnamen.

