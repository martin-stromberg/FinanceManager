# Bestandsaufnahme: Anhänge lassen sich nicht löschen — Bestätigungsdialog hinter Overlay

Analysiert wurde der Frontend-Bereich rund um den Bestätigungsdialog (`ConfirmDialog`/`ConfirmationDialogHost`) und die Overlay-Darstellung (`OverlayHost`, Listen-Overlay in `ListPage`, `.split-center`-Schichtung in `app.css`), bezogen auf die Anforderung, dass die Lösch-Bestätigung im `AttachmentsPanel` vom geöffneten Overlay verdeckt wird.

## Zusammenfassung

- Der Bestätigungsdialog (`ConfirmDialog.razor`) und alle Overlay-Container (`OverlayHost.razor`, `ListPage.razor`, `Home.razor`, `BudgetReport.razor`) verwenden denselben äußeren Container `.split-center` mit `z-index: 1000` (`app.css`, Zeilen 409–417). Es existiert **keine** dedizierte Schichtklasse für Bestätigungsdialoge; die Sichtbarkeit hängt derzeit allein von der DOM-Reihenfolge ab.
- Auf allen Seiten steht `ConfirmationDialogHost` im Markup **vor** den Overlay-Containern (`CardPage.razor` Zeile 41 vs. `OverlayHost` Zeile 66; `ListPage.razor` Zeile 30 vs. Listen-Overlay ab Zeile 56; `Home.razor` Zeile 22 vs. Mass-Import-Overlay Zeile 70; `ReportDashboard.razor` Zeile 29 vor drei `.modal-overlay`-Dialogen). Bei gleichem `z-index` überdeckt das später gerenderte Overlay den Dialog — der beschriebene Bug ist strukturell bestätigt.
- `AttachmentsPanel.DeleteAsync` (Zeilen 399–422) ruft `ConfirmationService.ConfirmAsync` mit `ConfirmationSeverity.Critical` auf und löscht bei `true` über `Api.Attachments_DeleteAsync`; derselbe Aufrufpfad wird auch von `ContactMergePanel` und `HomeKpiGrid` benutzt — der Bug ist nicht anhangsspezifisch.
- `ConfirmationService` ist vollständig implementiert (`ConfirmAsync`, `SetResult`, `Cancel`, `OnShow`/`OnChanged`, `CurrentRequest`, gecachte `ShowConfirmations`-Einstellung); das API-Löschen (`AttachmentsController.DeleteAsync` → `IAttachmentService.DeleteAsync`) funktioniert und ist per Tests abgedeckt.
- `theme.Dark.css` definiert für `.split-center`/`.split-dialog` nur Farben/Schatten, **keinen** eigenen `z-index` — die Schichtung ist zentral in `app.css` geregelt.
- Weitere Overlays mit `z-index: 1000` existieren: `.modal-overlay` (`app.ReportDashboard.css`, `app.SecurityPrices.css`, `theme.Dark.HomeKpiGrid.css`), `.modal-backdrop` (`app.ContactMergeDialog.css`, verwendet von `SetupBackupTab`), `.rsw-backdrop` (`z-index: 900`, `ReturnSummaryWidget`).

Test-Ausgangszustand: Alle vier ermittelten Suiten wurden ausgeführt — `FinanceManager.Tests` 1335/1335 bestanden, `FinanceManager.Tests.Integration` 134/134 bestanden, `FinanceManager.Tests.E2E` 99/100 bestanden (1 bekannter, bug-unabhängiger Fehlschlag: `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue`, Keepalive-Aufruf lieferte 401 statt 204), Node-Skript-Tests 26/26 bestanden. Details und Nachweise unter [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ConfirmationRequest`, `AttachmentDto`, `UserProfileSettingsDto`, `UiOverlaySpec`
- [Logik](inventory/logic.md) — `ConfirmDialog`, `ConfirmationDialogHost`, `OverlayHost`, `AttachmentsPanel`, `ConfirmationService`, `BaseViewModel`/`ViewModelBase`, Seiten-Hosts
- [Stil/Schichtung](inventory/styles.md) — `.split-center`, `.split-dialog`, `.confirm-dialog`, `.modal-overlay`, weitere `z-index`-Regeln
- [Enums](inventory/enums.md) — `ConfirmationSeverity`, `EmbeddedPanelPosition`, `BooleanSelection`
- [Interfaces](inventory/interfaces.md) — `IConfirmationService`, `IAttachmentService` (Kontext)
- [Tests](inventory/tests.md) — bestehende Testklassen und Testlauf-Ausgangszustand
