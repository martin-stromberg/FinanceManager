# Datenmodell

## `StatementHeader`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountNumber` | `string` | Kontonummer aus dem Auszug |
| `IBAN` | `string?` | Optionale IBAN |
| `BankCode` | `string?` | Bankcode/BLZ |
| `AccountHolder` | `string?` | Kontoinhaber |
| `PeriodStart`/`PeriodEnd` | `DateTime?` | Auszugszeitraum |
| `Description` | `string` | Beschreibungstext |

## `StatementMovement`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `EntryNumber` | `int` | Laufende Zeilennummer |
| `BookingDate` | `DateTime` | Buchungsdatum (Spalte `Buchung`) |
| `ValutaDate` | `DateTime` | Valutadatum (Spalte `Valuta`/`Wertstellungsdatum`) |
| `Amount` | `decimal` | Betrag (Spalte `Betrag`) |
| `Subject` | `string?` | Verwendungszweck (Template-Variable `Description`) |
| `Counterparty` | `string?` | Auftraggeber/Empfänger (Variable `SourceName`, wird akkumuliert) |
| `PostingDescription` | `string?` | Buchungstext |
| `CurrencyCode` | `string?` | Währung |
| `IsPreview` | `bool` | Buchung in der Zukunft/Vorschau |
| `IsError` | `bool` | Fehlerflag bei der Zeilenverarbeitung |
| `ContactId` | `Guid` | Zugeordneter Kontakt |
| `Quantity`/`TaxAmount`/`FeeAmount` | `decimal?` | Optional, für Wertpapier-/Steuerdaten |

**Hinweis:** Es existiert kein Feld für eine Bankreferenz — die neue `Referenz`-Spalte hat kein direktes Zielfeld.

## `StatementParseResult`
Datei: `FinanceManager.Application/Statements/IStatementFileParser.cs`

Kapselt `Header` + `Movements`; je IBAN-Block ein Ergebnis.

## Mass-Import-DTOs
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Klasse | Relevante Member |
|--------|------------------|
| `MassImportFileUploadDto` | `FileId`, `FileName`, `ContentType`, `Content` |
| `MassImportFileDecisionDto` | `FileId`, `Excluded`, `SelectedSecurityId` |
| `MassImportBatchRequestDto` | `DialogPolicy`, `ConfirmExecution`, `Files`, `Decisions` |
| `MassImportBatchFileResultDto` | `FileType`, `ServiceKey`, `ServiceDisplayName`, `CanImport`, `Excluded`, `SelectedSecurityId`, `DecisionSource`, `ExecutionStatus`, `ValidationMessage`, `StatementDraftId(s)`, `PriceImportResult` |
| `MassImportBatchResultDto` | `BatchId`, `DialogRequired`, `DialogSkipped`, `RequiresConfirmation`, `Files` |
