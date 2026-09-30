# Interfaces

## `IStatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/IStatementFileParser.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Parse` | `IStatementFile` | `IReadOnlyList<StatementParseResult>?` | Datei erkennen und Header + Buchungen extrahieren; `null` wenn nicht parsebar |
| `ParseDetails` | `IStatementFile` | `IReadOnlyList<StatementParseResult>?` | Detailparse (bei ING identisch zu `Parse`) |

## `IStatementFile`
Datei: `FinanceManager.Infrastructure/Statements/Files/IStatementFile.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `FileName` | – | `string` | Dateiname |
| `Load` | `fileName`, `fileBytes` | `bool` | Bytes laden; `false` wenn Format nicht passt |
| `ReadContent` | – | `IEnumerable<string>` | Zeileninhalt liefern |

## `IStatementFileFactory`
Datei: `FinanceManager.Infrastructure/Statements/Files/IStatementFileFactory.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Load` | `fileName`, `fileBytes` | `IStatementFile?` | Registrierte Dateitypen probieren; `null` wenn keiner passt |

## `IMassImportOrchestrator`
Datei: `FinanceManager.Application/Statements/IMassImportOrchestrator.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProcessAsync` | `ownerUserId`, `MassImportBatchRequestDto`, `traceId`, `ct` | `Task<MassImportBatchResultDto>` | Batch analysieren und ggf. ausführen |

## `IConfirmationService`
Datei: `FinanceManager.Web/Services/IConfirmationService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ConfirmAsync` | `ConfirmationRequest`, `ct` | `Task<bool>` | Bestätigungsdialog anzeigen |
| `SetResult`/`Cancel` | … | `void` | Dialogantwort setzen/abbrechen |
