# Schritt 1 – Hochrechnungs- und Posten-Overlays korrigieren

## Scope
- Ausgeblendete Erwartungen zeigen Betrag und werden auf den geöffneten Berichtskontext gefiltert.
- Unterdrückungen werden postenbezogen über die Quellposting-ID vorgenommen; Bestandsdaten ohne Posting-ID bleiben kompatibel.
- Nach dem Wiedereinbeziehen erscheint der Erwartungsposten direkt wieder im geöffneten Overlay.
- Reale Posten zeigen Beschreibung plus Verwendungszweck.

## Akzeptanzszenarien
1. Zwei Bankposten desselben Kontos am selben Datum und mit gleichem Betreff erzeugen zwei unabhängige Erwartungsposten; das Ausblenden des einen lässt den anderen sichtbar.
2. Beim Öffnen der Erwartungsposten von Konto A werden keine ausgeblendeten Einträge von Konto B gezeigt.
3. Nach „Einbeziehen“ ist die ausgeblendete Tabelle aktualisiert und der Posten erscheint wieder in der sichtbaren Erwartungsliste einschließlich korrekter Summe.
4. Der Posten-Overlay zeigt für einen Posten mit Betreff und Verwendungszweck beide Inhalte.
5. Unterdrückungen und Wiederherstellungen bleiben owner- und favorite-bezogen abgesichert.
