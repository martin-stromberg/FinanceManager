← [Zurück zur Übersicht](index.md)

# Sparpläne — Beschreibung

## Zweck

Sparpläne bilden wiederkehrende oder einmalige Sparziele mit optionalem Zielbetrag und Zieltermin ab.

## Funktionsweise

Sparpläne und Kategorien werden in der Sparplanverwaltung gepflegt. Buchungen können Sparplänen zugeordnet werden. Für wiederkehrende Pläne wird das Zieldatum bei Fälligkeit anhand des Intervalls fortgeschrieben.

Die Detailansicht eines bestehenden Sparplans zeigt neben den Stammdaten den aktuellen Saldo und den noch offenen Restbetrag an. Bei einmaligen Sparplänen wird zusätzlich der benötigte Monatsbetrag bis zum Zieldatum angezeigt, wenn noch ein Restbetrag offen ist und das Zieldatum in der Zukunft liegt. Die Kennzahlen sind reine Informationsfelder und können in der Detailansicht nicht direkt bearbeitet werden.

Die Sparplan-Analyse bewertet den Einzahlungsrhythmus zyklusbasiert: Fälligkeitspläne laufen in Finanzierungszyklen — Einzahlungen finanzieren den nächsten Zielbetrag, eine Entnahme in etwa Zielhöhe (oder ein erreichtes Zieldatum) schließt den Zyklus ab und eröffnet den nächsten. Der ausgewiesene Rückstand (`ScheduleDeficit`) zählt daher nur den laufenden Zyklus; revolvierende Pläne, deren Entnahmen die Einzahlungen etwa ausgleichen, gelten als zyklisch ausgeglichen und melden keinen Deficit.

Die beobachtete Rate leitet sich aus der dominanten Betragsklasse der Einzahlungen im laufenden Zyklus ab: Gleiche Beträge werden geclustert (Toleranz max(0,50 EUR, 5 %)), die häufigste Klasse gilt als wiederkehrende Einzahlung (bei Gleichstand gewinnt der größere Betrag). Mikro-Postings wie Zinsgutschriften (unter ~50 % des dominanten Klassenbetrags) fließen weder in die Rate noch in die erhaltene Summe ein — auch dann nicht, wenn sie die eigentlichen Einzahlungen an Zahl übersteigen. Die Einzahlungskadenz folgt dem Buchungsrhythmus selbst (Median der Monatsabstände zwischen den Belegmonaten der dominanten Klasse); das Plan-Intervall beschreibt den Fälligkeitsrhythmus der finanzierten Ausgabe und dient nur als Fallback, wenn keine Kadenz ableitbar ist.

## Kontoverknüpfung

In den Sparplan-Details können ein oder mehrere eigene Bankkonten vom Typ „Sparkonto" verknüpft werden (Abschnitt „Verknüpfte Konten", Info-Symbol erklärt Zweck und Wirkung). Bei mehreren Verknüpfungen zeigt die Liste je Konto dessen Saldo sowie Symbolbuttons zum Entfernen und zum Öffnen des Kontos; bei genau einer Verknüpfung entfällt die Saldospalte, die Symbolbuttons zum Öffnen und Entfernen bleiben. Kontoauszugszeilen eines verknüpften Kontos können keinem Sparplan zugeordnet werden — ihre Buchungen sind über den Kontostand bereits erfasst. Zusätzlich zum Saldo (Summe der Kontosalden) wird bei verknüpften Plänen in den Kopfdaten der „Eigenanteil" — die Summe der Plan-eigenen Buchungen — ausgewiesen. Sobald mindestens ein Konto verknüpft ist, folgt der angezeigte Saldo der Summe der Kontosalden — jedes verknüpfte Konto trägt seinen vollen Saldo bei, eine Aufteilung bei Sammelkonten findet nicht statt. Die Rhythmus-/Rückstandsanalyse nutzt dann den Zufluss der verknüpften Konten (eigene Einzüge, VL, Zinsen) statt der plan-verknüpften Buchungen. Plan-Buchungen, die die Speisung aus einem anderen Konto dokumentieren, werden dabei als „informell" gekennzeichnet (Merkmal in der Buchungsliste) und zählen nicht in Saldo und Erwartungswerte — der Geldfluss ist über den Kontostand bereits erfasst. Beim Entfernen des letzten Links fallen Saldo und Analyse auf die buchungsbasierte Auswertung zurück.

## Istmeldung

Für Pläne ohne verknüpfte Konten steht die Aktion „Istmeldung" (Gruppe „Buchungen") zur Verfügung: Der gemeldete reale Saldo korrigiert den Plan-Saldo über eine Differenzbuchung, ohne eine Kontobewegung zu erzeugen. Die Korrekturbuchung ist in der Buchungsliste und -detailansicht als Istmeldung gekennzeichnet. Bei verknüpften Plänen ist die Aktion deaktiviert — dort ist der Kontostand die alleinige Ist-Quelle.

## Beispiele

- Monatlicher Sparplan mit Zielbetrag und Kategorie.
- Archivierung eines Sparplans nach Verbuchung.
- Auswertung eines Sparplans in der Analyseansicht.
- Kontrolle eines einmaligen Sparziels anhand von aktuellem Saldo, Restbetrag und benötigtem Monatsbetrag.

## Einschränkungen

- Intervallbasierte Fortschreibung gilt nur für wiederkehrende Pläne.
- Archivierte Pläne sind nicht mehr aktiv.
- Symbol-Uploads sind auf die Anhang-Größenbegrenzung (`Attachments:MaxSizeBytes`, Standard 10 MB) beschränkt.
- Der benötigte Monatsbetrag wird nicht für wiederkehrende Sparpläne, erledigte Einmalziele oder Ziele mit heutigem beziehungsweise vergangenem Zieldatum angezeigt.
