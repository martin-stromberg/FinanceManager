# Detail: Attachment-Controller

## Relevante Datei

- `FinanceManager.Web/Controllers/AttachmentsController.cs`

## Upload-Flow

Die Upload-Action ist `UploadAsync`:

- `AttachmentsController.cs:118-123`: `POST api/attachments/{entityKind}/{entityId}` mit `[RequestSizeLimit(long.MaxValue)]`.
- `AttachmentsController.cs:130-133`: Datei oder URL muss vorhanden sein.
- `AttachmentsController.cs:138-148`: Leerdatei und fachliches Groessenlimit `_options.MaxSizeBytes`.
- `AttachmentsController.cs:150-157`: MIME-Whitelist prueft nur `file.ContentType`.
- `AttachmentsController.cs:197-206`: Stream wird geoeffnet und `file.FileName` sowie `file.ContentType ?? "application/octet-stream"` werden an den Service uebergeben.

## Download-Flow

Die Download-Action ist `DownloadAsync`:

- `AttachmentsController.cs:260-263`: `GET api/attachments/{id}/download`, anonym erlaubt, optionaler Query-Parameter `token`.
- `AttachmentsController.cs:265-272`: Authentifizierte Nutzer laden direkt ueber `_service.DownloadAsync`.
- `AttachmentsController.cs:274-289`: Anonyme Nutzer muessen den geschuetzten Query-Token liefern.
- `AttachmentsController.cs:270-289`: In beiden Pfaden wird der gespeicherte Content-Type an `File(...)` weitergegeben, mit Fallback auf `application/octet-stream`.

## Sicherheitsrelevante Befunde

- Das Action-Level-Requestlimit `long.MaxValue` widerspricht der Anforderung nach realistischer Controller-/Server-Grenze.
- Die fachliche Groessenpruefung bleibt erhalten, greift aber nach Request-Annahme und FormFile-Bereitstellung.
- Die MIME-Pruefung vertraut ausschliesslich auf einen clientgelieferten Header.
- Es gibt keine Magic-Number- oder Inhaltsvalidierung fuer erlaubte Typen.
- Der an den Service uebergebene Content-Type stammt vom Client oder aus einem generischen Fallback.
- Downloads verwenden den gespeicherten Content-Type, der aus frueheren ungeprueften Uploads stammen kann.
- `File(content, contentType, fileName)` setzt einen Download-Dateinamen. Fuer die Anforderung sollte explizit verifiziert oder erzwungen werden, dass `Content-Disposition: attachment` gesetzt wird.

## Bestehende Optionen und erlaubte Typen

`AttachmentUploadOptions` definiert aktuell:

- Default `MaxSizeBytes`: 10 MB.
- Default `AllowedMimeTypes`: PDF, PNG, JPEG, SVG, ICO, Text, CSV, ZIP.

Die Controller-Tests verwenden teils eigene Optionen, meist nur PDF oder PDF/PNG/Text.

## Umsetzungshinweise

- Die realistische Request-Groesse sollte aus `AttachmentUploadOptions.MaxSizeBytes` ableitbar sein. Attribute koennen keine Runtime-Optionen direkt lesen; Alternativen sind eine konstante Obergrenze, ein Filter oder eine Server-/FormOptions-Kopplung.
- Fuer PDF, PNG, JPEG, ZIP und ICO sind Magic Numbers gut pruefbar.
- SVG ist XML/Text und potenziell aktiv. Wenn es weiter fuer Symbole gebraucht wird, sollte die Planungsphase bewusst entscheiden, ob SVG-Upload erlaubt bleibt, normalisiert wird oder nur als Download mit `application/octet-stream`/Attachment-Disposition ausgeliefert wird.
- Text/CSV brauchen eine andere Validierungsstrategie, z. B. UTF-8/ASCII-kompatible Textpruefung, NUL-Byte-Ablehnung und sichere Typzuweisung.
- Um Altbestand zu haerten, sollte Download-Content-Type nicht blind aus der Datenbank uebernommen werden. Eine sichere Download-Policy im Controller kann gespeicherte Typen auf erlaubte sichere Typen reduzieren oder `application/octet-stream` nutzen.
