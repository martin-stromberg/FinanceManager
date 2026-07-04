## `MassImportFileUploadDto`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FileId` | `Guid` | Stabile Datei-ID zwischen Analyse- und Bestätigungsaufruf. |
| `FileName` | `string` | Ursprünglicher Dateiname. |
| `ContentType` | `string?` | MIME-Typ der Upload-Datei. |
| `Content` | `byte[]` | Binärinhalt der Datei. |

## `MassImportFileDecisionDto`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FileId` | `Guid` | Referenz auf die Datei aus der Analysephase. |
| `Excluded` | `bool` | Kennzeichnet, ob Datei übersprungen wird. |
| `SelectedSecurityId` | `Guid?` | Manuelle Security-Zuordnung für Preisimporte. |

## `MassImportBatchRequestDto`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `DialogPolicy` | `MassImportDialogPolicy` | Regel, wann ein Bestätigungsdialog erforderlich ist. |
| `ConfirmExecution` | `bool` | `false` = Analyse, `true` = Ausführung. |
| `Files` | `IReadOnlyList<MassImportFileUploadDto>` | Upload-Payload je Datei. |
| `Decisions` | `IReadOnlyList<MassImportFileDecisionDto>` | Nutzerentscheidungen aus dem Dialog. |

## `MassImportBatchFileResultDto`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `FileId` | `Guid` | Datei-Referenz. |
| `FileName` | `string` | Dateiname. |
| `FileType` | `MassImportFileType` | Erkannter Dateityp. |
| `ServiceKey` | `string` | Technischer Import-Service-Schlüssel. |
| `ServiceDisplayName` | `string` | Anzeigename des Import-Services. |
| `CanImport` | `bool` | Importierbarkeit der Datei. |
| `Excluded` | `bool` | Ob Datei ausgeschlossen wurde. |
| `SelectedSecurityId` | `Guid?` | Zugeordnete Security (falls nötig). |
| `SecurityAutoGuessed` | `bool` | Kennzeichnet automatische Security-Zuordnung. |
| `DecisionSource` | `MassImportDecisionSource` | Quelle der Entscheidung (`AutoDetected`/`UserConfirmed`). |
| `ExecutionStatus` | `MassImportFileExecutionStatus` | Status pro Datei (`Pending`, `Skipped`, `Imported`, `Failed`). |
| `ValidationMessage` | `string?` | Validierungs-/Fehlermeldung. |
| `StatementDraftId` | `Guid?` | Ergebnis-ID bei Statement-Import. |
| `PriceImportResult` | `SecurityPriceImportResultDto?` | Ergebnisdaten bei Security-Preisimport. |

## `MassImportBatchResultDto`
Datei: `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `BatchId` | `Guid` | ID des Batch-Laufs. |
| `DialogRequired` | `bool` | Kennzeichnet notwendigen Dialog. |
| `DialogSkipped` | `bool` | Kennzeichnet übersprungenen Dialog. |
| `RequiresConfirmation` | `bool` | Kennzeichnet Analyseantwort, die Bestätigung benötigt. |
| `Files` | `IReadOnlyList<MassImportBatchFileResultDto>` | Datei-Ergebnisse des Batches. |

## `LoginVm`
Datei: `FinanceManager.Web/Components/Pages/Login.razor`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Username` | `string` | Benutzername für Login-Formular (`[Required]`, `[MinLength(3)]`). |
| `Password` | `string` | Passwort für Login-Formular (`[Required]`, `[MinLength(6)]`). |

## `RegisterVm`
Datei: `FinanceManager.Web/Components/Pages/Register.razor`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Username` | `string` | Benutzername für Registrierungsformular (`[Required]`, `[MinLength(3)]`). |
| `Password` | `string` | Passwort für Registrierungsformular (`[Required]`, `[MinLength(6)]`). |
