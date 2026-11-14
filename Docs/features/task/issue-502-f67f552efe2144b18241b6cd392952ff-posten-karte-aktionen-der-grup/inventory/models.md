# Datenmodell – Bestand

Betroffene Daten-/Strukturklassen der Anforderung (Posten-Detailkarte, Ribbon-Aktionen der `Linked`-Gruppe, Anhangs-Overlay).

## `PostingsCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` — `public sealed class PostingsCardViewModel : BaseCardViewModel<(string Key, string Value)>`, Routen-Attribut `[CardRoute("postings")]` (Zeile 11).

| Member | Typ | Sichtbarkeit | Beschreibung |
|--------|-----|--------------|--------------|
| `Id` | `Guid` | `public`, `private set` | Id des geladenen Postens (Zeile 23). |
| `Posting` | `PostingServiceDto?` | `public`, `private set` | Geladenes Posten-DTO oder `null` (Zeile 28). |
| `Title` | `string` | `public override` | `Posting?.Subject ?? base.Title` (Zeile 33). |
| `CardRecord` | `CardRecord?` | geerbt (`public virtual`) | Kartenfelder aus `BuildCardRecordsAsync` (Zeilen 88–115). |

## `PostingServiceDto`

Datei: `FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs` — `public sealed record` (Zeilen 41–72).

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `Id` | `Guid` | Posten-Id. |
| `BookingDate` / `ValutaDate` | `DateTime` | Buchungs-/Valutadatum. |
| `Amount` | `decimal` | Betrag. |
| `Kind` | `PostingKind` | Postenart (Bank/Contact/SavingsPlan/Security). |
| `AccountId` | `Guid?` | Verknüpftes Konto — steuert `disabled` von `OpenAccount`. |
| `ContactId` | `Guid?` | Verknüpfter Kontakt — steuert `disabled` von `OpenContact`. |
| `SavingsPlanId` | `Guid?` | Verknüpfter Sparplan — steuert `disabled` von `OpenSavingsPlan`. |
| `SecurityId` | `Guid?` | Verknüpftes Wertpapier — steuert `disabled` von `OpenSecurity`. |
| `SourceId` | `Guid` | Ursprungs-Id. |
| `Subject` / `RecipientName` / `Description` | `string?` | Anzeigefelder der Karte. |
| `SecuritySubType` | `SecurityPostingSubType?` | Wertpapier-Untertyp. |
| `Quantity` | `decimal?` | Stückzahl (Wertpapier). |
| `GroupId` | `Guid` | Buchungsgruppe. |
| `LinkedPostingId` / `LinkedPostingKind` / `LinkedPostingAccountId` / `LinkedPostingAccountSymbolAttachmentId` / `LinkedPostingAccountName` | `Guid?` / `PostingKind?` / `Guid?` / `Guid?` / `string?` | Gegenbuchungs-Verknüpfung. |
| `BankPostingAccountId` / `BankPostingAccountSymbolAttachmentId` / `BankPostingAccountName` | `Guid?` / `Guid?` / `string?` | Bankkonto des Bankpostens. |
| `IsReversed` | `bool` | Posten wurde storniert — steuert `disabled` von `Reverse` (VM Zeile 146). |
| `IsReversal` | `bool` | Posten ist selbst Storno — steuert `disabled` von `Reverse`. |
| `ReversedByPostingId` / `ReversalForPostingId` | `Guid?` | Storno-Verknüpfungen. |
| `IsPreliminary` | `bool = false` | Vorbuchung. |
| `Informal` | `bool = false` | Informeller Finanzierungsposten. |
| `SavingsPlanSubType` | `SavingsPlanPostingSubType? = null` | Sparplan-Untertyp (z. B. Istmeldung). |

## `ReversalResultDto`

Datei: `FinanceManager.Shared/Dtos/Postings/ReversalResultDto.cs` — `public sealed record` (Zeilen 10–13): `ReversedPostingIds` (`IReadOnlyList<Guid>`), `CreatedReversalIds` (`IReadOnlyList<Guid>`), `StatementDraftId` (`Guid`). Rückgabe von `IApiClient.Postings_ReverseAsync`; `PostingsCardViewModel.ReverseAsync` nutzt `CreatedReversalIds.FirstOrDefault()` für die `NavigateToPosting`-UiAction (Zeilen 212–216).

## `GroupLinksDto`

Datei: `FinanceManager.Shared/Dtos/Postings/GroupLinksDto.cs` — `public sealed record` (Zeilen 11–16): `AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId` (je `Guid?`). API `Postings_GetGroupLinksAsync`; wird von `PostingsCardViewModel` **nicht** verwendet (nur im Test `PostingDetailViewModelTests` gemockt).

## Ribbon-Typen

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`.

| Typ | Form | Relevante Member |
|-----|------|-------------------|
| `UiRibbonAction` | `public sealed record` (Zeilen 42–76) | `Id`, `Label`, `IconSvg`, `Size` (`UiRibbonItemSize`), `Disabled` (`bool`), `Tooltip` (`string?`), `Callback` (`Func<Task>?`); zusätzlich `Action => Id` (Alias), `FileCallback`, `MobileShortcut`, `Hidden` (init-only). |
| `UiRibbonTab` | `public sealed record` (Zeile 85) | `Title`, `Items` (`List<UiRibbonAction>`), `Sort = 0`. |
| `UiRibbonRegister` | `public sealed record` (Zeilen 93–108) | `Kind` (`UiRibbonRegisterKind`), `Tabs` (`List<UiRibbonTab>?`); Kompat-Properties `Title`/`Items` des ersten Tabs. |
| `UiRibbonItem` | `public sealed record` (Zeilen 121–129) | Legacy-Flachform (Label, IconSvg, Size, Disabled, Action, Tooltip, Callback). |
| `UiRibbonGroup` | `public sealed record` (Zeile 137) | Legacy-Gruppenform (Title, Items). |

## `BaseViewModel`-Trägertypen

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`.

| Typ | Form | Relevante Member |
|-----|------|-------------------|
| `UiActionEventArgs` | `public sealed class : EventArgs` (Zeilen 190–226) | `Action` (`string?`), `Payload` (`string?`), `PayloadObject` (`object?`); zwei Konstruktoren (string-Payload bzw. Object-Payload). |
| `UiOverlaySpec` | `public sealed record` (Zeile 235) | `ComponentType` (`Type`), `Parameters` (`IReadOnlyDictionary<string,object?>?`), `Modal` (`bool = true`). |
| `EmbeddedPanelSpec` | `public sealed record` (Zeile 245) | `ComponentType`, `Parameters`, `Position` (`EmbeddedPanelPosition`), `Visible`. |
| `LookupItem` | `public sealed record` (Zeile 259) | `Key` (`Guid`), `Name` (`string`). |

## `AttachmentsPanel`-Parameter

Datei: `FinanceManager.Web/Components/Shared/AttachmentsPanel.razor` (Zeilen 136–139).

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `ParentKind` | `AttachmentEntityKind` | `[EditorRequired]` — Entitätsart des Anhang-Parents; `Posting = 7` existiert bereits. |
| `ParentId` | `Guid` | `[EditorRequired]` — Id des Parent-Datensatzes. |
| `OnUploaded` | `EventCallback<Guid>` | Optionaler Upload-Callback. |
| `AllowedMimeTypes` | `string[]?` | Optionaler MIME-Filter. |

Das Panel lädt/verwaltet Anhänge über `Attachments_ListAsync((short)ParentKind, ParentId, …)` (Zeile 264) und `Attachments_UploadFileAsync` (Zeile 361) — die API ist art-agnostisch.
