# Anforderung – Schritt 3: Analyse-Buttons vereinheitlichen (#603)

## Ausgangslage

Issue #603 (martin-stromberg/FinanceManager), Schritt 3 des Bugfix-Projekts (#614).

Die Analyse-Schaltflächen der Budgetübersicht (`list/budget/purposes`,
`BudgetPurposeListViewModel`) und des Budgetberichts (`/reports/budget`,
`BudgetReportViewModel`) verhalten sich unterschiedlich:

- **Übersicht:** 4 Buttons („Vormonat", „Aktueller Monat", „Vorjahr", „Aktuelles Jahr"),
  alle **absolut auf heute** verankert; „Vorjahr"/„Aktuelles Jahr" wechseln zudem die
  Zeitraumbreite auf ein ganzes Kalenderjahr. „Nächster Monat"/„Nächstes Jahr" fehlen.
- **Bericht:** 5 Buttons („Vormonat", „Aktueller Monat", „Nächster Monat", „Vorjahr",
  „Nächstes Jahr"), alle **relativ zur aktuellen Ansicht** (verschieben `Settings.AsOfDate`
  um ±1 Monat/Jahr, auf Monatsende normiert).

## Verbindliche Semantik (aus dem Issue, gilt für beide Ansichten identisch)

- „Vormonat" lädt ausgehend von der aktuellen Ansicht den Zeitraum einen Monat weiter zurück.
- „Aktueller Monat" setzt die Ansicht auf den aktuellen Kalendermonat.
- „Nächster Monat" verschiebt die Ansicht um einen Monat in die Zukunft.
- „Vorjahr"/„Nächstes Jahr" verschieben die Ansicht um ein Jahr zurück/vor.
- Navigation ist wiederholbar (mehrfaches Klicken wandert schrittweise weiter).
- Reihenfolge: „Vormonat", „Aktueller Monat", „Nächster Monat", „Vorjahr", „Nächstes Jahr".
- Labels aus resx (de+en); `Ribbon_NextMonth`/`Ribbon_NextYear` existieren bereits.

## Entschiedene offene Punkte (Issue nennt sie offen; Entscheidung dokumentiert)

1. **„Aktuelles Jahr" in der Übersicht → behalten.** Es bleibt als sechster Schnellfilter
   hinter den fünf vereinheitlichten Buttons stehen (kein Funktionsverlust; die fünf
   gemeinsamen Buttons stehen identisch und in gleicher Reihenfolge vorn). Alternative
   „entfernen" würde die strengste AC-Lesart erfüllen, aber einen funktionierenden
   Jahres-Schnellfilter streichen — abgelehnt.
2. **Zeitraumbreite bei Jahresnavigation in der Übersicht → Breite bleibt erhalten.**
   „Vorjahr"/„Nächstes Jahr" verschieben Von und Bis exakt um ±1 Jahr — unabhängig davon,
   ob der Zeitraum einen Monat, ein Jahr oder einen freien Bereich umfasst.
3. **Nicht monatsalignierte Zeiträume → exakte Kalenderverschiebung mit Tages-Clamping.**
   Grenzen werden um ±1 Kalendermonat bzw. ±1 Jahr verschoben (`DateTime.AddMonths`/
   `AddYears` — 31.03. → 28./29.02. clamped automatisch), kein Snapping auf Kalendermonate.
4. **Bericht „Aktueller Monat" bei `Months > 1` → Ist-Verhalten bleibt.** Die konfigurierte
   Berichtsbreite bleibt erhalten; das Fenster endet am Monatsende des laufenden Monats
   (`AsOfDate = Monatsende heute`). Eine Einschränkung auf genau einen Monat würde
   Nutzer-Konfiguration verwerfen.

## Akzeptanzkriterien

- [ ] Beide Ansichten bieten die fünf Buttons in gleicher Reihenfolge: Vormonat, Aktueller
  Monat, Nächster Monat, Vorjahr, Nächstes Jahr (Übersicht: + „Aktuelles Jahr" als 6.).
- [ ] „Vormonat"/„Nächster Monat" verschieben den **angezeigten** Zeitraum um genau 1 Monat.
- [ ] „Vorjahr"/„Nächstes Jahr" verschieben den **angezeigten** Zeitraum um genau 1 Jahr.
- [ ] „Aktueller Monat" springt auf den laufenden Kalendermonat (Übersicht: Von=1., Bis=Monatsende;
  Bericht: `AsOfDate`=Monatsende heute).
- [ ] Wiederholbarkeit: mehrfaches Klicken wandert schrittweise.
- [ ] resx-Labels de+en; keine neuen Schlüssel nötig.

## Scope-Ausschlüsse

- Keine Änderung an `BudgetReportViewModel`-Navigation (bereits spezifikationskonform).
- Keine Änderung an Berichtsinhalt/Aggregation, nur Ribbon-Semantik.
- Keine resx-/EF-/Enum-Änderungen.
