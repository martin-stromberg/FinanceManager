← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Ablauf für Anwender

## Voraussetzungen

- Sie sind angemeldet.
- Für das Bearbeiten der `security.txt`-Einstellungen besitzen Sie die Rolle `Admin`.

## Automatische Sitzungserhaltung

Wenn Sie auf geschützten Seiten aktiv sind, bleibt Ihre Anmeldung für Sie weitgehend unsichtbar erhalten. Mausklicks, Tastatureingaben und Quick-Edit-Felder lösen im Hintergrund einen Keepalive-Request aus; dadurch wird das vorhandene JWT verlängert, ohne dass Sie erneut auf `/login` geleitet werden.

Nur wenn die Sitzung fachlich ungültig wird (zum Beispiel durch Deaktivierung des Benutzers, Wechsel des `security_stamp` oder abgelaufenes Token) oder wenn Sie wirklich nicht mehr authentifiziert sind, erscheint der normale Login-Fluss.

## Schritt-für-Schritt-Anleitung

### 1. Setup-Bereich öffnen

Öffnen Sie die Seite **Setup** und wechseln Sie in den Abschnitt **security.txt**.

> **Hinweis:** Ohne Admin-Berechtigung wird der Bereich nicht angezeigt.

### 2. Pflichtfelder setzen

Tragen Sie mindestens **Kontakt** und **Ablaufdatum** ein. Ohne gültigen Kontakt bleibt die öffentliche Ausgabe deaktiviert.

> **Hinweis:** Das Ablaufdatum muss in der Zukunft liegen.

### 3. Optionalen Canonical-Wert pflegen

Wenn Ihre öffentliche Zieladresse von der internen Serveradresse abweicht (z. B. Reverse Proxy), tragen Sie unter **Canonical** die öffentliche HTTPS-URL ein.

> **Hinweis:** Zulässig sind nur absolute HTTPS-URLs ohne Query-String und ohne Fragment.

### 4. Speichern

Speichern Sie die Änderungen über die Ribbon-Aktion **Speichern**.

> **Hinweis:** Bei ungültigen Eingaben zeigt die Seite eine Fehlermeldung; die Werte werden dann nicht übernommen.

## Ergebnis

Nach erfolgreichem Speichern sind die Einstellungen persistent gespeichert.  
Die öffentlichen Adressen `/security.txt`, `/.well-known/security.txt`, `/.well-known/security.md` und `/.well-known/security.html` liefern die aktualisierten Inhalte.

## Barrierefreiheit

- Die Felder sind als Standard-Formularelemente mit sichtbaren Labels umgesetzt.
- Die Eingabebezeichnungen entsprechen den direkt sichtbaren Feldnamen (`Kontakt`, `Ablaufdatum`, `Canonical`, ...).
