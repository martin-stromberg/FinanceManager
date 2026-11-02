# Anforderung (übersetzt)

Quelle: Issue #472 (Code-Review) + Issue-Kommentar „Lege bitte für Befund 2 und Befund 3 ein Issue an und kümmere dich hier um Befund 1".

## Ausgangslage

Das außerordentliche Code-Review (siehe Issue-Kommentar vom 02.11.2026) hat drei Befunde ergeben:

- **Befund 1 (hier umzusetzen):** Der Geschäftsvorgang „Kurse von AlphaVantage abrufen und persistieren" wird von drei produktiven Aufrufern mit abweichenden Sequenzen und Nebenbedingungen komponiert:
  - `SecurityPriceWorker` (geplant): direkte `DbContext`-Persistenz, Signalauswertung, Fehler-Benachrichtigung
  - `SecurityPricesBackfillExecutor` (manuell): `CreateAsync` pro Kurs, keine Signalauswertung, keine Benachrichtigung
  - `IngSecurityPriceImportService` (CSV-Import): `UpsertDailyPricesAsync`, keine Fehlerflag-/Evaluierungsschritte
  - Konsequenzen: fehlende Portfolio-Cache-Invalidierung und `close < 0`-Validierung auf dem Worker-Pfad, fehlende Signalauswertung nach Backfill/Import, fehlende Fehlerbenachrichtigung beim Backfill, doppelte Fenster-/Wochenendlogik.
- **Befund 2:** `ISecurityPriceService` exportiert Einzelschritte ohne Pass-Einstieg → als Issue #473 ausgelagert.
- **Befund 3:** `SecurityPriceErrorRecoveryTests` charakterisieren Schrittfolgen per Reflection/Spy → als Issue #474 ausgelagert.

## Ziel

Ein einziger produktiver Einstiegspunkt („Pass") für den Kurs-Pflegevorgang in der Service-Schicht, über den alle Aufrufer denselben Vorgang mit denselben Nebenbedingungen laufen lassen:

1. Kursabruf über den Provider-Port
2. Fehlerzustand löschen/setzen inkl. deduplizierter Benutzerbenachrichtigung
3. Persistenz über den Upsert-Pfad (Validierung + Portfolio-Cache-Invalidierung, kein Duplikat-Risiko)
4. Nachgelagerte Signalauswertung (GoldenCross, TrendReversal)

Aufruferspezifisches verbleibt beim Aufrufer: Batch-Auswahl, Fensterberechnung, Quotas/Pacing, Fortschrittsreporting, Rate-Limit-Abbruchverhalten.

## Abgrenzung

- Befund 2 (Interface-Bereinigung) und Befund 3 (Test-Refactoring) werden nicht hier, sondern in #473/#474 bearbeitet. Testanpassungen sind hier nur so weit nötig, dass die Suite grün bleibt.
