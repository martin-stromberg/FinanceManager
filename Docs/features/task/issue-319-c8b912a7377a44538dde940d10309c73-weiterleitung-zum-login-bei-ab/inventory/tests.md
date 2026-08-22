# Tests und Abdeckung

## Vorhandene E2E-Tests

`FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs` prueft Registrierung, Login und Logout fuer Desktop und Mobile. Der Test erwartet nach Login `/` und nach Logout `/login`. `Helpers/AuthGateway.cs` navigiert beim Login ebenfalls explizit nach `/` und bildet damit kein ReturnUrl-Verhalten ab.

`FinanceManager.Tests.E2E/Tests/Navigation/ListNavigationPlaywrightTests.cs` und weitere Navigationstests decken normale Seitenwechsel ab, simulieren aber keinen abgelaufenen Cookie-/Bearer-Zustand waehrend einer laufenden Sitzung.

## Vorhandene Integrations- und API-Client-Tests

`FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs` prueft:

- Registrierung und Login.
- ungueltige Zugangsdaten.
- Logout.
- JWT-Issuer-/Audience-Fehler.
- Security-Stamp-Aenderung.
- Rollenentzug und Deaktivierung eines Benutzers.

Die Tests verifizieren, dass serverseitig `401 Unauthorized` entsteht. Sie verifizieren nicht, dass der Blazor-Client daraus eine Login-Navigation mit Rueckkehrziel erzeugt. Ein isolierter Test fuer `ApiClient`-Fehlerklassifizierung und die Bewahrung des Statuscodes ist ebenfalls nicht erkennbar.

## Fehlende Szenarien

- Geschuetzte Seite laden, Session ungueltig machen, danach navigieren oder Daten nachladen: Weiterleitung zu `/login`.
- Rueckkehr nach erfolgreichem Login auf die urspruengliche Route inklusive Querystring.
- Direkter Aufruf von `/login` ohne `returnUrl`: Rueckkehr nach `/`.
- Nicht-Auth-Fehler, zum Beispiel `500` oder fachlicher `400`, darf keine Login-Weiterleitung ausloesen.
- Mehrere parallele `401` duerfen keine Navigation-Schleife verursachen.
- Leerer/alter Seitenzustand darf nach dem Auth-Fehler nicht als Endzustand sichtbar bleiben.
