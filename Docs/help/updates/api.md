← [Zurück zur Übersicht](index.md)

# Automatische Updates — API-Dokumentation

## Übersicht

Die Update-API wird von der Verwaltungs-UI (`SetupUpdateTab.razor`) verwendet, um Status abzurufen, Konfiguration zu speichern, Prüfungen durchzuführen und Installationen zu starten. Alle Endpunkte sind Admin-only und erfordern entsprechende Authentifizierung.

## Authentifizierung

Alle Endpunkte erfordern:
- Anmeldetoken (Cookie oder Bearer-Token) des angemeldeten Anwenders
- Rolle `Admin` (geprüft in `UpdateController`)

Fehlerresponse bei fehlender Admin-Rolle: HTTP 403 Forbidden mit `Access_AdminOnly`-Lokalisierer.

## Endpunkte

### `GET /api/updates/status`

**Beschreibung:** Aktuellen Update-Status auslesen (zuletzt geladene Version, verfügbare Update, Lock-Status, Fehler)

**Parameter:** Keine

**Rückgabe:** `UpdateStatusDto`

```json
{
  "status": "Ready",
  "installedVersion": "2.4.0",
  "installedPublishedAt": "2026-01-15T10:30:00Z",
  "availableVersion": "2.5.0",
  "currentPlatform": "linux-x64",
  "availableUpdate": {
    "version": "2.5.0",
    "publishedAt": "2026-07-20T08:00:00Z",
    "releaseNotes": "Bug fixes and improvements"
  },
  "isLocked": true,
  "lockCreatedAt": "2026-07-20T14:30:00Z",
  "downloadedAssetName": "app-2.5.0-linux-x64.zip",
  "lastError": null,
  "lastCheckedAt": "2026-07-20T14:25:00Z"
}
```

**Fehler:**

| Code | Ursache |
|------|---------|
| 403 | Anwender ist nicht Admin |
| 500 | Fehler beim Lesen der Statusdatei |

---

### `GET /api/updates/settings`

**Beschreibung:** Update-Konfiguration auslesen

**Parameter:** Keine

**Rückgabe:** `UpdateSettingsDto`

```json
{
  "enabled": true,
  "checkIntervalMinutes": 60,
  "repositoryOwner": "my-org",
  "repositoryName": "my-app",
  "manifestAssetName": "manifest.json",
  "scheduledInstallTime": "03:00:00",
  "serviceName": "my-app-service",
  "executablePath": "/opt/app/MyApp",
  "workingDirectory": "/opt/app",
  "healthTimeoutSeconds": 120
}
```

**Fehler:**

| Code | Ursache |
|------|---------|
| 403 | Anwender ist nicht Admin |
| 500 | Fehler beim Lesen der Konfiguration |

---

### `POST /api/updates/settings`

**Beschreibung:** Update-Konfiguration speichern

**Request-Body:** `UpdateSettingsUpdateRequest`

```json
{
  "enabled": true,
  "checkIntervalMinutes": 60,
  "repositoryOwner": "my-org",
  "repositoryName": "my-app",
  "manifestAssetName": "manifest.json",
  "scheduledInstallTime": "03:00:00",
  "serviceName": "my-app-service",
  "executablePath": "/opt/app/MyApp",
  "workingDirectory": "/opt/app",
  "healthTimeoutSeconds": 120
}
```

**Rückgabe:** `UpdateSettingsDto` (gespeicherte Konfiguration, mit Normalisierung)

**Validierung:**
- `CheckIntervalMinutes`: 1–1440 (geclamped)
- `HealthTimeoutSeconds`: 10–600 (geclamped)

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | Ungültige Eingabe |
| 403 | Anwender ist nicht Admin |
| 500 | Fehler beim Speichern |

---

### `POST /api/updates/schedule`

**Beschreibung:** Geplante Installationszeit setzen (für zukünftige geplante Installationen)

**Request-Body:** `TimeOnly?` (z. B. `"03:00:00"` oder `null` zum Deaktivieren)

**Rückgabe:** `UpdateSettingsDto` (aktualisierte Konfiguration)

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | Ungültige Zeitformat |
| 403 | Anwender ist nicht Admin |
| 500 | Fehler beim Speichern |

---

### `POST /api/updates/check`

**Beschreibung:** Sofortige Prüfung auf verfügbare Updates auslösen

**Parameter:** Keine

**Rückgabe:** `UpdateCheckResultDto`

```json
{
  "isUpdateAvailable": true,
  "status": {
    "status": "Ready",
    "installedVersion": "2.4.0",
    "availableVersion": "2.5.0",
    ...
  },
  "message": "Update package is ready."
}
```

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | Update ist nicht aktiviert |
| 403 | Anwender ist nicht Admin |
| 409 | Ein Lock ist bereits aktiv |
| 500 | Fehler beim Manifest-Download oder bei Asset-Validierung |

**Fehler-Response-Body:**

```json
{
  "origin": "UpdateController",
  "code": "Err_Update_Locked",
  "message": "An update lock is active."
}
```

---

### `POST /api/updates/install`

**Beschreibung:** Installation eines bereiten Updates starten

**Request-Body:** `UpdateStartRequest`

```json
{
  "confirmDowntime": true
}
```

**Parameter:**
- `confirmDowntime` (bool, erforderlich): Muss `true` sein, sonst wird Fehler 400 zurückgegeben

**Rückgabe:** `UpdateStatusDto` (Status mit `status: Installing`)

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | `confirmDowntime` nicht `true`, oder Status ist nicht `Ready` |
| 403 | Anwender ist nicht Admin |
| 404 | Kein bereites Update vorhanden |
| 409 | Lock ist bereits aktiv (`Err_Update_Locked`) |
| 500 | Fehler beim Starten des Installer-Prozesses |

**Fehler-Response-Body:**

```json
{
  "origin": "UpdateController",
  "code": "Err_Update_NotReady",
  "message": "No ready update package is available."
}
```

---

### `POST /api/updates/reset-lock`

**Beschreibung:** Update-Lock manuell zurücksetzen (nur bei verwaisten Locks)

**Request-Body:** `UpdateLockResetRequest`

```json
{
  "reason": "Installer abgestürzt, Lock ist zu alt"
}
```

**Parameter:**
- `reason` (string?, optional): Grund für den Reset (wird in Status-Fehler-Meldung dokumentiert)

**Rückgabe:** `UpdateStatusDto` (Status mit `isLocked: false`)

**Fehler:**

| Code | Ursache |
|------|---------|
| 403 | Anwender ist nicht Admin |
| 409 | Lock ist zu jung oder Installation läuft noch |
| 500 | Fehler beim Löschen der Lock-Datei |

**Fehler-Response-Body:**

```json
{
  "origin": "UpdateController",
  "code": "Err_Update_InstallRunning",
  "message": "The current process still owns an update installation."
}
```

## Fehler-Codes

Alle `ApiErrorDto.code`-Werte sind Lokalisierungsschlüssel:

| Code | Bedeutung | HTTP |
|------|-----------|------|
| `Err_Update_Locked` | Ein Update-Lock ist aktiv (Installation läuft oder ist stecken geblieben) | 409 |
| `Err_Update_InstallRunning` | Der lokale Prozess führt noch eine Installation durch | 409 |
| `Err_Update_NotReady` | Kein bereites Update vorhanden | 404 |
| `Err_Update_InvalidState` | Ungültiger Update-Zustand (z. B. Installer fehlgeschlagen) | 400 |
| `Err_Update_InvalidRequest` | Ungültige Anfrage-Parameter | 400 |
| `Err_Update_HealthTimeout` | Health-Check-Timeout während Installation | 500 |
| `Err_Update_VersionMismatch` | Neue Version nach Update nicht erkannt | 500 |

## DTOs

### `UpdateStatusDto`

```csharp
public class UpdateStatusDto
{
    public UpdateStatusKind Status { get; set; }           // NoUpdate, Checking, Available, Downloading, Ready, Installing, Failed
    public string? InstalledVersion { get; set; }          // aktuell geladene Version
    public DateTimeOffset? InstalledPublishedAt { get; set; }
    public string? AvailableVersion { get; set; }          // verfügbare Version (falls neuer)
    public string CurrentPlatform { get; set; }             // z. B. "linux-x64"
    public UpdateManifestDto? AvailableUpdate { get; set; } // vollständiges Manifest
    public bool IsLocked { get; set; }                      // Lock aktiv?
    public DateTimeOffset? LockCreatedAt { get; set; }      // Zeitpunkt der Lock-Erstellung
    public string? DownloadedAssetName { get; set; }        // Name des heruntergeladenen Archivs
    public string? LastError { get; set; }                  // Letzte Fehlermeldung (oder lokalisierter Code)
    public DateTimeOffset? LastCheckedAt { get; set; }      // Zeitpunkt der letzten Prüfung
}
```

### `UpdateSettingsDto`

```csharp
public class UpdateSettingsDto
{
    public bool Enabled { get; set; }
    public int CheckIntervalMinutes { get; set; }           // 1-1440
    public string RepositoryOwner { get; set; }
    public string RepositoryName { get; set; }
    public string ManifestAssetName { get; set; }
    public TimeOnly? ScheduledInstallTime { get; set; }
    public string ServiceName { get; set; }                 // Windows Service oder systemd-Dienst
    public string ExecutablePath { get; set; }
    public string WorkingDirectory { get; set; }
    public int HealthTimeoutSeconds { get; set; }           // 10-600
}
```

### `UpdateCheckResultDto`

```csharp
public class UpdateCheckResultDto
{
    public bool IsUpdateAvailable { get; set; }
    public UpdateStatusDto Status { get; set; }
    public string Message { get; set; }                      // Info-Meldung
}
```

### `UpdateStartRequest`

```csharp
public class UpdateStartRequest
{
    public bool ConfirmDowntime { get; set; }               // muss true sein
}
```

### `UpdateLockResetRequest`

```csharp
public class UpdateLockResetRequest
{
    public string? Reason { get; set; }
}
```

## Ereignisse und Benachrichtigungen

Das System persistiert den Status in `status.json`, benachrichtigt aber **nicht aktiv** über Webhooks oder Events. Die Web-UI pollt regelmäßig (alle 2–5 Sekunden während Installation) den Status und aktualisiert die Anzeige.

Geplante Installationen werden nicht vom System verwaltet; der Administrator kann sie manuell über die UI triggern.
