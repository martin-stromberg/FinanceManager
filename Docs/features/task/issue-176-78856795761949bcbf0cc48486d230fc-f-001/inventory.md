# Bestandsaufnahme - Admin-User-Endpunkte absichern

Quelle: `Docs/features/task/issue-176-78856795761949bcbf0cc48486d230fc-f-001/requirement.md`

## Ergebnis

Die Anforderung betrifft eine klar abgegrenzte Sicherheitsluecke im Web-API-Layer. `FinanceManager.Web/Controllers/AdminController.cs` ist auf Controller-Ebene nur per JWT authentifiziert. Die IP-Block-Endpunkte pruefen danach explizit `_current.IsAdmin`, die User-Management-Endpunkte unter `api/admin/users` rufen dagegen direkt `IUserAdminService` auf. Dadurch reicht fuer diese sieben User-Endpunkte ein gueltiges Login ohne Admin-Rolle.

Die vorhandene Rollenbasis ist bereits passend: Admin-Tokens enthalten bei Admins den Standard-Claim `ClaimTypes.Role = "Admin"`, `CurrentUserService.IsAdmin` nutzt `User.IsInRole("Admin")`, und Testdaten weisen dem Bootstrap-Admin die Identity-Rolle `Admin` zu. Es fehlt keine neue Domain-Berechtigung, sondern die serverseitige Autorisierung an den betroffenen Actions bzw. eine zentrale Policy.

## Detaildokumente

- [API- und Autorisierungsbestand](inventory/api-authorization.md)
- [Testbestand und Teststrategie](inventory/tests.md)
- [Umsetzungsoptionen und Risiken](inventory/implementation-options.md)
- [Betroffene Dateien](inventory/affected-files.md)

## Betroffene Endpunkte

Alle folgenden Actions liegen in `AdminController` unter `api/admin/users` und benoetigen Admin-Autorisierung:

- `GET /api/admin/users` - `ListUsersAsync`
- `GET /api/admin/users/{id}` - `GetUserAsync`
- `POST /api/admin/users` - `CreateUserAsync`
- `PUT /api/admin/users/{id}` - `UpdateUserAsync`
- `POST /api/admin/users/{id}/reset-password` - `ResetPasswordAsync`
- `POST /api/admin/users/{id}/unlock` - `UnlockUserAsync`
- `DELETE /api/admin/users/{id}` - `DeleteUserAsync`

## Vorhandene Konventionen

- API-Controller verwenden haeufig `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` fuer authentifizierte Benutzer.
- Eine benannte Admin-Policy ist derzeit nicht registriert; `ProgramExtensions.RegisterAppServices` ruft nur `AddAuthorization()` ohne Policies auf.
- Die bestehende Admin-Pruefung fuer IP-Block-Endpunkte ist explizit pro Action ueber `_current.IsAdmin`.
- JWTs enthalten fuer Admins bereits `ClaimTypes.Role` mit Wert `Admin`.
- `CurrentUserService.IsAdmin` wertet denselben Rollenmechanismus ueber `User.IsInRole("Admin")` aus.

## Naheliegende Umsetzung

Die risikoaermste Umsetzung ist ein Rollen- oder Policy-Attribut nur auf den sieben User-Management-Actions. Das erfuellt die Anforderung minimal-invasiv und laesst das bestehende IP-Block-Verhalten unveraendert.

Eine zentrale Policy ist fachlich sauberer, erfordert aber eine Registrierung in `ProgramExtensions` und anschliessende Verwendung per `[Authorize(Policy = "...")]`. Ein direktes `[Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` auf den betroffenen Actions nutzt den bereits vorhandenen Rollenclaim ohne neue Konfiguration.

## Testbedarf

Die Integrationstests enthalten bereits einen positiven Admin-Flow fuer User-Management und IP-Blocks. Es fehlen negative Integrationstests fuer authentifizierte Nicht-Admins gegen alle sieben User-Endpunkte sowie optional ein 401-Regressionstest fuer nicht authentifizierte Aufrufe.

Fuer exakte Statuscode-Pruefungen sollten die Negativtests bevorzugt den rohen `HttpClient` aus `TestWebApplicationFactory.CreateClient(...)` verwenden. Die `ApiClient`-Wrapper werfen bei `403 Forbidden` ueber `EnsureSuccessOrSetErrorAsync` eine `HttpRequestException`, transportieren aber nicht komfortabel den erwarteten Statuscode als Assertion-Ziel.

## Offene Punkte fuer die Planung

- Soll fuer Konsistenz eine zentrale Admin-Policy eingefuehrt werden, obwohl im Projekt derzeit keine Policy-Konvention existiert?
- Soll die Admin-Autorisierung nur die sieben User-Actions betreffen oder der gesamte `AdminController` vereinheitlicht werden?
- Sollen IP-Block-Endpunkte im selben Zug von expliziter `_current.IsAdmin`-Pruefung auf Attribut-/Policy-Autorisierung migriert werden oder aus Regressionserwaegungen unveraendert bleiben?
