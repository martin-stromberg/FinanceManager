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

## Massenänderungen werden gemeinsam gespeichert

**Beschreibung:** Im Massenänderungsmodus für Kontoauszugsentwürfe können Bearbeitungen, Löschvormerkungen und neue Zeilen in einem gemeinsamen Speichervorgang übernommen werden.

**Bedingungen:**
- Der Entwurf gehört zum Benutzer.
- Der Entwurf befindet sich im Entwurfsstatus.
- Betroffene Bestandszeilen sind editierbar.
- Neue Zeilen enthalten mindestens Buchungsdatum, Betrag und Verwendungszweck; der Betrag darf nicht `0` sein.

**Verhalten:**
- Zum Löschen vorgemerkte Zeilen verschwinden sofort aus der Tabelle, werden aber erst beim Speichern gelöscht.
- Eine leere Eingabezeile am Tabellenende ermöglicht das Erfassen neuer Entwurfszeilen.
- Abbrechen verwirft lokale Bearbeitungen, Löschvormerkungen und neue noch nicht gespeicherte Zeilen.
- Speichern übernimmt alle gültigen Änderungen gemeinsam; bei einem Validierungsfehler wird keine Teiländerung übernommen.
- Bereits gebuchte oder angekündigte Zeilen können im Massenänderungsmodus nicht gelöscht werden.

**Umsetzung:** QuickEdit-Speicherung der Kontoauszugsentwurfszeilen über den erweiterten Batch-Speicherpfad.

## Sammelauszüge erzeugen mehrere Entwürfe

**Beschreibung:** Wenn ein Import mehrere Auszüge für unterschiedliche IBANs enthält, wird für jede IBAN ein eigener Entwurf erzeugt.

**Bedingungen:**
- Die Datei wird als Sammelauszug erkannt.

**Verhalten:**
- Jeder erkannte Auszug wird separat gespeichert.
- Unbekannte IBANs werden ohne Kontozuordnung abgelegt.

**Umsetzung:** `IStatementFileParser` und `StatementDraftService`.

## Verknüpfte IBANs werden für die Zuordnung berücksichtigt

**Beschreibung:** Wenn eine importierte IBAN bereits an einem Sammelkonto hinterlegt ist, wird der Entwurf automatisch diesem Konto zugeordnet.

**Bedingungen:**
- Das Zielkonto ist als Sammelkonto markiert.
- Die IBAN ist als verknüpfte IBAN gespeichert.

**Verhalten:**
- Die Kontozuordnung wird ohne Rückfrage gesetzt.
- Ist keine Verknüpfung vorhanden, bleibt der Entwurf unzugeordnet.

**Umsetzung:** `StatementDraftService` und `AccountService.GetLinkedIbansAsync`.
