← [Zurück zur Übersicht](index.md)

# Berichtswesen — Ablauf für Anwender

## Voraussetzungen

- Das Berichts-Dashboard ist geöffnet und mindestens eine Buchungsart (Konten, Kontakte, Sparpläne, Wertpapiere) ist ausgewählt.
- Für die Wirkung auf der Startseite wird der Bericht anschließend als Favorit gespeichert und einer KPI-Kachel zugeordnet.

## Schritt-für-Schritt-Anleitung

### 1. Filterdialog öffnen

In der Ribbon-Gruppe **Filter** die Aktion **Bearbeiten** wählen. Der Dialog „Filter auswählen“ öffnet sich.

### 2. Buchungsart wählen

Bei mehreren ausgewählten Buchungsarten erscheinen oben im Dialog Tabs je Buchungsart (**Konten**, **Kontakte**, **Sparpläne**, **Wertpapiere**). Den Tab der Buchungsart wählen, für die Textfilter gelten sollen. Bei nur einer Buchungsart entfällt die Tab-Leiste.

### 3. Textfilter eintragen

Unterhalb der Auswahlliste steht die Gruppe **Textfilter**. Den gewünschten Suchbegriff in das Feld „Suchtext eingeben…“ eintragen und **Hinzufügen** wählen. Der Eintrag erscheint als Liste und lässt sich über die Schaltfläche „Textfilter entfernen“ wieder löschen. Die Schritte können je Tab wiederholt werden — jede Buchungsart besitzt eine eigene Textfilterliste.

![Filterdialog „Filter auswählen" mit eingetragenen Textfiltern „Zins / Dividende" und „Abschluss"](../../screenshots/report-textfilter.png)

> **Hinweis:** Ein Textfilter berücksichtigt eine Buchung, wenn der Begriff im Verwendungszweck oder in der Buchungsbeschreibung vorkommt (Groß-/Kleinschreibung egal). Mehrere Einträge sind ODER-verknüpft — es genügt ein Treffer. Pro Eintrag sind höchstens 500 Zeichen erlaubt; leere Eingaben und Duplikate werden nicht übernommen.

### 4. Filter übernehmen

Mit **Übernehmen** den Dialog schließen — der Bericht wird mit den Textfiltern neu berechnet. **Abbrechen** verwirft die Änderungen am Dialog; **Filter löschen** entfernt alle Filter des Dialogs (inklusive aller Textfilter).

### 5. Optional: Als Favorit speichern

Über die Ribbon-Aktion **Speichern** bzw. **Speichern unter** den Bericht als Favorit ablegen. Die Textfilter werden mit dem Favoriten gespeichert und beim erneuten Öffnen wieder angewendet.

### 6. Optional: KPI-Kachel auf der Startseite

Beruht eine KPI-Kachel der Startseite auf diesem Favoriten, zeigt sie automatisch die gefilterten Werte. Änderungen an den Filtern des Favoriten werden auf der Startseite übernommen — veraltete Kacheldaten werden dabei automatisch verworfen.

## Ergebnis

Der Bericht wertet nur noch Buchungen aus, deren Verwendungszweck oder Buchungsbeschreibung mindestens einen der eingetragenen Suchbegriffe enthält. Die Textfilter wirken zusätzlich zu allen übrigen gewählten Filtern (Entitäten, Kategorien, Buchungsuntertypen).

## Barrierefreiheit

- Die Buchungsarten-Tabs im Dialog sind als `role="tab"` mit `aria-selected` ausgezeichnet.
- Die Entfernen-Schaltfläche eines Textfilter-Eintrags trägt die Beschriftung „Textfilter entfernen“ als `title`- und `aria-label`-Attribut.
- Die Hinzufügen-Schaltfläche ist deaktiviert, solange das Eingabefeld leer ist.
