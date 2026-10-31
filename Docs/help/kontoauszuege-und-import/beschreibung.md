← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Beschreibung

## Zweck

Der Bereich importiert Kontoauszugsdateien, erstellt daraus Entwürfe und verbucht die finalen Ergebnisse in das Buchungssystem. Sammelauszüge werden dabei in mehrere Entwürfe aufgeteilt, wenn die Datei mehrere IBANs enthält.

## Funktionsweise

Dateien werden hochgeladen und als Entwürfe vorbereitet. Danach folgen Klassifizierung, Validierung und optionale Nachbearbeitung pro Zeile, zum Beispiel Kontakt, Sparplan, Wertpapier, Split oder Kostenneutralität. Abschließend werden einzelne oder mehrere geprüfte Entwurfszeilen verbucht.

Im Massenänderungsmodus können editierbare Entwurfszeilen gemeinsam bearbeitet, zum Löschen vorgemerkt oder über die letzte leere Tabellenzeile neu ergänzt werden. Diese Änderungen bleiben zunächst lokal und werden erst beim Speichern des Massenänderungsmodus gemeinsam übernommen.

Im Schnellbearbeitungsmodus einer Kontoauszugdetailansicht können Werte aus der darüberliegenden Zeile übernommen werden, um aufeinanderfolgende, ähnliche Buchungen schneller zu erfassen: Die Taste `F8` im aktuellen Eingabefeld übernimmt den Wert dieses Feldes aus der Zeile darüber. Die Tastenkombination `Strg+F8` übernimmt alle editierbaren Werte der darüberliegenden Zeile in die aktuelle Zeile und überschreibt dabei vorhandene Werte.

Beim Verlassen eines Eingabefelds im Schnellbearbeitungsmodus hält die Anwendung die Anmeldung im Hintergrund aktiv. Nicht gespeicherte Eingaben bleiben dabei unverändert im Feld; es wird kein Entwurf neu geladen und keine zusätzliche Oberfläche angezeigt.

Zusätzlich kann der Fokus im Schnellbearbeitungsmodus zeilenübergreifend in derselben Feldspalte bewegt werden. `Strg+Pfeil hoch` fokussiert das gleiche Eingabefeld in der unmittelbar vorherigen sichtbaren Zeile, `Strg+Pfeil runter` das gleiche Eingabefeld in der unmittelbar nächsten sichtbaren Zeile. Gibt es in der jeweiligen Richtung keine Nachbarzeile, bleibt der aktuelle Fokus erhalten. Die Navigation gilt ausschließlich für Eingabefelder im Schnellbearbeitungsmodus.

Wenn die Klassifizierung keinen vorhandenen Kontakt findet, kann sie die mitgelieferte Liste bekannter Kontakte prüfen. Bei genau einem Treffer wird für den Benutzer automatisch ein Kontakt mit den hinterlegten Alias-Mustern angelegt und der Entwurfszeile zugeordnet. Die Funktion kann in den Einstellungen für den Kontoauszugsimport deaktiviert werden.

Auf mobilen Geräten werden Kontoauszugseinträge als Karten dargestellt. Datum und Betrag stehen dort in einer zweispaltigen Zeile. Bereits gebuchte Einträge erscheinen abgeschwächt, damit offene und erledigte Zeilen unterscheidbar bleiben. Lange Datei- und Textwerte brechen innerhalb der verfügbaren Breite um und erzeugen keine horizontale Seitenverschiebung.

Die mobile Karte zeigt die fachlich relevanten Zuordnungen direkt am Eintrag: einen abweichenden Kontakt oder, falls kein Kontakt gesetzt ist, den Empfänger, außerdem einen zugeordneten Sparplan und ein zugeordnetes Wertpapier. Bei Wertpapierzeilen steht die Buchungsart direkt neben dem Wertpapier in Klammern.

Über die Ribbon-Gruppe „Export" in der Detailansicht eines Kontoauszugs können die Entwurfszeilen als CSV- oder Excel-Datei (`.xlsx`) heruntergeladen werden. Der Export enthält alle Einträge des Entwurfs — einschließlich bereits gebuchter und angekündigter Zeilen — in der Reihenfolge des Auszugs. Zugeordnete Kontakte, Sparpläne und Wertpapiere werden dabei zu lesbaren Namen aufgelöst; Verarbeitungsstatus und Wertpapierdetails werden als eigene Spalten ausgegeben. Gesetzte Such- und Datumsfilter der Eintragsliste werden in den Export übernommen.

## Beispiele

- Ein CSV-Kontoauszug wird importiert und automatisch klassifiziert.
- Ein unbekannter Händler wird anhand der bekannten Kontakte automatisch als Benutzerkontakt angelegt.
- Ein Sammelauszug erzeugt mehrere Entwürfe, die später einzeln zugeordnet werden können.
- Einzelne Entwurfszeilen werden vor der Verbuchung manuell korrigiert.
- Mehrere Entwurfszeilen werden in einer Massenänderung bearbeitet, einzelne Zeilen zum Löschen vorgemerkt und neue Zeilen ergänzt.
- Mehrere Dateien werden als Massenimport mit Sicherheitszuordnung verarbeitet.
- Auf dem Smartphone zeigt eine Entwurfszeile Datum und Betrag nebeneinander sowie Kontakt, Sparplan und Wertpapier inklusive Buchungsart untereinander.
- Die Einträge eines Kontoauszug-Entwurfs werden als CSV- oder Excel-Datei exportiert, um sie extern auszuwerten.

## Einschränkungen

- Verbuchung ist an den Benutzer- und Kontokontext gebunden.
- Der Massenimport ist größenbegrenzt (Konfiguration `MassImport`): maximal 50 Dateien pro Batch, maximal 10 MB pro Datei und maximal 100 MB Gesamtinhalt; der Request-Body ist auf ca. 143 MB begrenzt.
- Mehrdeutige Treffer in der bekannten-Kontakte-Liste erzeugen keine automatische Kontaktanlage.
- Validierungswarnungen können das Buchen blockieren, wenn nicht explizit freigegeben.
- Die Anzahl exportierbarer Entwurfszeilen ist serverseitig begrenzt (Konfigurationsschlüssel `Exports:MaxRows`, Standard 50.000 Zeilen); bei Überschreitung wird der Export abgelehnt.
- Im CSV-Export werden Zellwerte, die Tabellenprogramme als Formel interpretieren würden (z. B. führendes `=`, `+`, `-`, `@`), mit einem führenden Apostroph entschärft; der Text bleibt lesbar.
