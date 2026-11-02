# Plan-Check

Status: **Plan vollständig**

- Alle drei divergenten Aufrufer werden auf denselben Pass geführt (Worker, Backfill, Import).
- Aufruferspezifika (Auswahl, Fenster, Pacing, Fortschritt, Rate-Limit-Abbruch) verbleiben korrekterweise in den Aufrufern.
- Layering geprüft: `IPriceProvider`/`RequestLimitExceededException` als Application-Port verschoben; `IAlphaVantageKeyResolver`/`INotificationWriter`/`IGoldenCrossLocalizer`/`ITrendReversalLocalizer` bleiben Host-Ports (DI resolve zur Laufzeit in Web; Infrastruktur-only-Container mockt sie).
- Testbedarf abgedeckt: betroffene Tests wurden angepasst (siehe `test-results.md`); Vollrefactoring der Test-Charakterisierung ist bewusst Issue #474.
- Kein UI-/E2E-Bedarf: keine UI-relevanten Änderungen.
