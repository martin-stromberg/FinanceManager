# Strukturierte Anforderung: Budgetwertungsart fuer Budgetzwecke

## Metadaten

- Erstellt am: 2026-07-18
- Anlass: Nachtraeglich gemeldete Erweiterung zur Istwertbildung bei Budgetzwecken
- Thema: Konfigurierbare Budgetwertungsart fuer Budgetzwecke und Kategorieauswertungen

## Zielbild

Budgetzwecke erhalten eine fachlich sichtbare und persistierte Budgetwertungsart. Die Wertungsart steuert, welche fachlich passenden Buchungen in den Istwert eines Budgetzwecks eingehen.

Bestehende Budgetzwecke behalten aus Kompatibilitaetsgruenden das bisherige Verhalten und erhalten standardmaessig die Wertungsart `Exakte Buchungen`.

Kategorieauswertungen muessen die Wertungsart der enthaltenen Budgetzwecke beruecksichtigen. Direkte Kategorie-Gesamtbudgets muessen ebenfalls eindeutig bewerten, ob passende Buchungen unabhaengig vom Vorzeichen in den Istwert einfliessen.

## Begriffe

- Budgetzweck: Fachlicher Zweck innerhalb der Budgetplanung, dem Budgetposten und passende Buchungen zugeordnet werden koennen.
- Budgetwertungsart: Fachliche Einstellung, die bestimmt, welche passenden Buchungen fuer den Istwert gewertet werden.
- Passende Buchung: Eine Buchung, die anhand der bestehenden Zuordnungslogik fachlich zu einem Budgetzweck oder einer Budgetkategorie passt.
- Gewertete Buchung: Eine passende Buchung, die aufgrund der Budgetwertungsart in den Istwert eingeht.
- Nicht gewertete Buchung: Eine passende Buchung, die aufgrund der Budgetwertungsart nicht in den Istwert eingeht.
- Nicht budgetierte Buchung: Eine Buchung bzw. ein Betrag, der in der regulaeren Auswertung der nicht budgetierten Betraege erscheint.

## Funktionale Anforderungen

### FR-001 Budgetwertungsart fuer Budgetzwecke

Budgetzwecke muessen eine Budgetwertungsart besitzen.

Die Budgetwertungsart muss:

- fachlich sichtbar sein,
- persistiert werden,
- bei bestehenden Budgetzwecken standardmaessig auf `Exakte Buchungen` gesetzt werden,
- bei neuen oder bearbeiteten Budgetzwecken konfigurierbar sein.

### FR-002 Wertungsart `Exakte Buchungen`

Bei der Wertungsart `Exakte Buchungen` bleibt das bestehende Verhalten erhalten.

Fuer einen negativen Budgetposten gilt:

- Nur passende negative Buchungen werden fuer den Istwert dieses Budgetzwecks gewertet.
- Passende positive Buchungen werden nicht fuer den Istwert dieses Budgetzwecks gewertet.
- Passende positive Buchungen duerfen den Istwert dieses Budgetzwecks weder erhoehen noch mit negativen Buchungen saldieren.
- Passende positive Buchungen gelten in diesem Kontext als nicht budgetiert.

Entsprechend gilt fuer positive Budgetposten:

- Nur passende positive Buchungen werden fuer den Istwert dieses Budgetzwecks gewertet.
- Passende negative Buchungen werden nicht fuer den Istwert dieses Budgetzwecks gewertet.
- Passende negative Buchungen gelten in diesem Kontext als nicht budgetiert.

### FR-003 Wertungsart `Gesamtbudget`

Bei der Wertungsart `Gesamtbudget` werden alle passenden Buchungen eines Budgetzwecks gemeinsam fuer den Istwert herangezogen.

Dabei gilt:

- Das Vorzeichen der Buchung ist fuer die Wertung unerheblich.
- Positive und negative passende Buchungen werden gemeinsam saldiert.
- Alle passenden Buchungen werden in der Postenauflistung des Budgetzwecks angezeigt.
- Der Istwert des Budgetzwecks entspricht der Summe aller passenden Buchungen.

### FR-004 Kategorieauswertungen

Kategorieauswertungen muessen die Budgetwertungsart der enthaltenen Budgetzwecke beruecksichtigen.

Dabei gilt:

- Istwerte von Budgetzwecken duerfen in Kategoriezeilen nur mit den nach ihrer jeweiligen Budgetwertungsart gewerteten Buchungen eingehen.
- Nicht gewertete passende Buchungen eines Budgetzwecks duerfen den Istwert dieses Budgetzwecks und den daraus abgeleiteten Kategorie-Istwert nicht erhoehen oder saldieren.
- Nicht gewertete passende Buchungen muessen weiterhin in der regulaeren Auswertung der nicht budgetierten Betraege enthalten sein.

### FR-005 Direkte Kategorie-Gesamtbudgets

Direkte Kategorie-Gesamtbudgets werden als Gesamtbudget betrachtet.

Dabei gilt:

- Alle passenden Buchungen der Kategorie fliessen unabhaengig vom Vorzeichen in den Istwert des direkten Kategorie-Gesamtbudgets ein.
- Positive und negative passende Buchungen werden gemeinsam saldiert.
- Diese Regel gilt fuer direkte Kategorie-Budgetposten, die nicht ueber einen Budgetzweck laufen.
- Falls direkte Kategorie-Budgets fachlich bearbeitbar sind, muss die Wertungslogik in der Oberflaeche eindeutig erkennbar sein.

### FR-006 Separate Ausweisung nicht gewerteter passender Buchungen

Buchungen, die fachlich zu einem Budgetzweck passen, aber aufgrund der Budgetwertungsart nicht in dessen Istwert eingehen, muessen in der Postenauflistung des Budgetzwecks separat als nicht budgetiert bzw. nicht gewertet ausgewiesen werden.

Dabei gilt:

- Die Buchungen bleiben dem Budgetzweck fachlich sichtbar zugeordnet.
- Die Buchungen werden nicht in den Istwert des Budgetzwecks eingerechnet.
- Die Buchungen erscheinen zusaetzlich weiterhin in der regulaeren Zeile bzw. Gruppe der nicht budgetierten Betraege.
- Die doppelte Sichtbarkeit ist fachlich gewollt: einmal als passende, aber nicht gewertete Buchung beim Budgetzweck und einmal in der allgemeinen nicht budgetierten Auflistung.

## Akzeptanzkriterien

### AC-001 Persistenz und Standardwert

Gegeben sind bestehende Budgetzwecke ohne gespeicherte Budgetwertungsart.

Wenn die Anwendung nach der Erweiterung gestartet oder die Daten migriert werden,
dann besitzen diese Budgetzwecke die Budgetwertungsart `Exakte Buchungen`.

### AC-002 Sichtbarkeit und Bearbeitung

Gegeben ist ein Budgetzweck.

Wenn der Budgetzweck angezeigt oder bearbeitet wird,
dann ist seine Budgetwertungsart fachlich sichtbar.

Wenn die Budgetwertungsart geaendert und gespeichert wird,
dann bleibt die geaenderte Wertungsart dauerhaft erhalten.

### AC-003 Gesamtbudget saldiert alle passenden Buchungen

Gegeben ist eine Kategorie mit einem Budgetzweck.

Und der Budgetzweck hat ein Budget von `-15,00 EUR`.

Und der Budgetzweck hat die Wertungsart `Gesamtbudget`.

Und es existieren zwei passende Buchungen fuer diesen Zweck:

- `-12,50 EUR`
- `+9,40 EUR`

Wenn die Postenauflistung des Budgetzwecks angezeigt wird,
dann werden beide Buchungen angezeigt.

Wenn der Istwert des Budgetzwecks berechnet wird,
dann werden beide Buchungen saldiert.

Dann betraegt der Istwert des Budgetzwecks `-3,10 EUR`.

### AC-004 Exakte Buchungen wertet nur passendes Vorzeichen

Gegeben ist dieselbe fachliche Situation wie in AC-003.

Und der Budgetzweck hat die Wertungsart `Exakte Buchungen`.

Wenn der Istwert des Budgetzwecks berechnet wird,
dann wird nur die passende negative Buchung `-12,50 EUR` gewertet.

Und die passende positive Buchung `+9,40 EUR` wird nicht in den Istwert eingerechnet.

Dann betraegt der Istwert des Budgetzwecks `-12,50 EUR`.

### AC-005 Nicht gewertete passende Buchung wird separat ausgewiesen

Gegeben ist die Situation aus AC-004.

Wenn die Postenauflistung des Budgetzwecks angezeigt wird,
dann ist die passende positive Buchung `+9,40 EUR` beim Budgetzweck als nicht gewertete bzw. nicht budgetierte Buchung sichtbar.

Und dieselbe Buchung `+9,40 EUR` erscheint zusaetzlich in der regulaeren Auflistung der nicht budgetierten Betraege.

### AC-006 Kategorieauswertung beruecksichtigt Zweckwertung

Gegeben ist eine Kategorie mit mindestens einem Budgetzweck.

Wenn die Kategorieauswertung berechnet wird,
dann werden die Istwerte der enthaltenen Budgetzwecke gemaess deren jeweiliger Budgetwertungsart berechnet.

Und nicht gewertete passende Buchungen eines Budgetzwecks erhoehen oder saldieren den Kategorie-Istwert nicht ueber diesen Budgetzweck.

### AC-007 Direktes Kategorie-Gesamtbudget wertet alle passenden Buchungen

Gegeben ist ein direktes Kategorie-Gesamtbudget ohne Budgetzweck.

Und es existieren passende positive und negative Buchungen fuer diese Kategorie.

Wenn der Istwert dieses direkten Kategorie-Gesamtbudgets berechnet wird,
dann werden alle passenden Buchungen unabhaengig vom Vorzeichen saldiert.

## Testfaelle

### TC-001 Gesamtbudget mit gemischten Vorzeichen

- Budgetzweck-Budget: `-15,00 EUR`
- Budgetwertungsart: `Gesamtbudget`
- Passende Buchungen:
  - `-12,50 EUR`
  - `+9,40 EUR`
- Erwartete Postenauflistung:
  - Beide Buchungen werden beim Budgetzweck angezeigt.
- Erwarteter Istwert:
  - `-12,50 EUR + 9,40 EUR = -3,10 EUR`

### TC-002 Exakte Buchungen mit gemischten Vorzeichen

- Budgetzweck-Budget: `-15,00 EUR`
- Budgetwertungsart: `Exakte Buchungen`
- Passende Buchungen:
  - `-12,50 EUR`
  - `+9,40 EUR`
- Erwartete Wertung:
  - `-12,50 EUR` wird fuer den Istwert gewertet.
  - `+9,40 EUR` wird fuer den Istwert nicht gewertet.
- Erwarteter Istwert:
  - `-12,50 EUR`
- Erwartete Ausweisung:
  - `+9,40 EUR` ist beim Budgetzweck als passende, aber nicht gewertete Buchung sichtbar.
  - `+9,40 EUR` erscheint zusaetzlich in der regulaeren nicht budgetierten Auflistung.

### TC-003 Standardwert fuer bestehende Budgetzwecke

- Ausgangslage:
  - Ein bestehender Budgetzweck hat noch keine gespeicherte Budgetwertungsart.
- Erwartung:
  - Der Budgetzweck wird als `Exakte Buchungen` behandelt.
  - Das bisherige Verhalten bleibt unveraendert.

### TC-004 Kategorieauswertung mit gemischten Zweckwertungen

- Ausgangslage:
  - Eine Kategorie enthaelt mehrere Budgetzwecke.
  - Mindestens ein Budgetzweck nutzt `Exakte Buchungen`.
  - Mindestens ein Budgetzweck nutzt `Gesamtbudget`.
- Erwartung:
  - Jeder Budgetzweck berechnet seinen Istwert nach eigener Wertungsart.
  - Die Kategorieauswertung uebernimmt diese fachlich korrekt berechneten Istwerte.
  - Nicht gewertete passende Buchungen bleiben in der nicht budgetierten Auflistung sichtbar.

## Nicht-Ziele

- Keine Aenderung der bestehenden Matching-Logik fuer fachlich passende Buchungen, sofern diese nicht fuer die neue Wertungsart erforderlich ist.
- Keine Aenderung der Abweichungsformel `Ist - Budget`.
- Keine Entfernung der regulaeren nicht budgetierten Auflistung.
- Keine Rueckwirkung, die bestehende Budgetzwecke ohne explizite Umstellung fachlich anders bewertet als bisher.

## Offene Punkte

Keine.
