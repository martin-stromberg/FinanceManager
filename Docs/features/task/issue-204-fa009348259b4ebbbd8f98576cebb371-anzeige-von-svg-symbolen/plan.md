# Umsetzungsplan: Anzeige von SVG-Symbolen

## Ziel

SVG-Anhänge, die als Kontakt-Symbole gespeichert sind, werden beim Download wieder mit `image/svg+xml` ausgeliefert und dadurch in den bestehenden `<img>`-Renderpfaden sichtbar angezeigt. Die bestehende Upload-Validierung fuer sichere SVGs und der Download-Schutz per `X-Content-Type-Options: nosniff` bleiben erhalten.

## Ausgangsbefund

- Sichere SVG-Dateien werden serverseitig bereits erkannt und beim Upload als `image/svg+xml` gespeichert.
- Die UI rendert Kontakt-Symbole zentral ueber `<img src="/api/attachments/{id}/download">` in generischen Listen, Karten und im `SymbolPicker`.
- `AttachmentContentPolicy.NormalizeDownloadContentType` erlaubt `image/svg+xml` beim Download aktuell nicht und normalisiert solche Anhänge deshalb auf `application/octet-stream`.
- `AttachmentsController.DownloadFile` setzt weiterhin `X-Content-Type-Options: nosniff`; dadurch darf der Browser den falschen MIME-Type nicht als SVG interpretieren.

## Umsetzungsstrategie

Die Korrektur erfolgt zentral im Attachment-Download. Es werden keine UI-spezifischen Sonderpfade, keine Datenmigration und keine Änderung am Kontaktmodell eingeplant.

## Arbeitspakete

### 1. Download-MIME-Allowlist erweitern

Datei: `FinanceManager.Web/Infrastructure/Attachments/AttachmentContentPolicy.cs`

- In `NormalizeDownloadContentType` den gespeicherten Typ `image/svg+xml` als sicheren Download-Typ durchreichen.
- Die bestehende Fallback-Logik fuer unbekannte oder riskante Typen unverändert auf `application/octet-stream` belassen.
- Keine Änderung an `SvgActiveContentRegex`, `IsSafeSvg` oder `DetectContentType`, weil diese Upload-Schutzlogik bereits die fachliche Akzeptanz sicherer SVGs abdeckt.

### 2. Controller-Regressionstest fuer SVG-Download ergänzen

Datei: `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

- Neuen Test `DownloadAsync_ShouldReturnSvgContentType_ForStoredSvg` ergänzen.
- Testaufbau analog zu `DownloadAsync_ShouldFallback_RiskyContentType_ToOctetStream`.
- Der gemockte Attachment-Service liefert ein gespeichertes Attachment mit Dateiname `symbol.svg` und Content-Type `image/svg+xml`.
- Erwartung:
  - Ergebnis ist `FileStreamResult`.
  - `FileDownloadName` bleibt `symbol.svg`.
  - `ContentType` ist `image/svg+xml`.
  - Header `X-Content-Type-Options` bleibt `nosniff`.

### 3. Upload-Test fuer sicheres SVG ergänzen

Datei: `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

- Neuen Test `UploadAsync_ShouldAccept_SafeSvg` ergänzen.
- Testoptionen enthalten mindestens `image/svg+xml`.
- Testdatei ist ein minimales, sicheres SVG, z. B. mit `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1 1"><path d="M0 0h1v1H0z"/></svg>`.
- Erwartung:
  - Upload endet mit `OkObjectResult`.
  - `IAttachmentService.UploadAsync` wird mit normalisiertem Content-Type `image/svg+xml` aufgerufen.
  - Bestehende Kategorie-/Rollenlogik wird nicht unnötig berührt; der Test kann ohne `AttachmentRole.Symbol` laufen, weil die MIME-Erkennung unabhängig von der Rolle ist.

### 4. Negativtest fuer unsichere SVG-Inhalte ergänzen

Datei: `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

- Neuen Test `UploadAsync_ShouldReject_UnsafeSvg` ergänzen.
- Testdatei nutzt ein riskantes SVG-Merkmal, z. B. `<script>` oder `onload=`.
- Erwartung:
  - Controller gibt `400 Bad Request` zurueck.
  - Fehlercode bleibt `Err_Invalid_ContentType`.
- Damit wird abgesichert, dass die Download-Freigabe fuer `image/svg+xml` nicht als Aufweichung der Upload-Sicherheitsregeln missverstanden wird.

### 5. Bestehende Risky-Type-Regression beibehalten

Datei: `FinanceManager.Tests/Controllers/AttachmentsControllerTests.cs`

- Den vorhandenen Test `DownloadAsync_ShouldFallback_RiskyContentType_ToOctetStream` unverändert beibehalten.
- Falls durch die neue SVG-Allowlist Assertions oder Hilfsdaten angepasst werden muessen, sicherstellen, dass `text/html` weiterhin auf `application/octet-stream` normalisiert wird.

## Nicht eingeplante Änderungen

- Keine Anpassung von `GenericListPage.razor`, `GenericCardPage.razor` oder `SymbolPicker.razor`; alle betroffenen UI-Stellen verwenden bereits den zentralen Download-Endpunkt.
- Keine Migration bestehender Attachment-Daten; bereits gespeicherte SVGs enthalten den benoetigten Content-Type und profitieren von der Download-Korrektur.
- Keine Erweiterung der erlaubten Dateiformate.
- Keine Änderung am Kontakt-Domainmodell, an DTOs oder ViewModels.

## Verifikation

Minimal auszufuehren:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter AttachmentsControllerTests
```

Optional bei Bedarf:

```powershell
dotnet test FinanceManager.sln
```

Manuelle Smoke-Pruefung:

1. Bestehenden Kontakt mit SVG-Symbol oeffnen.
2. Kontaktliste und Kontakt-Detailkarte pruefen.
3. Neues sicheres SVG als Kontaktsymbol hochladen.
4. Sicherstellen, dass PNG/JPEG/ICO-Symbole weiterhin sichtbar bleiben.

## Risiken und Gegenmaßnahmen

- Risiko: SVG wird mit `nosniff` nur dann gerendert, wenn der Content-Type exakt als Bild-MIME ausgeliefert wird. Gegenmaßnahme: Controller-Test prueft den effektiven `FileStreamResult.ContentType`.
- Risiko: SVG-Freigabe koennte als Sicherheitslockerung wirken. Gegenmaßnahme: Upload-Negativtest fuer aktive SVG-Inhalte ergaenzen und vorhandene Fallback-Tests fuer riskante Download-Typen beibehalten.
- Risiko: UI-Tests fuer alle Kontaktansichten waeren aufwendig und duplizieren die zentrale Ursache. Gegenmaßnahme: Die Planung fokussiert auf den Download-Endpunkt, da alle identifizierten Renderpfade denselben Endpunkt verwenden.

## Offene Punkte

Keine.
