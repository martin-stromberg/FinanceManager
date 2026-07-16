# UI und API-Client

## Relevante Dateien

- `FinanceManager.Shared/ApiClient.Backups.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor`
- `FinanceManager.Web/Resources/Components/Pages/Setup*.resx`
- `FinanceManager.Web/Resources/Pages*.resx`

## Aktueller Client-Ablauf

`ApiClient.Backups_StartApplyAsync(Guid id)` sendet `POST /api/setup/backups/{id}/apply/start` mit `content: null`.

`ApiClient.Backups_ApplyAsync(Guid id)` sendet `POST /api/setup/backups/{id}/apply` mit `content: null`.

`SetupBackupsViewModel.StartApplyAsync` ruft nur `Backups_StartApplyAsync(id)` auf und setzt `HasActiveRestore` anhand des Status.

`SetupBackupTab.razor` zeigt je Backup einen Restore-Icon-Button. Ein Klick ruft `StartApplyBackgroundAsync(b.Id)` auf, ohne Dialog, Bestaetigungstext oder erneute Pruefung.

## Upload-UI

Die Upload-Datei wird in `SetupBackupTab.razor` ueber `InputFile` gelesen. `OpenReadStream` wird mit `1024L * 1024L * 1024L` aufgerufen, also ebenfalls 1 GB. Eine clientseitige Dateityp-/Groessenpruefung ist nicht sichtbar.

Clientseitige Pruefungen duerfen nur Nutzerfeedback verbessern. Die verbindlichen Limits muessen serverseitig liegen.

## Restore-Bestaetigung

Die Anforderung verlangt Absicherung gegen unbeabsichtigte oder gefaelschte Ausloesung. UI-seitig naheliegend ist ein bestaetigender Dialog, aber die eigentliche Sicherheit muss serverseitig pruefbar sein.

Moegliche UI-Form:

- Klick auf Restore oeffnet Dialog mit Backup-Dateiname, Datum, Groesse und Hinweis auf destruktiven Replace.
- Nutzer muss einen exakten Text eingeben, z. B. den Backup-Dateinamen oder `WIEDERHERSTELLEN`.
- Der eingegebene Wert wird im Request-Body an `apply/start` gesendet.
- Optional kann der Server vorher eine kurzlebige Challenge ausstellen, die im Restore-Request mitgesendet wird.

## Lokalisierung

Neue UI-Texte muessen in den bestehenden Resource-Dateien fuer Setup/Pages ergaenzt werden. Betroffen sind voraussichtlich:

- Dialogtitel fuer Restore
- destruktiver Warntext
- Eingabelabel/Bestaetigungshinweis
- Fehlermeldung bei fehlender/falscher Bestaetigung
- Validierungsfehler fuer ungueltige Backups
- Statusmeldungen fuer abgelehnte Restores

## API-Client-Aenderungen

Der Shared-Client sollte ein DTO oder Parameter fuer Restore-Bestaetigungen erhalten. Die ViewModel-Tests muessen entsprechend angepasst werden, weil `StartApplyAsync` aktuell nur eine GUID uebergibt.
