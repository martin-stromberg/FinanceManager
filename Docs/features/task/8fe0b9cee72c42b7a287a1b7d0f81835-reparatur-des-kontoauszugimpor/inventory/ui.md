# UI

## `Home.razor` — Massenimport-Review-Dialog
Datei: `FinanceManager.Web/Components/Pages/Home.razor`

- Ribbon-Aktion `Import` (FileCallback) → `HomeViewModel.ProcessMassImportSelectionAsync`.
- Dialog erscheint, wenn `_vm.PendingMassImport != null`:
  - Titel: `MassImport_Dialog_Title` („Massenimport prüfen"), Hinweis `MassImport_Dialog_Hint`.
  - Liste: `@foreach (var file in _vm.PendingMassImport.Files.Where(x => !x.Excluded))` — **ausgeschlossene Dateien werden nicht angezeigt**. Da `NormalizePendingMassImportResult` alle `Unknown`-Dateien auf `Excluded=true` setzt, ist eine nicht erkannte Datei unsichtbar — ihre `ValidationMessage` (z. B. „File type could not be recognized.") wird nie dargestellt.
  - Pro sichtbarer Datei: Dateityp, Importservice, Wertpapier-Auswahl (nur `SecurityPrices`), `ValidationMessage` als Fehlerzeile, Entfernen-Button (`SetPendingFileExcluded(fileId, true)`).
  - Buttons: `Btn_Save` → `ConfirmPendingMassImportAsync` (deaktiviert via `CanSavePendingMassImport`, wenn eine sichtbare Datei unvollständig ist), `Btn_Cancel` → `CancelPendingMassImport`.
  - `muted-row`-CSS-Klasse existiert für nicht selektierbare Einträge — wird aktuell nie erreicht, weil diese Einträge vorher herausgefiltert werden.
- Nach `ConfirmMassImportAsync`: `ConfirmationDialogHost` zeigt `Confirmation_Finalize_Title` („Abschließen bestätigen") + `Confirmation_Finalize_Message` („Die Aktion kann nicht rückgängig gemacht werden."), Severity `Warning` — unabhängig davon, ob überhaupt eine Datei ausgeführt wird.
- Erfolgsanzeige: `Import_Success` + Link zum ersten Draft; bei ausschließlich `Skipped`-Dateien erscheint **keinerlei** Ergebnisfeedback.

## Ressourcen
Dateien: `FinanceManager.Web/Resources/Pages.{resx,de.resx,en.resx}`

- `MassImport_Dialog_Title`, `MassImport_Dialog_Hint`, `MassImport_Th_*`, `MassImport_SelectSecurity`, `MassImport_FileType_*`, `MassImport_Warning_MissingSecurityAssignment`, `Confirmation_Finalize_Title`, `Confirmation_Finalize_Message`.

## Einstellung
`SetupStatementTab.razor` + `SetupStatementsViewModel` pflegen `MassImportDialogPolicy` pro Benutzer (`UserSettings_GetImportSplitAsync`).
