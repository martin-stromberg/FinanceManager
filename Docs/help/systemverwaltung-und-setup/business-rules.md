← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Business Rules

## Import-Split-Einstellungen haben harte Grenzen

**Beschreibung:** Benutzerpräferenzen für Import-Splitting werden validiert.

**Bedingungen:**
- `ImportMaxEntriesPerDraft >= 1`
- `ImportMinEntriesPerDraft >= 1`
- `ImportMinEntriesPerDraft <= ImportMaxEntriesPerDraft`

**Verhalten:**
- Gültige Werte: Einstellungen werden gespeichert.
- Ungültige Werte: Fehler via `ArgumentOutOfRangeException`.

**Umsetzung:** `User.SetImportSplitSettings`.

## Massenimport-Dialogverhalten ist benutzerspezifisch

**Beschreibung:** Das Verhalten des Dialogs wird pro Benutzer persistiert.

**Bedingungen:**
- Policy-Wert liegt vor.

**Verhalten:**
- Gewählte Policy steuert Dialoganzeige bei Massenimport.

**Umsetzung:** `User.SetMassImportDialogPolicy`.

## Setup-Bereich ist in feste Sektionen gegliedert

**Beschreibung:** Die Setup-Navigation akzeptiert nur bekannte Sektionen.

**Bedingungen:**
- Schlüssel muss aus der statischen `SettingSections`-Liste stammen.

**Verhalten:**
- Gültiger Schlüssel: entsprechendes Panel wird geladen.
- Ungültiger/leer Schlüssel: keine Umschaltung.

**Umsetzung:** `SetupCardViewModel.TryGetSectionDefinition`.

## Ribbon-beitragende Section-ViewModels werden beim Laden vorab instanziiert

**Beschreibung:** Vier Section-ViewModels definieren Ribbon-Aktionen und müssen als Kind-ViewModels von `SetupCardViewModel` registriert sein, damit `BaseViewModel.GetRibbonRegisters()` ihre Aktionen aggregiert. Da die Setup-Seite ein Akkordeon-Layout verwendet, in dem Sektionen zu jedem Zeitpunkt zugeklappt sein können, darf die Ribbon-Sichtbarkeit nicht vom Aufklappzustand abhängen.

**Bedingungen:**
- `SetupCardViewModel.LoadAsync` wird aufgerufen.
- `_sectionViewModels.Count == 0` (Guard gegen Doppel-Registrierung bei Re-Navigation).

**Verhalten:**
- Beim ersten `LoadAsync`: `SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel` und `SetupStatementsViewModel` werden über `CreateSubViewModel<T>()` erzeugt und in `_childViewModels` eingetragen.
- Bei jedem folgenden `LoadAsync`: Guard greift, keine erneute Registrierung.
- `CreateSectionViewModel(key, sp)` gibt für diese vier Typen immer die gecachte Instanz zurück.

**Umsetzung:** `SetupCardViewModel.LoadAsync`, `SetupCardViewModel._sectionViewModels`.

## UploadBackup öffnet die Backup-Sektion automatisch

**Beschreibung:** Die `UploadBackup`-Ribbon-Aktion löst einen Datei-Picker in `SetupBackupTab.razor` aus. Dieser Handler ist nur registriert, wenn die Backup-Sektion im Akkordeon aufgeklappt und gerendert ist. Wird die Aktion bei zugeklappter Sektion ausgelöst, muss die Sektion zunächst geöffnet werden.

**Bedingungen:**
- `UploadBackup` wird aus dem Ribbon aufgerufen.
- Backup-Sektion ist zugeklappt (`_expandedSections` enthält `"backup"` nicht).

**Verhalten:**
- Wenn zugeklappt: `BeforeUploadCallback` löst `ExpandSectionRequested`-Event aus → `SetupSections.razor` klappt die Sektion auf und ruft nach dem Rendern `TriggerUploadRequest()` auf.
- Wenn aufgeklappt: `BeforeUploadCallback` ist ein No-Op; `TriggerUploadRequest()` wird nicht doppelt aufgerufen (der Callback feuert nur das Event; das Aufklappen selbst triggert kein weiteres Upload).

**Umsetzung:** `SetupBackupsViewModel.BeforeUploadCallback`, `SetupCardViewModel.ExpandSectionRequested`, `SetupSections.razor.OnExpandSectionRequested`, `SetupSections.razor.OnAfterRenderAsync`.
