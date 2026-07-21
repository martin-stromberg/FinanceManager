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

## SVG-Symbole müssen sicher sein

**Beschreibung:** SVG-Dateien können als Symbolanhänge verwendet und angezeigt werden, wenn sie die serverseitige Inhaltsprüfung bestehen.

**Bedingungen:**
- Der Anhang wird als SVG-Bild erkannt.
- Der SVG-Inhalt enthält keine aktiven oder unsicheren Bestandteile.

**Verhalten:**
- Sichere SVG-Symbole werden mit Bild-MIME-Type bereitgestellt und können in Symbolanzeigen gerendert werden.
- Unsichere SVG-Dateien werden beim Upload abgelehnt.

**Umsetzung:** Attachment-Content-Policy für Upload-Prüfung und Download-MIME-Type.
