# Interfaces

## `IConfirmationService`
Datei: `FinanceManager.Web/Services/IConfirmationService.cs`

Implementiert von `ConfirmationService` (Scoped, `ProgramExtensions.cs` Zeile 194) und `NullConfirmationService` (Singleton-Fallback, `internal`). Konsumiert u. a. von `ConfirmDialog`, `ConfirmationDialogHost`, `AttachmentsPanel`, `ContactMergePanel`, `HomeKpiGrid`, `ContactDetail`, `SetupUpdateTab`.

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `OnShow` (Event) | `EventHandler<ConfirmationRequest>?` | — | Wird ausgelöst, wenn ein Bestätigungsdialog angezeigt werden soll. |
| `OnChanged` (Event) | `EventHandler?` | — | Bei jeder Änderung des Pending-Requests (show/confirm/cancel/dismiss); UI-Hosts refreshen hierauf. |
| `CurrentRequest` (Property) | — | `ConfirmationRequest?` | Aktuell ausstehende Anfrage oder `null`. |
| `ConfirmAsync` | `ConfirmationRequest request`, `CancellationToken ct` | `Task<bool>` | `true` bei Bestätigung oder deaktivierten Bestätigungen; `false` bei Abbruch. |
| `SetResult` | `bool confirmed` | `void` | Wird von der UI bei Confirm/Cancel aufgerufen. |
| `Cancel` | — | `void` | Äquivalent `SetResult(false)`. |
| `InvalidateCacheAsync` | — | `Task` | Verwirft den gecachten `ShowConfirmations`-Wert. |

## `IAttachmentService` (Kontext — keine Änderung erforderlich)
Datei: `FinanceManager.Application/Attachments/IAttachmentService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `DeleteAsync` | `Guid ownerUserId`, `Guid attachmentId`, `CancellationToken ct` | `Task<bool>` | Zeile 97. Löscht einen Anhang; aufgerufen von `AttachmentsController.DeleteAsync` (Zeile 320). |

## `IApiClient` (Kontext — Ausschnitt)
Datei: `FinanceManager.Shared/ApiClient.Attachments.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Attachments_DeleteAsync` | `Guid id`, `CancellationToken ct` | `Task<bool>` | Zeile 83, `DELETE /api/attachments/{id}`. |
| `Attachments_ListAsync` | `short parentKind`, `Guid parentId`, `int skip`, `int take`, `Guid? categoryId`, ... | `Task<PageResult<AttachmentDto>>` | Listendaten für `AttachmentsPanel`. |
| `Attachments_ListCategoriesAsync` | `CancellationToken` | `Task<AttachmentCategoryDto[]>` | Kategorien für Filter/Edit. |
| `UserSettings_GetProfileAsync` | `CancellationToken` | `Task<UserProfileSettingsDto>` | Liefert `ShowConfirmations` für `ConfirmationService`. |
