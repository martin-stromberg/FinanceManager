# Umsetzungsplan: Weiterleitung zum Login bei abgelaufener Anmeldesession

## Ziel

Die Blazor-Anwendung soll einen Authentifizierungsfehler aus einem geschuetzten API-Aufruf zentral erkennen, die aktuell angeforderte interne Route inklusive Querystring und Fragment als `returnUrl` sichern und den Anwender einmalig zu `/login` weiterleiten. Nach erfolgreichem Login wird das gespeicherte Ziel konsumiert; bei einem direkten Login ohne Ziel bleibt die Weiterleitung nach `/` bestehen.

## Leitentscheidungen

- `401 Unauthorized` wird als abgelaufene oder ungueltige Session klassifiziert.
- `403 Forbidden` wird nur dann als Authentifizierungsfehler behandelt, wenn die Antwort ein explizites Authentifizierungssignal bzw. einen entsprechenden maschinenlesbaren Fehlercode enthaelt. Ein gewoehnlicher Berechtigungsfehler loest keine Login-Weiterleitung aus.
- Die Klassifizierung erfolgt zentral in `ApiClient`, damit alle Endpunkte denselben Fehlerpfad verwenden. Der bestehende Fehlertext und das Exception-Verhalten bleiben erhalten.
- Das Ziel wird als relative interne URL mit Pfad, Querystring und Fragment gespeichert. Absolute URLs, externe Hosts, `/login`, `/register`, `/error`, API-Routen und ungueltige Werte werden verworfen.
- Die globale `AuthRedirect`-Komponente uebernimmt die Navigation und dedupliziert parallele Authentifizierungsfehler, damit keine Redirect-Schleife entsteht.

## Umsetzungsschritte

1. **API-Fehler klassifizieren und publizieren**
   - `FinanceManager.Shared/ApiClient.cs`: In `EnsureSuccessOrSetErrorAsync` den HTTP-Status vor der allgemeinen Fehlerauswertung als Authentifizierungsfehler klassifizieren. Den Status bzw. ein dediziertes Ereignis/Signal veroeffentlichen, bevor `EnsureSuccessStatusCode()` die bestehende `HttpRequestException` ausloest.
   - `FinanceManager.Shared/IApiClient.cs`: Die neue maschinenlesbare Authentifizierungsinformation bzw. das Ereignis im gemeinsamen Client-Vertrag dokumentieren, falls der globale Web-Pfad ueber das Interface darauf zugreift.
   - Sicherstellen, dass `400`, `404`, `409`, `422`, `500` und ein nicht-authentifizierendes `403` ausschliesslich als normale API-Fehler behandelt werden.

2. **Zentralen Web-Navigationspfad anbinden**
   - `FinanceManager.Web/Components/App.razor`: Den zentralen Authentifizierungsfehlerpfad so registrieren, dass er fuer alle interaktiven Seiten und API-Aufrufe aktiv ist und beim Dispose sauber abgemeldet wird.
   - `FinanceManager.Web/Components/AuthRedirect.razor`: Aus dem aktuellen `NavigationManager.Uri` ein internes relatives Ziel inklusive Querystring und Fragment bilden, `/login?returnUrl=...` URL-kodiert aufrufen und parallele Meldungen waehrend der Weiterleitung ignorieren.
   - Die bestehende Pruefung fuer nicht-authentifizierte Navigation beibehalten, aber um eine gemeinsame ReturnUrl-Validierung ergaenzen. `/login` darf dabei weder erneut geschuetzt noch als Ziel gespeichert werden.
   - `FinanceManager.Web/Components/Routes.razor`: Nur anpassen, falls die globale Komponente fuer die Ereignisweitergabe in die Router-Hierarchie verschoben werden muss; der vorhandene `Router` und `MainLayout` bleiben ansonsten unveraendert.

3. **Login-Ziel uebernehmen und sicher konsumieren**
   - `FinanceManager.Web/Components/Pages/Login.razor`: Einen optionalen `returnUrl`-Queryparameter lesen, nach erfolgreichem Login nur einen validierten internen Wert verwenden und ihn genau einmal konsumieren. Ohne Parameter oder bei einem verworfenen Wert nach `/` navigieren.
   - Die bestehende direkte Login-Navigation und das Verhalten bei Login-Fehlern unveraendert lassen. Das Rueckkehrziel darf nicht aus untrusted externen oder absoluten URLs uebernommen werden.
   - `FinanceManager.Web/Components/Pages/ReportsHome.razor`: Den bestehenden lokalen `AuthenticationRequired`-Handler auf denselben ReturnUrl-/Redirect-Mechanismus umstellen oder entfernen, damit er kein Ziel mehr verliert und keine konkurrierende Navigation erzeugt.

4. **Gemeinsame Hilfslogik und Randfaelle absichern**
   - Falls die Validierung nicht sinnvoll in `AuthRedirect` und `Login.razor` gemeinsam implementiert werden kann, eine kleine interne Hilfsklasse unter `FinanceManager.Web/Infrastructure/Auth/` einfuehren; keine neue globale Abstraktion fuer normale Navigation erstellen.
   - Zielpfade mit Querystring und Fragment testen, sowie leere, mehrfach kodierte, externe, absolute und `/login`-Ziele ablehnen.
   - Nach einem Authentifizierungsfehler den betroffenen Lade-/Fehlerzustand nicht als dauerhaften leeren Endzustand stehen lassen; die Navigation muss fuer den Benutzer sichtbar und deterministisch erfolgen.

## Tests

### API-/Integrations-Tests

- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`: Tests fuer die zentrale Klassifizierung von `401` als Authentifizierungsfehler und fuer das unveraenderte Exception-/Fehlertextverhalten.
- `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`: Test fuer einen nicht-authentifizierenden `403` bzw. einen gewoehnlichen Fachfehler, der kein Authentifizierungssignal ausloest. Falls fuer die reine Clientklassifizierung ein isolierter Handler erforderlich ist, den Testaufbau dort um einen minimalen Stub-`HttpMessageHandler` ergaenzen.
- Bestehende Tests fuer ungueltige Tokens, Security-Stamp-Aenderung, Rollenentzug und deaktivierte Benutzer weiter ausfuehren und sicherstellen, dass die serverseitig erwarteten `401` unveraendert bleiben.

### E2E-Tests

- `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`: Sessionverlust auf einer geschuetzten Route simulieren, anschliessend einen API-Datenabruf bzw. eine Navigation ausloesen und auf `/login?returnUrl=...` pruefen.
- Derselbe Test: Nach erfolgreichem Login muss die urspruengliche Route inklusive Querystring geoeffnet werden und ihr Inhalt geladen sein.
- Derselbe Test: Direkter Aufruf von `/login` ohne `returnUrl` fuehrt nach Login weiterhin nach `/`.
- Derselbe Test oder ein eigener Testfall: ungueltiger/externer `returnUrl` fuehrt nach erfolgreichem Login nach `/`, nicht auf einen externen Host; mehrere parallele `401` duerfen nur eine Login-Navigation erzeugen.
- `FinanceManager.Tests.E2E/Helpers/AuthGateway.cs`: Nur erweitern, wenn fuer die Sessionverlust-Simulation eine gezielte Cookie-/Token-Manipulation oder ein Login mit bestehendem `returnUrl` benoetigt wird; der bestehende Standard-Login nach `/` bleibt als Regressionstest erhalten.

## Verifikation

1. `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj` ausfuehren.
2. `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj` mit der vorhandenen Playwright-Testumgebung ausfuehren.
3. Manuell oder im E2E-Test pruefen: abgelaufene Session, Rueckkehrziel mit Querystring/Fragment, direkter Login, nicht-authentifizierender Fehler und parallele Auth-Fehler.
4. Vor Abschluss sicherstellen, dass keine Redirect-Schleife entsteht und dass keine externen `returnUrl`-Ziele akzeptiert werden.

## Offene Punkte

Keine. Die Klassifizierung von `401` sowie die restriktive Behandlung von `403` und `returnUrl` sind fuer die Umsetzung festgelegt.
