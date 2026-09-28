← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Fehlerbehebung

## Bestätigungsdialog ist hinter einem Overlay verdeckt oder nicht bedienbar

**Symptom:** `ConfirmDialog` wird gerendert (im DOM vorhanden), liegt aber hinter einem geöffneten Overlay (`.split-center`, `.modal-overlay`) und ist dadurch nicht sichtbar oder nicht anklickbar — z. B. beim Löschen eines Anhangs aus der geöffneten Anhangsliste.

**Ursache:** Overlay und Bestätigungsdialog teilen die Grundschicht `.split-center` mit `z-index: 1000`. Steht das Overlay später im DOM, überdeckt es den Dialog bei identischem `z-index`. Dieser Fehlerzustand wurde durch die dedizierte Klasse `confirm-dialog-layer` (`z-index: 1100`) behoben.

**Lösung:**
1. Sicherstellen, dass der äußere Container in `ConfirmDialog.razor` die Klassen `split-center confirm-dialog-layer` trägt.
2. Sicherstellen, dass `app.css` die Regel `.split-center.confirm-dialog-layer { z-index: 1100; }` enthält.
3. Bei neuen Dialog- oder Overlay-Ebenen den `z-index` unterhalb von 1100 halten; nur eine fachlich begründete weitere Dialogebene darf höher liegen.

> **Hinweis:** Die Schichtung ist absichtlich unabhängig von der DOM-Reihenfolge der Hosts; `ConfirmationDialogHost` darf weiterhin vor den Overlay-Hosts in den Seiten platziert sein. Ein Klick auf den Bestätigungs-Backdrop erreicht den Overlay-Backdrop nicht und schließt daher nur die Bestätigung — dieses Verhalten ist gewollt.

## Bestätigungsdialog erscheint gar nicht

**Symptom:** `ConfirmAsync` liefert sofort `true`, ohne dass `ConfirmDialog` sichtbar wird.

**Ursache:** Die Benutzereinstellung `ShowConfirmations` ist deaktiviert. `ConfirmationService.GetShowConfirmationsAsync` liest den Wert über `Api.UserSettings_GetProfileAsync` und cached ihn pro Scope; bei Lesefehlern gilt der Standard `true`.

**Lösung:**
1. In der UI prüfen: Menü „Einrichtung" → Bereich „Profil" → „Bestätigungsdialoge anzeigen" muss aktiviert sein.
2. Der Cache wird beim Speichern des Profils über `SetupProfileViewModel.SaveAsync` → `ConfirmationService.InvalidateCacheAsync` verworfen; in eigenen ViewModels ggf. analog aufrufen.
3. Im Log nach „Confirmation suppressed for action …" (Einstellung deaktiviert) oder „Failed to read user confirmation preference" (Lesefehler) suchen.
