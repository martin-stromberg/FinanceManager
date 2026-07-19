# Strukturierte Anforderung

## Ausgangslage

Nach Einfuehrung der Budgetwertungsart werden in der Postenauflistung pro Berichtszeile passende, aber nicht fuer das Einzelpostenbudget gewertete Posten zusammen mit den budgetierten Posten angezeigt. Die Tabellenwerte sind korrekt, die Detailauflistung trennt die Posten aber visuell und strukturell nicht ausreichend.

Zusaetzlich dauert das Oeffnen der Postenauflistung pro Berichtszeile weiterhin zu lange. Vor den letzten Aenderungen war die Auflistung nahezu sofort sichtbar.

## Ziel

Die Postenauflistung pro Berichtszeile soll wieder schnell erscheinen und zugleich klar zwischen budgetierten und nicht budgetierten bzw. passenden, aber nicht gewerteten Posten unterscheiden.

## Fachliche Anforderungen

- Nicht budgetierte bzw. passende, aber nicht gewertete Posten muessen in der Postenauflistung deutlicher von budgetierten Posten unterscheidbar sein.
- Diese nicht budgetierten Posten erhalten eine `opacity` von `0.8`.
- Die Darstellung muss trotz reduzierter Deckkraft gut lesbar bleiben; Grau-auf-Grau-Kontraste sind zu vermeiden.
- Das Laden der Postenauflistung soll wieder nahezu sofort erfolgen.
- Die bestehende fachliche Unterscheidung `IsValuedForBudgetPurpose` darf nicht verloren gehen.
- Vorhandene lokale Aenderungen am Rawdaten-Cache und an der Overlay-Darstellung sollen erhalten oder fachlich sauber weiterentwickelt werden, soweit sie zur Anforderung passen.

## Akzeptanzkriterien

- Beim Oeffnen einer Zweck-Postenauflistung wird nicht pro Klick ein kompletter Budgetbericht neu berechnet, wenn die Rohdaten des aktuell geladenen Berichts bereits verfuegbar sind.
- Nach dem Laden eines Budgetberichts nutzt die Zweck-Postenauflistung die bereits aufgebauten bzw. gecachten Rohdaten.
- Nicht gewertete passende Posten werden mit `opacity: 0.8` dargestellt.
- Die Texte und Badges nicht gewerteter passender Posten bleiben in hellem und dunklem Theme gut lesbar.
- Bestehende Zahlenlogik fuer Einzelpostenbudget und Gesamtbudget bleibt unveraendert.

## Testfaelle

- Einzelpostenbudget mit passendem negativen Posten und passender positiver Buchung: Die Detailauflistung enthaelt beide Posten, markiert nur den negativen Posten als gewertet und den positiven als nicht budgetiert.
- Mehrfaches Oeffnen von Zweck-Postenauflistungen nach einem Berichtslauf verursacht keinen erneuten API-Rohdatenabruf pro Klick.
- CSS-Regeln fuer nicht budgetierte Detailposten enthalten `opacity: 0.8` und kontrastreiche Farben.

## Offene Punkte

Keine.
