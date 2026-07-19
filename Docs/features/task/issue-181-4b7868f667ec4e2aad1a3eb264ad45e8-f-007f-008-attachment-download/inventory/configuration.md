# Detail: Konfiguration und Runtime-Grenzen

## Relevante Dateien

- `FinanceManager.Web/Infrastructure/Attachments/AttachmentUploadOptions.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/appsettings.Production.json`

## AttachmentUploadOptions

`AttachmentUploadOptions` definiert:

- `MaxSizeBytes` mit Default 10 MB.
- `AllowedMimeTypes` mit Defaultwerten:
  - `application/pdf`
  - `image/png`
  - `image/jpeg`
  - `image/svg+xml`
  - `image/x-icon`
  - `image/vnd.microsoft.icon`
  - `text/plain`
  - `text/csv`
  - `application/zip`

Die Optionen werden in `ProgramExtensions.cs:87` aus der Konfigurationssektion `Attachments` gebunden.

## Aktuelle Runtime-Grenzen

- `AttachmentsController.cs:119`: Action-Attribut `[RequestSizeLimit(long.MaxValue)]`.
- `ProgramExtensions.cs:81-84`: `FormOptions.MultipartBodyLengthLimit = 1024L * 1024L * 1024L` (1 GB).
- `appsettings.json:9-12`: Kestrel `MaxRequestBodySize = 20971520` (20 MB).
- `AttachmentUploadOptions.cs:11`: fachliches Default-Limit 10 MB.

## Befund

Es gibt mehrere Groessengrenzen mit unterschiedlicher Zielgroesse:

- Kestrel: 20 MB
- FormOptions: 1 GB
- AttachmentUploadOptions: 10 MB
- Controller-Action: unbegrenzt

Die Anforderung verlangt, dass die Upload-Groessenbegrenzung auf Controller-/Serverebene an eine realistische maximale Attachment-Groesse gekoppelt wird und nicht `long.MaxValue` verwendet. Aktuell ist die fachliche Grenze zwar kleiner, aber nicht der frueheste wirksame Schutz.

## Umsetzungshinweise

- Eine gemeinsame Konstante oder zentrale Option fuer Attachment-Maximalgroesse sollte die Werte konsistent machen.
- Wenn Runtime-Konfiguration erforderlich bleibt, kann ein Resource-/Action-Filter das Requestlimit dynamisch aus Options lesen. Ein statisches Attribut kann dagegen nur konstante Werte verwenden.
- `FormOptions.MultipartBodyLengthLimit` sollte nicht grosszuegiger sein als die fachlich akzeptierte Attachment-Groesse, sofern keine anderen Multipart-Endpunkte groessere Bodies brauchen.
- Kestrel-Grenze sollte mit dem gewaehlten Attachment-Limit kompatibel sein. Wenn Kestrel bei 20 MB bleibt und Attachment bei 10 MB, ist das technisch akzeptabel, aber die explizite Controller-/Form-Grenze sollte trotzdem nicht unbegrenzt sein.
- Fehlermeldungen fuer ueberschrittene Grenzen sollten keine internen Details enthalten und bestehende fachliche Meldung "File too large" beibehalten, sofern der Request den Controller erreicht.
