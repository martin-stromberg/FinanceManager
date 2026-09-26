# Übersetzte Anforderung

## Fachliche Zusammenfassung

Beim Löschen eines Anhangs aus der Anhangsliste wird die Bestätigungsabfrage (`ConfirmDialog`) hinter dem geöffneten Overlay-Dialog der Anhangsliste (`AttachmentsPanel` innerhalb von `OverlayHost` bzw. dem Listen-Overlay in `ListPage.razor`) dargestellt und ist dadurch nicht sichtbar bzw. nicht bedienbar. Ursache ist ein Stapel-Konflikt: Overlay-Dialog und Bestätigungsdialog verwenden beide die CSS-Klasse `.split-center` mit identischem `z-index: 1000` (`FinanceManager.Web/wwwroot/css/app.css`, Zeile 415); das später im DOM gerenderte Overlay überdeckt den Bestätigungsdialog. Zu korrigieren ist die Schichtreihenfolge, sodass Bestätigungsdialoge immer oberhalb geöffneter Overlays dargestellt werden und das Löschen von Anhängen wieder möglich ist.

## Betroffene Klassen und Komponenten

- `FinanceManager.Web/Components/Shared/ConfirmDialog.razor` — Bestätigungsdialog; rendert äußeren Container `.split-center` > `.split-dialog.confirm-dialog` (Zeilen 7-8).
- `FinanceManager.Web/Components/Shared/ConfirmationDialogHost.razor` — Host, der `ConfirmDialog` bei `ConfirmationService.CurrentRequest != null` einblendet.
- `FinanceManager.Web/Components/Shared/OverlayHost.razor` — rendert Overlay-Komponenten (u. a. `AttachmentsPanel`) als `.split-center` > `.split-dialog` (Zeilen 10-11).
- `FinanceManager.Web/Components/Shared/AttachmentsPanel.razor` — `DeleteAsync` (Zeilen 399-422) ruft `ConfirmationService.ConfirmAsync` mit `ConfirmationSeverity.Critical` auf; Lösch-Button in Zeile 118.
- `FinanceManager.Web/Components/Pages/CardPage.razor` — Reihenfolge der Hosts: `ConfirmationDialogHost` (Zeile 41) vor `OverlayHost` (Zeile 66); das später gerenderte Overlay überdeckt bei gleichem `z-index` den Bestätigungsdialog.
- `FinanceManager.Web/Components/Pages/ListPage.razor` — analog: `ConfirmationDialogHost` (Zeile 30) vor dem Listen-Overlay `.split-center` (ab Zeile 56), das ebenfalls `AttachmentsPanel` hosten kann.
- `FinanceManager.Web/wwwroot/css/app.css` — `.split-center` mit `z-index: 1000` (Zeilen 409-417), `.split-dialog` (Zeilen 419-423), `.confirm-dialog` (Zeilen 425-441); hier ist die Schichtung anzupassen.
- `FinanceManager.Web/wwwroot/css/theme.Dark.css` — enthält ebenfalls `.split-center`-/`.split-dialog`-Regeln (Zeilen 131, 135); auf Konsistenz prüfen.
- `FinanceManager.Web/Services/ConfirmationService.cs` / `IConfirmationService.cs` — Ablauf der Bestätigungsabfrage (`ConfirmAsync`, `OnChanged`, `CurrentRequest`); keine fachliche Änderung nötig, aber relevanter Kontext.
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (`RequestOpenAttachments`, Zeilen 325-333) bzw. `ViewModelBase.cs` (Zeilen 200-208) — Auslöser des Overlays via `UiOverlaySpec`.
- Tests: `FinanceManager.Tests/Components/ConfirmDialogTests.cs`, `FinanceManager.Tests/Components/ConfirmationDialogHostTests.cs` — bestehende bUnit-Tests; ggf. Ergänzung eines Tests zur Schichtreihenfolge bzw. zur vergebenen CSS-Klasse.

## Implementierungsansatz

Der Bestätigungsdialog muss oberhalb jedes geöffneten Overlays liegen. Zwei naheliegende Ansätze (Annahme, finale Wahl in der Planungsphase):

1. **Dedizierte Schicht für Bestätigungsdialoge (bevorzugt):** Der äußere Container in `ConfirmDialog.razor` erhält eine zusätzliche Klasse (z. B. `confirm-dialog-layer`) oder die `.split-center`-Schicht des `ConfirmationDialogHost` wird per CSS selektiert; in `app.css` wird dieser Schicht ein höherer `z-index` als `1000` zugewiesen (z. B. `1100`). Damit ist die Lösung unabhängig von der DOM-Reihenfolge der Hosts und gilt auf allen Seiten (`CardPage`, `ListPage`, `Home`, `ReportDashboard`).
2. **DOM-Reihenfolge korrigieren:** `ConfirmationDialogHost` jeweils nach dem Overlay-Host platzieren (in `CardPage.razor` nach `OverlayHost`, in `ListPage.razor` nach dem Listen-Overlay-Block). Bei gleichem `z-index` gewinnt das spätere Element — funktional korrekt, aber fragiler, da jede Seite einzeln gepflegt werden muss.

Zusätzlich ist zu prüfen, ob die Backdrop-Klick-Behandlung (`@onclick` auf `.split-center` in `OverlayHost.razor` Zeile 10 bzw. `ConfirmDialog.razor` Zeile 7) durch die neue Schichtung ungewollt das darunterliegende Overlay schließt; der Klick auf den Bestätigungs-Backdrop bricht aktuell die Bestätigung ab (`OnBackdropClick` → `ConfirmationService.Cancel()`), das Overlay darunter bleibt geöffnet.

Keine Änderungen an `ConfirmationService`, `AttachmentsPanel`-Logik oder der API (`AttachmentsController`, `IAttachmentService`) erforderlich — der Lösch-Endpunkt funktioniert; es handelt sich ausschließlich um ein Darstellungs-/Schichtungsproblem im Frontend.

## Konfiguration

Kein Konfigurationsbedarf. Der Bug tritt nur auf, wenn die bestehende Benutzereinstellung `ShowConfirmations` aktiviert ist (Standard, siehe `ConfirmationService.GetShowConfirmationsAsync`); bei deaktivierten Bestätigungen wird `ConfirmAsync` sofort mit `true` beantwortet und kein Dialog gerendert.

## Offene Fragen

- Soll die Schichtkorrektur generisch für alle künftigen modalen Dialoge gelten (z. B. gestaffelte `z-index`-Ebenen pro Dialogtyp), oder reicht die gezielte Anhebung des Bestätigungsdialogs?
- Tritt das Problem auch bei anderen Overlays auf, die Bestätigungen auslösen (z. B. `ContactMergePanel`, `AssignStatementOverlay`, `SecurityPriceImportPanel`)? Die Lösung sollte idealerweise alle Fälle abdecken, nicht nur `AttachmentsPanel`.
- Ist der Backdrop-Klick auf den Bestätigungsdialog weiterhin als Abbruch der Bestätigung (ohne Schließen des darunterliegenden Overlays) gewünscht?
