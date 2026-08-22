# Bestandsaufnahme: Weiterleitung zum Login bei abgelaufener Anmeldesession

## Umfang

Untersucht wurden die Blazor/Web-Komponenten, die clientseitige Authentifizierung, `auth.js`, die registrierten `HttpClient`-/API-Client-Pfade, Navigation und Login-Zielrouten sowie vorhandene Authentifizierungs- und E2E-Tests.

## Ergebnis in Kurzform

- Die Anwendung verwendet Blazor Interactive Server mit einer global eingebundenen `AuthRedirect`-Komponente.
- API-Aufrufe laufen ueber den benannten `HttpClient` `Api`, `AuthenticatedHttpClientHandler` und `FinanceManager.Shared.ApiClient`.
- `ApiClient` setzt `LastError` und `LastErrorCode`, wertet HTTP-Status `401`/`403` aber nicht als eigenes Authentifizierungsereignis aus.
- Ein `AuthenticationRequired`-Event ist in den ViewModel-Basen vorhanden, wird aber nicht zentral aus API-Fehlern ausgelost. `ReportsHome` nutzt den Eventweg nur fuer den initialen lokalen Auth-Status.
- `AuthRedirect` navigiert bei fehlender Authentifizierung zu `/login`, bewahrt die aktuelle Route aber nicht als `returnUrl` auf.
- `Login.razor` navigiert nach erfolgreichem Login immer zu `/`; ein gespeichertes Rueckkehrziel wird nicht gelesen.
- Die E2E-Tests decken Registrierung, Login und Logout ab. Ein abgelaufener/ungueltiger Token mit Rueckkehr zur Ausgangsroute ist nicht abgedeckt.

## Relevante Detaildokumente

- [Blazor-Komponenten und ViewModel-Eventweg](inventory/blazor-components.md)
- [Auth.js, Authentifizierung und HTTP/API-Client](inventory/auth-http-api.md)
- [Navigation, Login und ReturnUrl](inventory/navigation-return-url.md)
- [Tests und Abdeckung](inventory/tests.md)

## Erkennbare Luecken und Ansatzpunkte

1. Einen zentralen, wiederverwendbaren Authentifizierungsfehlerpfad am API-/HTTP-Client definieren, der mindestens `401 Unauthorized` und die fachlich als Sessionverlust geltenden `403 Forbidden` unterscheiden bzw. klassifizieren kann.
2. Das Ereignis bis zu einer zentralen Blazor-Navigationsstelle propagieren, ohne allgemeine API- oder Validierungsfehler umzuleiten.
3. Das aktuelle relative Ziel inklusive Querystring vor der Login-Navigation speichern und nach erfolgreichem Login nur bei einem explizit vorhandenen Ziel verwenden.
4. Schutz gegen Redirect-Schleifen und gegen ungueltige/externe Return-URLs vorsehen.
5. E2E- und API-Client-Tests fuer Sessionverlust, Nicht-Auth-Fehler, direkte Login-Navigation und Rueckkehrziel ergaenzen.

## Nicht untersucht / nicht geaendert

Die serverseitige Laufzeit der Session und die eigentliche JWT-/Identity-Validierung wurden nur als Ursache der HTTP-Statuscodes betrachtet. Es wurden keine Quellcodeaenderungen vorgenommen.
