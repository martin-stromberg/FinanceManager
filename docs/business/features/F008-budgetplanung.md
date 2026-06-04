# F008 – Budgetplanung

## Einleitung

Mit der Budgetplanung legen Sie feste Beträge für Ihre Budgetzwecke fest.  
Neu ist ein optionales Suchmuster für den Verwendungszweck einer Buchung.  
Damit schränken Sie ein, welche Buchungen zu einer Regel passen.  
Ohne Suchmuster gilt die Regel wie bisher für alle passenden Buchungen des Budgetzwecks.

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in Buchhaltung und Controlling.  
Sie pflegen Budgets und möchten bestimmte Verträge klar trennen.

## Schritt-für-Schritt-Anleitung

1. Sie öffnen den Bereich **Budgetplanung**.
2. Sie öffnen eine **Budgetregel** oder legen eine neue Regel an.
3. Sie füllen **Betrag**, **Intervall**, **Start** und bei Bedarf **Ende**.
4. Sie tragen optional ein Suchmuster für den Verwendungszweck ein.
5. Sie entscheiden, ob das Muster als Textsuche oder als **Regex** genutzt wird.
6. Sie klicken **Speichern**.
7. Bei ungültiger Regex erhalten Sie eine Fehlmeldung und passen das Muster an.

## Beispiel

**Textmuster:**  
Sie tragen `ST6464646464` ein.  
Die Regel passt dann auf Buchungen, deren Verwendungszweck diese Zeichenfolge enthält.

**Regex-Muster:**  
Sie tragen `ST\d{10}` ein und aktivieren **Regex**.  
Die Regel passt dann auf Verwendungszwecke mit `ST` und danach zehn Ziffern.

## Was passiert im Hintergrund?

Das Muster ist optional.  
Ist kein Muster gesetzt, wird nicht nach Verwendungszweck gefiltert.  
Bei Textsuche wird ohne Beachtung von Groß- und Kleinschreibung gesucht.  
Bei **Regex** prüft das System beim Speichern nur die gültige Schreibweise.

## Häufige Fragen (FAQ)

**F: Muss ich immer ein Muster eintragen?**  
A: Nein. Das Feld ist optional.

**F: Was wird bei Regex beim Speichern geprüft?**  
A: Nur, ob der Ausdruck korrekt geschrieben ist.

**F: Prüft das System beim Speichern auch fachliche Treffer?**  
A: Nein. Es gibt nur eine syntaktische Prüfung.

**F: Wie lang darf das Muster sein?**  
A: Maximal 500 Zeichen.

**F: Was passiert bei einer ungültigen Regex?**  
A: Sie erhalten eine Validierungsmeldung und können erst nach Korrektur speichern.

## Verwandte Funktionen

- [F009 – Budgetberichte](./F009-budgetberichte.md)
- [F018 – Budgetwirkung während Buchung](./F018-budgetwirkung-buchung.md)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
