# Vorhandene Auth-Tests

## Unit-Tests

### UserAuthServiceTests

Datei: `FinanceManager.Tests/Auth/UserAuthServiceTests.cs`

Vorhandene Abdeckung:

- erster registrierter Benutzer wird Admin,
- doppelte Registrierung wird abgelehnt,
- PreferredLanguage wird bei Registrierung gesetzt,
- fehlende Registrierungsdaten werden abgelehnt,
- Login-Fehlversuche und Identity-Lockout,
- erfolgreicher Login gibt Token zurueck.

Nicht gefunden:

- Login eines deaktivierten Benutzers wird abgelehnt,
- `Active` wird vor Tokenausgabe explizit geprueft,
- SecurityStamp/TokenVersion-Claim wird in Token aufgenommen.

### UserAdminServiceTests

Datei: `FinanceManager.Tests/Auth/UserAdminServiceTests.cs`

Vorhandene Abdeckung:

- Benutzeranlage,
- Duplicate-Username-Konflikt,
- Umbenennen,
- Passwortreset,
- Unlock,
- Delete,
- Rollen-Mock fuer Add/Remove/IsInRole.

Nicht gefunden:

- Deaktivierung invalidiert Sessions/Tokens,
- Rollenentzug invalidiert Sessions/Tokens oder aktualisiert SecurityStamp/TokenVersion,
- SecurityStamp-Aenderung bei Admin-Aenderungen.

## Integrationstests

### ApiClientAuthTests

Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`

Vorhandene Abdeckung:

- Register setzt Auth-Cookie/Response,
- Login erfolgreich und invalides Passwort unauthorized,
- Logout,
- Bearer-Token mit falschem Issuer wird abgelehnt,
- Bearer-Token mit falscher Audience wird abgelehnt,
- Bearer-Token mit korrektem Issuer/Audience wird akzeptiert.

Nicht gefunden:

- abgelaufenes oder fast abgelaufenes Token triggert Refresh,
- Refresh fuer deaktivierten Benutzer wird abgelehnt,
- Refresh nach Rollenentzug erzeugt kein Admin-Token,
- SecurityStamp/TokenVersion-Mismatch wird abgelehnt.

### ApiClientUsersAdminTests

Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs`

Vorhandene Abdeckung:

- Admin kann Benutzer erstellen, listen, aktualisieren, Passwort zuruecksetzen, entsperren und loeschen,
- Nicht-Admin erhaelt Forbidden fuer Admin-Endpunkte,
- anonyme Admin-Anfrage erhaelt Unauthorized.

Nicht gefunden:

- Deaktivierter Benutzer kann sich nicht einloggen,
- Benutzer verliert Admin-Rolle und kommt mit altem Token nicht weiter,
- Rollenwechsel wirkt auf erneuerte Tokens.

## Testinfrastruktur

Datei: `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`

Die Factory nutzt SQLite InMemory, setzt Environment `Development`, deaktiviert Background Worker und seeded einen Bootstrap-Admin. Sie setzt aktuell keine JWT-Lifetime-Overrides; damit kann fuer Integrationstests die Development-Konfiguration mit `43200` Minuten relevant sein, sofern nicht anderweitig ueberschrieben.

`FixedUtcNow` erlaubt deterministische Zeit in Tests, wird aber von `JwtTokenService` und Refresh-Code aktuell nicht durchgaengig genutzt, weil diese `DateTime.UtcNow`/`DateTimeOffset.UtcNow` direkt verwenden.
