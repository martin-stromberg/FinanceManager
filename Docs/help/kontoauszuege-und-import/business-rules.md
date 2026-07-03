← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Business Rules

## Draft vor Buchung validieren

**Beschreibung:** Entwürfe müssen vor der Verbuchung fachlich geprüft werden.

**Bedingungen:**
- Entwurf gehört zum Benutzer.
- Pflichtzuordnungen sind vollständig.

**Verhalten:**
- Wenn valide: Buchung darf ausgeführt werden.
- Sonst: Validierungsfehler und keine Verbuchung.

**Umsetzung:** `StatementDraftService.ValidateAsync` und `StatementDraftService.BookAsync`.

## Klassifikation kann nachbearbeitet werden

**Beschreibung:** Automatische Zuordnungen sind nur Vorschläge und können manuell geändert werden.

**Bedingungen:**
- Entwurfszeile existiert im Benutzerkontext.

**Verhalten:**
- Manuelle Zuordnung überschreibt automatische Klassifikation.
- Änderungen fließen in die nächste Validierung/Buchung ein.

**Umsetzung:** `SetEntryContactAsync`, `AssignSavingsPlanAsync`, `SetEntrySecurityAsync`, `UpdateEntryCoreAsync`.
