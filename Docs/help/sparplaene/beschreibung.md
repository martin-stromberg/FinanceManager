← [Zurück zur Übersicht](index.md)

# Sparpläne — Beschreibung

## Zweck

Sparpläne bilden wiederkehrende oder einmalige Sparziele mit optionalem Zielbetrag und Zieltermin ab.

## Funktionsweise

Sparpläne und Kategorien werden in der Sparplanverwaltung gepflegt. Buchungen können Sparplänen zugeordnet werden. Für wiederkehrende Pläne wird das Zieldatum bei Fälligkeit anhand des Intervalls fortgeschrieben.

Die Detailansicht eines bestehenden Sparplans zeigt neben den Stammdaten den aktuellen Saldo und den noch offenen Restbetrag an. Bei einmaligen Sparplänen wird zusätzlich der benötigte Monatsbetrag bis zum Zieldatum angezeigt, wenn noch ein Restbetrag offen ist und das Zieldatum in der Zukunft liegt. Die Kennzahlen sind reine Informationsfelder und können in der Detailansicht nicht direkt bearbeitet werden.

Die Sparplan-Analyse bewertet den Einzahlungsrhythmus zyklusbasiert: Fälligkeitspläne laufen in Finanzierungszyklen — Einzahlungen finanzieren den nächsten Zielbetrag, eine Entnahme in etwa Zielhöhe (oder ein erreichtes Zieldatum) schließt den Zyklus ab und eröffnet den nächsten. Der ausgewiesene Rückstand (`ScheduleDeficit`) zählt daher nur den laufenden Zyklus; revolvierende Pläne, deren Entnahmen die Einzahlungen etwa ausgleichen, gelten als zyklisch ausgeglichen und melden keinen Deficit.

Die beobachtete Rate leitet sich aus der dominanten Betragsklasse der Einzahlungen im laufenden Zyklus ab: Gleiche Beträge werden geclustert (Toleranz max(0,50 EUR, 5 %)), die häufigste Klasse gilt als wiederkehrende Einzahlung (bei Gleichstand gewinnt der größere Betrag). Mikro-Postings wie Zinsgutschriften (unter ~50 % des dominanten Klassenbetrags) fließen weder in die Rate noch in die erhaltene Summe ein — auch dann nicht, wenn sie die eigentlichen Einzahlungen an Zahl übersteigen. Die Einzahlungskadenz folgt dem Buchungsrhythmus selbst (Median der Monatsabstände zwischen den Belegmonaten der dominanten Klasse); das Plan-Intervall beschreibt den Fälligkeitsrhythmus der finanzierten Ausgabe und dient nur als Fallback, wenn keine Kadenz ableitbar ist.

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
