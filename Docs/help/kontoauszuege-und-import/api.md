← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — API

## Übersicht

Die Schnittstelle wird über `StatementDraftsController` und `StatementDraftEntriesController` bereitgestellt.

## Endpunkte / Methoden

### `POST /api/statement-drafts/upload`

**Beschreibung:** Upload einer Datei und Erzeugung eines Entwurfs.

### `POST /api/statement-drafts/mass-import`

**Beschreibung:** Massenimport mehrerer Dateien.

### `POST /api/statement-drafts/{draftId}/classify`

**Beschreibung:** Startet Klassifikation für einen Entwurf.

### `GET /api/statement-drafts/{draftId}/validate`

**Beschreibung:** Liefert Validierungsergebnis.

### `POST /api/statement-drafts/{draftId}/book`

**Beschreibung:** Verbucht den Entwurf.

### `POST /api/statement-drafts/{draftId}/entries/{entryId}/book`

**Beschreibung:** Verbucht eine einzelne Entwurfszeile.

### `POST /api/statement-draft-entries/batch-update`

**Beschreibung:** Führt Batch-Änderungen auf Entwurfszeilen aus.

## Fehler

Typische Fehler: ungültige Draft-ID, fehlender Kontokontext, unvollständige Zuordnung, Validierungsfehler.
