# Anforderung: Hilfeseite inhaltlich und visuell ueberarbeiten

## Nutzerziel

Als Anwender moechte ich unter `/help` eine leicht verstaendliche und gut navigierbare Hilfe finden, damit ich die Anwendung ohne technische Vorkenntnisse bedienen kann.

## Problem

Die sichtbare Hilfeseite enthaelt derzeit neben anwenderorientierten Informationen auch technische Erklaerungen, Implementierungsdetails und interne Hinweise. Dadurch ist sie zu umfangreich, teilweise irritierend und nicht ausreichend auf die Nutzung der Anwendung ausgerichtet. Die Benutzeroberflaeche wirkt zudem generisch.

## Funktionale Anforderungen

1. `/help` darf ausschliesslich fuer Endanwender geeignete Inhalte anzeigen.
2. Technische Dokumentation, Implementierungsdetails und interne Hinweise muessen von der sichtbaren Anwenderhilfe getrennt bleiben und duerfen nicht versehentlich als primaere Hilfequelle verwendet werden.
3. Fuer jedes Help-Thema muss eindeutig festgelegt sein, welcher anwenderorientierte Dokumenttyp oder welche Datei fuer die Anzeige verwendet wird, zum Beispiel `beschreibung.md`, `ablauf-anwender.md` oder ein dediziertes UI-Hilfe-Dokument.
4. Die Hilfe muss eine Uebersicht der verfuegbaren Themen bieten.
5. Anwender muessen zwischen Uebersicht und Detailinhalten nachvollziehbar navigieren koennen.
6. Inhalte muessen in klarer, verstaendlicher Sprache verfasst und fuer schnelles Erfassen strukturiert sein.

## UI-Anforderungen

1. Die Help-Uebersicht und die Detailseiten muessen eine verbesserte Lesbarkeit und Scanbarkeit bieten.
2. Die Help-UI muss sich optisch in die bestehende Anwendung integrieren.
3. Themen, Navigation und Detailinhalte muessen visuell klar voneinander unterscheidbar sein.
4. Die Darstellung muss auf den unterstuetzten Bildschirmgroessen nutzbar bleiben.

## Abgrenzung

- Die Sicherheits- und Verfuegbarkeitsaspekte der Help-Assets sowie des Suchindex aus Issue #325 sind nicht Bestandteil dieser Anforderung.
- Technische Dokumentation darf weiterhin vorhanden sein, wird aber nicht als sichtbare Anwenderhilfe veroeffentlicht.

## Akzeptanzkriterien

- `/help` zeigt nur anwendergeeignete Inhalte und keine unnoetigen technischen Implementierungsdetails.
- Fuer jedes Help-Thema ist dokumentiert, welche Datei oder welcher Dokumenttyp auf der UI-Hilfeseite veroeffentlicht wird.
- Die technische Dokumentation bleibt verfuegbar, wird aber nicht versehentlich als Anwenderhilfe angezeigt.
- Die Help-Uebersicht und die Detailseiten bieten bessere Lesbarkeit, Navigation und Scanbarkeit.
- Ein Test oder E2E-Pfad stellt sicher, dass `/help` Inhalte anzeigt und technische-only Inhalte nicht als primaere Anwenderhilfe erscheinen.

## Lieferumfang

- Redaktionell gefilterte, anwenderorientierte Help-Inhalte je Thema.
- Ueberarbeitete Help-Uebersicht und Detailnavigation.
- Dokumentierte Zuordnung zwischen Help-Themen und den dafuer veroeffentlichten UI-Hilfe-Dokumenten.
- Tests fuer die Inhaltsauswahl und die Anzeige der Help-Seite.
