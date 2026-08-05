# Strukturierte Anforderung

## Metadaten

- Aufgaben-ID: ce3844de-27e9-43d0-ac65-af1cf9172413
- Branch: task/issue-238-ce3844de27e943d0ac65af1cf9172413-sparplandetailansicht
- Erstellt: 2026-08-05
- Titel: Sparplandetailansicht

## Ziel

Die Detailansicht eines Sparplans soll zusaetzliche finanzielle Kennzahlen anzeigen, damit der aktuelle Stand und der noch offene Sparbedarf direkt ersichtlich sind.

## Fachlicher Kontext

In der Detailansicht eines Sparplans fehlen aktuell Informationen zum aktuellen Saldo, zum Restbetrag und unter bestimmten Bedingungen zum durchschnittlich benoetigten Monatsbetrag bis zum Faelligkeitsdatum.

## Funktionale Anforderungen

- Die Sparplandetailansicht zeigt den aktuellen Saldo des Sparplans an.
- Die Sparplandetailansicht zeigt den Restbetrag des Sparplans an.
- Die Sparplandetailansicht zeigt den durchschnittlichen Monatsbetrag bis zum Faelligkeitsdatum nur dann an, wenn alle folgenden Bedingungen erfuellt sind:
  - Der Sparplan ist ein einmaliger Sparplan.
  - Es ist noch ein Restbetrag offen.
  - Das Faelligkeitsdatum liegt in der Zukunft.

## Akzeptanzkriterien

- In der Detailansicht eines Sparplans ist der aktuelle Saldo sichtbar.
- In der Detailansicht eines Sparplans ist der Restbetrag sichtbar.
- Bei einem einmaligen Sparplan mit offenem Restbetrag und zukuenftigem Faelligkeitsdatum ist der durchschnittliche Monatsbetrag bis zum Faelligkeitsdatum sichtbar.
- Bei wiederkehrenden Sparplaenen wird der durchschnittliche Monatsbetrag bis zum Faelligkeitsdatum nicht angezeigt.
- Bei einmaligen Sparplaenen ohne offenen Restbetrag wird der durchschnittliche Monatsbetrag bis zum Faelligkeitsdatum nicht angezeigt.
- Bei einmaligen Sparplaenen mit Faelligkeitsdatum heute oder in der Vergangenheit wird der durchschnittliche Monatsbetrag bis zum Faelligkeitsdatum nicht angezeigt.

## Nicht-Ziele

- Keine Aenderung an der Anlage oder Bearbeitung von Sparplaenen.
- Keine Aenderung an der Berechnung oder Verbuchung von Sparplantransaktionen, soweit diese nicht fuer die Anzeige der geforderten Kennzahlen erforderlich ist.
- Keine Anzeige des durchschnittlichen Monatsbetrags fuer andere Sparplantypen als einmalige Sparplaene.

## Offene Punkte

- Es ist zu klaeren, welche bestehende Datenquelle oder Berechnungslogik den aktuellen Saldo und den Restbetrag liefert.
- Es ist zu klaeren, ob der durchschnittliche Monatsbetrag monatsgenau, kalendertagsgenau oder anhand voller verbleibender Monate berechnet werden soll.
