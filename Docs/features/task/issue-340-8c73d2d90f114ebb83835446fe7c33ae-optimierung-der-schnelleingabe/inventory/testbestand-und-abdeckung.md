# Testbestand und Abdeckung

## Vorhandene Tests

### `FinanceManager.Tests.E2E/Tests/StatementDrafts/PreliminaryStatementDraftE2ETests.cs`

Der Test `CreatePreliminaryDraft_ViaRibbon_ShouldCreateAndOpenDraftWithQuickEdit` prüft, dass der Schnellbearbeitungsmodus über das Ribbon geöffnet wird und der initiale Fokus auf einem `qe_booking_...`-Feld liegt.

### `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs`

Die Klasse prüft:

- `F8` kopiert ein einzelnes Feld aus der vorherigen Zeile.
- `Strg + F8` kopiert alle editierbaren Felder und überschreibt vorhandene Zielwerte.

Die Tests erzeugen einen Kontoauszug, öffnen ihn mit `quickEdit=true` und lösen Keyboard-Events direkt am konkreten Input aus.

## Fehlende Abdeckung

Es gibt bislang keine Tests für:

- `Strg + Pfeil hoch` in jeder Richtung aus einer mittleren Zeile.
- `Strg + Pfeil runter` in einer mittleren Zeile.
- Erhalt derselben Feldspalte beim Wechsel.
- No-op am Anfang und Ende der sichtbaren Zeilenliste.
- Wirkungsausschluss außerhalb des Schnellbearbeitungsmodus.
- Verhalten bei nicht editierbaren oder zum Löschen ausgeblendeten Zeilen.

## Testanschluss

Die bestehende `StatementDraftQuickEditValueTakeoverE2ETests`-Klasse ist der naheliegende Ort für die neuen E2E-Szenarien. Für die Fokusprüfung kann `document.activeElement.id` nach dem Keyboard-Event ausgewertet werden. Die Tests sollten nicht nur die ID, sondern auch die Zeilenposition und Spalte über jeweils mindestens zwei unterschiedliche Feldselektoren verifizieren.

Ein separater Unit-Test für reine Index-/Nachbarlogik wäre möglich, sofern die Navigation im ViewModel gekapselt wird. Eine DOM-Fokusänderung bleibt jedoch ein E2E-relevanter Teil des Akzeptanzkriteriums.
