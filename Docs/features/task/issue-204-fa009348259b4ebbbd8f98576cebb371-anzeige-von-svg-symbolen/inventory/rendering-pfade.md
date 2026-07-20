# Rendering-Pfade fuer Kontaktsymbole

## Kontaktliste

`FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs` erstellt die Zeilen fuer die Kontaktuebersicht:

- `LoadPageAsync` liest Kontakte ueber `Contacts_ListAsync`.
- `ContactListItem` uebernimmt `c.SymbolAttachmentId`.
- `BuildRecords` erzeugt in der ersten Spalte `new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId)`.

Die generische Darstellung erfolgt in `FinanceManager.Web/Components/Pages/GenericListPage.razor`:

- Desktop: `ListCellKind.Symbol` wird als `<img src="/api/attachments/{cell.SymbolId}/download">` gerendert.
- Mobile: alle drei mobilen Renderpfade fuer `ListCellKind.Symbol` verwenden ebenfalls direkte `<img>`-Downloads.

Damit haengt die Sichtbarkeit vollstaendig daran, dass `/api/attachments/{id}/download` einen fuer `<img>` gueltigen Bild-Content-Type liefert.

## Kontakt-Detailkarte

`FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` erzeugt in `BuildCardRecordAsync` ein editierbares Symbolfeld:

- `new CardField("Card_Caption_Contact_Symbol", CardFieldKind.Symbol, symbolId: c.SymbolAttachmentId, editable: c.Id != Guid.Empty)`

`FinanceManager.Web/Components/Pages/GenericCardPage.razor` rendert dieses Feld:

- Nicht editierbare Symbole: `<img src="/api/attachments/{f.SymbolId}/download">`
- Editierbare Symbole: `<img src="/api/attachments/{f.SymbolId}/download" alt="symbol">`
- Neue Uploads laufen ueber `ValidateSymbolAsync`, danach wird das Feld mit der neuen Attachment-ID aktualisiert.

Auch hier gibt es keine SVG-spezifische Darstellung. SVG muss wie jedes andere Bild ueber den Download-Endpunkt als Bild-MIME ausgeliefert werden.

## SymbolPicker

`FinanceManager.Web/Components/Shared/SymbolPicker.razor` ist eine alternative Symbolkomponente:

- `AllowedMimeTypes` enthaelt standardmaessig `image/svg+xml`.
- Bestehende Vorschauen nutzen eine Download-URL mit Token: `/api/attachments/{id}/download?token=...`.
- Ohne Token-Fallback ist ebenfalls `/api/attachments/{id}/download` hinterlegt.

Der Picker zeigt damit denselben Download-Pfad wie die Listen/Karten an. Der Token aendert nur die Autorisierung, nicht den Content-Type.

## Schlussfolgerung

Die UI erwartet keine Konvertierung und kein Inline-SVG. Alle relevanten Symbolanzeigen sind normale `<img>`-Tags. Die Korrektur sollte deshalb nicht in jeder Ansicht dupliziert werden, sondern den zentralen Attachment-Download fuer sichere SVGs wieder als `image/svg+xml` ausliefern.
