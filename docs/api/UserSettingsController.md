# UserSettingsController

Pfad: `FinanceManager.Web/Controllers/UserSettingsController.cs`  
Route-Basis: `/api/user/settings`

Der Controller verwaltet benutzerbezogene Einstellungen für Profil, Benachrichtigungen und Import-Splitting inkl. Dialog-Policy für den Massenimport.

## Gemeinsame Authentifizierung

Alle Endpunkte sind geschützt:
- `Authorization: Bearer <token>` *(required)*
- `Content-Type: application/json` *(required für PUT)*

---

## GET `/api/user/settings/profile`

### Übersicht
Liefert Profileinstellungen des aktuellen Users inkl. Sprache, Zeitzone und AlphaVantage-Key-Flags.

### Request
Keine Path-/Query-Parameter.

### Response
- `200 OK` mit `UserProfileSettingsDto`
```json
{
  "preferredLanguage": "de",
  "timeZoneId": "Europe/Berlin",
  "hasAlphaVantageApiKey": true,
  "shareAlphaVantageApiKey": false
}
```
- `401 Unauthorized`

### curl
```bash
curl -X GET "https://your-domain/api/user/settings/profile" \
  -H "Authorization: Bearer <token>"
```

---

## PUT `/api/user/settings/profile`

### Übersicht
Aktualisiert Sprache, Zeitzone und AlphaVantage-Key-Optionen. Bei Sprach-/Zeitzonenänderung wird das Auth-Cookie neu ausgestellt.

### Request
Body: `UserProfileSettingsUpdateRequest`
- `preferredLanguage` (`string | null`, max 10) *(optional)*
- `timeZoneId` (`string | null`, max 100) *(optional)*
- `alphaVantageApiKey` (`string | null`, max 120) *(optional)*
- `clearAlphaVantageApiKey` (`boolean | null`) *(optional)*
- `shareAlphaVantageApiKey` (`boolean | null`) *(optional, nur Admin kann `true` setzen)*

### Response
- `204 No Content`
- `400 Bad Request` (Validierung/Domain-Regeln)
- `403 Forbidden` (`shareAlphaVantageApiKey=true` ohne Admin-Rechte)
- `404 Not Found` (User nicht gefunden)
- `500 Internal Server Error`
- `401 Unauthorized`

### curl
```bash
curl -X PUT "https://your-domain/api/user/settings/profile" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "preferredLanguage":"de",
    "timeZoneId":"Europe/Berlin",
    "alphaVantageApiKey":null,
    "clearAlphaVantageApiKey":null,
    "shareAlphaVantageApiKey":false
  }'
```

---

## GET `/api/user/settings/notifications`

### Übersicht
Liefert Reminder- und Feiertagsprovider-Einstellungen des aktuellen Users.

### Request
Keine Path-/Query-Parameter.

### Response
- `200 OK` mit `NotificationSettingsDto`
```json
{
  "monthlyReminderEnabled": true,
  "monthlyReminderHour": 10,
  "monthlyReminderMinute": 30,
  "holidayProvider": "Memory",
  "holidayCountryCode": "DE",
  "holidaySubdivisionCode": "BW"
}
```
- `401 Unauthorized`

### curl
```bash
curl -X GET "https://your-domain/api/user/settings/notifications" \
  -H "Authorization: Bearer <token>"
```

---

## PUT `/api/user/settings/notifications`

### Übersicht
Aktualisiert Reminder-Zeit und Feiertagsregion.

### Request
Body: `UserNotificationSettingsUpdateRequest`
- `monthlyReminderEnabled` (`boolean`) *(required)*
- `monthlyReminderHour` (`int | null`, 0..23) *(optional)*
- `monthlyReminderMinute` (`int | null`, 0..59) *(optional)*
- `holidayProvider` (`string`) *(required)*
- `holidayCountryCode` (`string | null`, Länge 2..10) *(optional)*
- `holidaySubdivisionCode` (`string | null`, Länge 2..20) *(optional)*

### Response
- `204 No Content`
- `400 Bad Request` (inkl. ungültigem `holidayProvider`)
- `404 Not Found` (User nicht gefunden)
- `401 Unauthorized`

### curl
```bash
curl -X PUT "https://your-domain/api/user/settings/notifications" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "monthlyReminderEnabled":true,
    "monthlyReminderHour":10,
    "monthlyReminderMinute":30,
    "holidayProvider":"Memory",
    "holidayCountryCode":"DE",
    "holidaySubdivisionCode":"BW"
  }'
```

---

## GET `/api/user/settings/import-split`

### Übersicht
Liefert Import-Split-Konfiguration und die Dialog-Policy für Startseiten-Massenimporte.

### Request
Keine Path-/Query-Parameter.

### Response
- `200 OK` mit `ImportSplitSettingsDto`
```json
{
  "mode": "MonthlyOrFixed",
  "maxEntriesPerDraft": 250,
  "monthlySplitThreshold": 250,
  "minEntriesPerDraft": 8,
  "massImportDialogPolicy": "OnMissingInformation"
}
```
- `401 Unauthorized`

### curl
```bash
curl -X GET "https://your-domain/api/user/settings/import-split" \
  -H "Authorization: Bearer <token>"
```

---

## PUT `/api/user/settings/import-split`

### Übersicht
Aktualisiert Split-Logik für Draft-Erzeugung und legt fest, wann der Mass-Import-Dialog gezeigt wird.

### Request
Body: `ImportSplitSettingsUpdateRequest`
- `mode` (`ImportSplitMode`) *(required)*
- `maxEntriesPerDraft` (`int`, 20..10000) *(required)*
- `monthlySplitThreshold` (`int | null`) *(optional; bei `MonthlyOrFixed` muss Wert `>= maxEntriesPerDraft` sein)*
- `minEntriesPerDraft` (`int`, 1..10000) *(required; zusätzlich `<= maxEntriesPerDraft`)*
- `massImportDialogPolicy` (`AlwaysConfirm` | `OnMissingInformation`) *(required)*

### Response
- `204 No Content`
- `400 Bad Request` (DataAnnotation- oder Regelverletzungen)
- `404 Not Found` (User nicht gefunden)
- `401 Unauthorized`

### curl
```bash
curl -X PUT "https://your-domain/api/user/settings/import-split" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "mode":"FixedSize",
    "maxEntriesPerDraft":100,
    "monthlySplitThreshold":null,
    "minEntriesPerDraft":5,
    "massImportDialogPolicy":"AlwaysConfirm"
  }'
```

---

## Integration mit `POST /api/statement-drafts/mass-import`

Empfohlener Client-Flow:
1. `GET /api/user/settings/import-split` lesen und `massImportDialogPolicy` übernehmen.
2. Analyze-Call mit `confirmExecution=false`.
3. Wenn `requiresConfirmation=true`: Dialog zeigen, Entscheidungen sammeln.
4. Confirm-Call mit `confirmExecution=true` und `decisions` (pro `fileId`).

Siehe Details in [StatementDraftsController](./StatementDraftsController.md).

## Tests / Verifikation

- `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs`

