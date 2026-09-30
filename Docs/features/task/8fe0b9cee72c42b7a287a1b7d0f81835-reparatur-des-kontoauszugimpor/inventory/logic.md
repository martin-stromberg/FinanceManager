# Logik

## `ING_CSV_StatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/ING_CSV_StatementFileParser.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CanParse` | protected override | Akzeptiert `ING_Csv_StatementFile` und internes `InlineING_CsvStatementFile` |
| `Parse` | public override | Erkennt Sammelkonten anhand mehrerer `IBAN;`-Zeilen und splittet in Blöcke; sonst Delegation an `base.Parse` |
| `ParseDetails` | public override | Delegiert an `Parse` |

- `_Templates`: ein einziges XML-Template. Sektionen: `Title` (ignore), `AccountInfo` (keyvalue: `IBAN`→`BankAccountNo` always, `Kunde`→`BankAccountNo` onlywhenempty), `Sortierung ` (ignore), `BlaBla` (ignore), `table` (table, `containsheader='true'`).
- Tabellenfelder (positionsbasiert, 9 Spalten): `Buchung`→`PostingDate`, `Valuta`→`ValutaDate`, `Auftraggeber/Empfänger`→`SourceName`, `Buchungstext`→`PostingDescription`, `Verwendungszweck`→`Description`, `Saldo`→`''` (ignoriert), `Währung`→`CurrencyCode`, `Betrag`→`Amount`, `Währung`→`CurrencyCode`.

## `TemplateStatementFileParser`
Datei: `FinanceManager.Infrastructure/Statements/Parsers/TemplateStatementFileParser.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Parse` | public override | Versucht alle `Templates` sequenziell; liefert erstes Ergebnis mit Movements, sonst `null` |
| `ParseNextLine` | private | Zustandsmaschine über `ParseMode` (Ignore/KeyValue/TableHeader/Table/DynamicTable) |
| `InitSection` | private | Aktiviert Modus je `section type`; liest `containsheader`, `fieldSeparator`, `endKeyword`, `ignore`, `stopOnError` |
| `InternalParseTableRecord` | private | Splittet Zeile am `TableFieldSeparator`, iteriert Template-Felder, `ArgumentOutOfRangeException` → `IsError`-Record wenn `StopOnError`, sonst rethrow |
| `ParseField` | private | Liest `Values[FieldIdx]` positionsbasiert; leeres `variable`-Attribut = Spalte ignorieren |
| `ParseVariable` | protected | Mappt Variablennamen auf `StatementMovement`-Properties; `PostingDate`/`ValutaDate`/`Amount` werfen `FormatException` bei ungültigem Wert |
| `ProcessFoundRecord` | protected virtual | Post-Processing-Hook pro Record |

- `FormatException` in `ParseTableRecord` wird **nicht** abgefangen → propagiert zu `Parse`, dort als Template-Fehlschlag geloggt (`ErrorList`) → nächstes Template wird versucht → bei letztem Template `null`.

## `ING_Csv_StatementFile`
Datei: `FinanceManager.Infrastructure/Statements/Files/ING_Csv_StatementFile.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Load` | public override | `base.Load` + prüft, ob eine der ersten 10 Zeilen `Bank;ING` enthält |

## `MassImportOrchestrator`
Datei: `FinanceManager.Infrastructure/Statements/MassImportOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProcessAsync` | public | Analysiert alle Uploads (`AnalyzeFile`), ermittelt `IsDialogRequired`; bei `RequiresConfirmation` Rückgabe ohne Ausführung; sonst `ImportStatementAsync`/`ImportSecurityPricesAsync` je Datei |
| `AnalyzeFile` | private | `IStatementFileFactory.Load` → alle `IStatementFileParser.Parse` → erstes Ergebnis mit Movements gewinnt; sonst Wertpapierkurs-Erkennung via `TryResolveByContent`; sonst `Unknown` + `"File type could not be recognized."` |
| `IsDialogRequired` | private static | `AlwaysConfirm` → immer; sonst wenn eine Datei `Unknown`, `!CanImport` oder `SecurityPrices` ohne `SelectedSecurityId` |
| `ImportStatementAsync` | private | `IStatementDraftService.CreateDraftAsync` → Draft-IDs, `ExecutionStatus.Imported` |
| `ResolveStatementService` | private static | Mapping Dateityp-Name → ServiceKey/DisplayName (`ing`, `sparkasse`, `barclays`, `wuestenrot`, `backup`) |
| `GuessSecurity` | private static | Heuristische Wertpapierzuordnung über normalisierte Namenstokens |

Wird aufgerufen von `StatementDraftsController` (Endpoint `ProcessMassImport`).

## `HomeViewModel`
Datei: `FinanceManager.Web/ViewModels/Home/HomeViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProcessMassImportSelectionAsync` | private | Ribbon-FileCallback: Uploads lesen, `StatementDrafts_ProcessMassImportAsync` mit `ConfirmExecution=false`; bei `RequiresConfirmation` → `PendingMassImport` setzen + `ActiveSecurities` laden; sonst `ApplyMassImportResult` |
| `ConfirmMassImportAsync` | public | **Immer** `ConfirmationService.ConfirmAsync` (`Confirmation_Finalize_*`, Severity Warning) → Request mit `ConfirmExecution=true` + Decisions → `ApplyMassImportResult` |
| `CancelMassImportDialog` | public | Dialog verwerfen |
| `SetPendingFileExcluded`/`SetPendingFileSecurity` | public | Benutzerentscheidungen in `PendingMassImport.Files` pflegen |
| `NormalizePendingMassImportResult` | private static | Nicht selektierbare Dateien (`Unknown`/kein `ServiceKey`) → `Excluded=true`, `CanImport=false` |
| `ApplyMassImportResult` | private | `FirstDraftId`/`ImportSuccess` aus Ergebnis ableiten |

`ConfirmationService` kommt aus `ViewModelBase`; Fallback `NullConfirmationService` (immer `true`) für Tests.
