# Datenmodell

## `StatementDraft`
Datei: `FinanceManager.Domain/Statements/StatementDraft.cs` (Zeilen 11–261)

Aggregate Root (`Entity`, `IAggregateRoot`). Hält interne Liste `_entries` vom Typ `List<StatementDraftEntry>`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer des Entwurfs (private setter; Besitzprüfung läuft darüber) |
| `OriginalFileName` | `string` | Originalname der hochgeladenen Auszugsdatei |
| `AccountName` | `string?` | Optional erkannter Kontoname/-nummer aus der Datei |
| `Description` | `string?` | Optionale Beschreibung; Default = Dateiname ohne Erweiterung |
| `DetectedAccountId` | `Guid?` | Erkanntes/zugeordnetes Bankkonto |
| `Status` | `StatementDraftStatus` | Entwurfsstatus (`Draft`/`Committed`/`Expired`) |
| `IsPreliminary` | `bool` | Vorläufiger (provisorischer) Buchungsentwurf |
| `Entries` | `ICollection<StatementDraftEntry>` | Geparste Einträge des Auszugs |
| `UploadGroupId` | `Guid?` | Gruppen-ID für Entwürfe aus demselben Upload |
| `Id`, `CreatedUtc`, `ModifiedUtc` | geerbt von `Entity` | Identität/Zeitstempel |

Relevante Methoden: `AddEntry(...)` (2 Überladungen, vergibt `EntryNumber` fortlaufend ab `Max+1`), `SetDetectedAccount`, `SetUploadGroup`, `MarkCommitted`, `Expire`, `MarkAsPreliminary`, `ToBackupDto`, `AssignBackupDto`. Enthält außerdem das Record `StatementDraftBackupDto` (Zeile 224).

## `StatementDraftEntry`
Datei: `FinanceManager.Domain/Statements/StatementDraft.cs` (Zeilen 266–654)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `DraftId` | `Guid` | ID des zugehörigen Entwurfs (Pflicht) |
| `EntryNumber` | `int` | Fortlaufende Nummer innerhalb des Entwurfs (Reihenfolge im Auszug) |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime?` | Valutadatum |
| `Amount` | `decimal` | Betrag |
| `Subject` | `string` | Verwendungszweck/Kurzbeschreibung |
| `RecipientName` | `string?` | Empfängername |
| `CurrencyCode` | `string` | Währung (Default `"EUR"`) |
| `BookingDescription` | `string?` | Buchungstext (länger) |
| `IsAnnounced` | `bool` | Vom Quellsystem angekündigt/vorgebucht |
| `IsCostNeutral` | `bool` | Kostenneutral (Default `false`) |
| `Status` | `StatementDraftEntryStatus` | Verarbeitungsstatus (`Open`/`Announced`/`Accounted`/`AlreadyBooked`) |
| `ContactId` | `Guid?` | Zugeordneter Kontakt |
| `SavingsPlanId` | `Guid?` | Zugeordneter Sparplan |
| `ArchiveSavingsPlanOnBooking` | `bool` | Sparplan beim Buchen archivieren |
| `SplitDraftId` | `Guid?` | Zugeordneter Split-Entwurf |
| `SecurityId` | `Guid?` | Zugeordnetes Wertpapier |
| `SecurityTransactionType` | `SecurityTransactionType?` | WP-Transaktionstyp |
| `SecurityQuantity` | `decimal?` | WP-Menge |
| `SecurityFeeAmount` | `decimal?` | WP-Gebühr |
| `SecurityTaxAmount` | `decimal?` | WP-Steuer |
| `Id`, `CreatedUtc`, `ModifiedUtc` | geerbt von `Entity` | Identität/Zeitstempel |

Relevante Methoden: `UpdateCore`, `MarkAlreadyBooked`, `MarkAccounted(contactId)`, `ClearContact`, `ResetOpen`, `MarkCostNeutral`, `AssignSavingsPlan`, `SetArchiveSavingsPlanOnBooking`, `AssignSplitDraft`, `ClearSplitDraft`, `AssignContactWithoutAccounting`, `SetSecurity`, `OverrideValutaDate`, `MarkNeedsCheck`, `ToBackupDto`, `AssignBackupDto`. Enthält das Record `StatementDraftEntryBackupDto` (Zeile 615).

## `StatementDraftDto`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftDto.cs` (sealed record)

| Positionsparameter | Typ |
|--------------------|-----|
| `DraftId` | `Guid` |
| `OriginalFileName` | `string` |
| `Description` | `string?` |
| `DetectedAccountId` | `Guid?` |
| `AccountBankContactId` | `Guid?` |
| `AttachmentSymbolId` | `Guid?` |
| `Status` | `StatementDraftStatus` |
| `TotalAmount` | `decimal` |
| `IsSplitDraft` | `bool` |
| `ParentDraftId` | `Guid?` |
| `ParentEntryId` | `Guid?` |
| `ParentEntryAmount` | `decimal?` |
| `UploadGroupId` | `Guid?` |
| `Entries` | `IReadOnlyList<StatementDraftEntryDto>` |
| `IsPreliminary` | `bool` (Default `false`) |

## `StatementDraftEntryDto`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryDto.cs` (sealed record)

| Positionsparameter | Typ |
|--------------------|-----|
| `Id` | `Guid` |
| `EntryNumber` | `int` |
| `BookingDate` | `DateTime` |
| `ValutaDate` | `DateTime?` |
| `Amount` | `decimal` |
| `CurrencyCode` | `string` |
| `Subject` | `string` |
| `RecipientName` | `string?` |
| `BookingDescription` | `string?` |
| `IsAnnounced` | `bool` |
| `IsCostNeutral` | `bool` |
| `Status` | `StatementDraftEntryStatus` |
| `ContactId` | `Guid?` |
| `SavingsPlanId` | `Guid?` |
| `ArchiveSavingsPlanOnBooking` | `bool` |
| `SplitDraftId` | `Guid?` |
| `SecurityId` | `Guid?` |
| `SecurityTransactionType` | `SecurityTransactionType?` |
| `SecurityQuantity` | `decimal?` |
| `SecurityFeeAmount` | `decimal?` |
| `SecurityTaxAmount` | `decimal?` |
| `BudgetImpact` | `BudgetImpactEvaluationDto?` (init-only, kein Positionsparameter) |

## `StatementDraftDetailDto`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftDetailDtos.cs` (sealed record)

Wie `StatementDraftDto` plus Navigations-/Anzeige-Zusätze: `PrevInUpload`, `NextInUpload` (`Guid?`), Symbol-/Namens-Maps `ContactSymbols`, `SavingsPlanSymbols`, `SavingsPlanNames`, `SecuritySymbols`, `SecurityNames`, `ContactNames` (`IReadOnlyDictionary<...>?`), `AccountBankContactId`, `SelfContactId`, `IsPreliminary`. Wird von `StatementDraftsController.GetAsync` befüllt; `StatementDraftCardViewModel.Draft` ist vom Typ `StatementDraftDetailDto`.

## `PostingExportRequest`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingExportDtos.cs` (sealed record)

| Positionsparameter | Typ | Default |
|--------------------|-----|---------|
| `Format` | `string?` | `"csv"` |
| `From` | `DateTime?` | `null` |
| `To` | `DateTime?` | `null` |
| `Q` | `string?` | `null` |

Wird im `PostingsController` als `[FromQuery]`-Parameter gebunden (`api/postings/{context}/{id}/export?format=&from=&to=&q=`).

## `PostingExportQuery`
Datei: `FinanceManager.Application/Reports/IPostingExportService.cs` (Zeilen 31–40, sealed record)

`OwnerUserId` (`Guid`), `ContextKind` (`PostingKind`), `ContextId` (`Guid`), `Format` (`PostingExportFormat`), `MaxRows` (`int`), `From`/`To` (`DateTime?`, optional), `Q` (`string?`, optional).

## `PostingExportRow`
Datei: `FinanceManager.Application/Reports/IPostingExportService.cs` (Zeilen 59–73, sealed record)

`BookingDate` (`DateTime`), `ValutaDate` (`DateTime`), `Amount` (`decimal`), `Kind` (`PostingKind`), `Subject`/`RecipientName`/`Description` (`string?`), `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` (`Guid?`), `SecuritySubType` (`SecurityPostingSubType?`), `Quantity` (`decimal?`). IDs werden beim Export zu Namen aufgelöst.

## `StatementDraftEntryItem`
Datei: `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs` (internal sealed, `IListItemNavigation`)

Lightweight-Listenitem der Embedded-Liste: `Id`, `DraftId`, `BookingDate`, `ValutaDate`, `Amount`, `RecipientName`, `Subject`, `BookingDescription`, `Status`, `IsAnnounced`, `ContactId`, `SavingsPlanId`, `SecurityId`, `SecurityTransactionType`, `BudgetImpact`, `IsNew`, `IsPlaceholder`, `CanDelete`. `GetNavigateUrl()` → `/card/statement-drafts/entries/{Id}?draftId={DraftId}`.

## Ribbon-Modelle
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`

| Typ | Signatur | Zweck |
|-----|----------|-------|
| `UiRibbonAction` | record `(Id, Label, IconSvg, Size, Disabled, Tooltip, Callback)` | Einzelne Ribbon-Aktion; Zusatz-Properties `Action` (Alias für `Id`), `FileCallback`, `MobileShortcut`, `Hidden` (init-only) |
| `UiRibbonTab` | record `(Title, List<UiRibbonAction> Items, int Sort = 0)` | Ribbon-Gruppe/Tab |
| `UiRibbonRegister` | record `(UiRibbonRegisterKind Kind, List<UiRibbonTab>? Tabs)` | Register mit Hilfsproperties `Title`/`Items` des ersten Tabs |
| `UiRibbonItem` / `UiRibbonGroup` | records | Legacy-Kompatibilitätstypen |

## `StreamCallbackResult`
Datei: `FinanceManager.Web/Infrastructure/StreamCallbackResult.cs` (`IActionResult`)

Konstruktor `(string contentType, Func<Stream, CancellationToken, Task> callback)`; Property `FileDownloadName` (init) setzt `Content-Disposition` mit ASCII-Fallback + UTF-8 `filename*` (RFC 6266). `ExecuteResultAsync` setzt `ContentType` und ruft den Callback mit `response.Body` + `RequestAborted` auf. Wird von `PostingsController.ExportAsync` für CSV-Streaming verwendet.
