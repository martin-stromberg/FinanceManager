# Umsetzungsplan

## Ziel

Die Postenauflistung pro Budgetberichtszeile soll nicht budgetierte bzw. passende, aber nicht gewertete Posten klarer darstellen und wieder ohne spuerbare Wartezeit oeffnen.

## Schritte

1. Server-Cache fuer Rawdaten im Overlay-Pfad nutzen
   - In `BudgetReportsController.GetRawAsync` den Aufruf auf `GetRawDataAsync(..., ignoreCache: false)` umstellen.
   - Der normale Reportabruf befuellt den Cache weiterhin beim Laden der Tabelle.
   - Der vorhandene ViewModel-Cache bleibt als clientseitige zweite Schutzschicht erhalten.

2. Optionalen weiteren Performance-Hebel pruefen
   - Falls ein Test zeigt, dass mehrere Overlay-Klicks weiterhin mehrfach API-Rohdaten abrufen, `ShowPurposePostingsAsync` so absichern, dass derselbe aktuelle Request im ViewModel nur einmal geladen wird.

3. Darstellung nicht budgetierter Detailposten verbessern
   - `.budget-report-posting-unbudgeted` in hellem und dunklem Theme mit `opacity: 0.8` versehen.
   - Farben so setzen, dass Text und Badge trotz reduzierter Deckkraft lesbar bleiben.

4. Tests ergaenzen
   - ViewModel-Test mit zwei Zweckzeilen und zaehlendem API-Client: nach geladenem Bericht und mehreren Zweck-Overlay-Aufrufen wird der Rawdaten-Endpunkt nur einmal verwendet.
   - Bestehenden Test fuer Einzelpostenbudget weiterverwenden, um die fachliche Markierung zu sichern.

5. Dokumentation pruefen
   - Fachliche Help-Doku nur anpassen, falls sich Regeln aendern. Erwartung: keine Aenderung, da nur Performance und Darstellung betroffen sind.

## Offene Punkte

Keine.
