# Datenmodell

## `ConfirmationRequest`
Datei: `FinanceManager.Web/Services/ConfirmationRequest.cs`

Record (positional parameters). Wird von `ConfirmationService.ConfirmAsync` entgegengenommen und an `ConfirmDialog.Request` weitergereicht.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TitleResourceKey` | `string` | Resource-Key für den Dialogtitel (wird in `ConfirmDialog` über `IStringLocalizer<Pages>` aufgelöst). |
| `MessageResourceKey` | `string` | Resource-Key für die Dialogmeldung. |
| `ContextId` | `string?` | Optionaler Kontext (z. B. Entitäts-/Aktionsname) für Logging/Diagnose. |
| `Severity` | `ConfirmationSeverity` | Visuelle Schwere; wird in `ConfirmDialog` als CSS-Klasse `confirm-severity-{wert}` gerendert (`GetSeverityClass`). |
| `ConfirmButtonResourceKey` | `string?` | Optionaler Resource-Key für den Bestätigen-Button; Fallback `Btn_Confirm`. |

## `AttachmentDto`
Datei: `FinanceManager.Shared/Dtos/Attachments/AttachmentDto.cs`

Record. Listeneintrag im `AttachmentsPanel`; `DeleteAsync` verwendet `Id`, die Tabelle zeigt `UploadedUtc`, `FileName`, `SizeBytes`, `CategoryId`, `IsUrl`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Anhangs-ID; Parameter für `Attachments_DeleteAsync`. |
| `EntityKind` | `short` | Numerische Art der Parent-Entität. |
| `EntityId` | `Guid` | ID der Parent-Entität. |
| `FileName` | `string` | Dateiname (im Panel editierbar). |
| `ContentType` | `string` | MIME-Typ. |
| `SizeBytes` | `long` | Dateigröße (Anzeige via `FormatSize`). |
| `CategoryId` | `Guid?` | Optionale Kategorie-Zuordnung. |
| `UploadedUtc` | `DateTime` | Upload-Zeitpunkt (UTC; lokale Anzeige). |
| `IsUrl` | `bool` | `true` bei URL-Anhang — Download-Link wird dann nicht gerendert. |
| `Role` | `short` | Optionale Rolle (z. B. Symbolrolle), Default `0`. |

## `UserProfileSettingsDto`
Datei: `FinanceManager.Shared/Dtos/Users/UserProfileSettingsDto.cs`

Klasse mit setzbaren Eigenschaften; wird von `ConfirmationService.GetShowConfirmationsAsync` via `IApiClient.UserSettings_GetProfileAsync` gelesen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ShowConfirmations` | `bool` | Default `true` (Zeile 21). Steuert, ob `ConfirmAsync` einen Dialog anzeigt oder sofort `true` zurückgibt. |
| (weitere Eigenschaften) | — | Für diese Anforderung nicht relevant. |

## `BaseViewModel.UiOverlaySpec`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeile 235)

Record: `UiOverlaySpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, bool Modal = true)`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ComponentType` | `Type` | Im Overlay zu rendernde Komponente (u. a. `AttachmentsPanel`, `ContactMergePanel`, `SecurityPriceImportPanel`, `SecurityPricesBackfillPanel`, `SetPasswordOverlay`, `AssignStatementOverlay`, `MassBookingOptionsPanel`). |
| `Parameters` | `IReadOnlyDictionary<string, object?>?` | Parameter-Dictionary für `DynamicComponent`; Schlüssel `OverlayTitle` und `Visible` werden vom Host herausgefiltert. |
| `Modal` | `bool` | Default `true`; `OverlayHost` setzt `_visible = spec.Modal`. |

## `BaseViewModel.EmbeddedPanelSpec`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeile 245)

Record: `EmbeddedPanelSpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, EmbeddedPanelPosition Position = AfterCard, bool Visible = true)` — nur Kontext: `SetupPanel` (mit `SetupSections` → `SetupUpdateTab`, das `ConfirmAsync` nutzt) wird als eingebettetes Panel gerendert, nicht als Overlay.
