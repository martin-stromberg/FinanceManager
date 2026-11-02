# Code-Review

Status: **Keine Befunde**

Geprüfte Punkte:

- `SecurityPriceUpdateService.RefreshFromProviderAsync`: Fehlerpfad setzt Flag via Service + deduplizierte Notification (bisherige Worker-Semantik inkl. `security:error:{id}`-Trigger übernommen); `ClearPriceErrorAsync` nur nach erfolgreichem Abruf; `prices.Count == 0` → `NoData` ohne Evaluierung (Worker-Parität).
- `StoreAndEvaluateAsync`: Evaluierung warn-and-continue wie bisher im Worker; `OperationCanceledException` wird nicht geschluckt.
- Worker/Backfill enthalten keine Geschäftslogik mehr (nur Auswahl, Fenster, Pacing, Fortschritt); keine Reste der direkten `DbContext`-Persistenz.
- `ISecurityPriceService` wird weiterhin für `GetLatestDateAsync`/`UpsertDailyPricesAsync`/Fehlerflag genutzt — keine ungenutzten Auflösungen in den Aufrufern (Backfill nutzt `GetLatestDateAsync` weiter für sein Fenster).
- Konstruktor-Injection durchgängig; keine Captured-Scoped-Probleme (Pass wird pro Scope aufgelöst wie zuvor die Einzelservices).
- Test-Mocks persistieren real (Upsert → `SecurityPrice`-Rows), Assertions prüfen Endzustand.

## Befunde

Keine.
