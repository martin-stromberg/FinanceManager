← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — API

## Übersicht

Die API für diesen Bereich wird primär über `AccountsController` und `PostingsController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/accounts`

**Beschreibung:** Liefert Konten des angemeldeten Benutzers.

### `POST /api/accounts`

**Beschreibung:** Legt ein Konto an. Sammelkonten können dabei direkt markiert werden.

### `PUT /api/accounts/{id}`

**Beschreibung:** Aktualisiert ein Konto inklusive Sammelkonto-Flag.

### `GET /api/accounts/{id}/linked-ibans`

**Beschreibung:** Liefert die verknüpften IBANs eines Sammelkontos.

### `POST /api/accounts/{id}/linked-ibans`

**Beschreibung:** Fügt eine verknüpfte IBAN hinzu.

### `DELETE /api/accounts/{id}/linked-ibans/{iban}`

**Beschreibung:** Entfernt eine verknüpfte IBAN.

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
