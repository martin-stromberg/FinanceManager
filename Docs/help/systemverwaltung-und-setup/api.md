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

### `GET /api/admin/ip-blocks`

**Beschreibung:** IP-Blocklisten verwalten.

### `POST /api/backups`

**Beschreibung:** Backup erstellen.

### `POST /api/backups/{id}/apply/start`

**Beschreibung:** Restore als Hintergrundtask starten.

### `GET /api/notifications`

**Beschreibung:** Benachrichtigungen laden.

### `POST /api/background-tasks/aggregates/rebuild`

**Beschreibung:** Rebuild von Aggregaten starten.
