# Tasks: Bestätigungsdialog hinter Overlay — Anhänge lassen sich nicht löschen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI | `ConfirmDialog.razor`: Äußeren Container (Zeile 7) um die Klasse `confirm-dialog-layer` ergänzen (`class="split-center confirm-dialog-layer"`) | Offen | — |
| 2 | Stil/Schichtung | `app.css`: Neue Regel `.split-center.confirm-dialog-layer { z-index: 1100; }` im Split-Dialog-Block (Zeilen 409–441) ergänzen | Offen | — |
| 3 | Stil/Schichtung | `theme.Dark.css` auf Konsistenz prüfen (kein eigener `z-index` auf `.split-center` — keine Änderung erwartet) | Offen | — |
| 4 | Tests | `ConfirmDialogTests`: Test `ConfirmDialog_RendersDedicatedLayerClass` ergänzen — Container trägt `split-center` und `confirm-dialog-layer` | Offen | — |
| 5 | E2E-Tests | `ConfirmationDialogE2ETests`: Test `AttachmentDelete_OverlayOpen_ShowsConfirmationAboveOverlayAndDeletes` — Kontaktkarte (`/card/contacts/{id}`), Kontakt + Anhänge seeden (`AttachmentEntityKind.Contact = 2`), Ribbon `#Attachments`, Anhang über Overlay löschen, Dialog oberhalb und bedienbar | Offen | — |
| 6 | E2E-Tests | `ConfirmationDialogE2ETests`: Test `AttachmentDelete_OverlayOpen_CancelKeepsAttachmentAndOverlay` — Abbrechen auf der Kontaktkarte erhält Anhang und Overlay | Offen | — |
| 7 | E2E-Tests | `ConfirmationDialogE2ETests`: Test `AttachmentDelete_OverlayOpen_BackdropClickCancelsOnlyConfirmation` — Backdrop-Klick auf der Kontaktkarte bricht nur die Bestätigung ab | Offen | — |
| 8 | Tests | Vollständiger Testlauf `FinanceManager.Tests` und `FinanceManager.Tests.E2E`; bekannter Fehlschlag `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` bleibt unbehandelt | Offen | — |
