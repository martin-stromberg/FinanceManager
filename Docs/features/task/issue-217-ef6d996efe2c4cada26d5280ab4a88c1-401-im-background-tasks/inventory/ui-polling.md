# UI-Polling

## Fundstellen

- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:6`: `PollInterval` ist standardmaessig `2000` ms.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:17`: `OnAfterRenderAsync` startet beim ersten Rendern die Polling-Schleife.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:23`: `PollLoopAsync` wird fire-and-forget gestartet.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:24`: Direkt nach Start erfolgt ein initialer Fetch.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:34-36`: Die Schleife wartet das Intervall ab und ruft dann `LoadTasksAsync`.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:47`: `LoadTasksAsync` ruft `Api.BackgroundTasks_GetActiveAsync(ct)`.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:61-62`: Fehler ausser Abbruch werden geschluckt.
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor:84-87`: `Dispose` bricht die Schleife beim Komponentenabbau ab.
- `FinanceManager.Web/Components/Pages/ListPage.razor:38`: Listen-Seiten rendern das Panel.
- `FinanceManager.Web/Components/Pages/CardPage.razor:38`: Karten-Seiten rendern das Panel.

## Verhalten

Das Panel wird auf Karten- und Listen-Seiten gerendert, sobald ein ViewModel bzw. Provider vorhanden ist. Es entscheidet zwar anhand von `AllowedTypes`, ob es sichtbar sein soll, laedt aber immer alle aktiven/queued Tasks. Diese Sichtbarkeitslogik verhindert keine Anfrage.

Der Kommentar im Panel beschreibt `AllowedTypes` als optionalen Filter nur fuer Sichtbarkeit. Dadurch pollt auch ein Panel mit leerem Ergebnis weiter, solange die Komponente lebt.

## Auth-Bezug

`BackgroundTaskStatusPanel` injiziert nur `IApiClient` und den Localizer. Es injiziert keinen `ICurrentUserService`, keinen Auth-State-Provider und prueft auch nicht `BaseViewModel.IsAuthenticated`. Dadurch ist die Komponente von der vorhandenen ViewModel-Authentifizierungslogik entkoppelt.

`BaseViewModel` stellt mit `IsAuthenticated` und `CheckAuthentication()` bereits Hilfen bereit, diese werden vom Panel aber nicht genutzt.

## Implementierungsrelevante Beobachtungen

- Das Problem sollte primaer im Panel oder in einer kleinen vom Panel nutzbaren Auth-Abfrage geloest werden.
- Eine Loesung sollte `401` nicht als transienten Fehler behandeln, weil sonst die Schleife unveraendert weiterlaeuft.
- Das Panel hat bereits einen CancellationTokenSource-Lifecycle; ein Stoppen bei fehlender Auth oder `401` passt in die bestehende Struktur.
- Cancel/Remove-Aktionen sollten ebenfalls nicht erneut eine Polling-Anfrage ausloesen, wenn keine Authentifizierung vorhanden ist.
