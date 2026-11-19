← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — Beschreibung

## Zweck

Der Bereich verwaltet Bankkonten und die daraus entstehenden Buchungen. Anwender können Konten pflegen, Buchungen nach Entität abrufen und Exporte erstellen.

## Funktionsweise

Konten werden als Stammdaten gepflegt und bilden die Grundlage für Buchungslisten, Exporte und Auswertungen. Die Bankübersicht zeigt zusätzlich Summen und Verteilungen für die aktuell sichtbaren Konten, darunter Gesamtwert sowie Veränderungen im laufenden Jahr und Monat. Buchungen können nach Konten, Kontakten, Sparplänen und Wertpapieren betrachtet werden. Sammelkonten können zusätzlich mehrere verknüpfte IBANs verwalten, etwa für Sparpläne oder Sparbriefe. Stornierungen erzeugen nachvollziehbare Gegenbuchungen.

Jede Bankbuchung trägt eine Postenart: Eingang, Ausgang, Zinsen oder Steuer. Die Postenart wird beim Import des Kontoauszugs automatisch bestimmt und kann auf der Karte der Entwurfszeile korrigiert werden; sie erscheint in der Postenliste als eigene Spalte.

Über die Ribbon-Aktion „Kontierungsregeln" in der Gruppe „Buchungen" der Bankkonto-Karte lässt sich ein Dialog öffnen, der über dem Karteninhalt liegt und die Kontierungsregeln des Kontos verwaltet — Regeln, mit denen die Postenart automatisch bestimmt wird: Eine Regel verknüpft ein Textfeld (Verwendungszweck oder Buchungsbeschreibung) mit einem Suchtext und einer Ziel-Postenart. Enthält das gewählte Feld den Suchtext, erhält die Buchung die Postenart der Regel. Trifft mehrere Regeln zu, gewinnt die Regel mit der kleinsten Reihenfolge; trifft keine zu, entscheidet das Betragsvorzeichen (negativ = Ausgang, sonst Eingang). Regeln werden in der Tabelle des Dialogs angelegt, bearbeitet und entfernt; die Aktionen je Regelzeile (Bearbeiten, Entfernen) und in der Eingabezeile (Hinzufügen, Speichern, Abbrechen) stehen als Symbolschaltflächen mit Tooltip bereit, und die Eingabetaste im Suchtext-Feld speichert die Regel ebenfalls. Das leere Reihenfolge-Feld hängt eine neue Regel ans Ende. Der Dialog lässt sich über die Schließen-Schaltfläche in der Titelzeile oder einen Klick außerhalb des Dialogs wieder schließen; bei einem neuen, noch nicht gespeicherten Konto ist die Ribbon-Aktion deaktiviert und begründet dies per Tooltip.

Über die Ribbon-Aktion „Postenarten neu bewerten" auf der Bankkonto-Karte lassen sich alle bereits gebuchten Bankbuchungen des Kontos im Hintergrund neu bewerten — zum Beispiel nach dem Anlegen neuer Regeln. Fortschritt und Ergebnis erscheinen im Statusbereich der Karte; die Aktion kann jederzeit wiederholt werden und ändert ausschließlich die Postenart, nie Beträge oder Zuordnungen.

In der Detailansicht einer Buchung bietet die Ribbon-Gruppe „Verknüpfte Informationen“ direkte Sprünge zu den verknüpften Datensätzen: „Konto öffnen“, „Kontakt öffnen“, „Sparplan öffnen“ und „Wertpapier öffnen“ führen zur jeweiligen Detailkarte; von dort bringt die Zurück-Aktion zurück zur Buchung. Ist eine Verknüpfung am Eintrag nicht gesetzt, ist der zugehörige Eintrag deaktiviert. Über „Anhänge“ lässt sich die Anhangsliste der Buchung öffnen und dort Dateien und Links verwalten.

## Beispiele

- Ein Girokonto wird angelegt und mit einem Bankkontakt verknüpft.
- Die Bankübersicht zeigt auf einen Blick den Gesamtbestand und die Verteilung nach Kontotyp oder Bankkontakt.
- Ein Sammelkonto bündelt mehrere Unter-IBANs und ordnet importierte Auszüge automatisch zu.
- Eine Kontierungsregel „Verwendungszweck enthält ‚Zins' → Zinsen" ordnet Zinsgutschriften automatisch der Postenart Zinsen zu; eine zweite Regel „Kapitalertragssteuer → Steuer" erkennt Steuerbuchungen.
- Nach dem Anlegen dieser Regeln bewertet die Aktion „Postenarten neu bewerten" alle bereits gebuchten Buchungen des Kontos neu, sodass auch der Altbestand Zinsen und Steuer als Postenart trägt.
- Für ein Konto wird eine Buchungsliste als CSV exportiert.
- Eine fehlerhafte Buchung wird storniert und als Reversal nachvollziehbar markiert.

## Einschränkungen

- Kontobezogene Aktionen sind benutzergebunden.
- Stornierung ist nur möglich, wenn keine fachlichen Sperrgründe vorliegen.
- Kontierungsregeln gelten nur für Bankkonten und prüfen ausschließlich die Felder Verwendungszweck und Buchungsbeschreibung; eine Regel ist genau „Feld enthält Suchtext" — komplexe Bedingungen wie Platzhalter, Betragsgrenzen oder Kombinationen werden nicht unterstützt.
- Die Postenart einer bereits gebuchten Buchung kann nicht einzeln bearbeitet werden; Änderungen am gebuchten Bestand erfolgen ausschließlich über „Postenarten neu bewerten" am Konto.
- Neue oder geänderte Regeln wirken erst auf künftig klassifizierte Entwurfszeilen; bereits gebuchte Buchungen ändern sich nur über die Neubewertung.
- Im CSV-Export werden Zellwerte, die Tabellenprogramme als Formel interpretieren würden (z. B. führendes `=`, `+`, `-`, `@`), mit einem führenden Apostroph entschärft; der Text bleibt lesbar.
