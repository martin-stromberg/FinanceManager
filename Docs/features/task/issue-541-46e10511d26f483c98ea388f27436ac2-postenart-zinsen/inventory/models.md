# Datenmodell

## `AccountPostingRule`

Datei: `FinanceManager.Domain/Accounts/AccountPostingRule.cs`

Domänenentität einer Kontierungsregel. Fachlich unverändert von der Anforderung (reine UI-Änderung); Vollständigkeit halber dokumentiert.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountId` | `Guid` | Besitzer-Konto (Konstruktorpflicht, `Guards.NotEmpty`); kein `OwnerUserId` — Besitzerpfad läuft über `Account` |
| `Field` | `PostingRuleField` | Textfeld, gegen das gematcht wird (Subject/BookingDescription) |
| `Pattern` | `string` | Enthalten-Prüfungstext (`Guards.NotNullOrWhiteSpace`, getrimmt) |
| `PostingType` | `BankPostingType` | Zuzuweisende Postenart |
| `OrderIndex` | `int` | Auswertungsreihenfolge; niedrigster Treffer gewinnt |
| `Account` | `Account?` | Navigation zum Konto |
| `Update(field, pattern, postingType, orderIndex)` | Methode | Aktualisiert Regel + `Touch()` |
| `AccountPostingRuleBackupDto` | Record | `(Guid Id, Guid AccountId, PostingRuleField Field, string Pattern, BankPostingType PostingType, int OrderIndex)` |
| `ToBackupDto()` / `AssignBackupDto(dto)` | Methoden | Backup-Serialisierung |

## `AccountPostingRuleDto`

Datei: `FinanceManager.Shared/Dtos/Accounts/AccountPostingRuleDto.cs`

`public sealed record AccountPostingRuleDto(Guid Id, PostingRuleField Field, string Pattern, BankPostingType PostingType, int OrderIndex);` — wird im Panel pro Regelzeile gerendert (`BeginEdit` liest alle Felder in die VM-Eingabefelder).

## `AccountPostingRuleUpsertRequest`

Datei: `FinanceManager.Shared/Dtos/Accounts/AccountPostingRuleUpsertRequest.cs`

`public sealed record AccountPostingRuleUpsertRequest(Guid? Id, PostingRuleField Field, [Required, MaxLength(200)] string Pattern, BankPostingType PostingType, int? OrderIndex);` — `Id == null` = Neuanlage; `OrderIndex == null` = anhängen (Neu) bzw. Position behalten (Update).

## `BaseViewModel.UiOverlaySpec`

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeile 235)

`public sealed record UiOverlaySpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, bool Modal = true);`

| Element | Typ | Beschreibung |
|---------|-----|--------------|
| `ComponentType` | `Type` | Zu rendernde Komponente (per `DynamicComponent`) |
| `Parameters` | `IReadOnlyDictionary<string, object?>?` | Parameter der Komponente; `OverlayHost` filtert `OverlayTitle` und `Visible` heraus, liest `Title`/`OverlayTitle` für die Titelzeile |
| `Modal` | `bool` | Steuert die initiale Sichtbarkeit im `OverlayHost` (`_visible = spec.Modal`) |

## `BaseViewModel.EmbeddedPanelSpec`

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeile 245)

`public sealed record EmbeddedPanelSpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, EmbeddedPanelPosition Position = EmbeddedPanelPosition.AfterCard, bool Visible = true);`

Alternativer Mechanismus für **eingebettete** Panels (nicht Overlay). Wird für diese Anforderung nicht gebraucht, da das Panel aus der festen Einbettung in `CardPage` kommt — dokumentiert der Vollständigkeit halber.

## `UiRibbonAction`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeile 42)

`public sealed record UiRibbonAction(string Id, string Label, string IconSvg, UiRibbonItemSize Size, bool Disabled, string? Tooltip, Func<Task>? Callback)`

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `Id` | `string` | Aktionskennung; wird als `id`-Attribut des gerenderten `<button>` verwendet (`button#ReevaluatePostingTypes` etc.) |
| `Label` | `string` | Lokalisierte Beschriftung |
| `IconSvg` | `string` | Inline-SVG-Markup, Konvention: `"<svg><use href='/icons/sprite.svg#{id}'/></svg>"` |
| `Size` | `UiRibbonItemSize` | `Small`/`Large` |
| `Disabled` | `bool` | Disabled-Darstellung; Ribbon ruft `Callback` bei Disabled nicht auf |
| `Tooltip` | `string?` | `title`-Attribut; Fallback: `Label` |
| `Callback` | `Func<Task>?` | Wird über `LoadingBar.RunAsync` ausgeführt |
| `MobileShortcut` | `bool` (init) | Icon-only-Shortcut in geschlossener mobiler Gruppe |
| `Hidden` | `bool` (init) | Item vollständig ausblenden (Alternative zu `Disabled`, relevant für Offene Frage 1) |
| `FileCallback` | `Func<InputFileChangeEventArgs, Task>?` (init) | Für Import-Aktionen |
| `Action` | `string` | Kompatibilitäts-Alias für `Id` |

## `UiRibbonTab`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeile 85)

`public sealed record UiRibbonTab(string Title, List<UiRibbonAction> Items, int Sort = 0);` — Ein `UiRibbonTab` entspricht einer Ribbon-Gruppe; Gruppen gleichen Titels werden in `Ribbon.razor` gemergt, Reihenfolge über Listenreihenfolge/`Sort`.

## `UiRibbonRegister`

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeile 93)

`public sealed record UiRibbonRegister(UiRibbonRegisterKind Kind, List<UiRibbonTab>? Tabs)` mit Komfort-Properties `Title`/`Items` (erster Tab).

## `UiRibbonItem` / `UiRibbonGroup` (Legacy)

Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 121, 137)

Flache Kompatibilitäts-Records für ältere Aufrufer; für die Anforderung nicht relevant.
