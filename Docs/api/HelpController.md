# HelpController

Pfad: `FinanceManager.Web/Controllers/HelpController.cs`  
Route-Basis: `/api/help`

## Zweck

Liefert Hilfeseiten und Suchindex-Dateien für die In-App-Hilfe aus.

## Endpunkte

### HTML-Hilfeseite (Legacy)
- `GET /api/help/{language}/{featureId}.html`
- Response: `text/html`

### Markdown-Inhalt für Feature-Hilfe
- `GET /api/help/markdown/{language}/{featureId}`
- Response: `text/plain` (UTF-8)
- Quelle: `Docs/business/features/*.md`
- Entfernt YAML-Frontmatter vor Auslieferung

### Suchindex je Sprache
- `GET /api/help/search-index/{language}.json`
- Response: `application/json`
- Quelle: `wwwroot/help/{language}/search-index.json`

## Sicherheits- und Validierungshinweise

- Pfadparameter werden gegen Traversal-Muster (`..`, `/`) validiert.
- Bei nicht vorhandenen Dateien: `404 NotFound`.
- Bei Infrastrukturfehlern: `500 InternalServerError`.

## Referenzen

- Business-Dokumentation: `Docs/business/features/`
