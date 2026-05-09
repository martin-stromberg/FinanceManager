# HelpController

Pfad: `FinanceManager.Web.Controllers.HelpController`

Öffentliche Endpunkte zum Ausliefern von Hilfeinhalten (HTML, Markdown, Search-Index).

---

## Endpunkt: Help-HTML laden (Legacy)

### 1) Übersicht
Lädt eine statische HTML-Hilfeseite für Sprache und Feature-ID.

### 2) HTTP-Methode & Pfad
`GET /api/help/{language}/{featureId}.html`

### 3) Authentifizierung
Keine Authentifizierung erforderlich.

### 4) Request
**Header**
- `Accept: text/html`

**Path-Parameter**
- `language` (`string`, required) – z. B. `de`, `en`
- `featureId` (`string`, required) – Feature-Key

**Request-Body**
- Kein Request-Body.

### 5) Response
**Erfolg (200 OK)**  
Beispiel-Payload (`text/html`):
```html
<html><body><h1>Hilfe</h1><p>Willkommen zur Funktionshilfe.</p></body></html>
```

**Fehlerfälle**
- `400 Bad Request`
```text
Language and featureId are required
```
- `401 Unauthorized` (aktuell nicht erwartet)
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found`
```text
Help page not found: de/portfolio.html
```
- `500 Internal Server Error`
```text
Error retrieving help page
```

### 6) Beispiel (`curl`)
```bash
curl -X GET "https://your-domain/api/help/de/portfolio.html" \
  -H "Accept: text/html"
```

Beispiel-Response:
```html
<html><body><h1>Hilfe</h1><p>Willkommen zur Funktionshilfe.</p></body></html>
```

---

## Endpunkt: Help-Markdown laden

### 1) Übersicht
Lädt den Markdown-Hilfetext eines Features. Die Auswahl erfolgt sprachabhängig mit Fallback-Logik.

### 2) HTTP-Methode & Pfad
`GET /api/help/markdown/{language}/{featureId}`

### 3) Authentifizierung
Keine Authentifizierung erforderlich.

### 4) Request
**Header**
- `Accept: text/plain`

**Path-Parameter**
- `language` (`string`, required) – z. B. `de`, `en`
- `featureId` (`string`, required) – Prefix der Dokumentdatei

**Request-Body**
- Kein Request-Body.

### 5) Response
**Erfolg (200 OK)**  
Beispiel-Payload (`text/plain`):
```text
# Portfolio

Diese Seite erklärt die Portfolio-Ansicht.
```

**Fehlerfälle**
- `400 Bad Request`
```text
Invalid characters in parameters
```
- `401 Unauthorized` (aktuell nicht erwartet)
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found`
```text
Documentation not found for feature: portfolio
```
- `500 Internal Server Error`
```text
Error retrieving markdown: <details>
```

### 6) Beispiel (`curl`)
```bash
curl -X GET "https://your-domain/api/help/markdown/de/portfolio" \
  -H "Accept: text/plain"
```

Beispiel-Response:
```text
# Portfolio

Diese Seite erklärt die Portfolio-Ansicht.
```

---

## Endpunkt: Search-Index laden

### 1) Übersicht
Lädt den sprachabhängigen JSON-Suchindex für die Hilfeoberfläche.

### 2) HTTP-Methode & Pfad
`GET /api/help/search-index/{language}.json`

### 3) Authentifizierung
Keine Authentifizierung erforderlich.

### 4) Request
**Header**
- `Accept: application/json`

**Path-Parameter**
- `language` (`string`, required) – z. B. `de`, `en`

**Request-Body**
- Kein Request-Body.

### 5) Response
**Erfolg (200 OK)**
```json
{
  "language": "de",
  "items": [
    {
      "id": "portfolio",
      "title": "Portfolio",
      "url": "/help/de/portfolio.html",
      "keywords": [
        "wertpapier",
        "depot"
      ]
    }
  ]
}
```

**Fehlerfälle**
- `400 Bad Request`
```text
Invalid language parameter
```
- `401 Unauthorized` (aktuell nicht erwartet)
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found`
```text
Search index not found for language: de
```
- `500 Internal Server Error`
```text
Error retrieving search index
```

### 6) Beispiel (`curl`)
```bash
curl -X GET "https://your-domain/api/help/search-index/de.json" \
  -H "Accept: application/json"
```

Beispiel-Response:
```json
{
  "language": "de",
  "items": [
    {
      "id": "portfolio",
      "title": "Portfolio",
      "url": "/help/de/portfolio.html",
      "keywords": [
        "wertpapier",
        "depot"
      ]
    }
  ]
}
```
