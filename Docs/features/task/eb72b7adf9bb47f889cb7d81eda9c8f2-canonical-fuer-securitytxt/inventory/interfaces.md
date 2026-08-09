## `ISecurityTxtSettingsService`
Datei: `FinanceManager.Application/Security/ISecurityTxtSettingsService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct` | `Task<SecurityTxtSettingsDto>` | Lädt aktuelle Admin-Einstellungen. |
| `UpdateAsync` | `SecurityTxtSettingsUpdateRequest request`, `CancellationToken ct` | `Task` | Persistiert Admin-Änderungen. |
| `BuildContentAsync` | `SecurityTxtFormat format`, `CancellationToken ct` | `Task<string?>` | Baut öffentliche `security.txt`-Ausgabe je Format. |

Querverweise:
- Implementiert von `SecurityTxtSettingsService`.
- Verwendet durch `SecurityTxtController`.
