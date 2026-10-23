# Bestandsaufnahme: Anmeldetoken bleibt während aktiver Nutzung gültig

Analysiert wurden die vorhandenen Authentifizierungs-, Token-, API- und Quick-Edit-Pfade für die Sitzungserhaltung bei aktiver Nutzung. Die Bestandsaufnahme bezieht sich auf die Anforderung in [requirement.md](requirement.md) und die zugehörige Issue-Beschreibung im Repository-Root (`issue.md`).

## Zusammenfassung

- Login und Registrierung setzen das `FinanceManager.Auth`-Cookie; `Login.razor` meldet über JavaScript im Browser an und erzwingt danach einen vollständigen Seitenaufruf.
- `JwtCookieAuthTokenProvider`, `AuthenticatedHttpClientHandler`, `JwtRefreshMiddleware` und `JwtRefreshService` bilden bereits mehrere Tokenbereitstellungs- und Erneuerungspfade. Der Provider bevorzugt den Request-Cookie-Wert; ohne `HttpContext` greift er auf einen Instanzcache zurück.
- Die dokumentierte Standardlaufzeit ist `Jwt:LifetimeMinutes = 30`; Middleware und Provider verwenden jeweils ein Erneuerungsfenster aus mindestens fünf Minuten oder der halben konfigurierten Laufzeit.
- Die Erneuerung validiert den JWT und reicht dessen Principal mit `security_stamp` an `IJwtRefreshService` weiter. Die Middleware erneuert anhand des eingehenden, authentifizierten Requests.
- `MainLayout` löst Keepalive bei Navigation aus; `financeManager.js` bündelt Aktivitätsereignisse und Quick-Edit-Blur-Aufrufe. Quick-Edit-Eingaben sind in `StatementDraftEntriesListViewModel` gepuffert und die UI hält Fokus gezielt.
- Authentifizierungsfehler des `ApiClient` lösen `AuthenticationRequired` aus; `AuthRedirect` reagiert darauf mit einem Login-Redirect. Der Keepalive-Endpunkt selbst liefert bei Erfolg 204.
- Es gibt bereits Unit-, Integration- und Playwright-Abdeckung für Token-Refresh, Login/Logout, Redirects und Quick-Edit-Blur. Offensichtlich nicht abgedeckt sind der direkte Nachweis der allerersten geschützten Startseiten-API-Anfrage nach UI-Login, Aktivität über die ursprüngliche JWT-Ablaufzeit hinweg, Keepalive-Ausbleiben bei Inaktivität und die Verbindung von Quick-Edit-Eingabe und erhaltenem Fokus während einer Token-Erneuerung.

**Test-Ausgangszustand:** Unit 1.467 erfolgreich / 1 fehlgeschlagen / 0 übersprungen; Integration 137 erfolgreich / 0 fehlgeschlagen / 0 übersprungen; E2E 0 erfolgreich / 112 fehlgeschlagen / 0 übersprungen. Alle E2E-Fehler entstanden vor den Testaktionen beim TLS-Healthcheck der gemeinsamen Fixture. Ergebnisse und verbleibende Testlücken stehen in [tests.md](inventory/tests.md). Die Unit-Fehlerdiagnose sowie der E2E-Infrastrukturfehler sind dort mit Test-IDs, Meldungen und Report-Verweisen dokumentiert.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
