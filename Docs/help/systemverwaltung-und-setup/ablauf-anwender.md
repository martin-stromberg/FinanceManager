← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Ablauf für Anwender

## Voraussetzungen

- Sie sind angemeldet.
- Für das Bearbeiten der `security.txt`-Einstellungen besitzen Sie die Rolle `Admin`.

## Automatische Sitzungserhaltung

Wenn Sie auf geschützten Seiten aktiv sind, bleibt Ihre Anmeldung für Sie weitgehend unsichtbar erhalten. Mausklicks, Tastatureingaben und Quick-Edit-Felder lösen im Hintergrund einen Keepalive-Request aus; dadurch wird das vorhandene Anmeldetoken verlängert, ohne dass Sie erneut auf `/login` geleitet werden.

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

## Eigenes Passwort ändern

### Voraussetzungen

- Sie sind angemeldet. Falls Sie die Seite ohne Anmeldung aufrufen, werden Sie zunächst zur Anmeldeseite geführt und nach der Anmeldung automatisch zurück auf die Seite **Passwort ändern**.

### Schritt-für-Schritt-Anleitung

#### 1. Seite öffnen

Klicken Sie im Anmeldebereich auf den Link **Passwort ändern** oder öffnen Sie die Adresse `/change-password`.

#### 2. Felder ausfüllen

Tragen Sie Ihr **Aktuelles Passwort**, das **Neues Passwort** und die Bestätigung unter **Neues Passwort bestätigen** ein.

> **Hinweis:** Das neue Passwort muss mindestens 8 Zeichen lang sein und mindestens eine Ziffer enthalten. Die Eingabe in **Neues Passwort bestätigen** muss mit dem neuen Passwort übereinstimmen.

#### 3. Änderung absenden

Klicken Sie auf **Passwort ändern**.

> **Hinweis:** Bei einem falschen aktuellen Passwort oder einem neuen Passwort, das die Regeln nicht erfüllt, erscheint eine Fehlermeldung; das Passwort wird dann nicht geändert.

### Ergebnis

Nach erfolgreicher Änderung erscheint die Meldung „Das Passwort wurde geändert.". Ihre aktuelle Sitzung bleibt angemeldet. Anmeldungen auf anderen Geräten oder Browsern werden beendet und erfordern eine neue Anmeldung mit dem neuen Passwort.

## Well-Known-Weiterleitung konfigurieren (Administrator)

### Voraussetzungen

- Sie sind angemeldet und besitzen die Rolle `Admin`.

### Schritt-für-Schritt-Anleitung

#### 1. Setup-Bereich öffnen

Öffnen Sie die Seite **Setup** und klappen Sie den Abschnitt **Well-Known** auf.

> **Hinweis:** Ohne Admin-Berechtigung sehen Sie in dem Abschnitt nur einen Hinweis, dass die Einstellungen Administratoren vorbehalten sind.

#### 2. Ziel-Adresse eintragen

Tragen Sie im Feld **Passwort-ändern-URL** das Weiterleitungsziel für die öffentliche Adresse `/.well-known/change-password` ein.

> **Hinweis:** Zulässig sind lokale Pfade, die mit `/` beginnen (z. B. `/change-password`), oder vollständige Adressen mit `http://` oder `https://`. Andere Eingaben werden beim Speichern abgelehnt.

#### 3. Speichern

Speichern Sie die Änderungen über die Ribbon-Aktion **Speichern**.

### Ergebnis

Nach dem Speichern leitet `/.well-known/change-password` sofort auf die neue Ziel-Adresse weiter. Ohne gültige Konfiguration gilt der Standard `/change-password`.

## Barrierefreiheit

- Die Felder sind als Standard-Formularelemente mit sichtbaren Labels umgesetzt.
- Die Eingabebezeichnungen entsprechen den direkt sichtbaren Feldnamen (`Kontakt`, `Ablaufdatum`, `Canonical`, ...).
