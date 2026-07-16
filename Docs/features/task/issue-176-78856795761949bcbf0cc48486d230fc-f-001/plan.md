# Umsetzungsplan: Admin-User-Endpunkte serverseitig absichern

## Uebersicht

Die sieben User-Management-Endpunkte unter `api/admin/users` werden serverseitig auf Administratoren beschraenkt. Die bestehende JWT-Authentifizierung auf `AdminController` bleibt erhalten; zusaetzlich erhalten nur die betroffenen User-Actions eine Rollenautorisierung fuer die vorhandene Rolle `Admin`. Dadurch erhalten authentifizierte Nicht-Admins vor Ausfuehrung der Action `403 Forbidden`, waehrend nicht authentifizierte Aufrufe weiterhin durch die Controller-weite JWT-Anforderung mit `401 Unauthorized` abgewiesen werden.

IP-Block-Endpunkte im selben Controller bleiben fachlich unveraendert. Ihre bestehenden `_current.IsAdmin`-Pruefungen werden nicht migriert, damit der Aenderungsumfang eng auf die gemeldete Sicherheitsluecke begrenzt bleibt.

---

## Designentscheidungen

| Bereich | Gewaehlter Ansatz | Begruendung |
|---------|-------------------|-------------|
| Admin-Autorisierung | Direktes Rollenattribut `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]` auf den sieben User-Actions | Minimal-invasive Aenderung ohne neue Policy-Konvention; nutzt die vorhandenen JWT-Rollenclaims und Identity-Rolle `Admin`. |
| Geltungsbereich | Nur `ListUsersAsync`, `GetUserAsync`, `CreateUserAsync`, `UpdateUserAsync`, `ResetPasswordAsync`, `UnlockUserAsync`, `DeleteUserAsync` | Erfuellt die Anforderung exakt und vermeidet unbeabsichtigte Verhaltensaenderungen an IP-Block-Actions. |
| Zentrale Policy | Keine neue `AdminOnly`-Policy in diesem Schritt | Im Projekt existiert noch keine Policy-Konvention. Fuer diese konkrete Luecke bringt eine Policy keinen funktionalen Zusatznutzen. |
| IP-Block-Endpunkte | Bestehende explizite `_current.IsAdmin`-Pruefungen bleiben bestehen | Reduziert Regressionrisiko und erhaelt das bekannte Vergleichsverhalten fuer `403 Forbidden`. |
| Negativtests | Roher `HttpClient` statt `ApiClient` fuer Nicht-Admin-Assertions | Der Shared `ApiClient` wirft bei `403` Exceptions; mit rohem `HttpClient` lassen sich Statuscodes direkt und eindeutig pruefen. |

---

## Programmablaeufe

### Nicht-Admin ruft User-Management-Endpunkt auf

1. Ein regulaerer Benutzer registriert sich oder meldet sich an und erhaelt ein gueltiges JWT ohne Rolle `Admin`.
2. Der Benutzer ruft einen Endpunkt unter `api/admin/users` auf.
3. ASP.NET-Core-Autorisierung prueft vor der Action-Ausfuehrung das Rollenattribut.
4. Da die Rolle `Admin` fehlt, wird `403 Forbidden` zurueckgegeben.
5. Die jeweilige `_userSvc`-Methode wird nicht aufgerufen; fachliche Fehler wie `404 NotFound` oder Validierungsfehler koennen den Nicht-Admin-Aufruf nicht ueberlagern.

Beteiligte Komponenten: `AdminController`, ASP.NET-Core-Authorization, JWT-Rollenclaim, `IUserAdminService`

---

### Admin ruft User-Management-Endpunkt auf

1. Der Bootstrap-Admin oder ein anderer Admin meldet sich an.
2. Das JWT enthaelt den Standard-Rollenclaim `ClaimTypes.Role = "Admin"`.
3. Das Rollenattribut auf der User-Action wird erfuellt.
4. Die Action fuehrt die bestehende Logik unveraendert aus und delegiert an `IUserAdminService`.
5. Bestehende Erfolgs- und Fehlerstatuscodes fuer autorisierte Admins bleiben erhalten.

Beteiligte Komponenten: `JwtTokenService`, `AdminController`, `UserAdminService`

---

### Nicht authentifizierter Aufruf

1. Ein Client ohne Login ruft einen Endpunkt unter `api/admin/users` auf.
2. Die Controller-weite JWT-Authentifizierungsanforderung greift weiterhin.
3. Der Aufruf wird mit `401 Unauthorized` abgewiesen.

Beteiligte Komponenten: `AdminController`, JWT-Bearer-Authentifizierung

---

## Aenderungen an bestehenden Klassen

### `FinanceManager.Web/Controllers/AdminController.cs`

- Auf folgenden Actions wird ein Rollenattribut ergaenzt:
  - `ListUsersAsync`
  - `GetUserAsync`
  - `CreateUserAsync`
  - `UpdateUserAsync`
  - `ResetPasswordAsync`
  - `UnlockUserAsync`
  - `DeleteUserAsync`
- Das Attribut soll den vorhandenen Authentication-Scheme explizit beibehalten:

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
```

- Das Controller-Level-Attribut bleibt bestehen:

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
```

- Die IP-Block-Actions und ihre `_current.IsAdmin`-Pruefungen bleiben unveraendert.
- `IUserAdminService`, `CurrentUserService`, `JwtTokenService`, `UserAuthService` und `ProgramExtensions` benoetigen keine fachliche Aenderung.

---

## Neue Klassen

Keine neuen Produktivklassen erforderlich.

Eine neue Integrationstestklasse ist optional sinnvoll, falls die bestehenden Admin-User-Tests uebersichtlich bleiben sollen:

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ApiClientUsersAdminAuthorizationTests` | Integrationstestklasse | Statuscode-genaue Negativtests fuer Nicht-Admins und 401-Regression fuer Admin-User-Endpunkte. |

Alternativ koennen die Tests in `ApiClientUsersAdminTests` ergaenzt werden.

---

## Konfigurationsaenderungen

Keine.

`ProgramExtensions.RegisterAppServices` muss nicht angepasst werden, weil keine neue Authorization-Policy eingefuehrt wird.

---

## Umsetzungsreihenfolge

1. **User-Actions mit Admin-Rolle absichern**
   - Datei: `FinanceManager.Web/Controllers/AdminController.cs`
   - Voraussetzung: Keine
   - Beschreibung: Auf alle sieben `api/admin/users`-Actions das Attribut `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]` setzen. Controller-weite Authentifizierung unveraendert lassen.

2. **Positiven Admin-Flow um Einzelabruf ergaenzen**
   - Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs`
   - Voraussetzung: Schritt 1
   - Beschreibung: Im bestehenden Test `Admin_CreateListUpdateDelete_User` nach dem Anlegen zusaetzlich `Admin_GetUserAsync(created.Id)` pruefen. Damit ist auch der positive Pfad fuer `GET /api/admin/users/{id}` abgedeckt.

3. **Nicht-Admin-Testclient vorbereiten**
   - Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs` oder neue Nachbarklasse
   - Voraussetzung: Keine
   - Beschreibung: Einen rohen `HttpClient` ueber `_factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })` erzeugen, mit `new FinanceManager.Shared.ApiClient(http)` einen regulaeren Benutzer per `Auth_RegisterAsync(new RegisterRequest(...))` registrieren und danach denselben `HttpClient` fuer rohe Admin-Requests weiterverwenden.

4. **403-Tests fuer alle User-Endpunkte ergaenzen**
   - Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs` oder neue Nachbarklasse
   - Voraussetzung: Schritt 3
   - Beschreibung: Mit dem authentifizierten Nicht-Admin folgende Requests ausfuehren und jeweils `HttpStatusCode.Forbidden` erwarten:
     - `GET /api/admin/users`
     - `GET /api/admin/users/{randomGuid}`
     - `POST /api/admin/users`
     - `PUT /api/admin/users/{randomGuid}`
     - `POST /api/admin/users/{randomGuid}/reset-password`
     - `POST /api/admin/users/{randomGuid}/unlock`
     - `DELETE /api/admin/users/{randomGuid}`
   - Fuer Requests mit Body vorhandene DTOs nutzen: `CreateUserRequest`, `UpdateUserRequest`, `ResetPasswordRequest`.
   - Fuer `{randomGuid}` bewusst eine nicht existierende Guid verwenden. Erwartet wird trotzdem `403`, nicht `404`; das belegt, dass Autorisierung vor Service-/Existenzpruefung greift.

5. **401-Regression fuer anonymen Zugriff ergaenzen**
   - Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs` oder neue Nachbarklasse
   - Voraussetzung: Schritt 1
   - Beschreibung: Einen nicht eingeloggten rohen Client verwenden und mindestens `GET /api/admin/users` gegen `HttpStatusCode.Unauthorized` pruefen. Optional kann ein zweiter Endpunkt mit Body geprueft werden, ist aber fuer die Regression nicht zwingend.

6. **Bestehende IP-Block-Regression mitlaufen lassen**
   - Datei: keine Aenderung geplant
   - Voraussetzung: Schritt 1
   - Beschreibung: `ApiClientIpBlocksTests.IpBlocks_List_Create_Block_Unblock_Delete` unveraendert ausfuehren. Da die IP-Block-Actions nicht angepasst werden, sollte dieser Test gruen bleiben.

---

## Tests

### Neue oder erweiterte Tests

| Test | Testklasse | Erwartung |
|------|------------|-----------|
| `Admin_CreateListUpdateDelete_User` erweitert um `Admin_GetUserAsync` | `ApiClientUsersAdminTests` | Admin kann einen angelegten User einzeln abrufen; vorhandener positiver Flow bleibt gruen. |
| `NonAdmin_UserAdminEndpoints_ReturnForbidden` | `ApiClientUsersAdminTests` oder `ApiClientUsersAdminAuthorizationTests` | Authentifizierter Nicht-Admin erhaelt fuer alle sieben `api/admin/users`-Endpunkte `403 Forbidden`. |
| `Anonymous_UserAdminEndpoint_ReturnsUnauthorized` | `ApiClientUsersAdminTests` oder `ApiClientUsersAdminAuthorizationTests` | Nicht eingeloggter Client erhaelt fuer `GET /api/admin/users` `401 Unauthorized`. |

### Betroffene bestehende Tests

| Test / Testklasse | Erwartung |
|-------------------|-----------|
| `ApiClientUsersAdminTests.Admin_CreateListUpdateDelete_User` | Bleibt gruen; optional um Einzelabruf erweitert. |
| `ApiClientIpBlocksTests.IpBlocks_List_Create_Block_Unblock_Delete` | Bleibt gruen; bestaetigt, dass IP-Block-Verhalten nicht versehentlich gebrochen wurde. |

### Auszufuehrende Testbefehle

```powershell
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter "FullyQualifiedName~ApiClientUsersAdmin"
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter "FullyQualifiedName~ApiClientIpBlocks"
```

Falls Projekt- oder Loesungsstruktur im Arbeitsstand abweicht, den entsprechenden Integrationstest-Projektpfad aus `rg --files -g *.csproj` verwenden.

---

## Seiteneffekte und Risiken

- **Attribut-Dopplung:** Das Rollenattribut wird siebenmal wiederholt. Das ist bewusst akzeptiert, weil es keine neue Policy-Konvention einfuehrt und den Aenderungsumfang klein haelt.
- **AuthenticationScheme nicht verlieren:** Das Action-Attribut muss `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` enthalten. Ohne Scheme besteht das Risiko abweichender Challenge-/Forbid-Behandlung, falls weitere Authentifizierungsschemata konfiguriert sind.
- **Statuscode-Tests mit `ApiClient`:** Negative Tests duerfen nicht ueber `ApiClient.Admin_*` assertieren, weil dieser bei `403` Exceptions wirft. Fuer `403`/`401` rohen `HttpClient` verwenden.
- **Falsche Nicht-Admin-Testdaten:** Die Testfactory seeded einen Bootstrap-Admin. Danach registrierte Benutzer sind regulaere Nicht-Admins und geeignet fuer die Negativtests.
- **Controller-weite Rollenautorisierung vermeiden:** Ein Rollenattribut auf dem gesamten `AdminController` wuerde auch IP-Block-Endpunkte beeinflussen und ist fuer diese Anforderung nicht noetig.

---

## Akzeptanzkriterien-Abdeckung

| Akzeptanzkriterium | Abdeckung im Plan |
|--------------------|-------------------|
| Alle `api/admin/users`-Endpunkte sind nur fuer Administratoren erreichbar. | Rollenattribut auf allen sieben User-Actions. |
| Authentifizierte Nicht-Admins erhalten `403 Forbidden`. | `NonAdmin_UserAdminEndpoints_ReturnForbidden` prueft alle sieben Endpunkte. |
| Admin-Benutzer koennen User-Management weiter nutzen. | Bestehender positiver Admin-Flow bleibt bestehen und wird um Einzelabruf ergaenzt. |
| Nicht authentifizierte Benutzer erhalten weiterhin `401 Unauthorized`. | `Anonymous_UserAdminEndpoint_ReturnsUnauthorized`. |
| Negative Integrationstests decken alle betroffenen Endpunkte ab. | Ein parametrisierter oder gebuendelter Nicht-Admin-Test prueft alle sieben Routen. |
| Bestehende Admin-User- und IP-Block-Tests bleiben erfolgreich. | Gezielte Ausfuehrung der Admin-User- und IP-Block-Integrationstests. |

---

## Offene Punkte

Keine. Die offenen Fragen aus Requirement und Inventory werden im Plan wie folgt entschieden:

| Frage | Entscheidung |
|-------|--------------|
| Direkte Rollenattribute oder zentrale Admin-Policy? | Direkte Rollenattribute auf den betroffenen Actions. |
| Gesamter `AdminController` oder nur User-Management-Actions? | Nur die sieben User-Management-Actions. |
| IP-Block-Endpunkte im selben Zug migrieren? | Nein, bestehende Pruefungen bleiben unveraendert. |
