# Implementierung an Planwiderspruch angehalten

## Ergebnis

Schritt 6 ist nicht abgeschlossen. Kein Produktivcode wurde verändert. Gemäß `/implement` wurde nach Feststellung des folgenden Widerspruchs angehalten, bevor der State-/Scope-Fix begonnen wurde.

## Widerspruch

Der Plan verlangt im Home-Inaktivitätstest bis nach `lastExp + 12 s` null Keepalives und keinen Login-Redirect, anschließend einen Kontenlink mit geschütztem API-401 als auslösende Nutzeraktion.

`FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor` startet auf Home jedoch einen `PollLoopAsync` alle 2.000 ms. `CanPollAsync` gibt nach initialer Authentifizierung dauerhaft `true` zurück, ohne den Ablauf erneut zu prüfen. `LoadTasksAsync` ruft `Api.BackgroundTasks_GetActiveAsync` auf. `FinanceManager.Shared/ApiClient.BackgroundTasks.cs:29` führt dafür `EnsureSuccessOrSetErrorAsync` aus; dieses löst über `TrySetErrorFromResponseAsync` bei 401 `AuthenticationRequired` aus. `AuthRedirect` leitet somit auch ohne Nutzeraktion zum Login. Das anschließende Abfangen des 401 im Panel verhindert das bereits veröffentlichte Ereignis nicht.

Der neue Circuit-State kann den Token bei Bedienung verlängern, beseitigt diesen Idle-Ablauf aber nicht. Der Plan nennt keine Änderung an BackgroundTaskStatusPanel/ApiClient.BackgroundTasks. Deren Anpassung wäre eine nicht freigegebene Abweichung vom verbindlichen Plan.

## Erforderliche Planergänzung

Das rein passive Polling muss bei abgelaufener oder widerrufener Circuit-Authentifizierung ohne Login-Redirect enden; konkrete Nutzeraktionen behalten den geschützten 401-/Redirect-Pfad. Die genaue Umsetzung und ein Regressionstest für den Hintergrundpoll sind im Plan festzulegen. Der vorhandene echte Home-Idle-E2E-Test bleibt verpflichtender Funktionsnachweis.

## Bereits erledigt

- Realen vorhandenen Formularlogin erhöht erfolgreich ausgeführt (1/1).
- Eigenen ausschließlich testseitigen Host, API-Beobachter, Zweiminutenfixture und sieben verpflichtende Browserfälle implementiert.
- Solution-Build mit Testhost: 0 Fehler.
- Originalfehler vor Produktivänderung real reproduziert: erste interaktive Home-APIs und Konten-APIs 401 ohne Bearer; siehe [Reproduktion](reproduction.md).
- Anschließend alle sieben neuen Fälle auf unverändertem Produktivstand ausgeführt: 7 fehlgeschlagen, Exit 2, Laufzeit 2m 06s. Die Folgefälle werden bereits bei Login/Home durch den initialen Circuit-Authfehler blockiert; ihre späteren Assertions sind noch kein separater Funktionsnachweis. [TRX](reproduction/original-all.trx), [API-Zeitlinie](reproduction/original-all-api.jsonl).

## Arbeitsstand

Uncommittete Änderungen beschränken sich auf `FinanceManager.sln`, das neue Testhostprojekt, E2E-Fixture/Loginhilfe, neue E2E-Hilfen/Tests und diese Nachweise. Kein Commit erstellt. Keine Reviews oder Lifecycle-Folgeschritte ausgeführt.
