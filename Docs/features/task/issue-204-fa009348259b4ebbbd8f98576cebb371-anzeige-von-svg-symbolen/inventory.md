# Bestandsaufnahme: Anzeige von SVG-Symbolen

## Zusammenfassung

SVG-Dateien werden im Upload-Pfad weiterhin als Symbol akzeptiert und als `image/svg+xml` gespeichert. Die Anzeige scheitert sehr wahrscheinlich im Download-Pfad: `AttachmentContentPolicy.NormalizeDownloadContentType` laesst `image/svg+xml` beim Ausliefern nicht durch und faellt auf `application/octet-stream` zurueck. Da die bestehenden Symbolanzeigen `<img src="/api/attachments/{id}/download">` verwenden und der Controller zusaetzlich `X-Content-Type-Options: nosniff` setzt, rendert der Browser SVG-Antworten mit `application/octet-stream` nicht als Bild.

Die betroffenen UI-Stellen sind generische Symbolzellen in Listen, Symbolfelder in Karten und die Symbolvorschau im `SymbolPicker`. Der Upload muss nicht fachlich erweitert werden; die Korrektur liegt primaer in der Download-MIME-Normalisierung und in Tests.

## Detaildokumente

- [Rendering-Pfade fuer Kontaktsymbole](inventory/rendering-pfade.md)
- [Attachment-Upload und Download-Policy](inventory/attachment-policy.md)
- [Test- und Absicherungsbestand](inventory/tests.md)

## Relevante Dateien

| Bereich | Datei | Relevanz |
|---------|-------|----------|
| Symbolanzeige in Kontaktliste | `FinanceManager.Web/ViewModels/Contacts/ContactListViewModel.cs` | Liefert `SymbolAttachmentId` als `ListCellKind.Symbol` an die generische Liste. |
| Generische Listenanzeige | `FinanceManager.Web/Components/Pages/GenericListPage.razor` | Rendert Symbolzellen auf Desktop und Mobile als `<img src="/api/attachments/{id}/download">`. |
| Kontakt-Detailkarte | `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` | Baut das editierbare Symbolfeld mit `CardFieldKind.Symbol`. |
| Generische Kartenanzeige | `FinanceManager.Web/Components/Pages/GenericCardPage.razor` | Rendert Symbolfelder als `<img src="/api/attachments/{id}/download">` und laedt neue Symbole hoch. |
| SymbolPicker | `FinanceManager.Web/Components/Shared/SymbolPicker.razor` | Akzeptiert SVG im Client und nutzt fuer Vorschauen Download-URLs mit Token. |
| Attachment-Controller | `FinanceManager.Web/Controllers/AttachmentsController.cs` | Liefert Attachments ueber `DownloadFile` mit normalisiertem Content-Type und `nosniff` aus. |
| Content-Policy | `FinanceManager.Web/Infrastructure/Attachments/AttachmentContentPolicy.cs` | Akzeptiert SVG beim Upload, entfernt `image/svg+xml` aber beim Download. |
| Upload-Optionen | `FinanceManager.Web/Infrastructure/Attachments/AttachmentUploadOptions.cs` | Enthalten `image/svg+xml` in den erlaubten MIME-Typen. |

## Befund

### Ursache

`AttachmentContentPolicy.DetectContentType` erkennt sichere SVGs und gibt `image/svg+xml` zurueck. `AttachmentUploadOptions.AllowedMimeTypes` erlaubt denselben MIME-Typ. Beim Download enthaelt `NormalizeDownloadContentType` jedoch nur PDF, PNG, JPEG, ICO, Text, CSV und ZIP. `image/svg+xml` fehlt dort und wird dadurch zu `application/octet-stream`.

`AttachmentsController.DownloadFile` setzt fuer jeden Download `X-Content-Type-Options: nosniff`. Diese Sicherheitsmassnahme ist sinnvoll, verhindert aber, dass der Browser einen falsch ausgelieferten SVG-Octet-Stream als Bild interpretiert. Damit bleibt die `<img>`-Anzeige leer, obwohl die Datei vorhanden und referenziert ist.

### Betroffene Ansichten

- Kontaktliste: Symbolspalte der Kontakte.
- Kontakt-Detailkarte: Symbolfeld des Kontakts.
- Mobile Kontaktlistenansicht: mobile Symbolzellen.
- Indirekt alle generischen Listen- und Kartenansichten, die `ListCellKind.Symbol` oder `CardFieldKind.Symbol` fuer Kontakt-Symbole nutzen.
- Bereits hinterlegte SVG-Symbole sind betroffen, weil der gespeicherte Attachment-Datensatz unveraendert bleibt und nur der Download-Content-Type korrigiert werden muss.

### Nicht primaer betroffen

- Kontaktmodell und DTOs: `SymbolAttachmentId` wird bereits transportiert.
- Upload-Erlaubnis: SVG ist clientseitig und serverseitig erlaubt.
- Datenmigration: Es ist keine Datenmodell-Aenderung erkennbar.
- Andere Bildformate: PNG, JPEG und ICO sind im Download-Allowlist bereits enthalten.

## Hinweise fuer die Planung

- Naheliegende Korrektur: `image/svg+xml` in `AttachmentContentPolicy.NormalizeDownloadContentType` als erlaubten Download-Typ aufnehmen.
- Sicherheitskontext beachten: Die Upload-Policy blockiert aktive SVG-Inhalte bereits ueber `SvgActiveContentRegex`; die Download-Freigabe sollte auf diesen bestehenden Schutz Bezug nehmen.
- Tests ergaenzen:
  - Upload eines sicheren SVG bleibt akzeptiert und wird als `image/svg+xml` gespeichert.
  - Download eines gespeicherten `image/svg+xml` liefert `image/svg+xml` statt `application/octet-stream`.
  - Bestehender Risky-Type-Fallback auf `application/octet-stream` bleibt erhalten.
  - Optional: unsicheres SVG mit `<script>`, Eventhandler oder externem/data/javascript Link bleibt abgelehnt.

## Offene Punkte

- Der konkrete Commit von Issue #197 ist in der sichtbaren Git-Historie nicht eindeutig ueber die Issue-Nummer identifizierbar. Inhaltlich passt die Aenderung zur eingefuehrten Attachment-Content-Policy mit Download-MIME-Allowlist und `nosniff`.
