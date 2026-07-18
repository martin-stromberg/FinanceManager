# Anforderung: Mobile Ansicht der Kontoauszüge korrigieren

## Kontext

Die mobile Ansicht der Kontoauszüge weist mehrere Darstellungs- und Informationsprobleme auf. Die Ansicht soll so angepasst werden, dass Buchungsstatus, Dateinamen, Datum, Betrag und zugeordnete fachliche Informationen auf kleinen Bildschirmen lesbar und unterscheidbar bleiben.

## Ziel

Die mobile Kontoauszugsansicht soll gebuchte Einträge visuell schwächer darstellen, lange Dateinamen ohne horizontales Scrollen umbrechen und relevante Zusatzinformationen zu Kontakt, Sparplan und Wertpapier vollständig anzeigen.

## Funktionale Anforderungen

### 1. Gebuchte Einträge schwächer darstellen

Als bereits gebucht erkannte Kontoauszugseinträge müssen in der mobilen Ansicht visuell schwächer dargestellt werden als nicht gebuchte Einträge.

Die Darstellung muss für Buchhalter klar erkennbar machen, welche Einträge bereits gebucht wurden.

### 2. Lange Dateinamen umbrechen

Lange Dateinamen dürfen in der mobilen Ansicht keine horizontale Scrollbarkeit der Seite verursachen.

Dateinamen müssen bei Bedarf erzwungene Zeilenumbrüche erhalten, sodass sie innerhalb der verfügbaren mobilen Breite lesbar bleiben.

### 3. Datum und Betrag zweispaltig anzeigen

Datum und Betrag eines Kontoauszugseintrags müssen in der mobilen Ansicht als zwei nebeneinanderliegende Spalten ausgegeben werden.

Die Darstellung muss auch bei schmalen Viewports ohne horizontales Scrollen funktionieren.

### 4. Kontakt oder Empfänger anzeigen

Wenn einem Kontoauszugseintrag ein Kontakt zugewiesen ist, der weder dem Kontakt des Bankkontos noch dem Self-Kontakt entspricht, muss dieser Kontakt in der mobilen Ansicht ausgegeben werden.

Der Empfänger darf nur ausgegeben werden, wenn dem Eintrag kein Kontakt zugewiesen ist und ein Empfänger vorhanden ist.

### 5. Zugewiesenen Sparplan anzeigen

Wenn einem Kontoauszugseintrag ein Sparplan zugewiesen ist, muss dieser Sparplan in der mobilen Ansicht ausgegeben werden.

### 6. Zugewiesenes Wertpapier anzeigen

Wenn einem Kontoauszugseintrag ein Wertpapier zugewiesen ist, muss dieses Wertpapier in der mobilen Ansicht ausgegeben werden.

Zusätzlich muss die Art der Buchung direkt daneben in Klammern angezeigt werden.

## Akzeptanzkriterien

- Bereits gebuchte Kontoauszugseinträge sind in der mobilen Ansicht eindeutig schwächer dargestellt als nicht gebuchte Einträge.
- Lange Dateinamen umbrechen innerhalb der mobilen Ansicht und erzeugen keine horizontale Scrollbarkeit der Seite.
- Datum und Betrag werden in der mobilen Ansicht als zwei nebeneinanderliegende Spalten dargestellt.
- Ein abweichender zugewiesener Kontakt wird angezeigt, wenn er nicht dem Bankkonto-Kontakt und nicht dem Self-Kontakt entspricht.
- Ein Empfänger wird nur angezeigt, wenn kein Kontakt zugewiesen ist und ein Empfängerwert vorhanden ist.
- Ein zugewiesener Sparplan wird angezeigt.
- Ein zugewiesenes Wertpapier wird angezeigt.
- Bei einem zugewiesenen Wertpapier wird die Buchungsart in Klammern neben dem Wertpapier angezeigt.

## Nicht-Ziele

- Keine Änderung der fachlichen Erkennung, ob ein Eintrag bereits gebucht ist.
- Keine Änderung der Kontakt-, Sparplan- oder Wertpapierzuordnung selbst.
- Keine Änderung der Desktop-Ansicht, sofern sie nicht technisch notwendig ist, um die mobile Darstellung korrekt umzusetzen.
