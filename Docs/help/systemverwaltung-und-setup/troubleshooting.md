← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Fehlerbehebung

## `security.txt` liefert HTTP 503

**Symptom:** Aufruf von `/security.txt` oder `/.well-known/security.txt` liefert `503 Service Unavailable`.

**Ursache:** Das Pflichtfeld `Contact` ist in den `security.txt`-Einstellungen noch leer.

**Lösung:**
1. Als Administrator den Setup-Bereich **security.txt** öffnen.
2. Ein gültiges Feld **Kontakt** (z. B. `mailto:security@example.com`) und ein Ablaufdatum in der Zukunft eintragen.
3. Speichern und die öffentliche Adresse erneut aufrufen.

## Speichern schlägt wegen `Canonical` fehl

**Symptom:** Beim Speichern der `security.txt`-Einstellungen erscheint ein Validierungsfehler.

**Ursache:** Der Wert in **Canonical** erfüllt die Regeln für öffentliche HTTPS-Adressen nicht.

**Lösung:**
1. Prüfen, dass `Canonical` eine absolute `https://`-URL ist.
2. Query-String (`?`) und Fragment (`#`) entfernen.
3. Keine localhost- oder Loopback-Adresse verwenden.
4. Erneut speichern.

> **Hinweis:** Ein leeres `Canonical`-Feld ist erlaubt.

## `Canonical` bleibt leer und Ausgabe bricht serverseitig ab

**Symptom:** Nach dem Leeren von `Canonical` wird keine gültige Ausgabe erzeugt und im Betrieb tritt ein Konfigurationsfehler auf.

**Ursache:** Der Fallback auf `Api:BaseAddress` kann nicht gebildet werden, weil der Wert fehlt oder keine absolute URL ist.

**Lösung:**
1. `Api:BaseAddress` in der Serverkonfiguration setzen.
2. Auf absolute URL prüfen (z. B. `https://finance.example.com/`).
3. Anwendung neu starten und Ausgabe erneut prüfen.

## Passwort ändern meldet „Das aktuelle Passwort ist nicht korrekt."

**Symptom:** Auf der Seite **Passwort ändern** erscheint nach dem Absenden die Meldung „Das aktuelle Passwort ist nicht korrekt.".

**Ursache:** Die Eingabe im Feld **Aktuelles Passwort** stimmt nicht mit dem bisherigen Passwort überein.

**Lösung:**
1. Das aktuelle Passwort erneut eingeben (Groß-/Kleinschreibung und Tastaturlayout prüfen).
2. Erneut auf **Passwort ändern** klicken.

> **Hinweis:** Wer das aktuelle Passwort nicht mehr kennt, muss es von einem Administrator zurücksetzen lassen; eine Wiederherstellung ohne Administrator ist nicht möglich.

## Passwort ändern meldet „Das neue Passwort erfüllt die Passwort-Richtlinien nicht."

**Symptom:** Auf der Seite **Passwort ändern** erscheint nach dem Absenden die Meldung „Das neue Passwort erfüllt die Passwort-Richtlinien nicht.".

**Ursache:** Das neue Passwort verstößt gegen die konfigurierten Passwort-Regeln (unter anderem mindestens 8 Zeichen und mindestens eine Ziffer).

**Lösung:**
1. Ein längeres Passwort mit mindestens einer Ziffer wählen.
2. Die Eingabe unter **Neues Passwort bestätigen** exakt wiederholen.
3. Erneut absenden.

## Nach der Passwortänderung sind andere Geräte abgemeldet

**Symptom:** Nach einer erfolgreichen Passwortänderung müssen Sie sich auf anderen Geräten oder in anderen Browsern neu anmelden.

**Ursache:** Das ist gewolltes Sicherheitsverhalten — beim Passwortwechsel werden alle bisher ausgestellten Anmeldetoken ungültig.

**Lösung:**
1. Auf den betroffenen Geräten mit dem neuen Passwort anmelden.

> **Hinweis:** Die Sitzung, in der die Passwortänderung erfolgte, bleibt ohne Neueingabe angemeldet.

## `/.well-known/change-password` leitet nicht auf das gewünschte Ziel weiter

**Symptom:** Der Aufruf von `/.well-known/change-password` landet auf einer anderen Seite als erwartet.

**Ursache:** Die hinterlegte **Passwort-ändern-URL** in der Setup-Sektion **Well-Known** ist nicht gesetzt, ungültig oder verweist auf ein anderes Ziel. Ungültige gespeicherte Werte werden durch den Standard `/change-password` ersetzt.

**Lösung:**
1. Als Administrator den Setup-Bereich **Well-Known** öffnen.
2. Das Feld **Passwort-ändern-URL** prüfen und korrigieren (lokaler Pfad mit führendem `/` oder absolute `http`/`https`-Adresse).
3. Speichern und die öffentliche Adresse erneut aufrufen.
