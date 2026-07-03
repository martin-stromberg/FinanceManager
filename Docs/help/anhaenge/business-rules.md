← [Zurück zur Übersicht](index.md)

# Anhänge — Business Rules

## Anhang braucht Datenquelle

**Beschreibung:** Ein Anhang ist nur gültig, wenn Binärinhalt, URL oder Referenz vorhanden ist.

**Bedingungen:**
- `Content`, `Url` und `ReferenceAttachmentId` werden geprüft.

**Verhalten:**
- Mindestens ein Wert vorhanden: Anhang gültig.
- Kein Wert vorhanden: Erstellung wird abgebrochen.

**Umsetzung:** Konstruktor `Attachment(...)`.

## Symbolanhänge sind fachlich markiert

**Beschreibung:** Symbole werden als spezielle Rollen im selben Anhangsmodell verwaltet.

**Bedingungen:**
- `Role = Symbol`.

**Verhalten:**
- Symbolanhänge können von Fachobjekten gezielt referenziert werden.

**Umsetzung:** `AttachmentRole` und Methoden wie `SetRole`.
