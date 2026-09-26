# Umsetzungsplan: Bestätigungsdialog hinter Overlay — Anhänge lassen sich nicht löschen

## Übersicht

Der Bestätigungsdialog (`ConfirmDialog`) und alle Overlay-Container (`OverlayHost`, Listen-Overlay in `ListPage`, Mass-Import-Dialog in `Home`) teilen sich die CSS-Klasse `.split-center` mit `z-index: 1000`; da `ConfirmationDialogHost` auf allen Seiten **vor** den Overlays im DOM steht, überdeckt das Overlay den Dialog. Es wird eine dedizierte Schichtklasse auf den äußeren Container des `ConfirmDialog` gelegt und in `app.css` ein höherer `z-index` vergeben — unabhängig von der DOM-Reihenfolge, auf allen Seiten und gegenüber allen Overlay-Varianten. Betroffen ist ausschließlich die Frontend-Darstellung (`FinanceManager.Web`); `ConfirmationService`, `AttachmentsPanel`-Logik und die Attachment-API bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Schichtung Bestätigungsdialog | Dedizierte Schichtklasse `confirm-dialog-layer` am `.split-center`-Container des `ConfirmDialog` mit `z-index: 1100` in `app.css` | Ansatz 1 aus der Anforderung (dort als bevorzugt markiert): Löst den Konflikt zentral und gilt seitenübergreifend (`CardPage`, `ListPage`, `Home`, `ReportDashboard`), ohne dass jede Seite einzeln gepflegt werden muss. Deckt auch die weiteren `z-index: 1000`-Overlays (`.modal-overlay`, `.modal-backdrop`) sowie `.rsw-backdrop` (900/901) ab — der Bug ist nicht anhangsspezifisch (`ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`, `HomeKpiGrid` nutzen denselben `ConfirmAsync`-Pfad). |
| DOM-Reihenfolge der Hosts | **Keine** Umstellung von `ConfirmationDialogHost` hinter die Overlay-Container | Ansatz 2 (fragilere Alternative) wird verworfen: Bei eigenem `z-index` ist die DOM-Reihenfolge irrelevant; ein Umstellen auf allen vier Seiten wäre redundante, fehleranfällige Pflege. |
| Generisches z-index-Schichtsystem | Keine gestaffelte Ebenen-Hierarchie pro Dialogtyp | YAGNI: Es existiert genau ein Dialogtyp oberhalb von Overlays (die Bestätigung). Eine generische Skala wird erst eingeführt, wenn ein zweiter Dialogtyp eine eigene Ebene benötigt. |
| E2E-Zielkarte für den Löschfluss | Kontaktkarte `/card/contacts/{id}` mit Ribbon-Selektor `#Attachments` und Seeding über `AttachmentEntityKind.Contact = 2` — **nicht** die Kontenkarte mit `#OpenAttachments` | Die ursprüngliche Fehlerbeschreibung schildert den Bug explizit in der Kontakt­detailansicht („Ein Kontakt hat mehrere Dateianhänge. Der Anwender ruft die Kontaktdetailansicht auf"). Zudem existiert die Ribbon-Id `Attachments` nur auf der Kontakt- und Wertpapierkarte (`ContactCardViewModel` Zeile 300, `SecurityCardViewModel` Zeile 383); auf der Kontenkarte heißt die Aktion `OpenAttachments` (`BankAccountCardViewModel` Zeile 277) — `#Attachments` wäre dort nicht klickbar. Der getestete Codepfad (`OverlayHost` → `AttachmentsPanel` → `ConfirmationService.ConfirmAsync`) ist identisch, daher bildet die Kontaktkarte die Anforderung am nächsten ab, ohne Testaussage zu verlieren. |

## Programmabläufe

### Anhang aus geöffnetem Anhangs-Overlay löschen (Happy Path)

1. Benutzer öffnet eine Kontaktkarte (`/card/contacts/{id}`) und klickt die Ribbon-Aktion `Attachments` (Ribbon-Id `Attachments`, `ContactCardViewModel` Zeile 300); `BaseViewModel.RequestOpenAttachments(AttachmentEntityKind.Contact, Id)` baut eine `UiOverlaySpec(typeof(AttachmentsPanel), ...)` und löst `UiActionRequested` aus.
2. `OverlayHost.OnUiActionRequested` rendert `.split-center` (`z-index: 1000`) mit `AttachmentsPanel` via `DynamicComponent`.
3. Benutzer klickt den Lösch-Button eines Eintrags; `AttachmentsPanel.DeleteAsync` ruft `ConfirmationService.ConfirmAsync` mit `ConfirmationSeverity.Critical` auf.
4. `ConfirmationService` setzt `CurrentRequest` und löst `OnChanged` aus; `ConfirmationDialogHost` rendert `ConfirmDialog`, dessen äußerer Container nun `class="split-center confirm-dialog-layer"` trägt und per `z-index: 1100` **oberhalb** des Overlays liegt — sichtbar und bedienbar.
5. Benutzer bestätigt; `ConfirmDialog.OnConfirm` → `ConfirmationService.SetResult(true)` → `ConfirmAsync` kehrt mit `true` zurück → `AttachmentsPanel.DeleteAsync` ruft `Api.Attachments_DeleteAsync` auf und lädt die Liste via `LoadAsync(reset: true)` neu — das Overlay bleibt dabei geöffnet.

Beteiligte Klassen/Komponenten: `BaseViewModel`, `OverlayHost`, `AttachmentsPanel`, `ConfirmationService`, `ConfirmationDialogHost`, `ConfirmDialog`, `AttachmentsController`/`IAttachmentService` (unverändert)

### Abbruchpfade des Bestätigungsdialogs über einem Overlay

1. Benutzer klickt Abbrechen oder den Schließen-Button im `ConfirmDialog` → `OnCancel` → `ConfirmationService.Cancel()` → Dialog wird ausgeblendet; das darunterliegende Overlay bleibt geöffnet, der Anhang bleibt erhalten.
2. Benutzer klickt auf den Backdrop (`.split-center.confirm-dialog-layer` außerhalb des Dialogs) → `OnBackdropClick` → `ConfirmationService.Cancel()` → identisches Verhalten: Nur die Bestätigung wird abgebrochen, das Overlay wird **nicht** geschlossen (Bestandsverhalten, unverändert). Da die `confirm-dialog-layer` den kompletten Viewport abdeckt, erreicht der Klick den Overlay-Backdrop gar nicht — ein versehentliches `CloseOverlay` ist durch die Schichtung ausgeschlossen.

Beteiligte Klassen/Komponenten: `ConfirmDialog`, `ConfirmationService`, `ConfirmationDialogHost`, `OverlayHost` (unverändert)

## Neue Klassen

Keine. Die Umsetzung besteht aus einer zusätzlichen CSS-Klasse im Markup und einer neuen CSS-Regel.

## Änderungen an bestehenden Klassen

### `ConfirmDialog` (`FinanceManager.Web/Components/Shared/ConfirmDialog.razor`)

- **Geändertes Markup:** Äußerer Container (Zeile 7) erhält die zusätzliche Klasse `confirm-dialog-layer`: `class="split-center confirm-dialog-layer"`. Damit ist der Backdrop-Container gezielt per CSS adressierbar, ohne andere `.split-center`-Verwendungen zu beeinflussen. Alle Methoden (`OnConfirm`, `OnCancel`, `OnBackdropClick`, `OnAfterRenderAsync`) bleiben unverändert.

### `app.css` (`FinanceManager.Web/wwwroot/css/app.css`)

- **Neue Regel:** `.split-center.confirm-dialog-layer { z-index: 1100; }` — unmittelbar nach dem `.split-center`-Block (ca. Zeile 417) bzw. im Block der `.confirm-dialog`-Regeln (Zeilen 425+). Der Wert `1100` liegt über allen bekannten Overlay-Schichten (`.split-center`, `.modal-overlay`, `.modal-backdrop` = 1000; `.rsw-backdrop`/Panel = 900/901; Sticky-Header = 500) und über `#blazor-error-ui` (1000).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es gibt keine neuen oder geänderten Eingaben.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Globale Schichtänderung für alle Bestätigungsdialoge:** Der `ConfirmDialog` liegt künftig auf allen Seiten über jedem Overlay — das ist die geforderte Verhaltenskorrektur und deckt neben `AttachmentsPanel` auch `ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`, `HomeKpiGrid` sowie die `.modal-overlay`-Dialoge in `ReportDashboard`/`Home` ab. Seiten ohne geöffnetes Overlay ändern sich nicht.
- **`#blazor-error-ui` (z-index 1000):** Liegt nun unterhalb eines gleichzeitig offenen Bestätigungsdialogs — praktisch irrelevant, da die Fehler-UI nur bei Verbindungsabbruch erscheint.
- **`theme.Dark.css`:** Enthält keinen eigenen `z-index` (nur Backdrop-Farbe `rgba(0,0,0,.55)` auf `.split-center`, Zeilen 131–133) — die neue Klasse erbt dieses Styling automatisch, keine Änderung nötig. Auf Konsistenz prüfen (Requirement).
- **Backdrop-Interaktion:** Unverändert — ein Klick auf den Confirm-Backdrop bricht nur die Bestätigung ab; der darunterliegende Overlay-Backdrop (`CloseOverlay` in `OverlayHost`, `CloseOverlay` in `ListPage`) ist durch die obere Schicht vor versehentlichen Klicks geschützt.
- **Keine Auswirkung bei deaktivierten Bestätigungen:** Ist `ShowConfirmations` ausgeschaltet, rendert `ConfirmDialog` nichts — keine Verhaltensänderung in diesem Modus.

## Umsetzungsreihenfolge

1. **`ConfirmDialog.razor`: Schichtklasse ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: Dem äußeren `<div>` (Zeile 7) die Klasse `confirm-dialog-layer` hinzufügen (`class="split-center confirm-dialog-layer"`).

2. **`app.css`: Schichtregel ergänzen**
   - Voraussetzungen: Schritt 1 (die Klasse existiert im Markup; die CSS-Regel ist ohne sie wirkungslos).
   - Beschreibung: Neue Regel `.split-center.confirm-dialog-layer { z-index: 1100; }` im Split-Dialog-Block der `app.css` (bei den Zeilen 409–441) hinzufügen.

3. **bUnit-Test für die Schichtklasse**
   - Voraussetzungen: Schritt 1; bestehende Testinfrastruktur `ConfirmDialogTests` mit `PassthroughLocalizer` und `IConfirmationService`-Mock (im Repo vorhanden).
   - Beschreibung: Neuen Test in `FinanceManager.Tests/Components/ConfirmDialogTests.cs` ergänzen, der prüft, dass der äußere Container beide Klassen `split-center` und `confirm-dialog-layer` trägt (verankert den CSS-Hook testbar, da bUnit kein CSS auswertet).

4. **E2E-Tests für den Löschfluss über geöffnetem Overlay**
   - Voraussetzungen: Schritte 1–2; `PlaywrightWebAppFixture`, `AuthGateway`, `TestUserSeeder`, `AccountsApiSeedHelper.CreateBankContactAsync` (DB-Seeding eines Kontakts, vorhanden), `BrowserApiHelper.PostMultipartAsync` (vorhanden); Upload-Endpunkt `POST /api/attachments/{entityKind}/{entityId}` mit `AttachmentEntityKind.Contact = 2` (vorhanden); Kontaktkarte `/card/contacts/{id}` mit Ribbon-Id `Attachments` (vorhanden).
   - Beschreibung: Neue Szenarien in `FinanceManager.Tests.E2E/Tests/Confirmation/ConfirmationDialogE2ETests.cs` (Happy Path Löschen, Abbruch per Button, Abbruch per Backdrop-Klick) auf der Kontaktkarte — Details im Abschnitt Tests.

5. **Regression: Testlauf**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet test` für `FinanceManager.Tests` und `FinanceManager.Tests.E2E` ausführen; der bekannte, bug-unabhängige Fehlschlag `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` bleibt bestehen und ist nicht Teil dieser Aufgabe.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ConfirmDialog_RendersDedicatedLayerClass` | `ConfirmDialogTests` (`FinanceManager.Tests/Components/ConfirmDialogTests.cs`) | Äußerer Container trägt `split-center` **und** `confirm-dialog-layer` — sichert den CSS-Hook, auf den die Schichtkorrektur zielt. |
| `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` | `ConfirmationDialogE2ETests` (`FinanceManager.Tests.E2E/Tests/Confirmation/ConfirmationDialogE2ETests.cs`) | Happy Path: Kontakt + zwei Anhänge seeden (`AccountsApiSeedHelper.CreateBankContactAsync` mit `_fixture.DatabasePath`/`user.Id`, `BrowserApiHelper.PostMultipartAsync` auf `POST /api/attachments/2/{contactId}` — `AttachmentEntityKind.Contact = 2`), `/card/contacts/{id}` öffnen, Ribbon `#Attachments` klicken (Ribbon-Id `Attachments`, `ContactCardViewModel` Zeile 300 — **nicht** `#OpenAttachments`, das nur auf der Kontenkarte existiert), Lösch-Button `button.icon-btn.danger` in der Anhangstabelle klicken, `.confirm-dialog` sichtbar, Bestätigen-Button anklicken (Playwright-Hit-Target-Check schlägt fehl, wenn der Dialog verdeckt ist), Anhangszeile verschwindet, Overlay bleibt offen. |
| `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` | `ConfirmationDialogE2ETests` | Abbruchpfad per Abbrechen-Button (`.confirm-dialog-actions button.secondary`): Dialog schließt, Overlay und Anhangszeile bleiben. |
| `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` | `ConfirmationDialogE2ETests` | Backdrop-Klick auf `.split-center.confirm-dialog-layer` (per `page.Mouse.ClickAsync` auf einen Punkt außerhalb des Dialogs, z. B. Viewport-Ecke): Bestätigung bricht ab, `.split-dialog` des Overlays bleibt sichtbar, Anhang bleibt erhalten. |

### Betroffene bestehende Tests

Keine — die Änderung am `ConfirmDialog`-Markup ist rein additiv (zusätzliche CSS-Klasse); bestehende bUnit-Tests (`ConfirmDialogTests`, `ConfirmationDialogHostTests`, `OverlayHostTests`, `CardPageTests`) prüfen keine Klassenattribute am Backdrop-Container und bleiben unverändert gültig.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Anhang im geöffneten Overlay der Kontaktkarte löschen: Dialog sichtbar/bedienbar, Bestätigung löscht den Anhang | `ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` | Löschen von Anhängen ist wieder möglich; Bestätigungsdialog liegt über dem Overlay | Der Bug ist ein reines CSS-Layering-Problem; bUnit wertet kein `z-index` aus — nur ein echter Browser-Stack kann die Schichtreihenfolge und Klickbarkeit nachweisen (Playwright-Hit-Target-Check beim Klick auf den Bestätigen-Button). |
| Pflicht | Abbrechen im Dialog über Overlay: Dialog schließt, Overlay + Anhang bleiben | `ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` | Abbruchpfad über Overlay funktioniert weiter | Sichtbarer Benutzerfluss; stellt sicher, dass die neue Schicht keine Interaktion blockiert. |
| Pflicht | Backdrop-Klick bricht nur die Bestätigung ab, Overlay bleibt offen | `ConfirmationDialogE2ETests.AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` | Bestätigungs-Backdrop schließt nicht versehentlich das Overlay | Anforderung verlangt explizit die Prüfung der Backdrop-Klick-Behandlung unter der neuen Schichtung; nur im Browser reproduzierbar. |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Der bestehende E2E-Test `AccountDelete_RibbonAction_ShowsConfirmationAndDeletesOnConfirm` löscht ohne geöffnetes Overlay und bleibt gültig. |

Hinweis zur Abdeckung: Für die übrigen betroffenen Overlays (`ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`, `HomeKpiGrid`, `.modal-overlay`-Dialoge) wird **kein** separater E2E-Test je Overlay-Typ geplant — alle nutzen denselben `ConfirmAsync`-Pfad und denselben `z-index: 1000`-Container, sodass die Schichtkorrektur durch die drei `AttachmentsPanel`-Szenarien repräsentativ nachgewiesen ist (vom Anwender so bestätigt).

## Offene Punkte

Keine.
