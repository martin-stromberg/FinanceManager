# Test- und Absicherungsbestand

## Vorhandene Tests

`FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs` deckt den Attachment-Controller bereits teilweise ab:

- Leere Dateien werden abgelehnt.
- Zu grosse Dateien werden abgelehnt.
- Nicht unterstuetzte Content-Types werden abgelehnt.
- Gueltige PDFs werden akzeptiert.
- Header-/Content-Mismatch wird abgelehnt.
- Unsichere Download-Typen wie `text/html` werden beim Download auf `application/octet-stream` normalisiert.
- `X-Content-Type-Options: nosniff` wird beim Download geprueft.

`FinanceManager.Tests/Web/ViewModels/ContactsViewModelTests.cs` prueft die Kontaktliste:

- Kontakte und Kategorien werden geladen.
- Paging und Suche funktionieren.
- Es gibt keine SVG- oder Bild-Rendering-Pruefung.

`FinanceManager.Tests/Web/ViewModels/Contacts/ContactCardViewModelTests.cs` prueft bisher nur, dass bei existierendem Kontakt das Detailpanel angefordert wird. Symbolfelder oder Symboluploads werden dort nicht abgesichert.

## Testluecken zur Anforderung

- Kein Test stellt sicher, dass `NormalizeDownloadContentType("image/svg+xml")` auch `image/svg+xml` zurueckgibt.
- Kein Controller-Test prueft Download eines gespeicherten SVG-Attachments.
- Kein Upload-Test prueft ein sicheres SVG mit `image/svg+xml`.
- Kein Negativtest prueft, dass unsichere SVG-Inhalte weiterhin abgelehnt werden.
- Keine Komponententests pruefen, ob Kontaktliste oder Kontaktkarte bei vorhandener `SymbolAttachmentId` ein `<img>` erzeugen. Das ist weniger kritisch als der zentrale Download-Test, weil die vorhandenen Renderpfade bereits eindeutig sind.

## Empfohlene Absicherung

Minimal fuer diese Korrektur:

1. `AttachmentsControllerTests.DownloadAsync_ShouldReturnSvgContentType_ForStoredSvg`
   - Service liefert `(stream, "symbol.svg", "image/svg+xml")`.
   - Erwartet `FileStreamResult.ContentType == "image/svg+xml"`.
   - Erwartet weiterhin Header `X-Content-Type-Options == "nosniff"`.

2. `AttachmentsControllerTests.UploadAsync_ShouldAccept_SafeSvg`
   - Optionen enthalten `image/svg+xml`.
   - Datei `symbol.svg` enthaelt ein minimales sicheres SVG.
   - Service wird mit normalisiertem Content-Type `image/svg+xml` aufgerufen.

Optional, aber sinnvoll:

3. Policy- oder Controller-Test fuer unsicheres SVG
   - Beispiel mit `<script>` oder `onload=`.
   - Erwartet Bad Request mit `Err_Invalid_ContentType`.

4. Regressionstest fuer andere Bildformate
   - PNG/JPEG/ICO-Download bleibt unveraendert.
   - Der bestehende `text/html`-Fallback bleibt erhalten.
