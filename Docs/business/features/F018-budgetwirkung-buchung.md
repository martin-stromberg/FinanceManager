# F018 – Budgetwirkung während Buchung

## Kurzbeschreibung

Beim Bearbeiten und Buchen von Kontoauszug-Entwürfen zeigt FinanceManager jetzt direkt die Budgetwirkung:
- **während der Zuordnung** (Kontakt/Sparplan/Save-All)
- **nach Abschluss der Buchung** als Zusammenfassung

So sehen Nutzer sofort, ob ein Budget überschritten wird oder sich die Zielerreichung stark verändert.

## Nutzen für Fachanwender

- Frühzeitige Warnung vor Budgetüberschreitung
- Transparenz über betroffene Budgetzwecke
- Verständliche Vorher/Nachher-Sicht je betroffenem Zweck

## Was sieht der Nutzer konkret?

## 1) Sofort-Hinweis in der Entry-Bearbeitung

Bei Änderungen an Kontakt, Sparplan oder erweiterten Feldern kann die Antwort des Systems ein `budgetImpact`-Objekt enthalten:
- Budgetzweck
- Periode
- Hinweisstufe (`Neutral`, `StronglyChanged`, `AlmostExhausted`, `Exceeded`)
- Soll-/Istwerte und Veränderung
- kurze Begründung

## 2) Abschluss-Summary nach Buchung

Nach `Book` (ganz oder je Entry) enthält `BookingResult` optional `budgetImpactSummary`:
- höchste Kritikalität
- Liste aller betroffenen Budgetzwecke
- Zielerreichung vorher/nachher + Delta

## Fachliche Regeln (vereinfacht)

- Zuordnung der Buchung zu Budgetzwecken erfolgt über `BudgetPurpose.SourceType`:
  - Kontakt
  - Sparplan
  - Kontaktgruppe
- Sollwerte kommen aus der Budgetplanung.
- Istwerte basieren auf bereits gebuchten Postings.
- Die neue Buchung wird für die Bewertung simuliert hinzugerechnet.

## Grenzen / Hinweise

- Wenn keine Budgetzuordnung möglich ist, wird ein neutraler Hinweis geliefert.
- Die Budgetwirkung ist eine Entscheidungsunterstützung und blockiert Buchungen nicht automatisch.

## Referenzen

- Flow: `Docs/flows/budget-impact-evaluation.md`
- Booking-Flow: `Docs/flows/statement-draft-booking.md`
- API: `Docs/api/StatementDraftsController.md`
