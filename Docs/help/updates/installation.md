# Automatische Updates — Installation und Konfiguration

## Voraussetzungen

- ASP.NET Core 8.0+ (für die Web-Anwendung)
- GitHub-Repository mit Releases (als Update-Quelle)
- Admin-Berechtigungen zum Verwalten von Updates
- Auf Windows: Windows Service mit entsprechender Dienstkonfiguration
- Auf Linux: systemd-Service oder Daemon-Mechanismus für Dienst-Restart
- Auf Linux: Dienstbenutzer muss Berechtigungen zum Starten transienter systemd-Units besitzen (siehe Abschnitt „Dienstbenutzer-Berechtigungen“)

## Dienstbenutzer-Berechtigungen (Linux)

Damit der Installer-Prozess korrekt ausgeführt werden kann, muss der Dienstbenutzer in der Lage sein, systemd-run auszuführen und transient service units zu starten. Dies ist notwendig, weil der Installer-Prozess nicht direkt vom Host-Prozess ausgeführt wird, sondern über systemd-run als eigenständige Unit gestartet wird. Der Host-Prozess kann sich dadurch selbst beenden, während die Installation unabhängig weiterläuft.

Folgende Voraussetzungen müssen erfüllt sein:

1. Der Dienst läuft unter einem regulären Benutzerkonto (z. B. financemanager) mit gültiger Login-Shell wie /bin/bash.
   - Prüfung: grep <user> /etc/passwd
   - Falls notwendig: usermod -s /bin/bash <user>

2. Der Benutzer benötigt Berechtigungen, um systemd-Operationen auszuführen. Dies wird über eine Polkit-Regel ermöglicht, die Aktionen mit Präfix org.freedesktop.systemd1.* für diesen Benutzer erlaubt.
   - Datei erstellen unter /etc/polkit-1/rules.d/10-allow-systemd-run.rules
   - Inhalt: Polkit-Regel, die alle systemd1-Aktionen für den Dienstbenutzer erlaubt.
   ```
   polkit.addRule(function(action, subject) {
    if (subject.user == "financemanager" &&
        action.id.startsWith("org.freedesktop.systemd1.")) {
        return polkit.Result.YES;
    }
   });
   ```
   - Polkit neu laden: systemctl restart polkit

3. Der Benutzer benötigt Schreibrechte auf das Update-Verzeichnis (BaseDirectory), einschließlich Lock-Datei, Status-Datei und heruntergeladenen Assets.
   - Verzeichnis erstellen: mkdir -p <BaseDirectory>
   - Eigentümer setzen: chown <user>:<user> <BaseDirectory>
   - Schreibrechte sicherstellen: chmod 755 <BaseDirectory>

4. Der Benutzer muss das Installer-Skript ausführen können, einschließlich der darin enthaltenen Befehle wie unzip, rm, mv oder systemctl restart.
   - Sicherstellen, dass unzip installiert ist: apt install unzip oder dnf install unzip
   - Sicherstellen, dass das Skript ausführbar ist: chmod +x <Pfad zum Skript>
   - Der Benutzer benötigt Rechte für systemctl restart <ServiceName>. Dies wird durch die Polkit-Regel aus Punkt 2 abgedeckt.

5. Der systemd-Dienst der Anwendung muss so konfiguriert sein, dass der Dienstbenutzer Prozesse starten darf. Dies erfordert keine speziellen systemd-Optionen, solange systemd-run über Polkit freigeschaltet ist.
   - Dienstdatei prüfen: systemctl cat <ServiceName>
   - User=<user> muss gesetzt sein.
   - Optional: Delegate=yes kann gesetzt werden, ist aber nicht erforderlich, wenn systemd-run direkt aus dem Terminal oder aus der Anwendung heraus ausgeführt wird.

Wenn diese Voraussetzungen nicht erfüllt sind, wird der Installer-Prozess zwar als Unit angelegt, aber nicht ausgeführt. Die Ausgabe des Skripts erscheint ausschließlich im Journal der transienten Unit. In diesem Fall zeigt systemd-run lediglich die Meldung „Running as unit: <UnitName>.service“, ohne dass das Skript tatsächlich gestartet wird.

Zusammenfassung der notwendigen Konfigurationsbefehle:

- Benutzer-Shell sicherstellen: usermod -s /bin/bash <user>
- Polkit-Regel erstellen: Datei unter /etc/polkit-1/rules.d/10-allow-systemd-run.rules anlegen
- Polkit neu laden: systemctl restart polkit
- Update-Verzeichnis vorbereiten: mkdir -p <BaseDirectory>, chown <user>:<user> <BaseDirectory>, chmod 755 <BaseDirectory>
- Skript ausführbar machen: chmod +x <Pfad zum Skript>
- unzip installieren: apt install unzip oder dnf install unzip
- Dienstbenutzer in systemd-Dienstdatei setzen: User=<user>

Diese Konfiguration stellt sicher, dass der Installer-Prozess zuverlässig ausgeführt wird und die Anwendung nach der Installation korrekt neu gestartet werden kann.


## Installationsschritte

Das Update-System ist bereits im Projekt integriert. Es erfordert keine zusätzliche Installation, sondern nur Konfiguration.

1. **Abhängigkeiten in DI-Container registrieren** (in `Program.cs`):
   ```csharp
   services.AddUpdateServices(configuration);
   ```
   Registriert `IUpdateOrchestrator`, `IUpdateExecutor`, `UpdateFileStore` und alle Abhängigkeiten.

2. **Repository konfigurieren** (siehe Konfiguration unten)

3. **Update-Check-Background-Service starten**
   - Der Service `UpdateCheckBackgroundService` startet automatisch und prüft in konfigurierten Intervallen

4. **Web-UI aktivieren**
   - `SetupUpdateTab.razor` ist in der Admin-Setup-Seite integriert
   - Nur Admin-Benutzer können Updates verwalten

## Konfiguration

Update-Einstellungen werden in `appsettings.json` konfiguriert und können auch über die Web-UI überschrieben werden.

### appsettings.json

```json
{
  "UpdateOptions": {
    "BaseDirectory": "/opt/app/updates",
    "MaxAssetBytes": 536870912
  },
  "UpdateSettings": {
    "Enabled": true,
    "CheckIntervalMinutes": 60,
    "RepositoryOwner": "my-org",
    "RepositoryName": "my-app",
    "ManifestAssetName": "manifest.json",
    "ScheduledInstallTime": null,
    "ServiceName": "my-app-service",
    "ExecutablePath": "/opt/app/MyApp",
    "WorkingDirectory": "/opt/app",
    "HealthTimeoutSeconds": 120
  }
}
```

### Konfigurationsparameter

| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|----------|--------------|
| `UpdateOptions.BaseDirectory` | string | `/var/lib/myapp/updates` | Verzeichnis für Lock-, Status- und Asset-Dateien |
| `UpdateOptions.MaxAssetBytes` | int | 536 MB | Maximale Größe eines herunterladbaren Assets (verhindert DoS) |
| `UpdateSettings.Enabled` | bool | false | Aktiviert/deaktiviert automatische Prüfung |
| `UpdateSettings.CheckIntervalMinutes` | int | 60 | Prüf-Intervall in Minuten (1–1440, auf UI geclamped) |
| `UpdateSettings.RepositoryOwner` | string | — | GitHub-Organisation oder Benutzername |
| `UpdateSettings.RepositoryName` | string | — | GitHub-Repository-Name |
| `UpdateSettings.ManifestAssetName` | string | `manifest.json` | Name des Release-Assets mit Manifest |
| `UpdateSettings.ScheduledInstallTime` | time | null | Geplante Installationszeit (z. B. `"03:00:00"`) — derzeit nicht automatisiert |
| `UpdateSettings.ServiceName` | string | — | Windows Service-Name oder systemd-Service-Name |
| `UpdateSettings.ExecutablePath` | string | — | Pfad zur ausführbaren Datei (z. B. `/opt/app/MyApp` oder `C:\Program Files\App\MyApp.exe`) |
| `UpdateSettings.WorkingDirectory` | string | — | Arbeitsverzeichnis für Installer (z. B. `/opt/app`) |
| `UpdateSettings.HealthTimeoutSeconds` | int | 120 | Wartezeit für Dienst-Neustart (10–600 Sekunden, auf UI geclamped) |

### Manifest-Format (GitHub Release Asset)

Das `manifest.json`-Asset in GitHub Releases muss folgendes Format haben:

```json
{
  "version": "2.5.0",
  "publishedAt": "2026-07-20T08:00:00Z",
  "releaseNotes": "Bug fixes and performance improvements",
  "assets": [
    {
      "name": "app-2.5.0-linux-x64.zip",
      "assetName": "app-2.5.0-linux-x64.zip",
      "downloadUrl": "https://github.com/my-org/my-app/releases/download/v2.5.0/app-2.5.0-linux-x64.zip",
      "size": 12345678,
      "sha256": "abc123..."
    },
    {
      "name": "app-2.5.0-win-x64.zip",
      "assetName": "app-2.5.0-win-x64.zip",
      "downloadUrl": "https://github.com/my-org/my-app/releases/download/v2.5.0/app-2.5.0-win-x64.zip",
      "size": 13456789,
      "sha256": "def456..."
    }
  ]
}
```

Wichtig:
- `version` muss verwendbar mit `System.Version`-Vergleich sein (z. B. `2.5.0`, nicht `v2.5.0`)
- `assets` muss ein Asset für alle unterstützten Plattformen enthalten
- Asset-Namen folgen Konvention `app-{version}-{runtimeid}.zip`

## Umgebungsvariablen

| Variable | Pflicht | Beispiel | Beschreibung |
|----------|---------|----------|--------------|
| `UPDATE_BASE_DIR` | Nein | `/var/lib/myapp/updates` | Überschreibt `UpdateOptions.BaseDirectory` |
| `GITHUB_TOKEN` | Nein | `ghp_xxxxx` | GitHub Personal Access Token für private Repositories (optional) |

## Überprüfung nach Installation

1. **Web-UI öffnen:**
   - Navigiere zur Admin-Setup-Seite (`/admin/setup`)
   - Reiter "Update" sollte sichtbar sein
   - Als Admin-Benutzer: Einstellungen sollten konfigurierbar sein

2. **Manualle Prüfung auslösen:**
   - Button "Jetzt prüfen" klicken
   - System sollte GitHub-Release-Manifest laden
   - Verfügbares Update sollte angezeigt werden (falls neuer verfügbar)

3. **Logs prüfen:**
   - Bei Startup: `IUpdateOrchestrator` sollte in DI registriert sein
   - Bei Prüfung: Logging sollte Download und Validierung zeigen
   - Bei Fehler: `LastError` in Status-UI sollte Fehler anzeigen

4. **Konfiguration testen:**
   - `BaseDirectory` sollte existieren und schreibbar sein
   - Entsprechende Pfade für Service/Executable sollten korrekt sein

## Troubleshooting

### Status zeigt "Unbekannte Version"

**Ursache:** `InstalledReleaseMetadataProvider` kann Versionsnummer nicht auslesen.

**Lösung:**
- Sicherstellen, dass `.version`-Datei im WorkingDirectory existiert (wird von Installer geschrieben)
- Oder: `AssemblyVersion` ist nicht korrekt gesetzt
- Oder: `CLAUDE.md` hat keine `current-version`-Marker

### "An update lock is active" dauerhaft

**Ursache:** Lock-Datei existiert, aber Installation ist nicht aktiv.

**Lösung:**
1. Admin-UI öffnen, Button "Reset Lock" klicken (nur wenn Lock ≥ `HealthTimeoutSeconds` alt ist)
2. Oder manuell Lock-Datei löschen: `rm /var/lib/myapp/updates/update.lock`
3. Dann Status-UI aktualisieren

### "No ready update package is available"

**Ursache:** Update wurde heruntergeladen, aber nicht erfolgreich validiert.

**Lösung:**
1. "Jetzt prüfen" erneut ausführen
2. Prüfen ob `RepositoryOwner`, `RepositoryName`, `ManifestAssetName` korrekt konfiguriert sind
3. GitHub-Zugriffsrechte prüfen (ggf. `GITHUB_TOKEN` setzen)

### Installer-Prozess schlägt fehl (Windows)

**Ursache:** PowerShell-Skript konnte nicht ausgeführt werden.

**Lösung:**
1. PowerShell Execution Policy prüfen: `Get-ExecutionPolicy`
2. Bei Bedarf: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine`
3. Service-Konto prüfen: muss Schreibrechte auf `ExecutablePath` haben

### Installer-Prozess schlägt fehl (Linux)

**Ursache:** Bash-Skript konnte nicht ausgeführt werden oder `systemctl`-Befehl schlägt fehl.

**Lösung:**
1. Dateisystem-Schreibrechte prüfen: `ls -la /opt/app/`
2. Service-Name korrekt in Konfiguration? `systemctl list-units --type service | grep myapp`
3. Service-Benutzer hat Neustartrechte? Ggf. `sudoers` anpassen

## Hinweise zur systemd-run Integration

Der Installer-Prozess wird über systemd-run als transient service unit gestartet. Dadurch läuft die Installation unabhängig vom Host-Prozess weiter. Die Ausgabe des Skripts erscheint im Journal der Unit. Der Dienst kann nach Installation neu gestartet werden. Die Unit ist kurzlebig und wird nach Abschluss automatisch entfernt.

