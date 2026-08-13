# Strukturierte Anforderung

## Titel
Textfarbe im Ribbon-Menü bei mobiler Ansicht im Dark Mode korrigieren

## Ausgangslage
In der mobilen Ansicht werden Texte im Ribbon-Menü bei aktivem Dark Mode schwarz dargestellt. Dadurch ist die Lesbarkeit auf dem dunklen Hintergrund stark eingeschränkt.

## Ziel
Die Texte im mobilen Ribbon-Menü sollen bei aktivem Dark Mode mit einer hellen Schriftfarbe angezeigt werden, sodass sie gut lesbar sind.

## Funktionale Anforderungen
- Bei mobiler Ansicht und aktivem Dark Mode müssen alle sichtbaren Texte im Ribbon-Menü eine helle, kontrastreiche Schriftfarbe verwenden.
- Die Änderung muss für die relevanten Menütexte des Ribbon-Menüs gelten, nicht nur für einzelne Einträge.
- Die Darstellung im Light Mode darf durch die Änderung nicht verschlechtert werden.
- Die Desktop-Ansicht darf durch die Änderung nicht unbeabsichtigt verändert werden.

## Akzeptanzkriterien
- In der mobilen Ansicht sind die Texte des Ribbon-Menüs im Dark Mode nicht schwarz.
- Die Texte des Ribbon-Menüs sind im Dark Mode auf mobilen Viewports gut lesbar.
- Im Light Mode bleiben die Menütexte weiterhin lesbar und optisch konsistent.
- Die Änderung ist auf das Styling der mobilen Ribbon-Menü-Darstellung begrenzt.

## Nicht-Ziele
- Keine strukturelle Umgestaltung des Ribbon-Menüs.
- Keine Änderung der Menüfunktionen oder Navigation.
- Keine allgemeine Überarbeitung des Dark-Mode-Designs außerhalb des betroffenen Ribbon-Menüs.

## Hinweise
- Der Fehler betrifft die Kombination aus mobiler Ansicht, Ribbon-Menü und Dark Mode.
- Die Ursache liegt voraussichtlich in fehlenden oder überschriebenen Dark-Mode-Styles für Textfarben im mobilen Menü.
