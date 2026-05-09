# SecuritiesController

Pfad: `FinanceManager.Web.Controllers.SecuritiesController`

Diese Dokumentation fokussiert auf die API-relevanten Endpunkte für Kursabruf-Fehlerbehandlung, Backfill und die im DTO sichtbaren Fehlerfelder – inklusive Feature **Backfill-Fehlerbenachrichtigung**.

Verwandte Endpunkte:
- [BackgroundTasksController](./BackgroundTasksController.md)
- [NotificationsController](./NotificationsController.md)

---

## Feature-Update: AlphaVantage `PriceProviderException` (Analyse/Fix)

Die Ursache im AlphaVantage-Pfad wurde für `TIME_SERIES_DAILY` analysiert und im Fehler-Mapping präzisiert:  
`Invalid API call` wird nun deterministisch als `INVALID_SYMBOL_OR_FUNCTION` klassifiziert.

Zusätzlich wurden Logging und Retry-Verhalten konkretisiert:
- Strukturiertes Logging mit Sanitizing (inkl. `apikey=***`, keine Schlüssel-Leaks in Logs/Exceptions).
- Verbesserte Fehlerklassifikation für Provider-/Transport-Fehler.
- Retry nur für transiente Fehler; kein Retry bei `RATE_LIMIT` oder `INVALID_SYMBOL_OR_FUNCTION`.

---

## Endpunkt: Securities auflisten

### 1) Übersicht
Liefert alle Wertpapiere des aktuellen Benutzers. Der Response enthält den aktuellen Kursabruf-Fehlerstatus pro Security.

### 2) HTTP-Methode & Pfad
`GET /api/securities`

### 3) Authentifizierung
Bearer Token erforderlich.

### 4) Request
**Header**
- `Authorization: Bearer <JWT>`
- `Accept: application/json`

**Query-Parameter**
- `onlyActive` (`boolean`, optional, default `true`) – nur aktive Securities zurückgeben.

**Request-Body**
- Kein Request-Body.

### 5) Response
**Erfolg (200 OK)**  
Vollständiges Beispiel:
```json
[
  {
    "id": "2b36b462-e071-4bc5-8da9-e95f04843d4c",
    "name": "Microsoft Corp.",
    "description": "US Equity",
    "identifier": "US5949181045",
    "alphaVantageCode": "MSFT",
    "currencyCode": "USD",
    "categoryId": "59071a23-5cd3-4b70-a0f5-62057fd019a6",
    "categoryName": "Aktien",
    "isActive": true,
    "createdUtc": "2026-03-10T09:15:22Z",
    "archivedUtc": null,
    "symbolAttachmentId": null,
    "hasPriceError": true,
    "priceErrorClass": "INVALID_SYMBOL_OR_FUNCTION",
    "priceErrorMessage": "Für 'Microsoft Corp.' (US5949181045) konnte kein Kurs geladen werden (2026-03-10 09:21 UTC). Bitte Symbol prüfen, speichern und anschließend den Abruf erneut starten.",
    "priceErrorSinceUtc": "2026-03-10T09:21:03Z"
  }
]
```

**Fehlerfälle**
- `400 Bad Request` (ungültige Parameter)
```json
{
  "origin": "API_Securities",
  "code": "Err_InvalidArgument",
  "message": "Invalid request."
}
```
- `401 Unauthorized`
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found` (nicht erwartet bei Listen-Endpoint, Referenz für generische Fehlerbehandlung)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Resource not found."
}
```
- `500 Internal Server Error`
```json
{
  "origin": "API_Securities",
  "code": "Err_Unexpected",
  "message": "Unexpected error"
}
```

### 6) Beispiel (`curl`)
```bash
curl -X GET "https://your-domain/api/securities?onlyActive=true" \
  -H "Authorization: Bearer <JWT>" \
  -H "Accept: application/json"
```

Beispiel-Response:
```json
[
  {
    "id": "2b36b462-e071-4bc5-8da9-e95f04843d4c",
    "name": "Microsoft Corp.",
    "description": "US Equity",
    "identifier": "US5949181045",
    "alphaVantageCode": "MSFT",
    "currencyCode": "USD",
    "categoryId": "59071a23-5cd3-4b70-a0f5-62057fd019a6",
    "categoryName": "Aktien",
    "isActive": true,
    "createdUtc": "2026-03-10T09:15:22Z",
    "archivedUtc": null,
    "symbolAttachmentId": null,
    "hasPriceError": true,
    "priceErrorClass": "INVALID_SYMBOL_OR_FUNCTION",
    "priceErrorMessage": "Für 'Microsoft Corp.' (US5949181045) konnte kein Kurs geladen werden (2026-03-10 09:21 UTC). Bitte Symbol prüfen, speichern und anschließend den Abruf erneut starten.",
    "priceErrorSinceUtc": "2026-03-10T09:21:03Z"
  }
]
```

---

## Endpunkt: Security nach ID laden

### 1) Übersicht
Liefert ein einzelnes Wertpapier des aktuellen Benutzers inklusive Kursabruf-Fehlerzustand.

### 2) HTTP-Methode & Pfad
`GET /api/securities/{id}`

### 3) Authentifizierung
Bearer Token erforderlich.

### 4) Request
**Header**
- `Authorization: Bearer <JWT>`
- `Accept: application/json`

**Path-Parameter**
- `id` (`uuid`, required) – Security-ID.

**Request-Body**
- Kein Request-Body.

### 5) Response
**Erfolg (200 OK)**
```json
{
  "id": "2b36b462-e071-4bc5-8da9-e95f04843d4c",
  "name": "Microsoft Corp.",
  "description": "US Equity",
  "identifier": "US5949181045",
  "alphaVantageCode": "MSFT",
  "currencyCode": "USD",
  "categoryId": "59071a23-5cd3-4b70-a0f5-62057fd019a6",
  "categoryName": "Aktien",
  "isActive": true,
  "createdUtc": "2026-03-10T09:15:22Z",
  "archivedUtc": null,
  "symbolAttachmentId": null,
  "hasPriceError": false,
  "priceErrorClass": null,
  "priceErrorMessage": null,
  "priceErrorSinceUtc": null
}
```

**Fehlerfälle**
- `400 Bad Request`
```json
{
  "origin": "API_Securities",
  "code": "Err_Invalid_id",
  "message": "The value is invalid."
}
```
- `401 Unauthorized`
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `404 Not Found`
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Security not found."
}
```
- `500 Internal Server Error`
```json
{
  "origin": "API_Securities",
  "code": "Err_Unexpected",
  "message": "Unexpected error"
}
```

### 6) Beispiel (`curl`)
```bash
curl -X GET "https://your-domain/api/securities/2b36b462-e071-4bc5-8da9-e95f04843d4c" \
  -H "Authorization: Bearer <JWT>" \
  -H "Accept: application/json"
```

Beispiel-Response:
```json
{
  "id": "2b36b462-e071-4bc5-8da9-e95f04843d4c",
  "name": "Microsoft Corp.",
  "description": "US Equity",
  "identifier": "US5949181045",
  "alphaVantageCode": "MSFT",
  "currencyCode": "USD",
  "categoryId": "59071a23-5cd3-4b70-a0f5-62057fd019a6",
  "categoryName": "Aktien",
  "isActive": true,
  "createdUtc": "2026-03-10T09:15:22Z",
  "archivedUtc": null,
  "symbolAttachmentId": null,
  "hasPriceError": false,
  "priceErrorClass": null,
  "priceErrorMessage": null,
  "priceErrorSinceUtc": null
}
```

---

## Endpunkt: Kurs-Backfill als Background Task starten

### 1) Übersicht
Startet den asynchronen Backfill für historische Kurse. Der Endpunkt enqueued nur den Task; Fortschritt, Fehlerzustand und Abschluss werden über [BackgroundTasksController](./BackgroundTasksController.md) abgefragt.

Notification-semantik des **ausgeführten** Backfill-Tasks:
- Bei `PriceProviderException` mit Klassen **außer** `RATE_LIMIT` wird `SetPriceErrorAsync(...)` aufgerufen.
- User-Notification wird nur für `INVALID_SYMBOL_OR_FUNCTION` und `UNKNOWN_PROVIDER_ERROR` erstellt.
- Für `TRANSIENT_NETWORK` wird **keine** User-Notification erstellt.
- Bei `RATE_LIMIT` wird der Task mit Exception abgebrochen (kein `SetPriceErrorAsync`, keine Notification).

### 2) HTTP-Methode & Pfad
`POST /api/securities/backfill`

### 3) Authentifizierung
Bearer Token erforderlich.

### 4) Request
**Header**
- `Authorization: Bearer <JWT>`
- `Content-Type: application/json`

**Path-/Query-Parameter**
- keine

**Request-Body**
- `securityId` (`uuid`, optional) – nur dieses Wertpapier backfillen.
- `fromDateUtc` (`string(date-time)`, optional) – Startdatum.
- `toDateUtc` (`string(date-time)`, optional) – Enddatum.

Hinweise zur Ausführung:
- Alle Felder sind optional (Record `SecurityBackfillRequest(Guid? SecurityId, DateTime? FromDateUtc, DateTime? ToDateUtc)`).
- Der Task wird mit `allowDuplicate: false` enqueued. Existiert für denselben User bereits ein `Queued`/`Running`-Backfill-Task, wird dieser bestehende Task zurückgegeben.

Beispiel:
```json
{
  "securityId": "2b36b462-e071-4bc5-8da9-e95f04843d4c",
  "fromDateUtc": "2024-01-01T00:00:00Z",
  "toDateUtc": "2024-12-31T00:00:00Z"
}
```

### 5) Response
**Erfolg (200 OK)**
```json
{
  "id": "c7b9d97a-80e8-4d7f-b4e3-3f1a5e8f7cc1",
  "type": "SecurityPricesBackfill",
  "userId": "d754ce4b-49e9-4606-8739-812fd2d4dcf4",
  "enqueuedUtc": "2026-03-10T09:40:00Z",
  "status": "Queued",
  "processed": 0,
  "total": 0,
  "message": "Queued",
  "warnings": 0,
  "errors": 0,
  "errorDetail": null,
  "startedUtc": null,
  "finishedUtc": null,
  "payload": "{\"SecurityId\":\"2b36b462-e071-4bc5-8da9-e95f04843d4c\",\"FromDateUtc\":\"2024-01-01T00:00:00.0000000Z\",\"ToDateUtc\":\"2024-12-31T00:00:00.0000000Z\"}",
  "processed2": null,
  "total2": null,
  "message2": null
}
```

**Fehlerfälle**
- `400 Bad Request`
```json
{
  "origin": "API_Securities",
  "code": "Err_InvalidArgument",
  "message": "Invalid request."
}
```
- `401 Unauthorized`
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication required."
}
```
- `500 Internal Server Error`
```json
{
  "origin": "API_Securities",
  "code": "Err_Unexpected",
  "message": "Unexpected error"
}
```

### 6) Beispiel (`curl`)
```bash
curl -X POST "https://your-domain/api/securities/backfill" \
  -H "Authorization: Bearer <JWT>" \
  -H "Content-Type: application/json" \
  -d "{\"securityId\":\"2b36b462-e071-4bc5-8da9-e95f04843d4c\",\"fromDateUtc\":\"2024-01-01T00:00:00Z\",\"toDateUtc\":\"2024-12-31T00:00:00Z\"}"
```

Beispiel-Response:
```json
{
  "id": "c7b9d97a-80e8-4d7f-b4e3-3f1a5e8f7cc1",
  "type": "SecurityPricesBackfill",
  "userId": "d754ce4b-49e9-4606-8739-812fd2d4dcf4",
  "enqueuedUtc": "2026-03-10T09:40:00Z",
  "status": "Queued",
  "processed": 0,
  "total": 0,
  "message": "Queued",
  "warnings": 0,
  "errors": 0,
  "errorDetail": null,
  "startedUtc": null,
  "finishedUtc": null,
  "payload": "{\"SecurityId\":\"2b36b462-e071-4bc5-8da9-e95f04843d4c\",\"FromDateUtc\":\"2024-01-01T00:00:00.0000000Z\",\"ToDateUtc\":\"2024-12-31T00:00:00.0000000Z\"}",
  "processed2": null,
  "total2": null,
  "message2": null
}
```

---

## Kursabruf-Fehlerklassifikation und Semantik

Stabile Fehlercodes:
- `INVALID_SYMBOL_OR_FUNCTION`
- `RATE_LIMIT`
- `TRANSIENT_NETWORK`
- `UNKNOWN_PROVIDER_ERROR`

Spezialfall AlphaVantage:
- Wenn Provider `Error Message` mit `Invalid API call` **und** `TIME_SERIES_DAILY` liefert, wird nach `INVALID_SYMBOL_OR_FUNCTION` klassifiziert.
- Andere `Error Message`-Varianten werden als `UNKNOWN_PROVIDER_ERROR` klassifiziert.

Strukturiertes Logging & Sanitizing:
- Provider-Texte werden vor Persistenz/Logging sanitisiert (Control-Chars entfernt, Länge begrenzt).
- API-Keys in URLs und Provider-Texten werden maskiert (`apikey=***`).
- Nutzerhinweise enthalten keine rohen Providertexte.

Retry-Verhalten (`AlphaVantagePriceProvider`):
- **Transient (`TRANSIENT_NETWORK`)**: Retry mit Backoff; bei fortgesetztem Fehler Exception nach ausgeschöpften Versuchen.
- **Rate-Limit (`RATE_LIMIT`)**: kein Retry, sofortiger kontrollierter Abbruchpfad.
- **Invalid Symbol/Function (`INVALID_SYMBOL_OR_FUNCTION`)**: kein Retry, persistenter fachlicher Fehlerstatus.

Worker-Verhalten:
- Bei `RATE_LIMIT`: Lauf wird sofort beendet (weitere Securities im aktuellen Run werden nicht verarbeitet).
- Bei `TRANSIENT_NETWORK`: kein persistenter Fehler am Security-Objekt; Worker fährt mit nächster Security fort.
- Bei `INVALID_SYMBOL_OR_FUNCTION` und `UNKNOWN_PROVIDER_ERROR`: Fehler wird persistiert, sichere User-Notification wird erstellt, Worker fährt mit nächster Security fort.

Backfill-Verhalten (`SecurityPricesBackfillExecutor`):
- Verarbeitet nur aktive Securities mit `AlphaVantageCode` und ohne aktiven Preisfehler (`HasPriceError == false`).
- Bei `RATE_LIMIT`: Backfill bricht sofort mit `PriceProviderException` ab; keine Error-Persistenz, keine Notification.
- Bei `TRANSIENT_NETWORK`: Fehlerstatus wird via `SetPriceErrorAsync(...)` persistiert; keine Notification; Verarbeitung läuft mit nächster Security weiter.
- Bei `INVALID_SYMBOL_OR_FUNCTION` und `UNKNOWN_PROVIDER_ERROR`: Fehlerstatus wird persistiert **und** Notification wird erstellt:
  - `title`: `Kursabruf fehlgeschlagen`
  - `type`: `SystemAlert`
  - `target`: `HomePage`
  - `scheduledDateUtc`: `DateTime.UtcNow.Date`
  - `trigger`: `security:error:{securityId}`

User-sichere Hinweise:
- `priceErrorMessage` enthält ausschließlich sichere, lokalisierte Hinweise.
- Roher Provider-Text wird **nicht** in User-Notifications übernommen.

Persistierte Fehlerdetails:

| Feld | Bedeutung | API sichtbar |
|---|---|---|
| `hasPriceError` | Aktiver Fehlerstatus | Ja (`SecurityDto`) |
| `priceErrorClass` | Stabiler Maschinen-Code | Ja (`SecurityDto`) |
| `priceErrorMessage` | Sichere User-Zusammenfassung | Ja (`SecurityDto`) |
| `priceErrorSinceUtc` | Startzeitpunkt des aktiven Fehlers | Ja (`SecurityDto`) |
| `priceErrorProviderMessage` | Internes, sanitisiertes Provider-Detail (max. 2000 Zeichen, ohne Control-Chars) | Nein (nur Persistenz/Diagnostik) |

---

## Testreferenzen

- `FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs`
- `FinanceManager.Tests/Web/Services/AlphaVantagePriceProviderRetryTests.cs`
- `FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs`
- `FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs`
- `FinanceManager.Tests/Web/Services/SecurityPricesBackfillExecutorNotificationTests.cs`
- `FinanceManager.Tests/Web/Services/SecurityPriceProviderErrorUserMessageBuilderTests.cs`

Die genannten Tests sind für das Feature erweitert und grün ausgeführt (Klassifikation, Sanitizing, Retry, Worker-/Backfill-Verhalten, Notification- und User-Message-Semantik).

## Querverweise (Flows/Business/Tests/Lifecycle)

- Flow: [SecurityPriceWorker – Kursabruf & Fehlerpfade](../flows/security-price-worker.md)
- Business: [F007 – Wertpapierpreise](../business/features/F007-wertpapierpreise.md)
- Business (Infra): [F007 – Wertpapierpreise (Infrastructure-Perspektive)](../business/features/F007-wertpapierpreise-infrastructure.md)
- Tests: [`FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs`](../../FinanceManager.Tests/Web/Services/AlphaVantageErrorHandlingTests.cs)
- Planung (Backfill-Notification): [../security-price-backfill-notification-planning-overview.md](../security-price-backfill-notification-planning-overview.md)
- Requirement (Backfill-Notification): [../requirements/security-price-backfill-notification-alignment.md](../requirements/security-price-backfill-notification-alignment.md)
- Dokumentations-/Lifecycle-Report: [Documentation Plan – AlphaVantage PriceProviderException Fix](../documentation-plan.md)
