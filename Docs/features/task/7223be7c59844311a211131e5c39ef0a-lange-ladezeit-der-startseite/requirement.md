# Anforderung: Ladezeit der Startseite optimieren

## Metadaten

- **Aufgaben-ID:** 7223be7c-5984-4311-a211-131e5c39ef0a
- **Branch:** task/7223be7c59844311a211131e5c39ef0a-lange-ladezeit-der-startseite
- **Prioritaet:** Nicht angegeben

## Ausgangslage

Der Aufruf der Startseite dauert zu lange. Ursache ist der zeitintensive Abruf der monatlichen KPI durch `GetMonthlyKpiAsync.GetMonthlyKpiAsync`.

## Ziel

Die Startseite soll schnell sichtbar werden, ohne auf den Abschluss des KPI-Abrufs zu warten. Die monatlichen KPI sollen nach dem initialen Seitenaufbau asynchron geladen werden.

## Fachliche Anforderungen

1. Die Startseite wird zunächst in einer Skeleton-Darstellung geladen.
2. Der initiale Seitenaufbau darf nicht durch den Abruf der monatlichen KPI blockiert werden.
3. Die monatlichen KPI werden nach dem initialen Seitenaufbau asynchron über `GetMonthlyKpiAsync.GetMonthlyKpiAsync` geladen.
4. Nach erfolgreichem Abschluss des Abrufs ersetzt die Anwendung die Skeleton-Darstellung durch die geladenen KPI.
5. Das bestehende Verhalten und die Darstellung der KPI nach erfolgreichem Laden bleiben erhalten.

## Akzeptanzkriterien

- Beim Aufruf der Startseite wird vor Abschluss des KPI-Abrufs eine Skeleton-Darstellung angezeigt.
- Die übrigen Inhalte der Startseite können angezeigt und genutzt werden, während die KPI geladen werden.
- Der KPI-Abruf startet asynchron nach Beginn des initialen Seitenaufbaus.
- Nach erfolgreichem KPI-Abruf werden die tatsächlichen monatlichen KPI angezeigt und die Skeleton-Darstellung entfernt.
- Ein langsamer KPI-Abruf verlängert nicht die Zeit bis zur ersten sichtbaren Darstellung der Startseite.

## Abgrenzungen

- Eine fachliche Änderung an der Berechnung oder den Werten der monatlichen KPI ist nicht Bestandteil dieser Anforderung.
- Eine Änderung der Datenquelle oder eine grundsätzliche Performance-Optimierung des KPI-Abrufs ist nicht Bestandteil dieser Anforderung.

## Offene Punkte

- Verhalten bei einem Fehler des asynchronen KPI-Abrufs ist nicht spezifiziert.
- Verhalten bei fehlenden oder leeren KPI-Daten ist nicht spezifiziert.
