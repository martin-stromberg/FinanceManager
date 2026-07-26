# Bestandsaufnahme

## Zusammenfassung

Die wiederkehrenden `401`-Warnungen entstehen sehr wahrscheinlich durch `BackgroundTaskStatusPanel.razor`. Das Panel startet beim ersten Rendern immer eine Polling-Schleife mit einem Standardintervall von 2 Sekunden und ruft dabei `GET /api/background-tasks/active` auf. Fehler werden im Panel geschluckt, die Schleife laeuft aber weiter.

Der Backend-Endpunkt ist korrekt durch JWT-Bearer-Authentifizierung geschuetzt. Wenn kein gueltiger Token an den internen API-Client angehaengt werden kann, antwortet der Endpunkt mit `401`. Die `RequestLoggingMiddleware` protokolliert jeden Statuscode `>= 400` als Warning, wodurch die Polling-Schleife das Log flutet.

## Detaildokumente

- [UI-Polling](inventory/ui-polling.md)
- [Backend, Authentifizierung und Logging](inventory/backend-auth-logging.md)
- [Tests und Absicherung](inventory/tests.md)

## Relevante Dateien

| Bereich | Datei | Bedeutung |
|--------|-------|-----------|
| UI-Polling | `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor` | Startet Polling und ruft aktive Background-Tasks ab. |
| UI-Einbindung | `FinanceManager.Web/Components/Pages/ListPage.razor` | Rendert das Panel auf Listen-Seiten. |
| UI-Einbindung | `FinanceManager.Web/Components/Pages/CardPage.razor` | Rendert das Panel auf Karten-Seiten. |
| API-Client | `FinanceManager.Shared/ApiClient.BackgroundTasks.cs` | Fuehrt `GET /api/background-tasks/active` aus und behandelt Nicht-Erfolg als Fehler. |
| Backend-Endpunkt | `FinanceManager.Web/Controllers/BackgroundTasksController.cs` | Schuetzt Background-Task-Endpunkte mit JWT-Bearer-Auth. |
| Auth-Token | `FinanceManager.Web/Infrastructure/Auth/JwtCookieAuthTokenProvider.cs` | Liefert Bearer-Token aus Cookie oder Cache, sonst `null`. |
| HTTP-Handler | `FinanceManager.Web/Infrastructure/Auth/AuthenticatedHttpClientHandler.cs` | Haengt Token an ausgehende API-Requests an, wenn vorhanden. |
| Logging | `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs` | Loggt alle Statuscodes `>= 400` als Warning. |
| Service-Setup | `FinanceManager.Web/ProgramExtensions.cs` | Registriert API-Client, Authentifizierung, Middleware und Controller. |

## Ist-Zustand

- `BackgroundTaskStatusPanel.razor` startet in `OnAfterRenderAsync(firstRender)` immer `PollLoopAsync`.
- `PollLoopAsync` ruft alle 2 Sekunden `LoadTasksAsync` auf.
- `LoadTasksAsync` ruft `Api.BackgroundTasks_GetActiveAsync(ct)` auf.
- `BackgroundTasks_GetActiveAsync` sendet `GET /api/background-tasks/active`.
- Der Controller ist mit `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` geschuetzt.
- Bei fehlendem oder ungueltigem Token gibt ASP.NET Core `401 Unauthorized` zurueck.
- Das Panel faengt Exceptions ab, beendet die Polling-Schleife aber nicht.
- Das Request-Logging schreibt jeden dieser `401` als Warning.

## Vermutete Ursache

Die Polling-Schleife wird rein UI-seitig gestartet und prueft nicht vorab, ob der Nutzer authentifiziert ist oder ob ein Token verfuegbar ist. Ein nicht angemeldeter, abgelaufener oder nicht mehr gueltiger Auth-Zustand fuehrt deshalb zu wiederholten nicht autorisierten API-Anfragen.

## Naheliegender Loesungsraum

- Das Polling im Panel nur starten oder fortsetzen, wenn ein authentifizierter Nutzer bekannt ist.
- Bei `401 Unauthorized` das Polling stoppen und nicht nur die Exception schlucken.
- Optional dem API-Client oder Panel eine explizite Behandlung fuer `401` geben, damit Auth-Fehler von transienten Fehlern unterscheidbar sind.
- Den Backend-Endpunkt nicht fuer anonyme Nutzer freigeben, da aktive Tasks nutzerbezogene Informationen enthalten.

## Risiken

- Eine rein serverseitige Logging-Unterdrueckung fuer diesen Endpunkt wuerde die Symptomlast reduzieren, aber die unnoetigen Requests nicht beseitigen.
- Eine Freigabe des Endpunkts fuer anonyme Nutzer wuerde das Sicherheitsmodell aufweichen und passt nicht zu den Akzeptanzkriterien.
- Das Panel darf fuer authentifizierte Nutzer weiterhin alle laufenden/queued Tasks anzeigen und Cancel/Remove-Aktionen ausfuehren.

## Offene Punkte

- Keine fachlichen offenen Punkte.
