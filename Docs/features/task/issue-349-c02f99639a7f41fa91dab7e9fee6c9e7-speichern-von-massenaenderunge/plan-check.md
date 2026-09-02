# Plan-Check: Speichern von Massenänderungen

## Status
**Plan vollständig**

## Kritische Probleme
Keine. Der Plan deckt alle Akzeptanzkriterien aus der Anforderung und verankert für jedes Kriterium mindestens einen Happy-Path- und einen Negativ-Test.

## Wesentliche Schwächen / Risiken
1. **Jahr-Grenze 1000 als Heuristik:** Der Plan schlägt vor, Datumsangaben mit `dt.Year < 1000` als ungültig abzulehnen. Das verhindert korrekt die Übernahme von `0002-01-01` und ähnlichen partiellen Werten, birgt aber das Risiko, dass ggf. historische Buchungsdaten (Jahr < 1000) abgelehnt werden. Kontoauszüge betreffen in der Regel das aktuelle Finanzjahr, daher ist diese Heuristik vertretbar.
2. **E2E-Test für partiellen Jahres-Input:** Der E2E-Test `E2E_QuickEdit_PartialYearInput_DoesNotCopyToValuta` erfordert, dass Playwright in ein `input type="date"` inkrementelle Eingaben simulieren kann. Je nach Browser/Blazor-Verhalten könnte `onchange` erst bei vollständigem Datum feuern, wodurch das Szenario schwer reproduzierbar wird. Eine Unit-Abdeckung in `StatementDraftEntriesListViewModel` ist daher zwingend.
3. **Validierung aller sichtbaren Zeilen:** Wenn vorhandene Kontoauszugszeilen im Quick-Edit kein Valutadatum haben, wird der Save-Button initial deaktiviert. Das entspricht der Anforderung, kann aber die Arbeitsabläufe von Anwendern mit altem Datenbestand überraschen. Notfalls muss die Anforderung nachjustiert werden.

## Offene Fragen an den Anwender
Keine. Alle offenen Punkte ließen sich aus dem Codebase und der Anforderung beantworten.
