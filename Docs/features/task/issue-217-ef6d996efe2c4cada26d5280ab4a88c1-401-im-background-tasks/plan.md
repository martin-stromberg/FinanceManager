# Umsetzungsplan

## Zielbild

`BackgroundTaskStatusPanel` fragt `GET /api/background-tasks/active` nur noch ab, wenn der aktuelle Nutzer authentifiziert ist. Falls waehrend einer laufenden Sitzung trotzdem ein `401 Unauthorized` vom API-Client zurueckkommt, beendet das Panel die Polling-Schleife, leert den lokalen Task-Zustand und loest keine weiteren Folgeaufrufe aus.

Der Backend-Endpunkt bleibt weiterhin durch JWT-Bearer-Authentifizierung geschuetzt. Das Request-Logging wird nicht angepasst, weil die Warnung fuer einzelne `401`-Antworten korrekt ist; beseitigt wird die wiederholte nicht autorisierte Anfrage.

## Technische Entscheidungen

- Authentifizierung wird im UI-Panel ueber den vorhandenen `FinanceManager.Application.ICurrentUserService` geprueft.
- `OnAfterRenderAsync(firstRender)` startet die Polling-Schleife nur, wenn `ICurrentUserService.IsAuthenticated` `true` ist.
- Ein `401 Unauthorized` aus `Api.BackgroundTasks_GetActiveAsync` wird im Panel explizit behandelt. Die Komponente stoppt das Polling dauerhaft fuer ihre aktuelle Lebensdauer.
- Transiente Fehler bleiben toleriert, damit temporaere Netzwerk- oder Serverfehler das autorisierte Polling nicht unnoetig deaktivieren.
- `CancelTaskAsync` und `RemoveQueuedAsync` pruefen denselben Auth-/Stop-Zustand, damit Button-Aktionen nach einem Auth-Verlust keine erneute Statusabfrage ausloesen.
- Controller, Authorization-Attribute und RequestLoggingMiddleware werden nicht geaendert.

## Umsetzungsschritte

1. `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor` erweitern:
   - `@using FinanceManager.Application` ergaenzen.
   - `ICurrentUserService` injizieren.
   - internes Flag fuer dauerhaft gestopptes Polling einfuehren, z. B. `_pollingDisabled`.
   - Hilfsmethode einfuehren, die `CurrentUser.IsAuthenticated` und `_pollingDisabled` gemeinsam prueft.

2. Startlogik im Panel anpassen:
   - In `OnAfterRenderAsync(firstRender)` vor Erstellung der `CancellationTokenSource` abbrechen, wenn kein authentifizierter Nutzer vorhanden ist.
   - Bei nicht authentifiziertem Zustand keine initiale `LoadTasksAsync`-Ausfuehrung starten.

3. Fehlerbehandlung in `LoadTasksAsync` konkretisieren:
   - `OperationCanceledException` weiterhin ignorieren.
   - `HttpRequestException` mit `StatusCode == HttpStatusCode.Unauthorized` separat behandeln.
   - In diesem Fall Polling stoppen, `Tasks` leeren, `_shouldShow = false` setzen und `StateHasChanged` ausloesen.
   - Andere Exceptions wie bisher als transiente Fehler behandeln.

4. Stop-Mechanik kapseln:
   - Kleine private Methode, z. B. `DisablePollingAsync`, anlegen.
   - Diese Methode setzt `_pollingDisabled`, cancelt `_cts`, leert die Anzeige und rendert die Komponente neu.
   - Die Methode muss mehrfach aufrufbar sein, ohne Fehler zu werfen.

5. Button-Aktionen absichern:
   - `CancelTaskAsync` und `RemoveQueuedAsync` verlassen die Methode sofort, wenn Polling aktuell nicht erlaubt ist.
   - Nach erfolgreicher Cancel-/Remove-Anfrage nur dann `LoadTasksAsync` aufrufen, wenn weiterhin Polling erlaubt ist.

6. Optional kleine Aufraeumung innerhalb des Panels:
   - Kommentare bei Bedarf an das neue Verhalten anpassen.
   - Keine Aenderung der `AllowedTypes`-Semantik: Der Filter entscheidet weiterhin nur ueber Sichtbarkeit, nicht ueber API-Abfrageumfang.

## Tests

1. Neue bUnit-Tests fuer `BackgroundTaskStatusPanel` in `FinanceManager.Tests/Components/BackgroundTaskStatusPanelTests.cs` anlegen.

2. Testfall: nicht authentifizierter Nutzer
   - `ICurrentUserService.IsAuthenticated = false` registrieren.
   - Fake oder Mock fuer `IApiClient` registrieren.
   - Panel rendern.
   - Erwartung: `BackgroundTasks_GetActiveAsync` wird nicht aufgerufen.

3. Testfall: authentifizierter Nutzer
   - `ICurrentUserService.IsAuthenticated = true` registrieren.
   - `BackgroundTasks_GetActiveAsync` liefert eine laufende oder queued Task.
   - Panel rendern.
   - Erwartung: initialer API-Aufruf erfolgt und die passende Panel-Struktur wird angezeigt.

4. Testfall: `401 Unauthorized`
   - `ICurrentUserService.IsAuthenticated = true` registrieren.
   - `BackgroundTasks_GetActiveAsync` wirft `HttpRequestException` mit `StatusCode = Unauthorized`.
   - Panel mit sehr kurzem `PollInterval` rendern und laenger als ein Intervall warten.
   - Erwartung: Nach dem ersten `401` entstehen keine weiteren API-Aufrufe.

5. Testfall: transienter Fehler
   - Erster Aufruf wirft eine nicht-401-Exception.
   - Folgeaufruf liefert Tasks.
   - Erwartung: Polling laeuft weiter und Tasks werden angezeigt.

6. Abschliessend ausfuehren:
   - `dotnet test`

## Manuelle Pruefung

1. Anwendung ohne Anmeldung oeffnen und mehrere Sekunden auf einer Listen- oder Kartenseite verweilen.
2. Sicherstellen, dass keine wiederholten Logeintraege `HTTP GET /api/background-tasks/active responded 401` entstehen.
3. Mit Anmeldung eine Seite oeffnen, deren ViewModel `VisibleBackgroundTaskTypes` nutzt.
4. Einen Background-Task starten und pruefen, dass Statusanzeige, Fortschritt sowie Cancel/Remove weiterhin funktionieren.

## Risiken und Gegenmassnahmen

- Wenn `ICurrentUserService.IsAuthenticated` waehrend einer bestehenden interaktiven Sitzung veraltet ist, kann trotzdem ein einzelner `401` auftreten. Gegenmassnahme: Das Panel stoppt beim ersten `401` dauerhaft fuer die aktuelle Komponenteninstanz.
- Wenn ein Nutzer nachtraeglich innerhalb derselben Komponenteninstanz authentifiziert wird, startet ein zuvor deaktiviertes Panel nicht automatisch neu. Das ist akzeptabel, weil Login/Logout in der Anwendung typischerweise Navigation oder Neurendering der Seiten ausloest; falls spaeter ein dynamischer Auth-Wechsel ohne Navigation eingefuehrt wird, kann das Panel ueber einen Auth-State-Event reaktiviert werden.
- Zu kurze Polling-Intervalle in Tests koennen instabil sein. Gegenmassnahme: Tests verwenden zaehlende Fakes und bUnit-Wait-Mechanismen statt fixer langer Sleeps.

## Offene Punkte

Keine.
