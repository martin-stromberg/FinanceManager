# Detail: Attachment-Service und Persistenz

## Relevante Dateien

- `FinanceManager.Infrastructure/Attachments/AttachmentService.cs`
- `FinanceManager.Application/Attachments/IAttachmentService.cs`
- `FinanceManager.Domain/Attachments/Attachment.cs`
- `FinanceManager.Shared/Dtos/Attachments/AttachmentDto.cs`

## Ist-Zustand

`IAttachmentService` stellt Upload- und Download-Methoden bereit:

- `IAttachmentService.cs:22`: Upload ohne Rolle mit `Stream`, `fileName`, `contentType`.
- `IAttachmentService.cs:28`: Upload mit Rolle.
- `IAttachmentService.cs:49`: Download gibt `(Stream Content, string FileName, string ContentType)?` zurueck.

`AttachmentService.UploadAsync`:

- `AttachmentService.cs:63-68`: liest den kompletten Stream in einen `MemoryStream`.
- `AttachmentService.cs:69`: berechnet SHA-256.
- `AttachmentService.cs:71`: erzeugt `Attachment` mit unveraendertem `fileName`, `contentType`, Groesse, Hash und Bytes.
- `AttachmentService.cs:72-75`: speichert und loggt Metadaten.

`AttachmentService.DownloadAsync`:

- `AttachmentService.cs:187-198`: ruft die interne Download-Logik auf.
- `AttachmentService.cs:200-213`: gibt fuer Master oder Referenz `MemoryStream`, `FileName`, `ContentType` aus der Datenbank zurueck.

`Attachment`:

- `Attachment.cs:43-48`: `FileName` und `ContentType` werden als Strings gespeichert.
- `Attachment.cs:128-130`: Konstruktor uebernimmt beide Werte ohne Normalisierung.
- `Attachment.cs:204-220`: Backup-Restore kann Content-Type aus Backup-Daten wiederherstellen.

## Sicherheitsrelevante Befunde

- Der Service validiert Inhalt und Content-Type nicht.
- Der Service liest den gesamten Upload in Memory; die Groessenbegrenzung muss also zwingend vor oder spaetestens waehrend dieses Aufrufs wirksam sein.
- Da der Service den gespeicherten Content-Type beim Download wieder ausgibt, koennen Altbestaende und Backup-Restores riskante oder falsche Content-Types enthalten.
- Eine Aenderung des `IAttachmentService`-Contracts haette Auswirkungen auf mehrere Konsumenten, darunter Statement-Draft- und Setup-Flows.

## Umsetzungshinweise

- Die risikoarme Variante ist, die Validierung und Content-Type-Normalisierung vor dem Serviceaufruf im Controller oder in einem Web-Infrastructure-Validator zu erledigen und den bestehenden Service-Contract beizubehalten.
- Falls Validierung wiederverwendbar sein soll, kann ein Dienst wie `IAttachmentContentValidator` im Web-Projekt oder Application-Layer eingefuehrt werden. Der bestehende Service kann dann weiterhin nur validierte Werte persistieren.
- Download-Haertung sollte nicht allein im Service erfolgen, weil der Controller die HTTP-Header setzt. Der Service kann Rohdaten liefern; die HTTP-sichere Auslieferung gehoert in den Controller oder eine dedizierte Download-Policy.
- Backup-/Altbestand-Risiko spricht dafuer, auch beim Download eine defensive Content-Type-Policy anzuwenden.
