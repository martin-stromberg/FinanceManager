# Tests

## Vorhandene Teststruktur

Das Test-Projekt `FinanceManager.Tests` ist in Unterordner gegliedert, die den Produktiv-Namespaces entsprechen:

```
FinanceManager.Tests/
  Accounts/
  Aggregates/
  Attachments/
  Auth/
  Budget/
  Components/
  Contacts/
  Controllers/
  Domain/
  Infrastructure/
  Notifications/
  Reports/
  Savings/
  Securities/
  Shared/
  Statements/
  TestHelpers/
  Updates/
  ViewModels/
  Web/
```

### Relevante Unterordner (Referenz)

- `Controllers/` — Enthält Controller-Tests; zeigt das Muster für Unit-Tests von API-Controllern.
- `Infrastructure/` — Enthält Tests für Infrastructure-Services; zeigt, wie `SecurityTxtSettingsService` getestet werden soll.
- `TestHelpers/` — Hilfsmethoden und Factories für Testdaten; sollte für `SecurityTxtSettings`-Testdaten erweitert werden.

## Fehlende Tests

| Testklasse | Ablageort | Was soll getestet werden |
|------------|-----------|--------------------------|
| `SecurityTxtSettingsServiceTests` | `FinanceManager.Tests/Infrastructure/` | Korrekte RFC-9116-Serialisierung (PlainText, Markdown, Html), automatische `Canonical`-Befüllung, Pflichtfeld-Validierung |
| `SecurityTxtControllerTests` | `FinanceManager.Tests/Controllers/` | HTTP-200 ohne Authentifizierung für öffentliche Endpunkte; HTTP-401/403 für Admin-Endpunkte ohne Admin-Rolle |

## Integrations-/E2E-Tests

Vorhanden sind die Projekte `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E`. Für die security.txt-Feature fehlen:

- Integrationstests für alle fünf öffentlichen Endpunkte (`/security.txt`, `/.well-known/security.txt`, `/.well-known/security.md`, `/.well-known/security.html`) auf HTTP-200 ohne Auth.
- Integrationstests für `GET api/admin/security-txt` und `PUT api/admin/security-txt` auf korrekte Zugriffssteuerung.
