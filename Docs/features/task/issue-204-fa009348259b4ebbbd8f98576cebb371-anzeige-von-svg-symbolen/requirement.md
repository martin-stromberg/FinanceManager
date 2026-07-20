# Anforderung: Anzeige von SVG-Symbolen

## Metadaten

- Aufgaben-ID: fa009348-259b-4ebb-bd8f-98576cebb371
- Branch: task/issue-204-fa009348259b4ebbbd8f98576cebb371-anzeige-von-svg-symbolen
- Erstellt: 2026-07-20

## Ausgangslage

Seit der Umsetzung von Issue #197 werden Symboldateien im SVG-Format nicht mehr auf der Webseite angezeigt. Die SVG-Datei kann weiterhin als Symbol hinterlegt werden, die visuelle Anzeige bleibt jedoch aus.

## Betroffener Ablauf

1. Ein Kontakt wird neu angelegt oder ein bestehender Kontakt wird geoeffnet.
2. Eine SVG-Bilddatei wird als Symbol hochgeladen.
3. Das Symbol ist fachlich hinterlegt, wird auf der Webseite aber nicht angezeigt.

## Zielverhalten

SVG-Dateien, die als Symbol fuer einen Kontakt hochgeladen oder hinterlegt sind, werden wieder korrekt auf der Webseite angezeigt.

## Funktionale Anforderungen

- SVG-Dateien muessen als Symboldateien fuer Kontakte weiterhin akzeptiert werden.
- Ein hinterlegtes SVG-Symbol muss in allen bestehenden Symbolanzeigen fuer Kontakte sichtbar gerendert werden.
- Bereits hinterlegte SVG-Symbole muessen nach der Korrektur ohne erneuten Upload angezeigt werden.
- Die Korrektur darf die Anzeige anderer unterstuetzter Bildformate fuer Symbole nicht beeintraechtigen.

## Akzeptanzkriterien

- Gegeben ist ein neu angelegter oder bestehender Kontakt, wenn eine SVG-Datei als Symbol hochgeladen wird, dann wird dieses Symbol auf der Webseite angezeigt.
- Gegeben ist ein Kontakt mit bereits hinterlegtem SVG-Symbol, wenn die Kontaktseite geoeffnet wird, dann wird das SVG-Symbol angezeigt.
- Gegeben ist ein Kontakt mit einem Symbol in einem anderen unterstuetzten Bildformat, wenn die Kontaktseite geoeffnet wird, dann bleibt dieses Symbol unveraendert sichtbar.
- Das SVG-Symbol ist nicht nur gespeichert oder referenziert, sondern im sichtbaren UI gerendert.

## Nicht-Ziele

- Keine Erweiterung der Symbolverwaltung um neue Dateiformate.
- Keine fachliche Aenderung am Kontaktmodell.
- Keine Neugestaltung der Kontaktseite oder der Symbol-Upload-Oberflaeche.

## Offene Punkte

- Welche Aenderung aus Issue #197 die SVG-Anzeige beeinflusst hat, ist in der Bestandsaufnahme zu klaeren.
- Die konkret betroffenen Ansichten und Komponenten sind in der Bestandsaufnahme zu identifizieren.
