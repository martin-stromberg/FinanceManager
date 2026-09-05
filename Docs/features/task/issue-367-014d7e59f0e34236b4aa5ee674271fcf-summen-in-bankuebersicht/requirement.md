# Übersetzte Anforderung

## Ausgangslage
Die Übersichtsseite der Bankkonten enthält derzeit eine Tabelle, aber keine zusammengefasste statistische Darstellung der Kontosalden und ihrer Verteilung.

## Betroffene Benutzerflüsse
- Anwender öffnet die Übersichtsseite der Bankkonten.
- Anwender betrachtet die Tabelle der Bankkonten.
- Anwender wertet die Gesamtentwicklung der Kontosalden für das aktuelle Jahr und den aktuellen Monat aus.
- Anwender analysiert die Verteilung der Kontosalden nach Kontoart und Bankkontakt.

## Funktionale Anforderungen

### FA-1 Statistische Infokachel
Auf der Übersichtsseite wird zusätzlich zur Tabelle eine Infokachel mit Statistiken zu den Bankkonten angezeigt.

### FA-2 Summe der Bankkontosalden
Die Infokachel zeigt die Summe aller Bankkontosalden an.

### FA-3 Veränderung im aktuellen Jahr
Die Infokachel zeigt die Veränderung der Summe der Bankkontosalden im aktuellen Kalenderjahr an.

### FA-4 Veränderung im aktuellen Monat
Die Infokachel zeigt die Veränderung der Summe der Bankkontosalden im aktuellen Kalendermonat an.

### FA-5 Verteilung nach Kontoart
Die Infokachel zeigt die Verteilung der Bankkontosalden nach Kontoart in einem Tortendiagramm an.

### FA-6 Verteilung nach Bankkontakt
Die Infokachel zeigt die Verteilung der Bankkontosalden nach Bankkontakt in einem Tortendiagramm an.

### FA-7 Konsistenz der Auswertungen
Alle dargestellten Summen, Veränderungen und Verteilungen beziehen sich auf dieselbe Menge der in der Bankübersicht berücksichtigten Bankkonten.

## Nicht-funktionale Anforderungen
- Die Infokachel ist zusätzlich zur bestehenden Tabelle verfügbar und beeinträchtigt deren Nutzung nicht.
- Die statistischen Werte und Diagramme werden in einer verständlichen, eindeutig beschrifteten Darstellung angezeigt.
- Geldbeträge werden konsistent mit der bestehenden Bankübersicht formatiert.
- Die Darstellung passt sich an die verfügbaren Bildschirmgrößen an.

## Akzeptanzkriterien
1. Auf der Übersichtsseite wird neben der bestehenden Tabelle eine Infokachel mit Statistiken angezeigt.
2. Die Infokachel zeigt die Summe aller Bankkontosalden an.
3. Die Infokachel zeigt die Veränderung der Bankkontosalden im aktuellen Kalenderjahr an.
4. Die Infokachel zeigt die Veränderung der Bankkontosalden im aktuellen Kalendermonat an.
5. Ein Tortendiagramm zeigt die Verteilung der Bankkontosalden nach Kontoart.
6. Ein weiteres Tortendiagramm zeigt die Verteilung der Bankkontosalden nach Bankkontakt.
7. Die dargestellten Werte stimmen mit den in der Tabelle berücksichtigten Bankkonten und deren Salden überein.
