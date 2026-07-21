← [Zurück zur Übersicht](index.md)

# Automatische Updates — Beschreibung

## Zweck

Das Update-System automatisiert die Erkennung, den Download und die Installation von Programmaktualisierungen auf produktiven Servern. Der Administrator definiert ein GitHub-Repository mit Releases und erlaubt damit dem System, eigenständig nach Aktualisierungen zu prüfen und diese zu installieren, ohne manuelle Eingriffe auf dem Server erforderlich zu machen.

## Funktionsweise

Das System arbeitet in vier Phasen:

### 1. Automatische Prüfung (periodisch)
Der `UpdateOrchestrator` prüft in konfigurierten Intervallen (Standard: alle 60 Minuten), ob ein neueres Release im definierten GitHub-Repository verfügbar ist. Die Prüfung läuft im Hintergrund und schreibt den Status in eine lokal gespeicherte `status.json`-Datei.

### 2. Download vorbereiten
Sobald eine neuere Version erkannt wird, wird das entsprechende Asset (`.zip`-Archiv für die aktuelle Plattform) heruntergeladen und validiert. Der Status wird auf `Ready` gesetzt.

### 3. Installation durchführen
Der Administrator startet die Installation über die Web-Benutzeroberfläche und bestätigt eine erforderliche Ausfallzeit. Das System:
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
| `SetupUpdateTab.razor` | Web-UI für Administrator (Einstellungen, Prüfung, Installation, Lock-Reset) |
| `SetupUpdateViewModel` | ViewModel mit Polling-Logik für Live-Status-Updates während Installation |

## Beispiele

### Szenario: Regelmäßige Prüfung
1. Administrator aktiviert Updates in der Konfiguration und setzt Intervall auf 60 Minuten
2. Das System prüft alle 60 Minuten GitHub und findet Version 2.5.0 (aktuell installiert: 2.4.0)
3. Version 2.5.0 wird heruntergeladen und als `Ready` gekennzeichnet
4. Administrator wird benachrichtigt (Statusseite zeigt verfügbares Update)
5. Administrator klickt **Installation starten**, System wechselt zu `Installing`
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
