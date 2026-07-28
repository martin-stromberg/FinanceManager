← [Zurück zur Übersicht](index.md)

# Automatische Updates — Beschreibung

## Zweck

Das Update-System automatisiert die Erkennung, den Download und die Installation von Programmaktualisierungen auf produktiven Servern. Die Update-Quelle ist fest auf das GitHub-Repository `martin-stromberg/FinanceManager` und das Manifest-Asset `update.json` eingestellt. Administratoren steuern in der Oberfläche nur noch, ob geprüft wird, in welchem Intervall geprüft wird, welche Installationszeit vorgesehen ist und welcher Windows- oder Linux-Dienst neu gestartet werden soll.

## Funktionsweise

Das System arbeitet in vier Phasen:

### 1. Automatische Prüfung (periodisch)
Der `UpdateOrchestrator` prüft in konfigurierten Intervallen (Standard: alle 60 Minuten), ob ein neueres Release im definierten GitHub-Repository verfügbar ist. Die Prüfung läuft im Hintergrund und schreibt den Status in eine lokal gespeicherte `status.json`-Datei.

### 2. Download vorbereiten
Sobald eine neuere Version erkannt wird, wird das entsprechende Asset (`.zip`-Archiv für die aktuelle Plattform) heruntergeladen und validiert. Der Status wird auf `Ready` gesetzt.

### 3. Installation durchführen
Der Administrator startet die Installation über das Ribbon der Setup-Seite und bestätigt eine erforderliche Ausfallzeit. Das System:
- Erstellt einen Update-Lock, um parallele Installationen zu verhindern
- Generiert ein Installer-Skript (PowerShell unter Windows, Bash unter Linux)
- Startet das Skript als separaten Prozess
- Beendet den Anwendungsprozess
- Wartet auf Dienst-Neustart und Wiederherstellung

### 4. Validierung nach Neustart
Nach dem Neustart prüft das System, dass die neue Version tatsächlich geladen wurde. Stimmt die erkannte Version mit der Zielversion überein, ist das Update erfolgreich; andernfalls wird ein Fehler protokolliert.

## Key-Komponenten

| Komponente | Zweck |
|------------|-------|
| `UpdateOrchestrator` | Zentrale Orchestrierung: Lock-Verwaltung, Manifest-Abfragen, Installer-Aufruf |
| `UpdateExecutor` | Ausführung des Installer-Prozesses mit Lock-Management |
| `UpdateFileStore` | Persistierung von Lock-Dateien und Status-JSON |
| `SetupUpdateTab.razor` | Web-UI für Administrator (editierbare Update-Einstellungen, Status, Release-Informationen, Service-Autocomplete) |
| `SetupUpdateViewModel` | ViewModel mit Polling-Logik für Live-Status-Updates während Installation |

## Bedienung in der Setup-Oberfläche

Die Update-Sektion folgt dem allgemeinen Setup-Speicherverhalten. Änderungen an den verbleibenden Feldern werden nicht über einen eigenen Button im Update-Register gespeichert, sondern über den globalen Ribbon-Button **Speichern**.

Editierbar sind:
- Update-Prüfung aktiviert/deaktiviert
- Prüfintervall in Minuten
- geplante Installationszeit
- Service-Name

Der Service-Name bietet Autocomplete-Vorschläge aus den Diensten des aktuellen Systems. Unter Windows liest das System Windows-Dienste, unter Linux systemd-Services. Auf anderen Plattformen oder bei fehlenden Systemwerkzeugen bleibt die Vorschlagsliste leer.

Die Aktionen **Jetzt prüfen**, **Update installieren** und **Update-Lock zurücksetzen** sind im Ribbon der Setup-Seite verfügbar. **Update installieren** ist nur aktiv, wenn der Status `Ready` ist; **Update-Lock zurücksetzen** ist nur aktiv, wenn ein Lock gemeldet wird.

Die technischen Werte `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory`, `ExecutablePath` und `HealthTimeoutSeconds` werden Anwendern nicht mehr als Eingabefelder angezeigt. Beim Speichern normalisiert der Server Repository, Manifest und Arbeitsverzeichnis auf die festen Werte der Anwendung; der Health-Timeout kommt aus der Serverkonfiguration mit Fallback `120` Sekunden.

## Beispiele

### Szenario: Regelmäßige Prüfung
1. Administrator aktiviert Updates in der Konfiguration und setzt Intervall auf 60 Minuten
2. Das System prüft alle 60 Minuten GitHub und findet Version 2.5.0 (aktuell installiert: 2.4.0)
3. Version 2.5.0 wird heruntergeladen und als `Ready` gekennzeichnet
4. Administrator wird benachrichtigt (Statusseite zeigt verfügbares Update)
5. Administrator klickt im Ribbon **Update installieren**, bestätigt die Downtime, System wechselt zu `Installing`
6. Nach Neustart prüft das System Versionsnummer und bestätigt Erfolg

### Szenario: Lock-Recovery nach Fehler
1. Installation startet, aber Installer-Prozess bricht ab (z. B. Datei-Zugriff fehlgeschlagen)
2. Lock wird automatisch bereinigt, In-Memory-Flag zurückgesetzt
3. Update-Status wechselt auf `Failed` mit Fehlermeldung
4. Administrator kann Lock-Reset-Button drücken oder nächste Installation versuchen

## Einschränkungen

- **Nur GitHub-Releases**: Manifeste müssen in GitHub-Releases verfügbar sein
- **Keine automatische Installation**: Installation erfordert Admin-Bestätigung über Web-UI
- **Ein Lock pro System**: Nur eine Installation gleichzeitig (Lock verhindert Parallelität)
- **Keine Rollback-Automatik**: Fehlerhafte Updates müssen manuell rückgängig gemacht werden
- **Plattformspezifisches Asset**: System prüft `RuntimeIdentifier` (z. B. `linux-x64`, `win-x64`) und wählt passendes Asset
