# Umsetzungsplan

## Zielbild

Die Zweck-Postenauflistung verwendet eine Overlay-Zeile, die neben den Buchungsdaten auch enthaelt, ob die Buchung fuer den Budgetzweck gewertet wird. Nicht gewertete passende Buchungen werden im Overlay als nicht budgetiert gekennzeichnet und optisch abgeschwaecht. Die Budgetbericht-Zahlen und die regulaere nicht-budgetierte Liste bleiben unveraendert.

## Umsetzungsschritte

1. ViewModel um eine dedizierte Overlay-Zeile erweitern, die `PostingServiceDto` plus `IsValuedForBudgetPurpose` enthaelt.
2. Beim Oeffnen eines Budgetzwecks die vorhandenen Rohdaten des Budgetberichts fuer den aktuellen Zeitraum laden bzw. wiederverwenden und daraus die Posten des Zwecks mit korrekter Wertungsinformation bestimmen.
3. Die bestehenden unbudgeted Overlays weiterhin als gewertete Standard-Overlay-Zeilen abbilden, damit die bestehende Anzeige stabil bleibt.
4. `BudgetReport.razor` auf die neue Overlay-Zeile umstellen, nicht gewertete Posten kennzeichnen und visuell schwaecher darstellen.
5. Tabellenkopf der Postenauflistung kontrastreicher stylen.
6. Integrationstest fuer `Exakte Buchungen` ergaenzen: Zweck-Overlay enthaelt gewertete und nicht gewertete passende Buchung, die nicht gewertete ist markiert; regulaere unbudgeted Liste enthaelt sie weiterhin.
7. Fokussierte Tests und Build ausfuehren.

## Nicht-Ziele

- Keine Aenderung am Datenmodell.
- Keine Aenderung an Budgetberechnung, Summen, Export oder Migrationen.
- Keine Aenderung an der regulaeren nicht-budgetierten Auflistung ausser technischer Anpassung an den neuen Overlay-Zeilentyp.

## Offene Punkte

Keine.

## Abweichung vom Skill

Es steht kein separates Unteragenten-Tool zur Verfuegung. Die Planung wurde lokal erstellt.
