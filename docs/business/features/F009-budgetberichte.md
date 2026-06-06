# F009 – Budgetberichte

## Einleitung

Budgetberichte zeigen, welche Buchungen in ein Budget eingerechnet wurden.  
Neu beeinflusst ein optionales Verwendungszweck-Muster die Zuordnung.  
So sehen Sie genauer, welche Buchungen wirklich zu einer Regel gehören.  
Nicht passende Buchungen bleiben im Bericht außerhalb der Budgetsumme.

## Wer nutzt es?

Diese Funktion nutzen Fachanwender aus Controlling und Sachbearbeitung.  
Sie prüfen Monatswerte und klären Abweichungen.

## Schritt-für-Schritt-Anleitung

1. Sie öffnen **Berichte** und danach **Budgetberichte**.
2. Sie wählen den gewünschten Zeitraum.
3. Sie öffnen die Details eines Budgetzwecks.
4. Sie vergleichen den Wert **Ist** mit den einzelnen Buchungen.
5. Sie prüfen bei Abweichungen den Verwendungszweck der Buchungen.
6. Sie kontrollieren, ob das hinterlegte Muster zur Regel passt.

## Beispiel

**Textmuster in der Regel:** `ST6464646464`  
- Buchung `Abrechnung ST6464646464 Januar` wird eingerechnet.  
- Buchung `Service ohne Vertragsnummer` wird nicht eingerechnet.

**Regex-Muster in der Regel:** `ST\d{10}`  
- Buchung mit `ST` plus zehn Ziffern wird eingerechnet.  
- Andere Texte bleiben außerhalb der Budgetsumme.

## Was passiert im Hintergrund?

Das System kombiniert die Textteile der Buchung und prüft das Muster.  
Bei Textmuster nutzt es eine Suche ohne Groß-/Kleinschreibung.  
Bei **Regex** nutzt es das hinterlegte Muster.  
Wenn kein Muster hinterlegt ist, zählt die Buchung wie bisher mit.

## Häufige Fragen (FAQ)

**F: Warum erscheint eine Buchung nicht im Budgetzweck?**  
A: Meist passt der Verwendungszweck nicht zum hinterlegten Muster.

**F: Wo sehe ich nicht passende Buchungen?**  
A: Diese bleiben im Bericht als nicht budgetiert sichtbar.

**F: Was passiert bei sehr schwierigen Regex-Mustern?**  
A: Wenn kein Treffer sicher ermittelt wird, bleibt die Buchung unbudgetiert.

**F: Wird bei Regex auch inhaltlich geprüft, ob die Regel sinnvoll ist?**  
A: Nein. Geprüft wird nur die korrekte Schreibweise.

## Verwandte Funktionen

- [F008 – Budgetplanung](./F008-budgetplanung.md)
- [F018 – Budgetwirkung während Buchung](./F018-budgetwirkung-buchung.md)
- [F016 – Berichte & Dashboards](./F016-berichte-dashboards.md)
