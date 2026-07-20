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

**Symptom:** Button "Installation starten" ist deaktiviert; Status zeigt "A lock is active since [Zeit]".

**Ursache:**
1. Installation läuft noch (Installer-Skript ist aktiv)
2. Installation ist abgestürzt und Lock wurde nicht bereinigt (verwaister Lock)
3. In-Memory-Flag `IsInstallRunning` ist gesetzt, aber Prozess existiert nicht mehr

**Lösung (Schritt für Schritt):**

1. **Installation läuft noch?** → Warten Sie
   - Prüfen Sie Dienst-Status auf dem Server: `systemctl status myapp` (Linux) oder Services (Windows)
   - Falls Dienst neu startet, warten Sie einige Minuten
   - Status aktualisieren (Browser-Seite neu laden) → sollte zu `NoUpdate` oder `Failed` wechseln

2. **Lock ist zu jung zum Reset?**
   - Status zeigt "The update lock is not old enough to be considered stale"
   - Lock muss mindestens `HealthTimeoutSeconds` alt sein (Standard: 120 Sekunden)
   - Warten Sie, bis Lock alt genug ist, oder:
   - Erhöhen Sie `HealthTimeoutSeconds` in der Konfiguration und reduzieren Sie sie danach (workaround)

3. **Lock manuell zurücksetzen (wenn alt genug):**
   - Button "Reset Lock" klicken
   - System fragt nach Bestätigung und Grund
   - Geben Sie einen Grund ein (z. B. "Installer abgestürzt") und bestätigen
   - Lock sollte gelöscht und Status auf `NoUpdate` gesetzt werden

4. **Manuelles Löschen (Linux):**
   ```bash
   rm -f /var/lib/myapp/updates/update.lock
   ```
   Dann Browser aktualisieren

5. **Manuelles Löschen (Windows):**
   ```powershell
   Remove-Item -Path "C:\ProgramData\MyApp\updates\update.lock" -ErrorAction SilentlyContinue
   ```
   Dann Browser aktualisieren

---

## "No update package is available" — Installation kann nicht gestartet werden

**Symptom:** Button "Installation starten" ist deaktiviert; Status zeigt "No ready update package is available".

**Ursache:**
1. Noch kein Update heruntergeladen (Status ist nicht `Ready`)
2. Heruntergeladene Datei wurde gelöscht
3. Prüfung hat Fehler gefunden

**Lösung:**

1. **Status prüfen:**
   - Welcher Status wird angezeigt? (`Checking`, `NoUpdate`, `Failed`, ...)
   - Falls `Failed`: Welche `LastError`-Meldung wird gezeigt?

2. **Neue Prüfung auslösen:**
   - Button "Jetzt prüfen" klicken
   - Warten Sie, bis Prüfung abgeschlossen ist (Status sollte wechseln)
   - Falls neuer verfügbar: Status sollte auf `Ready` gehen

3. **Falls Prüfung fehlschlägt:**
   - GitHub-Credentials prüfen: `GITHUB_TOKEN` ist für private Repos erforderlich
   - Manifest-Dateinamen prüfen: `ManifestAssetName` muss genau dem Namen im Release-Asset entsprechen
   - Repository-Einstellungen prüfen: `RepositoryOwner`, `RepositoryName`
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

3. **HealthTimeoutSeconds erhöhen (wenn Applikation einfach langsam startet):**
   - Admin-UI: Update-Einstellungen
   - `HealthTimeoutSeconds` auf z. B. 180 oder 300 erhöhen
   - Speichern und erneut versuchen

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
   - Suchen Sie nach dem konfigurierten `ManifestAssetName`
   - Muss exakt (Groß-/Kleinschreibung) übereinstimmen

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
   curl -H "Authorization: token $GITHUB_TOKEN" \
     https://api.github.com/repos/my-org/my-app/releases/latest
   ```
   Prüfen Sie JSON-Struktur und Asset-Namen

---

## Zu häufige Update-Prüfungen (Server-Last)

**Symptom:** Server-Logs zeigen sehr viele Update-Check-Logs; `CheckIntervalMinutes` ist niedrig eingestellt.

**Ursache:** 
- Administrator hat `CheckIntervalMinutes` zu niedrig gesetzt (z. B. 1 Minute)
- Viele Background-Service-Instanzen prüfen parallel

**Lösung:**

1. **CheckIntervalMinutes erhöhen:**
   - Admin-UI: Update-Einstellungen
   - `CheckIntervalMinutes` auf vernünftigen Wert erhöhen (z. B. 60, 120, 1440)
   - Speichern

2. **Background-Service überprüfen:**
   - Nur eine Instanz sollte laufen
   - Bei Mehrfach-Deployment prüfen, ob Background-Service richtig konfiguriert ist

---

## Lock-Reset-Button ist deaktiviert

**Symptom:** Button "Reset Lock" ist grau/deaktiviert, obwohl Lock angezeigt wird.

**Ursache:**
- Lock ist zu jung (muss mindestens `HealthTimeoutSeconds` alt sein)
- Installation läuft noch aktiv

**Lösung:**

1. **Warten:**
   - Warten Sie bis Lock mindestens `HealthTimeoutSeconds` alt ist (Standard: 120 Sekunden)
   - Button sollte dann aktiviert werden

2. **HealthTimeoutSeconds reduzieren (temporär):**
   - Admin-UI: Update-Einstellungen
   - `HealthTimeoutSeconds` auf z. B. 30 reduzieren
   - Speichern
   - Button sollte jetzt aktiv sein
   - Lock zurücksetzen
   - `HealthTimeoutSeconds` wieder auf normal erhöhen

---

## Andere Fehlermeldungen

### `Err_Update_Locked`
**Bedeutung:** Ein Update-Lock ist aktiv.  
**Aktion:** Sehen Sie "An update lock is active" oben.

### `Err_Update_InstallRunning`
**Bedeutung:** Der lokale Prozess führt noch eine Installation durch.  
**Aktion:** Warten Sie, bis Installation abgeschlossen ist, oder starten Sie die Applikation neu.

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
