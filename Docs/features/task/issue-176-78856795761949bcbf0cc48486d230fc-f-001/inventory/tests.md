# Testbestand und Teststrategie

## Vorhandene Integrationstests

Dateien:

- `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientIpBlocksTests.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`

`ApiClientUsersAdminTests.Admin_CreateListUpdateDelete_User` prueft den positiven Admin-Flow:

- Login als Bootstrap-Admin
- User anlegen
- User listen
- User aktualisieren
- Passwort zuruecksetzen
- User entsperren
- User loeschen

Der Test deckt nicht `Admin_GetUserAsync` als separaten positiven Schritt ab, obwohl die Methode im Shared Client existiert.

`ApiClientIpBlocksTests.IpBlocks_List_Create_Block_Unblock_Delete` prueft einen positiven Admin-Flow fuer IP-Blocks. Negative Nicht-Admin-Tests fuer IP-Blocks sind nicht sichtbar, aber die Anforderung verlangt vor allem, dass das bestehende Verhalten nicht abgeschwaecht wird.

## Testinfrastruktur

`TestWebApplicationFactory` erstellt eine frische SQLite-In-Memory-Datenbank, migriert sie und seeded:

- `BootstrapAdminUsername = "bootstrap.admin"`
- `BootstrapAdminPassword = "Bootstr4pAdmin!"`
- Identity-Rolle `Admin`
- Bootstrap-Admin mit `isAdmin: true` und Rollenmitgliedschaft `Admin`

Der Kommentar in der Factory erklaert, dass der Bootstrap-Admin verhindert, dass erste Testregistrierungen automatisch Admin werden. Daher sind normale per `Auth_RegisterAsync` angelegte Testbenutzer gute Nicht-Admin-Subjects.

## Shared ApiClient Verhalten

Dateien:

- `FinanceManager.Shared/ApiClient.cs`
- `FinanceManager.Shared/ApiClient.Admin.cs`

Admin-Clientmethoden rufen `EnsureSuccessOrSetErrorAsync(resp)` auf. Bei `403 Forbidden` wird dadurch `LastError` gesetzt und danach `HttpResponseMessage.EnsureSuccessStatusCode()` ausgefuehrt. Fuer Statuscode-genaue Negativtests ist deshalb roher `HttpClient` einfacher als der `ApiClient`-Wrapper.

## Empfohlene Negativtests

Eine neue oder erweiterte Integrationstestklasse sollte:

1. Einen normalen Benutzer per `Auth_RegisterAsync` registrieren.
2. Den durch Cookie/JWT authentifizierten rohen `HttpClient` weiterverwenden.
3. Alle sieben `api/admin/users`-Endpunkte mit realistischen Payloads aufrufen.
4. Jeweils `HttpStatusCode.Forbidden` erwarten.

Betroffene Requests:

- `GET /api/admin/users`
- `GET /api/admin/users/{id}`
- `POST /api/admin/users`
- `PUT /api/admin/users/{id}`
- `POST /api/admin/users/{id}/reset-password`
- `POST /api/admin/users/{id}/unlock`
- `DELETE /api/admin/users/{id}`

Fuer Endpunkte mit `{id}` kann ein beliebiger syntaktisch gueltiger `Guid` genutzt werden. Bei korrekt vorgeschalteter Autorisierung muss vor fachlicher Existenzpruefung `403 Forbidden` kommen, nicht `404 NotFound`.

## 401-Regression

Die Controller-Level-Authentifizierung soll erhalten bleiben. Ein roher, nicht eingeloggter Client sollte fuer mindestens einen User-Endpunkt `401 Unauthorized` erhalten. Falls ein Attribut pro Action verwendet wird, muss das AuthenticationScheme erhalten bleiben, damit aus fehlender Authentifizierung weiterhin `401` und aus fehlender Rolle `403` wird.

## Service-Call-Schutz

Die bestehende Integrationstest-Infrastruktur testet echte Services. Ein direkter Nachweis "Service wurde nicht aufgerufen" waere dort nur mit Service-Ersatz/Spy moeglich und wuerde die Testklasse aufwendiger machen. Praktisch ist der Statuscode-Test mit nicht existierenden IDs ausreichend aussagekraeftig: Wenn ein Nicht-Admin fuer `GET /api/admin/users/{randomGuid}` `403` erhaelt, wurde nicht zuerst fachlich auf `404` ausgewertet.
