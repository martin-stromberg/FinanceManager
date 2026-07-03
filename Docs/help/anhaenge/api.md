← [Zurück zur Übersicht](index.md)

# Anhänge — API

## Übersicht

Die Schnittstelle wird über `AttachmentsController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/attachments/{entityKind}/{entityId}`

**Beschreibung:** Liefert Anhänge einer Entität.

### `POST /api/attachments/{entityKind}/{entityId}`

**Beschreibung:** Lädt Anhang hoch.

### `GET /api/attachments/{id}/download`

**Beschreibung:** Lädt Anhang herunter.

### `PUT /api/attachments/{id}`

**Beschreibung:** Aktualisiert Metadaten.

### `PUT /api/attachments/{id}/category`

**Beschreibung:** Setzt Kategorie.

### `GET /api/attachments/categories`

**Beschreibung:** Liefert Anhangkategorien.

### `POST /api/attachments/categories`

**Beschreibung:** Legt Anhangkategorie an.
