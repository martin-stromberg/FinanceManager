# Implementierung Iteration 3

## Auftrag und Ursache

Nur der verbleibende Budget-Demodatenfehler der vollständigen Iteration-2-E2E-Suite (114/115 grün) wurde bearbeitet. Die Authentifizierungsimplementierung bleibt unverändert.

Der Test klickte `#PrevMonth` und wartete lediglich auf eine bereits sichtbare Berichtstabelle sowie einen nicht sichtbaren Spinner. Die Zustellung des Blazor-Klickereignisses kann aber noch ausstehen. Beide Bedingungen treffen schon auf den bisherigen Bericht zu. Damit konnte der Test den aktuellen statt des vorherigen Monats auslesen. In `DemoDataService` wird das aktuelle Gehalt erst am letzten Arbeitstag erzeugt; am 24. Oktober ist dessen Istwert im aktuellen Monat 0,00, im abgeschlossenen September dagegen 3.642,50. Das erklärt den unveränderten Budgetwert bei abweichendem Istwert ohne einen Authentifizierungsfehler.

## Reproduzierender Nachweis

Der unverzögerte vollständige Budgetfall bestand erneut in 36,955 s. Für einen deterministischen Regressionstest verzögert ein einmaliger DOM-Capture-Handler ausschließlich die Weiterleitung des echten Vormonat-Klicks um 750 ms. Danach wird derselbe Klick unverändert ausgeführt. Registrierung, tatsächlicher Formularlogin, Hintergrundtask, Datenbank und Bericht bleiben echt. Keine Uhr-/Tokenänderung, keine Datenwertmanipulation.

Mit der alten Ready-Prüfung scheitert die neue Periodenassertion nach 45,715 s: tatsächlich `01.11.2025 … 31.10.2026`, erwartet `30.09.2026`. Damit ist direkt belegt, dass die bisherige Synchronisation einen noch unveränderten Bericht freigibt. [Roter TRX](verification/iteration3/budget-delayed-red.trx), [Log](verification/iteration3/budget-delayed-red.log).

Ein vorheriger Setupfehlversuch kombinierte zwei Theorie-Datenzeilen auf derselben Erstbenutzer-Fixture. Die erste bestand; die zweite scheiterte folgerichtig an der bereits belegten Datenbank. Dieser Ansatz wurde vor dem eigentlichen roten Nachweis auf genau einen verzögerten Fall reduziert. [Setup-TRX](verification/iteration3/budget-red.trx), [Log](verification/iteration3/budget-red.log). Dieser Setupfehler ist kein Produktbefund und keine behauptete Baseline.

## Minimaler Fix

Nur `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs` wurde in dieser Iteration geändert. `WaitForBudgetReportReadyAsync` wartet beim Monatswechsel zuerst per Playwright auf das gerenderte Zielperiodenende, danach auf die vorhandene Tabelle und das Ladeende. Die zusätzliche Periodenassertion bleibt erhalten. Alle Betragsassertionen, Kategorien, Summen und Buchungsdialogprüfungen bleiben unverändert; kein Retry des Szenarios, keine Abschwächung auf 0, kein Skip. Der verzögerte Klick bleibt als deterministischer Nachweis der asynchronen Ereigniszustellung erhalten.

## Validierung

Vollständiger Solution-Build unmittelbar vor dem grünen Lauf: 0 Fehler, 12 Warnungen. [Build](verification/iteration3/green-build.log).

Der gezielte grüne Lauf umfasst den unveränderten Registrieren-/Anmelden-/Abmelden-Fall auf der üblichen Fixture und anschließend den vollständigen Budget-Demodatenfall auf seiner isolierten Erstbenutzer-Fixture einschließlich verzögerter Klickzustellung.

Ergebnis: 2/2 bestanden, 0 übersprungen. Registrierung 3,397 s; vollständiger Budgetfall mit verzögerter Klickzustellung 54,265 s; Gesamt 67,357 s. [Grüner TRX](verification/iteration3/budget-green.trx), [Log](verification/iteration3/budget-green.log). Alle gestarteten Testprozesse sind beendet. Keine pauschale Wiederholung der 115 E2E-Tests oder der bereits grünen Unit-/Integrationssuiten; diese Iteration ändert ausschließlich die Synchronisation dieses E2E-Falls. Die vollständigen Iteration-2-Zahlen 1.472 Unit / 139 Integration bleiben Nachweise des vorigen Stands. Die abschließende aktuelle Regression wird durch die Lifecycle-Orchestrierung separat durchgeführt.


