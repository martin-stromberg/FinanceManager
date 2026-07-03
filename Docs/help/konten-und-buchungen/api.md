← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — API

## Übersicht

Die API für diesen Bereich wird primär über `AccountsController` und `PostingsController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/accounts`

**Beschreibung:** Liefert Konten des angemeldeten Benutzers.

### `POST /api/accounts`

**Beschreibung:** Legt ein Konto an.

### `PUT /api/accounts/{id}`

**Beschreibung:** Aktualisiert ein Konto.

### `GET /api/postings/account/{accountId}`

**Beschreibung:** Liefert Buchungen zu einem Konto.

### `GET /api/postings/account/{accountId}/export`

**Beschreibung:** Exportiert Buchungen des Kontos.

### `POST /api/postings/{id}/reverse`

**Beschreibung:** Führt eine fachliche Stornierung durch.

### `GET /api/postings/{id}/validate-reversal`

**Beschreibung:** Prüft, ob eine Stornierung zulässig ist.

## Fehler

Typische Fehler sind ungültige IDs, fehlende Berechtigung und fachliche Validierungsfehler bei Stornierungen.
