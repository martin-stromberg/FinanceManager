## `SecurityTxtFormat`
Datei: `FinanceManager.Application/Security/SecurityTxtFormat.cs`

| Wert | Bedeutung |
|------|-----------|
| `PlainText` | Ausgabe im RFC-9116-ähnlichen PlainText-Format. |
| `Markdown` | Ausgabe als Markdown-Sektionen. |
| `Html` | Ausgabe als HTML-Sektionen. |

Querverweise:
- Wird von `SecurityTxtController` an `ISecurityTxtSettingsService.BuildContentAsync(...)` übergeben.
- Wird in `SecurityTxtSettingsService.BuildContentAsync(...)` per `switch` auf Renderpfade gemappt.
