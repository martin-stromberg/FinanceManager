# Detail: Tests und Testluecken

## Relevante Dateien

- `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`
- `FinanceManager.Tests/FinanceManager.Tests.csproj`

## Vorhandene Attachment-Controller-Tests

Die Testklasse `AttachmentsControllerTests` nutzt xUnit, Moq, `FormFile`, `HeaderDictionary`, `DataProtectionProvider` und `NullLogger`.

Abgedeckte Upload-Faelle:

- `UploadAsync_ShouldReject_EmptyFile` prueft Leerdateien.
- `UploadAsync_ShouldReject_TooLarge` prueft fachliches `_options.MaxSizeBytes`.
- `UploadAsync_ShouldReject_UnsupportedContentType` prueft Ablehnung anhand von `file.ContentType`.
- `UploadAsync_ShouldAccept_ValidPdf` prueft Serviceaufruf mit `application/pdf`.
- `UploadAsync_ShouldPass_CategoryId_ToService_OnUpload` prueft Kategorieuebergabe.
- URL-Upload-Faelle sind separat vorhanden.

Abgedeckte Download-Faelle:

- `DownloadAsync_ShouldReturn_NotFound_WhenMissing`.
- `DownloadAsync_ShouldReturn_FileContentResult` prueft `FileDownloadName` und `ContentType`.

## Fehlende Tests fuer die Anforderung

- Keine Tests fuer `RequestLoggingMiddleware`.
- Keine Tests fuer Query-Redigierung mit `token`, `Token`, gemischter Schreibweise oder mehreren Parametern.
- Keine Tests fuer Exception-Logging-Pfad der Middleware.
- Keine Tests, die verhindern, dass der rohe Tokenwert im Logger-State oder formatierten Logeintrag steht.
- Keine Tests fuer Controller-/Server-Requestlimit statt `long.MaxValue`.
- Keine Tests fuer Magic-Number-/Signaturvalidierung.
- Keine Tests fuer Header/Bytes-Mismatch, z. B. `ContentType = application/pdf` mit Nicht-PDF-Bytes.
- Keine Tests fuer sichere Download-Auslieferung von riskanten gespeicherten Content-Types.
- Keine expliziten Tests fuer `Content-Disposition: attachment`.

## Testinfrastruktur

`FinanceManager.Tests.csproj` referenziert `FinanceManager.Web`, `FinanceManager.Application`, `FinanceManager.Infrastructure`, `FinanceManager.Domain` und `FinanceManager.Shared`. Vorhandene Pakete enthalten xUnit v3, Moq, FluentAssertions und ASP.NET-Core-Typen. Neue Unit-Tests koennen ohne neues Testprojekt im bestehenden Testprojekt ergaenzt werden.

## Empfohlene neue Tests

### Middleware

- Neuer In-Memory-Logger oder Moq-basierter `ILogger<RequestLoggingMiddleware>`, der den formatierten Log-State erfasst.
- Test fuer Erfolgspfad: `GET /api/attachments/{id}/download?token=secret&foo=bar`.
- Test fuer Exception-Pfad: downstream Delegate wirft Exception; Log darf `secret` nicht enthalten.
- Case-insensitive Varianten: `token`, `Token`, `TOKEN`.
- Nicht-sensitive Parameter bleiben sichtbar.

### Controller Upload

- Valide Signaturen:
  - PDF: `%PDF-`
  - PNG: `89 50 4E 47 0D 0A 1A 0A`
  - JPEG: `FF D8 FF`
  - ZIP: `50 4B 03 04` oder weitere ZIP-Header nach Policy
- Mismatch-Faelle:
  - Header `application/pdf`, Inhalt `not pdf`.
  - Header leer oder `application/octet-stream`, Inhalt PDF.
  - Datei mit riskanter SVG/XML-Nutzlast, je nach Policy ablehnen oder sicher behandeln.
- Service-Verify sollte den serverseitig bestimmten Content-Type erwarten, nicht den Originalheader.

### Controller Download

- Gespeicherter leerer Content-Type fuehrt zu `application/octet-stream`.
- Gespeicherter riskanter Content-Type wie `image/svg+xml` oder `text/html` wird nicht inline-riskant ausgeliefert.
- `FileDownloadName` bleibt gesetzt.
- Falls Header direkt getestet werden sollen, kann ein ControllerContext mit `DefaultHttpContext` verwendet und das Result ausgefuehrt werden; alternativ reicht fuer Unit-Ebene zunaechst die `FileStreamResult`-Eigenschaft plus explizite Policy-Methode.
