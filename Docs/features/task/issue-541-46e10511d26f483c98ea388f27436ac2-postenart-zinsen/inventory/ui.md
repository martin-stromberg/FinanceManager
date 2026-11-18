# UI-Ressourcen

## resx-Schlüssel (`FinanceManager.Web/Resources/Pages.{resx,de.resx,en.resx}`)

Alle drei Dateien sind gespiegelt (neutral/de/en identisch aufgebaut). Localizer im Panel und in `CardPage`/`OverlayHost` ist `IStringLocalizer<Pages>` (bzw. `IStringLocalizer`).

### PostingRules_* (Z. 3452–3465, de/en)

| Key | de | en |
|-----|----|----|
| `PostingRules_Section_Title` | Kontierungsregeln | Posting rules |
| `PostingRules_Empty` | Keine Kontierungsregeln hinterlegt. | No posting rules configured. |
| `PostingRules_Th_Field` | Feld | Field |
| `PostingRules_Th_Pattern` | Suchtext | Pattern |
| `PostingRules_Th_PostingType` | Postenart | Posting type |
| `PostingRules_Th_OrderIndex` | Reihenfolge | Order |
| `PostingRules_Add_Placeholder` | Suchtext eingeben... | Enter pattern... |
| `PostingRules_Add_Button` | Hinzufügen | Add |
| `PostingRules_Edit_Button` | Bearbeiten | Edit |
| `PostingRules_Save_Button` | Speichern | Save |
| `PostingRules_Remove_Button` | Entfernen | Remove |
| `PostingRules_Cancel_Button` | Abbrechen | Cancel |
| `PostingRules_Error_PatternRequired` | Bitte einen Suchtext angeben. | Please enter a pattern. |
| `PostingRules_OrderIndex_Hint` | Leer = am Ende | Empty = append at end |

Im Panel werden alle Keys mit Fallback-String auf Englisch verwendet (`Localizer?["Key"] ?? "..."`) — Tooltip-Umstellung kann dieselben Keys wiederverwenden.

### Ribbon-Keys der Bankkonto-Karte (de/en)

| Key | de | en |
|-----|----|----|
| `Ribbon_Group_Bookings` (Z. 300) | Buchungen | Bookings |
| `Ribbon_CreatePreliminaryDraft` (Z. 2835) | Vorläufige Buchung erfassen | Create preliminary statement |
| `Ribbon_ReevaluatePostingTypes` (Z. 3466) | Postenarten neu bewerten | Re-evaluate posting types |
| `Ribbon_Group_Navigation` / `Ribbon_Group_Manage` / `Ribbon_Group_Linked` | Navigieren / Verwalten / Verknüpfte Informationen | Navigation / Manage / Linked information |

Für den neuen Ribbon-Button („Kontierungsregeln"/„Posting rules") und ggf. einen Overlay-Titel fehlt aktuell ein Key — `PostingRules_Section_Title` ist als Titeltext wiederverwendbar.

### Weitere relevante Keys

- `Btn_Close` (Schließen-Button des `OverlayHost`), `Btn_Add`/`Btn_Save`/`Btn_Cancel`/`Btn_Edit`/`Btn_Delete` (in anderen Panels für `icon-btn`-Tooltips verwendet, z. B. `SetupAttachmentCategoriesTab`).
- `EnumType_PostingRuleField_Subject` = Verwendungszweck/Purpose, `EnumType_PostingRuleField_BookingDescription` = Buchungsbeschreibung/Booking description (Z. 3448–3449).
- `EnumType_BankPostingType_*` = Nicht angegeben/Eingang/Ausgang/Zinsen/Steuer (Z. 3443–3447).
- `LinkedIbans_*`-Keys existieren separat (verwandtes Schwester-Panel `LinkedIbansPanel`, unverändert).

## Icon-Sprite `/icons/sprite.svg`

Datei: `FinanceManager.Web/wwwroot/icons/sprite.svg` — Verwendung: `<svg><use href="/icons/sprite.svg#{id}"/></svg>`.

Vorhandene Symbol-IDs:

`account`, `accounts`, `archive`, `arrow-left`, `arrow-right`, `attachment`, `back`, `bank`, `calendar`, `chart`, `check`, `chevron-down`, `chevron-left`, `chevron-right`, `clear`, `close`, `delete`, `download`, `edit`, `external`, `eye`, `filters`, `groups`, `image`, `info`, `key`, `layers`, `level-up`, `link`, `list`, `merge`, `new`, `next`, `plus`, `postings`, `prev`, `refresh`, `reset`, `restore`, `save`, `save-as`, `search`, `security`, `settings`, `tag`, `trash`, `undo`, `unlink`, `unlock`, `upload`, `view`

Plausible Zuordnung für die Anforderung (bestehende Konventionen in anderen Panels): Bearbeiten `#edit`, Entfernen `#trash`/`#delete`, Abbrechen `#undo` oder `#clear`, Hinzufügen `#plus`/`#check`, Speichern `#save`/`#check`; Ribbon „Kontierungsregeln" z. B. `#filters`, `#list`, `#settings` oder `#tag`. Kein dediziertes „Regeln"-Icon vorhanden.

## CSS-Stile

| Stil | Datei / Zeilen | Wirkung |
|------|----------------|---------|
| `.icon-btn` | `app.css` Z. 347–360 (Layout) + `theme.Dark.css` Z. 80–88 (Theme) | Etablierter Icon-Button: Padding, inline-flex, `svg` 1.1rem; Hover `:not(:disabled)` |
| `.icon-btn.small` | `app.ReportDashboard.css` Z. 180–186 | Kleinere Variante (nicht global) |
| `button.danger` (→ `icon-btn danger`) | `theme.Dark.css` Z. 57–63 | Gefahren-Styling, u. a. in `AttachmentsPanel`/`SetupAttachmentCategoriesTab` als `class="icon-btn danger"` verwendet |
| `button` / `button:hover:not(:disabled)` | `theme.Dark.css` Z. 48–55 | Generisches Button-Theme (wirkt auch auf die jetzigen `btn`-Buttons) |
| `.btn`, `.btn-sm`, `.btn-primary`, `.btn-danger` | — | **Keine globalen Definitionen** (nur vereinzelt in seiten-spezifischen CSS-Dateien wie `app.ReturnAnalysis.css`, `app.HomeKpiGrid.css`); die aktuellen Panel-Buttons sind faktisch ungestylt bis auf Inline-Styles und `button.danger` |
| `.split-center` / `.split-dialog` | `app.css` Z. 409–427 + `theme.Dark.css` Z. 131–139 | Overlay-Backdrop (fixed, z-index 1000) bzw. Dialog-Container |
| `.fm-table`, `.card`, `.card-table`, `.card-input` | `app.css` Z. 374 ff. u. a. | Tabellen-/Input-Stile des Panels |

## Referenz: `icon-btn`-Verwendung in vergleichbaren Panels

- `AttachmentsPanel.razor` Z. 113–123: `<button class="icon-btn" title="..."><svg><use href="/icons/sprite.svg#edit"/></svg></button>`, `class="icon-btn danger"` mit `#delete`, `#save`, `#undo` — direktes Muster für FA-1.
- `SetupAttachmentCategoriesTab.razor` Z. 22, 71–137: `icon-btn` mit `disabled`-Bindung und `Btn_*`-Tooltips.
- `SavingsPlanLinkedAccountsPanel.razor` Z. 11–91: `class="icon-btn small"`.
- `OverlayHost.razor` Z. 35: Schließen-Button `icon-btn` + `#clear` + `title="@Localizer["Btn_Close"]"`.
