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
