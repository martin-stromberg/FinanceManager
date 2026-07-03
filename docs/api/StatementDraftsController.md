# StatementDraftsController

Pfad: `FinanceManager.Web/Controllers/StatementDraftsController.cs`  
Route-Basis: `/api/statement-drafts`

Diese Dokumentation beschreibt den Endpunkt für den Massenimport von Kontoauszügen und ING-Wertpapierkursen.

## Endpunkt: Mass Import

### Übersicht
`POST /api/statement-drafts/mass-import` analysiert mehrere Dateien in einem Batch und führt – je nach Dialog-Policy und `ConfirmExecution` – direkt den Import aus oder liefert einen bestätigungspflichtigen Analysezustand zurück.

### HTTP-Methode & Pfad
`POST /api/statement-drafts/mass-import`

### Authentifizierung
`Authorization: Bearer <token>` (JWT, geschützter Endpunkt).

### Request

#### Header
- `Content-Type: application/json` *(required)*
- `Authorization: Bearer <token>` *(required)*

#### Body (`MassImportBatchRequestDto`)
- `dialogPolicy` (`AlwaysConfirm` | `OnMissingInformation`) *(required)*
- `confirmExecution` (`boolean`) *(required)*
- `files` (`MassImportFileUploadDto[]`) *(required, mind. 1 Element)*
  - `fileId` (`uuid`) *(required, stabil zwischen Analyze und Confirm)*
  - `fileName` (`string`) *(required)*
  - `contentType` (`string | null`) *(optional)*
  - `content` (`string`, Base64 für `byte[]`) *(required)*
- `decisions` (`MassImportFileDecisionDto[]`) *(optional; bei Confirm empfohlen)*
  - `fileId` (`uuid`) *(required)*
  - `excluded` (`boolean`) *(required)*
  - `selectedSecurityId` (`uuid | null`) *(optional, relevant für `SecurityPrices`)*

Beispiel (Analyze):
```json
{
  "dialogPolicy": "OnMissingInformation",
  "confirmExecution": false,
  "files": [
    {
      "fileId": "11111111-1111-1111-1111-111111111111",
      "fileName": "ing_unmapped_prices.csv",
      "contentType": "text/csv",
      "content": "WmVpdDtLdXJzCjAxLjA3LjIwMjYgMDA6MDA6MDA7MTAsMDAK"
    }
  ],
  "decisions": []
}
```

### Response

#### Erfolgsfall: `200 OK` (`MassImportBatchResultDto`)
- `batchId` (`uuid`)
- `dialogRequired` (`boolean`)
- `dialogSkipped` (`boolean`)
- `requiresConfirmation` (`boolean`)
- `files` (`MassImportBatchFileResultDto[]`) mit u. a.:
  - `fileType` (`Unknown` | `AccountStatement` | `SecurityPrices`)
  - `canImport`, `excluded`, `selectedSecurityId`, `securityAutoGuessed`
  - `decisionSource` (`AutoDetected` | `UserConfirmed`)
  - `executionStatus` (`Pending` | `Skipped` | `Imported` | `Failed`)
  - `validationMessage` (`string | null`)
  - `statementDraftId` (`uuid | null`)
  - `priceImportResult` (`SecurityPriceImportResultDto | null`)

Beispiel (Analyze mit Bestätigung erforderlich):
```json
{
  "batchId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "dialogRequired": true,
  "dialogSkipped": false,
  "requiresConfirmation": true,
  "files": [
    {
      "fileId": "11111111-1111-1111-1111-111111111111",
      "fileName": "ing_unmapped_prices.csv",
      "fileType": "SecurityPrices",
      "serviceKey": "ing",
      "serviceDisplayName": "ING",
      "canImport": false,
      "excluded": false,
      "selectedSecurityId": null,
      "securityAutoGuessed": false,
      "decisionSource": "AutoDetected",
      "executionStatus": "Pending",
      "validationMessage": "Missing security assignment.",
      "statementDraftId": null,
      "priceImportResult": null
    }
  ]
}
```

#### Fehlerfälle
- `400 Bad Request`: Request fehlt/ungültig (z. B. keine Dateien)
  ```json
  {
    "errorCode": "API_StatementDraft_Err_Invalid_File",
    "message": "At least one file is required."
  }
  ```
- `401 Unauthorized`: kein/ungültiges Token
- `500 Internal Server Error`: unerwarteter Fehler (z. B. Orchestrator nicht verfügbar)

### Beispiel (curl)

Analyze:
```bash
curl -X POST "https://your-domain/api/statement-drafts/mass-import" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "dialogPolicy":"OnMissingInformation",
    "confirmExecution":false,
    "files":[
      {
        "fileId":"11111111-1111-1111-1111-111111111111",
        "fileName":"unknown.bin",
        "contentType":"application/octet-stream",
        "content":"AQIDBA=="
      }
    ],
    "decisions":[]
  }'
```

Confirm + Execute:
```bash
curl -X POST "https://your-domain/api/statement-drafts/mass-import" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "dialogPolicy":"OnMissingInformation",
    "confirmExecution":true,
    "files":[
      {
        "fileId":"11111111-1111-1111-1111-111111111111",
        "fileName":"unknown.bin",
        "contentType":"application/octet-stream",
        "content":"AQIDBA=="
      }
    ],
    "decisions":[
      {
        "fileId":"11111111-1111-1111-1111-111111111111",
        "excluded":true,
        "selectedSecurityId":null
      }
    ]
  }'
```

## Confirm-Dialog-Policy-Integration (Client Flow)

1. **Einstellung laden**: `GET /api/user/settings/import-split` und `massImportDialogPolicy` verwenden.  
2. **Analyze Call**: `confirmExecution=false` senden.  
3. **Wenn `requiresConfirmation=true`**: Benutzerentscheidungen sammeln (`excluded`, `selectedSecurityId`).  
4. **Confirm Call**: denselben File-Batch mit identischen `fileId` + `confirmExecution=true` + `decisions` senden.  
5. **Ergebnis pro Datei auswerten**: `executionStatus`, `decisionSource`, `validationMessage`, `statementDraftId`, `priceImportResult`.

## Verwandte Endpunkte

- User-Policy lesen/schreiben: [UserSettingsController](./UserSettingsController.md)
- Draft-Entry-Bearbeitung: [StatementDraftEntriesController](./StatementDraftEntriesController.md)

## Tests / Verifikation

- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`
- `FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs`

