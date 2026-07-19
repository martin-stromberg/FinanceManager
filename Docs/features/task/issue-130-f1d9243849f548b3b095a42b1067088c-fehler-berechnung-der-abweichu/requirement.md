# Anforderung

## Ausgangslage

Bei Budgetzwecken mit Budgetwertungsart `Exakte Buchungen` sind die Zahlen im Budgetbericht korrekt: passende Buchungen, die aufgrund der Wertungsart nicht fuer den Istwert des Budgetzwecks zaehlen, bleiben unbudgetiert. In der Postenauflistung des Budgetzwecks werden diese unbudgetierten passenden Buchungen aktuell jedoch zusammen mit den budgetierten Buchungen ohne Unterscheidung angezeigt.

Zusaetzlich ist die Tabellenueberschrift dieser Postenauflistung hellgrau auf weissem Hintergrund und schwer lesbar.

## Ziel

Die Postenauflistung eines Budgetzwecks muss budgetierte und passende, aber nicht budgetierte Buchungen klar unterscheidbar darstellen. Nicht budgetierte passende Buchungen sollen weiterhin in der Zweck-Postenauflistung sichtbar bleiben, dort aber gekennzeichnet und visuell schwaecher dargestellt werden. Die regulaere Auflistung der nicht budgetierten Betraege bleibt zusaetzlich bestehen.

Die Tabellenueberschrift der Postenauflistung soll auf hellem Hintergrund gut lesbar sein.

## Akzeptanzkriterien

- Bei `Exakte Buchungen` bleiben die Tabellenzahlen unveraendert: nur gewertete Buchungen fliessen in den Istwert des Budgetzwecks ein.
- Passende, aber nicht gewertete Buchungen werden in der Zweck-Postenauflistung separat erkennbar als nicht budgetiert ausgewiesen.
- Diese nicht gewerteten Buchungen werden in der Zweck-Postenauflistung visuell schwaecher dargestellt.
- Die Buchungen bleiben weiterhin auch in der regulaeren nicht-budgetierten Auflistung sichtbar.
- Die Tabellenueberschrift der Postenauflistung hat ausreichenden Kontrast auf hellem Hintergrund.

## Testfall

Ein Budgetzweck ist als `Exakte Buchungen` konfiguriert und hat ein negatives Budget. Es gibt eine passende negative Buchung und eine passende positive Buchung. Die negative Buchung ist budgetiert und zaehlt zum Istwert. Die positive Buchung ist nicht budgetiert, wird in der Zweck-Postenauflistung als nicht budgetiert gekennzeichnet und schwaecher dargestellt, erscheint aber weiterhin auch in der regulaeren nicht-budgetierten Auflistung.

## Offene Punkte

Keine.
