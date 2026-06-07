# F018 – Budgetwirkung während Buchung

## Einleitung

Beim Buchen von Kontoauszügen sehen Sie die erwartete Budgetwirkung direkt mit.  
Neu fließt dabei auch das optionale Verwendungszweck-Muster aus der Budgetregel ein.  
So erhalten Sie Hinweise nur für Buchungen, die wirklich zum Muster passen.  
Das hilft bei schnellen und sicheren Buchungsentscheidungen.

## Wer nutzt es?

Diese Funktion nutzen Sachbearbeiter in der täglichen Buchung.  
Sie prüfen während der Zuordnung sofort die Auswirkungen auf Budgets.

## Schritt-für-Schritt-Anleitung

1. Sie öffnen einen Kontoauszug-Entwurf.
2. Sie ordnen Buchungen zu und prüfen den Verwendungszweck.
3. Sie buchen den Entwurf.
4. Sie lesen die Hinweise zur Budgetwirkung.
5. Sie prüfen bei Bedarf die Regel in der **Budgetplanung**.

## Beispiel

**Textmuster:**  
Regel enthält `ST6464646464`.  
Eine Buchung mit `Abrechnung ST6464646464 Juni` erzeugt eine Budgetwirkung.

**Regex-Muster:**  
Regel enthält `ST\d{10}` mit aktivierter **Regex**-Nutzung.  
Eine Buchung ohne passende Nummer bleibt ohne Budgetwirkung.

## Was passiert im Hintergrund?

Die Budgetwirkung wird nur für passende Budgetzwecke berechnet.  
Dazu prüft das System zuerst die normale Zuordnung der Buchung.  
Danach prüft es das optionale Muster im Verwendungszweck.  
Nur bei Treffer erscheint die Buchung in der Wirkung des Budgetzwecks.

## Häufige Fragen (FAQ)

**F: Warum ist die Budgetwirkung bei einer Buchung neutral?**  
A: Meist passt kein Budgetzweck oder das Muster trifft nicht zu.

**F: Wird bei Regex fachlich geprüft, ob das Muster sinnvoll ist?**  
A: Nein. Es wird nur die korrekte Schreibweise geprüft.

**F: Was passiert bei einem ungültigen Regex-Muster?**  
A: Die Regel lässt sich erst nach Korrektur speichern.

**F: Stoppt eine Warnung die Buchung automatisch?**  
A: Nein. Die Hinweise unterstützen Ihre Entscheidung.

## Verwandte Funktionen

- [F008 – Budgetplanung](./F008-budgetplanung.md)
- [F009 – Budgetberichte](./F009-budgetberichte.md)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
