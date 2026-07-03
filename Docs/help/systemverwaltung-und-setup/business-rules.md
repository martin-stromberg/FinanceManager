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

**Umsetzung:** `SetupCardViewModel.ChangeView`.
