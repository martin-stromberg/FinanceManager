# Bestandsaufnahme: Attachment-Download-Tokens und Attachment-Upload absichern

## Kontext

- Solution: `FinanceManager.sln`
- Anforderung: `Docs/features/task/issue-181-4b7868f667ec4e2aad1a3eb264ad45e8-f-007f-008-attachment-download/requirement.md`
- Betroffene Kernbereiche:
  - `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs`
  - `FinanceManager.Web/Controllers/AttachmentsController.cs`
  - `FinanceManager.Infrastructure/Attachments/AttachmentService.cs`
  - `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

## Detaildokumente

- [Logging und Token-Redigierung](inventory/logging.md)
- [Attachment-Controller](inventory/attachments-controller.md)
- [Attachment-Service und Persistenz](inventory/attachment-service.md)
- [Tests und Testluecken](inventory/tests.md)
- [Konfiguration und Runtime-Grenzen](inventory/configuration.md)

## Zusammenfassung der Ist-Situation

Die Request-Logging-Middleware protokolliert erfolgreiche, fehlerhafte und exception-basierte Requests mit `context.Request.Path + context.Request.QueryString`. Dadurch wird der `token`-Query-Parameter von anonymen Attachment-Downloads im Klartext in alle aktiven Logger geschrieben, darunter Console-Logging und optional File-Logging.

Der Attachment-Upload ist auf Action-Ebene mit `[RequestSizeLimit(long.MaxValue)]` praktisch unbegrenzt. Parallel existieren zwar fachliche Upload-Optionen mit `MaxSizeBytes`, diese greifen aber erst im Controller nach Modellbindung/Form-Parsing. Die globale Multipart-Grenze ist in `ProgramExtensions` auf 1 GB gesetzt, waehrend Kestrel in `appsettings.json` 20 MB konfiguriert.

Die Upload-Validierung prueft derzeit nur Groesse, Leerdatei und den vom Client gelieferten `IFormFile.ContentType`. Die Bytes werden ohne serverseitige Signaturpruefung an den Service uebergeben. Der Service speichert `fileName`, `contentType` und `bytes` unveraendert.

Downloads geben den gespeicherten Content-Type direkt zurueck und setzen ueber `File(content, contentType, fileName)` einen Download-Dateinamen. Ein expliziter `Content-Disposition: attachment`-Header wird im Controller nicht gesetzt; das MVC-FileResult erzeugt voraussichtlich durch `fileName` eine Attachment-Disposition, dies ist aber in Tests nicht verifiziert und nicht sichtbar gehaertet.

## Hauptbefunde

| Bereich | Fundstelle | Befund | Relevanz |
|---------|------------|--------|----------|
| Logging | `RequestLoggingMiddleware.cs:51`, `RequestLoggingMiddleware.cs:63` | Pfad und QueryString werden roh zusammengesetzt. | Erfuellt F-007 aktuell nicht; Download-Tokens landen in Logs. |
| Upload-Limit | `AttachmentsController.cs:118-123` | `[RequestSizeLimit(long.MaxValue)]` hebelt fruehe Groessenbegrenzung aus. | Erfuellt F-008 aktuell nicht. |
| Multipart-Limit | `ProgramExtensions.cs:81-84` | Globale Multipart-Grenze ist 1 GB. | Nicht an Attachment-Maximalgroesse gekoppelt. |
| Upload-MIME | `AttachmentsController.cs:150-157` | Whitelist basiert ausschliesslich auf `file.ContentType`. | Manipulierter Header reicht fuer Akzeptanz. |
| Persistenz | `AttachmentService.cs:63-75` | Content-Type und Bytes werden unveraendert persistiert. | Keine serverseitige Normalisierung oder Validierung. |
| Download | `AttachmentsController.cs:270-289` | Gespeicherter Content-Type wird direkt ausgeliefert. | Riskante Inhalte koennen mit ungeprueftem Typ ausgeliefert werden. |
| Tests | `AttachmentsControllerTests.cs` | Bestehende Tests decken Groesse, Header-MIME und Basisdownload ab. | Neue Sicherheitsanforderungen sind noch ungetestet. |

## Bestehende Erweiterungspunkte

- `AttachmentUploadOptions` enthaelt bereits `MaxSizeBytes` und `AllowedMimeTypes`; es ist der naheliegende Ort fuer erlaubte Typen und eventuell serverseitige Signaturregeln.
- `AttachmentsController.UploadAsync` ist der zentrale Eintrittspunkt fuer Benutzer-Uploads und kann vor dem Serviceaufruf Inhaltstyp validieren und normalisieren.
- `AttachmentService.UploadAsync` speichert den uebergebenen Content-Type unveraendert; wenn der Controller serverseitig normalisiert, bleibt das Service-Interface kompatibel.
- `RequestLoggingMiddleware` ist klein und isoliert; eine private Redigierungsfunktion laesst sich dort oder in einer kleinen Hilfsklasse testen.
- `FinanceManager.Tests` referenziert `FinanceManager.Web`, nutzt xUnit v3, Moq und ASP.NET Core Testtypen; neue Unit-Tests koennen ohne neues Testprojekt ergaenzt werden.

## Risiken fuer die Planung

- `image/svg+xml` ist aktuell in der Default-Whitelist und wird auch im SymbolPicker akzeptiert. SVG ist fuer Inline-Ausfuehrung besonders riskant; fuer Downloads sollte mindestens Attachment-Disposition erzwungen werden. Fuer Uploads muss geplant werden, ob SVG weiter erlaubt, anders behandelt oder abgelehnt wird.
- Textformate wie `text/plain` und `text/csv` haben keine eindeutigen Magic Numbers. Eine "vergleichbare Signatur" kann hier nur ueber sichere Textdekodierung, Steuerzeichen-/NUL-Pruefung und Fallback auf `application/octet-stream` erfolgen.
- Bestehende gespeicherte Attachments koennen bereits ungepruefte Content-Types enthalten. Die Download-Haertung sollte daher auch Altbestand absichern und nicht nur neue Uploads.
- Das Service-Interface wird in mehreren Bereichen verwendet (`StatementDraftService`, Setup/Demo, Controller-Tests). Eine Interface-Aenderung haette hoeheren Anpassungsaufwand; eine Controller-seitige Validierung mit unveraendertem Interface ist risikoaermer.

## Testbedarf

- Unit-Tests fuer Query-Redigierung: `token`, `Token`, gemischte Schreibweise, mehrere Werte, weitere nicht-sensitive Parameter bleiben sichtbar.
- Middleware-Tests fuer Erfolgspfad und Exception-Pfad, damit beide bisherigen `Path + QueryString`-Logpfade abgedeckt sind.
- Controller-Tests fuer fruehe Upload-Limit-Kopplung soweit per Attribut/Metadaten pruefbar.
- Controller-Tests fuer Magic-Number-Validierung: valide PDF/PNG/JPEG, Header/Bytes-Mismatch, unbekannter oder leerer Content-Type.
- Controller-Tests fuer Download-Header: sicherer Fallback-Content-Type und explizite Attachment-Disposition bzw. `FileDownloadName`.

## Empfehlung fuer die Umsetzung

Die kleinste robuste Umsetzung ist, die Redigierung direkt in der Logging-Middleware zu kapseln, Upload-Grenzen aus einer zentralen Attachment-Maximalgroesse abzuleiten, im Controller den MIME-Typ aus Dateiinhalt und erlaubter Policy zu bestimmen und Downloads unabhaengig vom gespeicherten Typ mit sicherem Content-Type plus Attachment-Disposition auszuliefern. Altbestand wird dadurch beim Download abgesichert, ohne eine Migration zu erzwingen.
