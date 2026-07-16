← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — API

## Übersicht

Dieser Bereich nutzt mehrere Controller für Authentifizierung, Administration, Benutzerpräferenzen, Benachrichtigungen und Betrieb.

## Endpunkte / Methoden

### `POST /api/auth/login`

**Beschreibung:** Anmeldung.

### `POST /api/auth/register`

**Beschreibung:** Registrierung.

### `GET /api/user-settings/profile`

**Beschreibung:** Profil laden.

### `PUT /api/user-settings/import-split`

**Beschreibung:** Import-Split-Einstellungen speichern.

### `GET /api/admin/users`

**Beschreibung:** Benutzerverwaltung.

**Berechtigung:** Die Admin-User-Management-Endpunkte unter `api/admin/users` erfordern serverseitig die Rolle `Admin`. Authentifizierte Nicht-Admins erhalten `403 Forbidden`.

### `GET /api/admin/ip-blocks`

**Beschreibung:** IP-Blocklisten verwalten.

### `POST /api/setup/backups`

**Beschreibung:** Backup erstellen.

### `POST /api/setup/backups/upload`

**Beschreibung:** ZIP-Backup hochladen.

**Request:** `multipart/form-data` mit Datei.

**Validierung:** Es werden nur ZIP-Dateien mit genau einem zulässigen NDJSON-Eintrag (`backup.ndjson` oder `backup-*.ndjson`) akzeptiert. Die Standardlimits sind 100 MB komprimiert, 250 MB entpackt, maximal ein ZIP-Entry und Kompressionsverhältnis höchstens 25. Die Backup-Metadaten müssen `Type = "Backup"` und `Version = 3` enthalten.

**Antworten:**
- `200 OK` mit `BackupDto` bei erfolgreichem Upload.
- `400 ApiErrorDto` bei leerer Datei, doppeltem Dateinamen oder ungültigem Backup-Format.

### `POST /api/setup/backups/{id}/apply`

**Beschreibung:** Backup synchron wiederherstellen. Der Restore ist destruktiv und ersetzt vorhandene Daten.

**Request-Body:** `BackupRestoreRequestDto`

```json
{
  "confirmationText": "backup-20260716120000.zip",
  "expectedFileName": "backup-20260716120000.zip"
}
```

`confirmationText` muss exakt dem gespeicherten Backup-Dateinamen entsprechen. `expectedFileName` ist optional, wird aber bei Angabe ebenfalls exakt gegen den gespeicherten Dateinamen geprüft.

**Antworten:**
- `204 No Content` bei erfolgreichem Restore.
- `404 Not Found`, wenn das Backup nicht existiert.
- `400 ApiErrorDto` bei fehlender/falscher Bestätigung, ungültiger Backup-Datei oder Importfehler.

### `POST /api/setup/backups/{id}/apply/start`

**Beschreibung:** Restore als Hintergrundtask starten. Der Task wird nur nach serverseitig erfolgreicher Dateinamen-Bestätigung angelegt.

**Request-Body:** `BackupRestoreRequestDto` wie bei `POST /api/setup/backups/{id}/apply`.

**Antworten:**
- `200 OK` mit `BackupRestoreStatusDto`, wenn der Restore-Task angelegt wurde.
- `404 Not Found`, wenn das Backup nicht existiert.
- `400 ApiErrorDto` bei fehlender/falscher Bestätigung.
- `409 ApiErrorDto`, wenn bereits ein Restore läuft oder in der Warteschlange steht.

### `GET /api/setup/backups/restore/status`

**Beschreibung:** Status des aktuellen oder letzten Restore-Hintergrundtasks abrufen.

### `POST /api/setup/backups/restore/cancel`

**Beschreibung:** Laufenden Restore-Hintergrundtask abbrechen.

### `GET /api/notifications`

**Beschreibung:** Benachrichtigungen laden.

### `POST /api/background-tasks/aggregates/rebuild`

**Beschreibung:** Rebuild von Aggregaten starten.
