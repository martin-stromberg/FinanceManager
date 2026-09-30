# Enums

## `MassImportDialogPolicy`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `AlwaysConfirm = 0` | Dialog immer vor der Ausführung anzeigen |
| `OnMissingInformation = 1` | Dialog nur bei fehlenden Informationen (Standard) |

## `MassImportFileType`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unknown = 0` | Dateityp nicht erkannt |
| `AccountStatement = 1` | Kontoauszug |
| `SecurityPrices = 2` | Wertpapierkurse |

## `MassImportFileExecutionStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `Pending = 0` | Wartet auf Bestätigung |
| `Skipped = 1` | Übersprungen (ausgeschlossen/nicht importierbar) |
| `Imported = 2` | Erfolgreich importiert |
| `Failed = 3` | Import fehlgeschlagen |

## `MassImportDecisionSource`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `AutoDetected = 0` | Automatisch erkannt |
| `UserConfirmed = 1` | Vom Benutzer gesetzt |
