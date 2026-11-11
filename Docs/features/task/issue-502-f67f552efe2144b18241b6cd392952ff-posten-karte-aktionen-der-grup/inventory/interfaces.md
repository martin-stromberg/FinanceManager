# Interfaces – Bestand

## `IRibbonProvider`

Datei: `FinanceManager.Web/ViewModels/ViewModelBase.cs` (Zeilen 14–36). Implementiert von `BaseViewModel` (und damit allen Card-ViewModels) sowie vom Legacy-`ViewModelBase`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Liefert Ribbon-Register inkl. Gruppen/Aktionen; von `Ribbon.razor` konsumiert. |
| `GetActiveTab<TTabEnum>` | — | `TTabEnum?` | Aktives Ribbon-Tab. |
| `SetActiveTab<TTabEnum>` | `TTabEnum id` | `void` | Setzt aktives Ribbon-Tab. |

## `ICardInitializable`

Datei: `FinanceManager.Web/ViewModels/Common/ICardInitializable.cs` (Zeilen 10–32). Implementiert von `BaseCardViewModel<T>`; `CardPage.razor` ruft `SetBackNavigation`, `SetInitValue`, `InitializeAsync` (Zeilen 393–398).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `InitializeAsync` | `Guid id` | `Task` | Lädt Karte für Id; `Guid.Empty` = Neuanlage. |
| `SetInitValue` | `string? prefill` | `void` | Prefill-Payload (`?prefill=`). |
| `SetBackNavigation` | `string? backUrl` | `void` | Zurück-URL (`?back=`), abrufbar über `InitBack`. |

## `ISymbolAssignableCard`

Datei: `FinanceManager.Web/ViewModels/Common/ISymbolAssignableCard.cs` (Zeilen 8–25). Implementiert von `BaseCardViewModel<T>`; für diese Anforderung nicht relevant (`PostingsCardViewModel` deaktiviert Symbol-Uploads).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ValidateSymbolAsync` | `Stream stream, string fileName, string contentType` | `Task<Guid?>` | Symbol-Upload-Validierung. |

## `IApiClient` — relevante Ausschnitte

Datei: `FinanceManager.Shared/IApiClient.cs`.

| Member | Parameter | Rückgabewert | Verwendung |
|--------|-----------|--------------|------------|
| `LastError` | — | `string?` | Fehlertext bei `null`-Rückgaben (Zeile 20); von `PostingsCardViewModel.LoadAsync`/`ReverseAsync` genutzt. |
| `LastErrorCode` | — | `string?` | Fehlercode (Zeile 25). |
| `Postings_GetByIdAsync` | `Guid id, CancellationToken ct = default` | `Task<PostingServiceDto?>` | `LoadAsync` (Zeile 739). |
| `Postings_GetGroupLinksAsync` | `Guid groupId, CancellationToken ct = default` | `Task<GroupLinksDto?>` | Zeile 783 — wird vom VM **nicht** aufgerufen (nur Test-Mock). |
| `Postings_ReverseAsync` | `Guid id, CancellationToken ct = default` | `Task<ReversalResultDto?>` | `ReverseAsync` (Zeile 788). |
| `Attachments_ListAsync` | `short entityKind, Guid entityId, int skip = 0, int take = 50, Guid? categoryId = null, bool? isUrl = null, string? q = null, CancellationToken ct = default` | `Task<PageResult<AttachmentDto>>` | Zeile 330 — art-agnostisch, vom `AttachmentsPanel` genutzt. |
| `Attachments_UploadFileAsync` | `short entityKind, Guid entityId, Stream fileStream, string fileName, string contentType, Guid? categoryId = null, short? role = null, CancellationToken ct = default` | `Task<AttachmentDto>` | Zeile 341 — art-agnostisch. |

## `ICurrentUserService` (Test-Infrastruktur)

`FinanceManager.Application` — in den VM-Tests als Stub implementiert (`UserId`, `PreferredLanguage`, `IsAuthenticated`, `IsAdmin`); wird von `BaseViewModel.IsAuthenticated`/`CheckAuthentication` aufgelöst.
