# Backend, Authentifizierung und Logging

## Backend-Endpunkt

`FinanceManager.Web/Controllers/BackgroundTasksController.cs`:

- Zeile 20: Route `api/background-tasks`.
- Zeile 21: Der gesamte Controller ist mit JWT-Bearer-Auth geschuetzt.
- Zeile 69: `GET active` ist der betroffene Endpunkt.
- Zeile 71: `GetActiveAndQueued()` liefert aktive oder queued Tasks.
- Zeile 73: Die Filterung erfolgt anhand der aktuellen User-ID.

Der Endpunkt ist fachlich nutzerbezogen. Eine anonyme Freigabe waere nicht passend, weil Tasks pro User gefiltert und verwaltet werden.

## API-Client

`FinanceManager.Shared/ApiClient.BackgroundTasks.cs`:

- Zeile 29: `BackgroundTasks_GetActiveAsync` kapselt den Aufruf.
- Zeile 31: Aufruf von `/api/background-tasks/active`.
- Zeile 32: `EnsureSuccessOrSetErrorAsync(resp)` behandelt Nicht-Erfolg als Fehler.

Wenn der Server `401` liefert, wird der Fehler im API-Client nicht als spezieller Auth-Fall zurueckgegeben. Das Panel faengt den daraus resultierenden Fehler generisch ab und pollt weiter.

## Token-Ermittlung

`FinanceManager.Web/ProgramExtensions.cs`:

- Zeile 141: `IAuthTokenProvider` ist als `JwtCookieAuthTokenProvider` registriert.
- Zeile 142: Der benannte HttpClient `Api` wird konfiguriert.
- Zeile 153: `AuthenticatedHttpClientHandler` wird an den API-Client gehaengt.
- Zeile 211: JWT-Bearer-Authentifizierung ist das Default-Schema.
- Zeile 218: `OnMessageReceived` liest das Token aus dem Cookie `FinanceManager.Auth`.

`FinanceManager.Web/Infrastructure/Auth/AuthenticatedHttpClientHandler.cs`:

- Der Handler ruft `IAuthTokenProvider.GetAccessTokenAsync`.
- Gibt der Provider `null` zurueck, wird keine `Authorization`-Header gesetzt.
- Token-Ermittlungsfehler werden ignoriert; der Request laeuft dann unauthentifiziert weiter.

`FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs`:

- Bei Request-Kontext wird das Token aus dem Cookie gelesen.
- Fehlt das Cookie, wird der Cache invalidiert und `null` zurueckgegeben.
- Ohne Request-Kontext kann ein noch gueltiger gecachter Token verwendet werden.
- Bei ungueltigem oder nicht refreshbarem Token wird ebenfalls `null` geliefert.

## Request-Logging

`FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs`:

- Zeile 57: Statuscodes `< 400` werden als Debug geloggt, alle anderen als Warning.
- Zeile 63-64: Das Logformat entspricht dem Beispiel aus der Anforderung.

Die Warning ist also erwartetes Verhalten fuer `401`. Das eigentliche Problem ist nicht die Log-Level-Entscheidung selbst, sondern die dauerhaft wiederholte unauthorisierte Anfrage.

## Pipeline

`FinanceManager.Web/ProgramExtensions.cs`:

- Zeile 349: `RequestLoggingMiddleware` laeuft frueh in der Pipeline.
- Zeile 407-408: Authentifizierung und Autorisierung laufen nach Static Files/Antiforgery.
- Zeile 417: Controller werden gemappt.

Da das Logging die gesamte nachgelagerte Pipeline umschliesst, werden auch durch Authorization erzeugte `401`-Antworten protokolliert.
