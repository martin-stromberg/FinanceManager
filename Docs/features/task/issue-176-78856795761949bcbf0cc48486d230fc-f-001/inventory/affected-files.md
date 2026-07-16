# Betroffene Dateien

## Direkt betroffen

`FinanceManager.Web/Controllers/AdminController.cs`

- Enthaelt alle betroffenen `api/admin/users`-Actions.
- Controller-Level-Attribut erzwingt aktuell nur JWT-Authentifizierung.
- User-Actions benoetigen Rollen- oder Policy-Autorisierung.
- IP-Block-Actions dienen als bestehender Vergleich fuer `403 Forbidden` bei Nicht-Admins.

`FinanceManager.Web/ProgramExtensions.cs`

- Relevant, falls eine zentrale Admin-Policy eingefuehrt wird.
- Aktueller Stand: `builder.Services.AddAuthorization();` ohne Policy-Konfiguration.
- Enthält zudem Role-Seeding/Synchronisierung fuer `Admin`.

## Autorisierungsgrundlage

`FinanceManager.Infrastructure/Auth/JwtTokenService.cs`

- Fuegt bei Admins `ClaimTypes.Role = "Admin"` in JWTs ein.
- Keine Aenderung erforderlich, solange Rollenautorisierung genutzt wird.

`FinanceManager.Web/Services/CurrentUserService.cs`

- `IsAdmin` basiert auf `User.IsInRole("Admin")`.
- Keine Aenderung erforderlich.

`FinanceManager.Infrastructure/Auth/UserAuthService.cs`

- Ermittelt Adminstatus beim Login/Registrieren ueber `UserManager.IsInRoleAsync`.
- Uebergibt Adminstatus an JWT-Erzeugung.
- Keine Aenderung erwartet.

`FinanceManager.Infrastructure/Auth/UserAdminService.cs`

- Fuehrt administrative User-Operationen aus.
- Enthaelt keine Autorisierung und sollte fachlich unveraendert bleiben.

`FinanceManager.Application/Users/IUserAdminService.cs`

- Service-Contract fuer Admin-User-Operationen.
- Keine Signaturaenderung erforderlich.

## Client und UI

`FinanceManager.Shared/ApiClient.Admin.cs`

- Enthält alle Admin-User-Clientmethoden.
- Keine Signaturaenderung erforderlich.
- Bei `403` wirft der Client ueber `EnsureSuccessOrSetErrorAsync` eine `HttpRequestException`.

`FinanceManager.Shared/IApiClient.cs`

- Enthält die Admin-User-Methodensignaturen.
- Keine Aenderung erforderlich.

`FinanceManager.Web/Components/Pages/Setup/SetupSecurityTab.razor`

- Blendet Setup-Security fuer Nicht-Admins aus.
- UI-Schutz ersetzt nicht die serverseitige Autorisierung; voraussichtlich keine Aenderung erforderlich.

## Tests

`FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs`

- Bestehender positiver Admin-Test.
- Geeigneter Ort fuer neue negative Nicht-Admin-Tests oder fuer eine neue Nachbarklasse.

`FinanceManager.Tests.Integration/ApiClient/ApiClientIpBlocksTests.cs`

- Positiver IP-Block-Regressionsbereich.
- Sollte nach der Aenderung weiterhin gruen bleiben.

`FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`

- Seeded Bootstrap-Admin und Admin-Rolle.
- Ermoeglicht normale Testregistrierungen als Nicht-Admins.
- Keine Aenderung erwartet.
