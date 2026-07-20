# Attachment-Upload und Download-Policy

## Upload

`FinanceManager.Web/Infrastructure/Attachments/AttachmentUploadOptions.cs` erlaubt SVG bereits:

- `AllowedMimeTypes` enthaelt `image/svg+xml`.

`FinanceManager.Web/Infrastructure/Attachments/AttachmentContentPolicy.cs` erkennt SVGs in `DetectContentType`:

- Die Datei muss als sicherer Text lesbar sein.
- `IsSafeSvg` verlangt ein SVG-Root-Element.
- `SvgActiveContentRegex` blockiert Skripte, Eventhandler und riskante `href`/`xlink:href`-Ziele wie `http`, protocol-relative URLs, `data:` und `javascript:`.
- Bei sicherem Inhalt und `.svg`-Dateiendung oder Client-MIME `image/svg+xml` wird `image/svg+xml` zurueckgegeben.

`FinanceManager.Web/Controllers/AttachmentsController.cs` verwendet die validierte Policy:

- `UploadAsync` ruft `_contentPolicy.ValidateUploadAsync(file, ct)` auf.
- Bei Erfolg wird der normalisierte `fileValidation.ContentType` gespeichert.
- Symbol-Uploads werden zusaetzlich ueber `AttachmentRole.Symbol` in die Systemkategorie `Symbole` einsortiert.

Bewertung: Der Upload-Pfad akzeptiert sichere SVG-Symbole bereits und muss fuer die Anforderung nicht erweitert werden.

## Download

`AttachmentsController.DownloadAsync` ruft am Ende `DownloadFile` auf. Dort werden zwei sicherheitsrelevante Dinge gesetzt:

- Header `X-Content-Type-Options: nosniff`
- Content-Type aus `_contentPolicy.NormalizeDownloadContentType(payload.ContentType)`

`NormalizeDownloadContentType` laesst aktuell folgende Typen passieren:

- `application/pdf`
- `image/png`
- `image/jpeg`
- `image/x-icon`
- `image/vnd.microsoft.icon`
- `text/plain`
- `text/csv`
- `application/zip`

`image/svg+xml` fehlt. Dadurch wird ein gespeichertes SVG beim Download als `application/octet-stream` ausgeliefert.

## Warum das SVG-Rendering bricht

Die UI verwendet `<img>` mit dem Attachment-Download als `src`. Ein Browser rendert ein SVG in einem `<img>` nur verlaesslich, wenn die Antwort als Bild ausgeliefert wird. Durch `application/octet-stream` plus `X-Content-Type-Options: nosniff` darf der Browser nicht selbst auf SVG sniffen. Das ist konsistent mit dem beobachteten Verhalten: Upload und Referenz funktionieren, sichtbares Rendering bleibt aus.

## Korrekturrichtung

Die zentrale Stelle ist `AttachmentContentPolicy.NormalizeDownloadContentType`. Dort sollte `image/svg+xml` in die erlaubte Download-Liste aufgenommen werden, solange der Upload-Pfad nur sichere SVGs speichert.

Eine zusaetzliche Einschraenkung auf Symbol-Rollen ist im aktuellen Download-Endpunkt nicht ohne weiteres verfuegbar, weil die Normalisierung nur den gespeicherten Content-Type erhaelt. Das ist aber nicht zwingend noetig, da die Upload-Policy fuer alle Attachment-SVGs greift.
