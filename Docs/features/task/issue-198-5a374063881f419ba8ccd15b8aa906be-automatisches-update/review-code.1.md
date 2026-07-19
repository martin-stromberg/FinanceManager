# Code-Review - Automatisches Update

Status: Befunde vorhanden

## Befunde

### 1. ZIP-Inhalte werden vor der Installation nicht auf sichere Pfade validiert

Schweregrad: Hoch

Fundstellen:
- `FinanceManager.Web/Services/Updates/UpdateValidator.cs:53`
- `FinanceManager.Web/Services/Updates/UpdateScriptGenerator.cs:60`
- `FinanceManager.Web/Services/Updates/UpdateScriptGenerator.cs:90`

Der Validator prueft nur, ob das ZIP lesbar und nicht leer ist. Die anschliessende Installation extrahiert das Archiv per `Expand-Archive` bzw. `unzip` und kopiert alle Inhalte ins Anwendungsverzeichnis. Es gibt keine Pruefung auf absolute Pfade, `..`-Segmente, leere/ungueltige Entry-Namen, Symlinks oder Eintraege ausserhalb des erwarteten Publish-Roots.

Auswirkung: Ein manipuliertes oder fehlerhaft erzeugtes Release-Asset kann beim Update Dateien ausserhalb des Staging-Verzeichnisses schreiben oder unerwartete Inhalte in die Installation bringen. Gerade weil das Feature produktive Self-Updates aus Release-Artefakten ausfuehrt, muss das Archiv vor dem Skriptstart strikt validiert werden.

Empfehlung: ZIP-Eintraege vorab gegen einen kanonischen Staging-Zielpfad validieren, absolute Pfade und Traversal ablehnen, Symlinks/Unix-Special-Files ablehnen und einen Test ergaenzen, der ein `../evil.txt`-Archiv ablehnt.

### 2. Admin-konfigurierter WorkingDirectory-Wert wird gespeichert, aber vom Dateispeicher ignoriert

Schweregrad: Mittel

Fundstellen:
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs:65`
- `FinanceManager.Web/Services/Updates/UpdateFileStore.cs:18`

Die API akzeptiert und persistiert `WorkingDirectory`, aber `UpdateFileStore.RootDirectory` liest dauerhaft `_options.WorkingDirectory` aus `appsettings`. Der gespeicherte Wert aus `settings.json` wird fuer Pending-ZIP, Staging, Status, Lock und Skripte nicht verwendet.

Auswirkung: Die UI/API suggeriert eine wirksame Betriebseinstellung, waehrend der Updater tatsaechlich weiter in das statisch konfigurierte Verzeichnis schreibt. Das kann Updates in ein unerwartetes Verzeichnis legen, Lock-Resets am falschen Ort ausfuehren und produktive Installationen schwer diagnostizierbar machen.

Empfehlung: Entweder `WorkingDirectory` aus den administrativen Einstellungen entfernen oder den FileStore so umstellen, dass er fuer operationsbezogene Pfade konsistent den gespeicherten, validierten Wert nutzt. Dazu einen Integrationstest fuer geaendertes `WorkingDirectory` ergaenzen.

### 3. Die Warteseite kann sofort Erfolg melden, bevor das Update ueberhaupt abgeschlossen ist

Schweregrad: Mittel

Fundstellen:
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor:115`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor:127`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor:130`

Nach `install/start` startet die UI direkt das Health-Polling. Jeder erfolgreiche `/health`-Call fuehrt sofort zu `forceLoad`. Der alte Prozess kann in dieser Phase aber noch laufen: Das Skript schlaeft zuerst drei Sekunden, und `StopApplication()` beendet den Host kontrolliert, nicht notwendigerweise vor dem ersten 2-Sekunden-Poll.

Auswirkung: Die UI kann das Update als abgeschlossen behandeln und die Seite neu laden, obwohl der Dienst erst danach gestoppt und Dateien ersetzt werden. Benutzer sehen dann keinen belastbaren Wartestatus und koennen waehrend der eigentlichen Downtime in einen inkonsistenten Zustand laufen.

Empfehlung: Das Polling muss zuerst einen Ausfall oder eine neue Instanz/Version erkennen, bevor ein spaeterer Health-Erfolg als Abschluss gilt. Alternativ Health um Versions-/Boot-ID erweitern und erst bei geaenderter Boot-ID reloaden. Dazu einen Komponenten- oder JS-Flow-Test fuer "Health bleibt zunaechst 200" ergaenzen.

### 4. Lock und Status bleiben bei Startfehlern dauerhaft in einem falschen Zustand

Schweregrad: Mittel

Fundstellen:
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:42`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:47`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:49`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs:58`

`StartAsync` erstellt zuerst die Lock-Datei und setzt `IsInstallRunning = true`. Wenn danach die Skripterzeugung, das Statusschreiben oder `Process.Start` fehlschlaegt, gibt es kein `try/catch/finally`, das den Lock loescht, `IsInstallRunning` zuruecksetzt oder den Status auf `Failed` setzt.

Auswirkung: Ein lokaler Fehler vor dem eigentlichen Host-Shutdown kann das System in einem installierenden/gelockten Zustand zuruecklassen. Der Admin-Reset-Endpunkt verweigert in derselben Prozessinstanz den Reset, weil `IsInstallRunning` true bleibt, obwohl kein Updateprozess laeuft.

Empfehlung: Startpfad transaktional behandeln: bei Fehler vor erfolgreichem externem Prozessstart Lock loeschen, `IsInstallRunning` zuruecksetzen, Status `Failed` mit Fehler schreiben und Unit-Tests fuer Fehler in Generator/Runner ergaenzen.

### 5. Geplante Installation wird nach der geplanten Uhrzeit bei jedem spaeteren Ready-Status erneut als faellig betrachtet

Schweregrad: Mittel

Fundstellen:
- `FinanceManager.Web/Services/Updates/UpdateScheduler.cs:47`
- `FinanceManager.Web/Services/Updates/UpdateScheduler.cs:54`
- `FinanceManager.Web/Services/Updates/UpdateScheduler.cs:55`

Der Scheduler speichert nur eine Tageszeit und prueft `now >= scheduledTime`. Nach Erreichen der Uhrzeit bleibt die Bedingung fuer den Rest des Tages wahr. Wenn ein Installationsstart wegen fehlender Dienstkonfiguration, `Process.Start`-Fehler oder eines anderen transienten Problems fehlschlaegt und der Status `Ready` bleibt, versucht der Hosted Service jede Minute erneut die Installation.

Auswirkung: Ein fehlerhaft konfigurierter produktiver Host erzeugt endlose Installationsversuche, Logs und potentiell wiederholte Lock-/Skript-Nebenwirkungen. Das ist fuer ein Feature, das den Host beenden darf, ein Betriebsrisiko.

Empfehlung: Geplante Installationen mit konkretem Datum/Zeitpunkt oder "already attempted for this schedule"-Marker modellieren. Nach einem Startfehler Status `Failed` setzen oder den Schedule deaktivieren, bis ein Admin erneut plant. Scheduler-Tests fuer wiederholte Minuten nach einer fehlgeschlagenen Installation ergaenzen.

## Fehlende Tests

- Kein Test fuer ZIP-Traversal, absolute Pfade, Symlinks oder sonstige gefaehrliche Archive.
- Kein Test dafuer, dass ein gespeicherter `WorkingDirectory`-Wert wirklich fuer Lock, Status, Pending und Staging verwendet wird.
- Kein Test fuer Fehlerpfade im `UpdateExecutor` nach Lock-Erstellung.
- Kein Test fuer Health-Polling, wenn `/health` unmittelbar nach Installationsstart noch `200 OK` liefert.
- Kein Scheduler-Test fuer wiederholte automatische Installationsversuche nach fehlgeschlagenem Start.

## Ausgefuehrte Pruefungen

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore` - bestanden, 794 Tests; bestehende NuGet-Warnungen zu `SQLitePCLRaw.lib.e_sqlite3` und trim-bezogenen PackageReferences.
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore` - bestanden, 80 Tests; gleiche bestehende Warnungen.
- `npm run test:release-version` - bestanden, 21 Tests.
