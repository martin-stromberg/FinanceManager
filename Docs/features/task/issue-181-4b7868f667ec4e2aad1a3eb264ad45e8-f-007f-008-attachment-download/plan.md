# Umsetzungsplan: Attachment-Download-Tokens und Attachment-Upload absichern

## Zielbild

Attachment-Downloads duerfen Query-Tokens nicht mehr im Klartext in Request-Logs schreiben. Uploads sollen frueh und fachlich begrenzt werden, Content-Types serverseitig anhand einer Attachment-Policy bestimmt werden und Downloads sollen auch fuer Altbestand defensiv mit sicherem Content-Type und `Content-Disposition: attachment` ausgeliefert werden.

## Leitentscheidungen

- Die Service-Contracts fuer Attachments bleiben unveraendert. Validierung, Content-Type-Normalisierung und HTTP-Header-Haertung werden im Web-Projekt umgesetzt.
- Der `token`-Query-Parameter wird case-insensitive redigiert. Nicht-sensitive Query-Parameter bleiben fuer Diagnosezwecke sichtbar.
- Der Upload-Controller bekommt keine unbegrenzte Request-Groesse mehr. Die Grenze wird an die zentrale Attachment-Maximalgroesse gekoppelt und die bestehende fachliche Pruefung in `UploadAsync` bleibt erhalten.
- Serverseitige Content-Type-Ermittlung wird vor dem Serviceaufruf ausgefuehrt. Der an den Service uebergebene Content-Type ist der validierte oder sichere Fallback-Typ, nicht der rohe Client-Header.
- Downloads setzen immer einen Download-Dateinamen und damit Attachment-Disposition. Riskante oder unbekannte gespeicherte Content-Types werden auf `application/octet-stream` reduziert; zusaetzlich soll `X-Content-Type-Options: nosniff` gesetzt werden.

## Umsetzungsschritte

### 1. Request-Logging redigieren

Betroffene Datei:

- `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs`

Vorgehen:

1. Eine kleine Hilfsfunktion in der Middleware oder eine interne Hilfsklasse einfuehren, die aus `HttpRequest.Path` und `HttpRequest.QueryString` einen log-sicheren Pfad erzeugt.
2. Sensitive Parameternamen als case-insensitive Set fuehren; initial mindestens `token`.
3. Query-Werte sensibler Parameter durch einen konstanten Platzhalter wie `[REDACTED]` ersetzen.
4. URL-Encoding und mehrfach vorhandene Query-Werte ueber ASP.NET-Core-Helfer wie `QueryHelpers.ParseQuery`/`QueryBuilder` oder aequivalente strukturierte APIs behandeln.
5. Beide bisherigen Logpfade in `InvokeAsync` auf die neue Hilfsfunktion umstellen:
   - Erfolgspfad nach `_next(context)`
   - Exception-Pfad im `catch`

Akzeptanz:

- Ein Request mit `?token=secret`, `?Token=secret` oder gemischter Schreibweise enthaelt `secret` in keinem formatierten Logeintrag mehr.
- Nicht-sensitive Parameter wie `page=1` oder `foo=bar` bleiben sichtbar.

### 2. Attachment-Upload-Grenzen vereinheitlichen

Betroffene Dateien:

- `FinanceManager.Web/Infrastructure/Attachments/AttachmentUploadOptions.cs`
- `FinanceManager.Web/Controllers/AttachmentsController.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- optional `FinanceManager.Web/appsettings.json`

Vorgehen:

1. In `AttachmentUploadOptions` eine zentrale Default-Konstante fuer die maximale Attachment-Groesse einfuehren und `MaxSizeBytes` darauf initialisieren.
2. `[RequestSizeLimit(long.MaxValue)]` am Attachment-Upload entfernen und durch eine realistische Grenze ersetzen.
3. Wenn eine statische Attributgrenze verwendet wird, dieselbe zentrale Default-Konstante fuer `RequestSizeLimit` und `RequestFormLimits` nutzen. Wenn runtime-konfigurierbare Limits erforderlich bleiben, stattdessen einen Resource-Filter einsetzen, der `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` vor dem Lesen des Bodys aus `IOptions<AttachmentUploadOptions>` setzt.
4. `FormOptions.MultipartBodyLengthLimit` in `ProgramExtensions` fuer normale Multipart-Requests nicht mehr auf 1 GB setzen, sondern an `Attachments:MaxSizeBytes` beziehungsweise die zentrale Default-Groesse koppeln. Der bestehende `BackupsController` behaelt seine expliziten Backup-Limits.
5. Die fachliche Pruefung `file.Length > _options.MaxSizeBytes` im Controller beibehalten, damit kontrollierte Fehlermeldungen erhalten bleiben, wenn der Request den Controller erreicht.

Akzeptanz:

- Der Attachment-Upload verwendet nirgends mehr `long.MaxValue` als Controller-/Action-Limit.
- Die globale Multipart-Grenze ist nicht grosszuegiger als die fachliche Attachment-Groesse, soweit keine explizit anders begruendete Ausnahme fuer andere Endpunkte existiert.

### 3. Serverseitige Attachment-Content-Policy einfuehren

Betroffene Dateien:

- neuer Typ unter `FinanceManager.Web/Infrastructure/Attachments/`, z. B. `AttachmentContentPolicy.cs`
- `FinanceManager.Web/Controllers/AttachmentsController.cs`
- `FinanceManager.Web/ProgramExtensions.cs`, falls die Policy per DI registriert wird

Vorgehen:

1. Eine dedizierte Policy/Validator-Klasse anlegen, die Dateiinhalt und optional den Client-Content-Type prueft.
2. Die Policy soll als Ergebnis mindestens liefern:
   - `IsAllowed`
   - normalisierter serverseitiger `ContentType`
   - sichere Fehlermeldungs-/Fehlercode-Information fuer den Controller
3. Signaturregeln fuer binaere erlaubte Typen implementieren:
   - PDF: `%PDF-`
   - PNG: `89 50 4E 47 0D 0A 1A 0A`
   - JPEG: `FF D8 FF`
   - ZIP: uebliche ZIP-Header wie `50 4B 03 04`, `50 4B 05 06`, `50 4B 07 08`
   - ICO: `00 00 01 00`
4. Text-/CSV-Regeln als vergleichbare Inhaltsvalidierung implementieren:
   - Text muss als UTF-8 oder kompatibler Text lesbar sein.
   - NUL-Bytes und auffaellige binaere Steuerzeichen werden abgelehnt.
   - CSV kann dieselbe Textbasis verwenden und anhand erlaubtem Header/Dateiendung als `text/csv` normalisiert werden.
5. SVG nur akzeptieren, wenn es sicher als Text/XML erkannt wird, ein `<svg>`-Root besitzt und offensichtliche aktive Inhalte wie `<script>`, Inline-Eventhandler oder externe Referenzen abgelehnt werden. Wenn diese Pruefung zu aufwendig oder unsicher wird, SVG-Uploads ablehnen und bestehende SVG-Altbestaende beim Download als `application/octet-stream` ausliefern.
6. Der Client-Content-Type darf nur noch als Hinweis dienen. Bei Mismatch zwischen Header und Signatur wird abgelehnt, sofern der erkannte Typ nicht explizit in `AllowedMimeTypes` erlaubt ist und die Policy eine sichere Normalisierung begruendet.
7. Vor dem Serviceaufruf den Upload-Stream so lesen oder puffern, dass die Signaturpruefung den Stream nicht fuer `AttachmentService.UploadAsync` verbraucht. Danach einen neuen `MemoryStream` oder einen zurueckgesetzten Stream mit dem validierten Content-Type an den Service uebergeben.

Akzeptanz:

- Ein als `application/pdf` deklarierter Nicht-PDF-Inhalt wird abgelehnt.
- Ein gueltiger PDF-/PNG-/JPEG-/ZIP-/ICO-Inhalt wird akzeptiert, wenn sein normalisierter Typ in `AllowedMimeTypes` enthalten ist.
- Der Service erhaelt den serverseitig bestimmten Content-Type.

### 4. Download-Header und Content-Type defensiv setzen

Betroffene Datei:

- `FinanceManager.Web/Controllers/AttachmentsController.cs`

Vorgehen:

1. Die Download-Auslieferung beider Pfade in eine private Methode kapseln:
   - authentifizierter Download
   - anonymer Token-Download
2. Gespeicherten Content-Type ueber eine Download-Policy normalisieren:
   - leere, unbekannte oder riskante Typen auf `application/octet-stream`
   - serverseitig als passiv betrachtete Typen nur dann durchreichen, wenn sie explizit erlaubt sind
3. `FileDownloadName` immer setzen, damit MVC eine Attachment-Disposition erzeugt.
4. `Response.Headers["X-Content-Type-Options"] = "nosniff"` setzen.
5. Falls Unit-Tests zeigen, dass `FileDownloadName` nicht ausreicht, explizit `Content-Disposition: attachment; filename=...` ueber eine sichere Header-Erzeugung setzen.

Akzeptanz:

- Downloads verwenden keinen ungeprueften clientgelieferten Content-Type.
- Altbestaende mit `text/html`, `image/svg+xml` oder leerem Typ werden nicht inline-riskant ausgeliefert.
- `Content-Disposition` bleibt `attachment` und der Dateiname bleibt erhalten.

### 5. Tests ergaenzen

Betroffene Dateien:

- neuer Test z. B. `FinanceManager.Tests/Infrastructure/RequestLoggingMiddlewareTests.cs`
- `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

Vorgehen:

1. Middleware-Tests mit einem In-Memory-Logger ergaenzen:
   - Erfolgspfad redigiert `token`
   - Exception-Pfad redigiert `token`
   - Schreibweisen `token`, `Token`, `TOKEN` werden gleich behandelt
   - nicht-sensitive Query-Parameter bleiben sichtbar
   - der rohe Tokenwert kommt weder im formatierten Logeintrag noch im Logger-State vor
2. Controller-Tests fuer Upload-Grenzen ergaenzen:
   - Upload-Action enthaelt kein `RequestSizeLimitAttribute` mit `long.MaxValue`
   - falls `RequestFormLimits` genutzt wird, ist dessen Grenze an die Attachment-Groesse gekoppelt
3. Controller-/Policy-Tests fuer Content-Validierung ergaenzen:
   - gueltiger PDF-Inhalt wird akzeptiert
   - Header/Bytes-Mismatch wird abgelehnt
   - leerer oder manipulierter Content-Type wird nicht als alleinige Wahrheit verwendet
   - mindestens PNG/JPEG/ZIP oder repraesentative Typen pruefen
   - Text/CSV-Faelle fuer NUL-Byte-Ablehnung pruefen
4. Download-Tests erweitern:
   - leerer gespeicherter Content-Type wird `application/octet-stream`
   - riskanter gespeicherter Content-Type wird `application/octet-stream`
   - `FileDownloadName` ist gesetzt
   - `X-Content-Type-Options: nosniff` wird gesetzt oder die gekapselte Download-Methode/Policy liefert diesen Header nachweisbar

## Validierung

Auszufuehrende Pruefungen:

1. `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
2. Falls verfuegbar, gezielt nur die neuen Tests ausfuehren, um schnelle Rueckmeldung zu erhalten.
3. Optional manueller Smoke-Test:
   - Attachment-Download-URL mit `?token=<wert>` gegen lokales Logging ausfuehren und pruefen, dass nur `[REDACTED]` erscheint.
   - Upload eines gueltigen PDFs und eines manipulierten PDF-Headers pruefen.

## Risiken und Gegenmassnahmen

- SVG ist fachlich bereits erlaubt, aber sicherheitlich riskant. Die Umsetzung soll SVG nur mit restriktiver Text/XML-Pruefung akzeptieren; beim Download wird SVG defensiv als Attachment und bei Unsicherheit als `application/octet-stream` behandelt.
- Bestehende Attachments koennen falsche gespeicherte Content-Types besitzen. Die Download-Policy schuetzt Altbestand ohne Migration.
- Dynamische Request-Limits sind in ASP.NET Core frueh in der Pipeline relevant. Wenn ein Options-basierter Filter gewaehlt wird, muss er vor Body/Form-Parsing greifen; andernfalls ist die zentrale Default-Konstante die robustere, testbare Variante.
- Die Policy liest Upload-Bytes zur Signaturpruefung. Das ist akzeptabel, weil die Groessenpruefung vorher greift und der Service heute ohnehin in Memory liest.

## Offene Punkte

Keine.
