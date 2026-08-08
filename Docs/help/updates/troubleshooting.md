← [Zurück zur Übersicht](index.md)

# Automatische Updates — Fehlerbehebung

## Installation zeigt sich nicht im Admin-Setup

**Symptom:** Reiter "Update" oder "Automatische Updates" ist in der Admin-Setup-Seite nicht sichtbar.

**Ursache:** 
- Update-Services sind nicht in DI registriert
- Oder: Benutzer ist nicht Admin
- Oder: `SetupUpdateTab.razor` ist nicht in der Setup-Komponente eingebunden

**Lösung:**
1. Sicherstellen, dass `Program.cs` folgende Zeile enthält:
   ```csharp
   services.AddUpdateServices(configuration);
   ```
2. Benutzer muss Admin-Rolle haben
3. Web-Browser-Cache leeren und Seite neu laden

---

## "An update lock is active" — Installation kann nicht gestartet werden

**Symptom:** Die Ribbon-Aktion "Update installieren" ist deaktiviert; Status zeigt "A lock is active since [Zeit]".

**Ursache:**
1. Installation läuft noch (Installer-Skript ist aktiv)
2. Installation ist abgestürzt und Lock wurde nicht bereinigt (verwaister Lock)
3. In-Memory-Flag `IsInstallRunning` ist gesetzt, aber Prozess existiert nicht mehr

**Hinweis:** Der Anzeigestatus wird bei jeder Statusabfrage automatisch gegen die tatsächliche Lock-Datei auf der Festplatte abgeglichen (siehe "Automatische Lock-Status-Reconciliation" im technischen Ablauf). Ein Fall, in dem "Update installieren" wegen "Lock aktiv" deaktiviert bleibt, während "Update-Lock zurücksetzen" gleichzeitig mit "kein aktiver Lock vorhanden" fehlschlägt, sollte dadurch nicht mehr dauerhaft bestehen bleiben — ein einfaches Neuladen der Seite (löst eine neue Statusabfrage aus) reicht in der Regel aus, um die Anzeige zu korrigieren. Bleibt der Widerspruch dennoch bestehen, mit den folgenden Schritten fortfahren.

**Lösung (Schritt für Schritt):**

1. **Installation läuft noch?** → Warten Sie
   - Prüfen Sie Dienst-Status auf dem Server: `systemctl status myapp` (Linux) oder Services (Windows)
   - Falls Dienst neu startet, warten Sie einige Minuten
   - Status aktualisieren (Browser-Seite neu laden) → sollte zu `NoUpdate` oder `Failed` wechseln

2. **Lock ist zu jung zum Reset?**
   - Status zeigt `Err_Update_Reset_LockNotStale` oder die lokalisierte Meldung "Der Update-Lock ist noch nicht alt genug und kann noch nicht zurückgesetzt werden."
   - Lock muss mindestens `HealthTimeoutSeconds` alt sein (Standard: 120 Sekunden)
   - Warten Sie, bis Lock alt genug ist, oder:
   - Erhöhen Sie `HealthTimeoutSeconds` in der Konfiguration und reduzieren Sie sie danach (workaround)

3. **Lock manuell zurücksetzen (wenn alt genug):**
   - Im Ribbon "Update-Lock zurücksetzen" klicken
   - System fragt nach Bestätigung und Grund
   - Geben Sie einen Grund ein (z. B. "Installer abgestürzt") und bestätigen
   - Lock sollte gelöscht und Status auf `NoUpdate` gesetzt werden
   - Falls der Reset abgelehnt wird, zeigt die UI jetzt den konkreten Grund: kein aktiver Lock, Lock noch nicht alt genug, Lock-Datei nicht löschbar oder technischer Reset-Fehler

4. **Reset-Meldung einordnen:**
   - `Err_Update_Reset_NoLock`: Status neu laden; wahrscheinlich ist der Lock bereits entfernt.
   - `Err_Update_Reset_LockNotStale`: Warten, bis der Lock mindestens `HealthTimeoutSeconds` alt ist.
   - `Err_Update_Reset_DeleteFailed`: Schreibrechte, Dateisperren und Eigentümer des Update-Verzeichnisses prüfen.
   - `Err_Update_Reset_Failed`: Server-Logs prüfen; dort stehen Fehlerart, Quelle und technische Ursache.

5. **Manuelles Löschen (Linux):**
   ```bash
   rm -f /var/lib/myapp/updates/update.lock
   ```
   Dann Browser aktualisieren

6. **Manuelles Löschen (Windows):**
   ```powershell
   Remove-Item -Path "C:\ProgramData\MyApp\updates\update.lock" -ErrorAction SilentlyContinue
   ```
   Dann Browser aktualisieren

---

## "No update package is available" — Installation kann nicht gestartet werden

**Symptom:** Die Ribbon-Aktion "Update installieren" ist deaktiviert; Status zeigt "No ready update package is available".

**Ursache:**
1. Noch kein Update heruntergeladen (Status ist nicht `Ready`)
2. Heruntergeladene Datei wurde gelöscht
3. Prüfung hat Fehler gefunden

**Lösung:**

1. **Status prüfen:**
   - Welcher Status wird angezeigt? (`Checking`, `NoUpdate`, `Failed`, ...)
   - Falls `Failed`: Welche `LastError`-Meldung wird gezeigt?

2. **Neue Prüfung auslösen:**
   - Im Ribbon "Jetzt prüfen" klicken
   - Warten Sie, bis Prüfung abgeschlossen ist (Status sollte wechseln)
   - Falls neuer verfügbar: Status sollte auf `Ready` gehen

3. **Falls Prüfung fehlschlägt:**
   - Bei "GitHub hat die Update-Pruefung wegen einer Rate-Limit-Begrenzung voruebergehend abgelehnt": später erneut prüfen; das öffentliche Repository kann weiterhin erreichbar sein, GitHub begrenzt aber anonyme API-Abfragen zeitweise
   - Manifest-Dateinamen prüfen: Das erwartete Release-Asset heißt fest `update.json`
   - Repository prüfen: Die Updatequelle ist fest `martin-stromberg/FinanceManager`
   - Prüfen ob GitHub-Release existiert und öffentlich zugänglich ist
   - Browser-Konsole öffnen (F12) → Network-Tab → Fehler beim Asset-Download?

4. **Server-Logs prüfen:**
   ```bash
   journalctl -u myapp -n 50  # Linux
   ```
   Suchen Sie nach `UpdateOrchestrator` oder `CheckAsync`-Logs

---

## Update zeigt nach Installation alte Version an

**Symptom:** Installation schließt erfolgreich ab (Lock ist weg), aber Status zeigt weiterhin alte Version, Status ist `Failed` mit `Err_Update_VersionMismatch`.

**Ursache:**
1. Installer-Skript konnte neue Version nicht bereitstellen
2. Dienst-Restart hat alte Version erneut gestartet
3. Versionserkennung funktioniert nicht korrekt

**Lösung:**

1. **Installer-Log prüfen (falls vorhanden):**
   - Linux: `/var/log/myapp/installer.log` (falls geschrieben)
   - Windows: Event Viewer → Application Logs

2. **Manuelle Versionsprüfung:**
   - Auf dem Server: `grep "version" /opt/app/.version` (Linux)
   - Oder: Prüfen Sie `AssemblyVersion` in der DLL
   - Oder: Schauen Sie nach `current-version` in `CLAUDE.md`

3. **Installer-Skript auf Korrektheit prüfen:**
   - Zippen-Befehl: Wird Datei wirklich in korrektes Verzeichnis entpackt?
   - Dienst-Restart: `systemctl restart myapp` (Linux) oder `Restart-Service` (Windows) erfolgreich?

4. **Fallback: Manuell beheben**
   - Alte Version manuell entfernen
   - Neue Version manuell bereitstellen/extrahieren
   - Dienst neu starten: `systemctl restart myapp`
   - Status-API aufrufen → sollte neue Version erkennen

---

## Installation hängt während "Warte auf Neustart" fest

**Symptom:** Status zeigt `Installing`, Progressanzeige ist bei "Neustart wird durchgeführt..." stecken geblieben, mehrere Minuten vergangen.

**Ursache:**
1. Dienst-Neustart schlägt fehl (Service kann nicht neu gestartet werden)
2. Dienst startet neu, aber Applikation dauert länger als `HealthTimeoutSeconds`
3. Netzwerk-Fehler: Health-Abfrage kann den Server nicht erreichen

**Lösung:**

1. **Dienst-Status auf dem Server prüfen:**
   ```bash
   systemctl status myapp  # Linux
   Get-Service -Name "MyApp-Service"  # Windows
   ```
   Startet der Dienst?

2. **Health-Endpoint testen:**
   ```bash
   curl http://localhost:5000/health
   ```
   Antwortet der Server?

3. **Health-Timeout prüfen (wenn Applikation einfach langsam startet):**
   - `HealthTimeoutSeconds` ist keine UI-Einstellung mehr.
   - Prüfen Sie die Serverkonfiguration `UpdateOptions.HealthTimeoutSeconds` und starten Sie die Anwendung nach einer Änderung neu.

4. **Installer-Prozess auf dem Server überprüfen:**
   ```bash
   ps aux | grep -E "unzip|dotnet|bash"  # Linux
   ```
   Läuft noch ein Installer-Prozess?

5. **Im Extremfall: Lock manuell zurücksetzen** (s. o.)
   - Installation abbrechen
   - Lock zurücksetzen
   - Server manual inspizieren
   - Ggf. Datensicherung wiederherstellen

---

## "No newer update is available" — Obwohl neue Version in GitHub existiert

**Symptom:** GitHub-Release mit neuer Version existiert, aber Status zeigt "No newer update is available".

**Ursache:**
1. Versionsnummern-Vergleich ist fehlgeschlagen
2. Asset für aktuelle Plattform nicht im Manifest vorhanden
3. Manifest-Dateiname stimmt nicht überein

**Lösung:**

1. **Manifest-Dateiname prüfen:**
   - Öffnen Sie GitHub Release
   - Suchen Sie nach `update.json`
   - Der Name muss exakt (Groß-/Kleinschreibung) übereinstimmen

2. **Asset-Namen für aktuelle Plattform prüfen:**
   - Status zeigt `CurrentPlatform` (z. B. `linux-x64`, `win-x64`)
   - Im Manifest sollte es ein entsprechendes Asset geben
   - z. B. `app-2.5.0-linux-x64.zip`

3. **Versionsnummern prüfen:**
   - Installierte Version: Status zeigt `InstalledVersion`
   - Verfügbare Version im Manifest: z. B. `2.5.0`
   - Versionsnummern müssen nutzbar mit `System.Version`-Parsing sein
   - Beispiel: `2.5.0` ist OK, `v2.5.0` ist nicht OK

4. **GitHub-Manifest direkt abrufen (zu Debug-Zwecken):**
   ```bash
   curl https://api.github.com/repos/martin-stromberg/FinanceManager/releases/latest
   ```
   Prüfen Sie JSON-Struktur und Asset-Namen

---

## Update-Prüfung meldet GitHub-Rate-Limit

**Symptom:** Status oder manueller Check zeigt eine Rate-Limit-/später-erneut-versuchen-Meldung.

**Ursache:**
- GitHub hat anonyme API-Abfragen für die aktuelle IP vorübergehend begrenzt.
- Das Repository kann trotzdem öffentlich und korrekt konfiguriert sein.
- Mehrere Instanzen oder häufige manuelle Checks können die Begrenzung schneller erreichen.

**Lösung:**

1. Später erneut prüfen.
2. **Background-Service überprüfen:**
   - Nur eine Instanz sollte laufen
   - Bei Mehrfach-Deployment prüfen, ob Background-Service richtig konfiguriert ist
3. Das automatische Prüfzeitfenster nutzen; die Hintergrundprüfung läuft täglich und nicht in frei konfigurierbaren Kurzintervallen.

---

## Ribbon-Aktion "Update-Lock zurücksetzen" ist deaktiviert

**Symptom:** Die Ribbon-Aktion "Update-Lock zurücksetzen" ist deaktiviert, obwohl Lock angezeigt wird.

**Ursache:**
- Lock ist zu jung (muss mindestens `HealthTimeoutSeconds` alt sein)
- Installation läuft noch aktiv

**Lösung:**

1. **Warten:**
   - Warten Sie bis Lock mindestens `HealthTimeoutSeconds` alt ist (Standard: 120 Sekunden)
   - Button sollte dann aktiviert werden

2. **Serverseitigen Health-Timeout prüfen:**
   - Der Timeout wird nicht mehr in der UI geändert.
   - Prüfen Sie `UpdateOptions.HealthTimeoutSeconds` in der Serverkonfiguration, wenn die Staleness-Schwelle dauerhaft unpassend ist.

---

## Service-Name-Autocomplete zeigt keine Vorschläge

**Symptom:** Im Feld "Service-Name" erscheinen keine Vorschläge.

**Ursache:**
1. Die Anwendung läuft nicht unter Windows oder Linux.
2. `sc.exe` oder `systemctl` ist nicht verfügbar.
3. Das Systemkommando liefert keine passenden Dienste oder läuft in ein Timeout.
4. Der eingegebene Suchtext filtert alle Treffer heraus.

**Lösung:**

1. Suchtext löschen und Feld erneut fokussieren.
2. Auf dem Server prüfen:
   ```bash
   systemctl list-units --type=service --all --no-legend --no-pager
   ```
   oder unter Windows:
   ```powershell
   sc.exe query type= service state= all
   ```
3. Den Service-Namen manuell eintragen, wenn keine Vorschläge verfügbar sind. Das Feld bleibt auch ohne Autocomplete nutzbar.

---

## Andere Fehlermeldungen

### `Err_Update_Locked`
**Bedeutung:** Ein Update-Lock ist aktiv.  
**Aktion:** Sehen Sie "An update lock is active" oben.

### `Err_Update_InstallRunning`
**Bedeutung:** Der lokale Prozess führt noch eine Installation durch.  
**Aktion:** Warten Sie, bis Installation abgeschlossen ist, oder starten Sie die Applikation neu.

### `Err_Update_Reset_NoLock`
**Bedeutung:** Es ist kein aktiver Update-Lock vorhanden.  
**Aktion:** Status aktualisieren; vermutlich wurde der Lock bereits entfernt.

### `Err_Update_Reset_LockNotStale`
**Bedeutung:** Der Update-Lock ist noch nicht alt genug für einen Reset.  
**Aktion:** Warten Sie mindestens bis zum serverseitigen `HealthTimeoutSeconds`-Schwellwert und versuchen Sie den Reset erneut.

### `Err_Update_Reset_DeleteFailed`
**Bedeutung:** Die Lock-Datei konnte nicht entfernt werden.  
**Aktion:** Schreibrechte, Dateisperren und Eigentümer des Update-Verzeichnisses auf dem Server prüfen.

### `Err_Update_Reset_Failed`
**Bedeutung:** Der Reset ist wegen eines sonstigen technischen Fehlers fehlgeschlagen.  
**Aktion:** Server-Logs prüfen; der Reset-Fehler wird mit Fehlerart, Quelle und technischer Ursache protokolliert.

### `Err_Update_NotReady`
**Bedeutung:** Kein bereites Update vorhanden.  
**Aktion:** Sehen Sie "No update package is available" oben.

### `Err_Update_InvalidState`
**Bedeutung:** Ungültiger Update-Status.  
**Aktion:** Status-JSON könnte beschädigt sein. Manuell prüfen: `/var/lib/myapp/updates/status.json`

### `Err_Update_VersionMismatch`
**Bedeutung:** Nach Update hat sich die Version nicht geändert.  
**Aktion:** Sehen Sie "Update zeigt nach Installation alte Version an" oben.

### `Err_Update_HealthTimeout`
**Bedeutung:** Health-Check-Timeout während Installation.  
**Aktion:** Sehen Sie "Installation hängt während Neustart fest" oben.
