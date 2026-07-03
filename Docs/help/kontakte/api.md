← [Zurück zur Übersicht](index.md)

# Kontakte — API

## Übersicht

Die Kontaktfunktionen liegen in `ContactsController` und `ContactCategoriesController`.

## Endpunkte / Methoden

### `GET /api/contacts`

**Beschreibung:** Liefert Kontakte.

### `POST /api/contacts`

**Beschreibung:** Legt einen Kontakt an.

### `PUT /api/contacts/{id}`

**Beschreibung:** Aktualisiert einen Kontakt.

### `GET /api/contacts/{id}/aliases`

**Beschreibung:** Liefert Aliasnamen eines Kontakts.

### `POST /api/contacts/{id}/aliases`

**Beschreibung:** Fügt Alias hinzu.

### `POST /api/contacts/{id}/merge`

**Beschreibung:** Führt Kontakte zusammen.

### `GET /api/contact-categories`

**Beschreibung:** Liefert Kontaktkategorien.

## Fehler

Typische Fehler sind ungültige Kontakt-IDs und fehlende Berechtigungen auf fremde Kontakte.
