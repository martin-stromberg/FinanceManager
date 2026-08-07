# Übersetzte Anforderung: Update-Lock-Handling — Inkonsistenzen im Status und Reset

**Aufgaben-ID:** 22274cda-0f3c-4275-84b1-3fd0982825fc  
**Branch:** task/22274cda0f3c427584b13fd0982825fc-neues-update-laesst-sich-nicht  
**Erstellt:** 2026-08-07

---

## Fachliche Zusammenfassung

Das Update-System zeigt einen Lock-Zustand an, der in sich widersprüchlich ist: Obwohl ein aktiver Lock mit Erstellungszeit gemeldet wird, ist der "Update installieren"-Button deaktiviert (was korrekt ist, wenn ein Lock vorhanden ist), aber der "UpdateLock zurücksetzen"-Button antwortet auf Aktivierung mit der Fehlermeldung "Es ist kein aktiver Update-Lock vorhanden." Dies deutet auf eine Desynchronisation zwischen der Lock-Dateiprüfung (für die UI-Button-Aktivierung) und der Lock-Reset-Logik hin. Nach einem Neustart des Anwendungsdienstes funktioniert die Installation korrekt, was impliziert, dass das Lock-Cleanup nach der Installation nicht vollständig oder konsistent erfolgt.

---

## Betroffene Klassen und Komponenten

### Logikklassen / Services
- `UpdateOrchestrator` — zentrale Orchestrierung, insbesondere Lock-Abfragen in `GetStatusAsync()` und `ResetLockAsync()`
- `UpdateExecutor` — Ausführung der Installation und Lock-Management
- `UpdateFileStore` — Persistierung und Abfrage von Lock-Dateien (`TryCreateLockAsync`, `GetLockCreatedAtAsync`, `DeleteLockAsync`, `HasActiveLockAsync`)
- `UpdateOrchestratorAdapter` — API-Adapter und Exception-Mapping für Reset-Fehler
- `SetupUpdateViewModel` — ViewModel mit Polling und Status-Refresh für UI-Button-Activation

### UI-Komponenten / Controller
- `SetupUpdateTab.razor` — Web-UI mit Ribbon-Buttons "Update installieren", "UpdateLock zurücksetzen" und Status-Display
- API-Controller für Update-Verwaltung — HTTP-Endpoints für `GetStatusAsync`, `StartInstallAsync`, `ResetLockAsync`

### Fehlertypen
- `UpdateLockResetException` — Typisierte Exception für Lock-Reset-Fehler mit `Kind`, `FailureSource`, optionalen Metadaten

---

## Implementierungsansatz

### Identifizierte Probleme

1. **Lock-Statusabfrage ist inkonsistent:**
   - Die Logik in `GetStatusAsync()` oder `WithRuntimeStateAsync()` prüft, ob eine Lock-Datei vorhanden ist und meldet dies als "Lock aktiv"
   - Die Logik in `ResetLockAsync()` prüft möglicherweise mit anderen Kriterien (z. B. `IsInstallRunning`-Flag, oder die Lock-Datei wurde zwischenzeitlich gelöscht)
   - Ergebnis: UI zeigt "Lock aktiv seit [Zeit]", aber Reset sagt "kein aktiver Lock"

2. **Reset-Button-Aktivierung in der UI:**
   - Der Button sollte nur aktiviert sein, wenn ein Lock vorhanden UND alt genug ist (Staleness-Prüfung)
   - Möglicherweise werden unterschiedliche Lock-Quellen abgefragt (Datei-System vs. In-Memory-Flag)

3. **Lock-Cleanup nach Installation:**
   - Das Installer-Skript soll die Lock-Datei entfernen
   - Möglicherweise tritt ein Fehler auf, der die Lock-Datei nicht löscht, aber den Status zu `Installing` oder `NoUpdate` setzt
   - Der nächste Status-Check nach dem Neustart muss das Cleanup wiederherstellen

### Behebungsansatz

- **Einheitliche Lock-Prüfung:** `UpdateFileStore.HasActiveLockAsync()` oder äquivalent sollte die einzige Quelle der Wahrheit sein
- **Atomare Status-Updates:** Sicherstellen, dass Lock-Datei-Existenz und Status in `status.json` nicht auseinanderdriften
- **Robustes Cleanup:** Nach erfolgreichem Installer-Abschluss soll das Cleanup explizit validiert werden; im Fehlerfall soll der Status `Failed` mit konkretem Grund gespeichert werden
- **Post-Install-Reconciliation:** Nach Neustart prüft `ReconcileInstallingAsync()` Lock-Existenz und aktualisiert Status korrekt; hier ist sicherzustellen, dass Locks in Fehler-Szenarien entfernt werden

---

## Konfiguration

- **Lock-Staleness-Schwelle:** `max(HealthTimeoutSeconds, 60)` Sekunden (serverseitig in `UpdateOptions.HealthTimeoutSeconds`, Fallback `120` Sekunden, Clamp `10..600`)
- **Retry-Logik für Lock-Cleanup:** Möglicherweise muss der Installer-Prozess mehrfach versuchen, die Lock-Datei zu löschen, um Race Conditions zu vermeiden

---

## Offene Fragen und Annahmen

1. **Lock-Prüfung für UI-Buttons:**
   - Wie wird derzeit festgestellt, ob der "UpdateLock zurücksetzen"-Button aktiviert sein soll? (Abfrage der Lock-Datei? In-Memory-Flag? Status-JSON?)
   - Sollte der Button nur aktiviert sein, wenn `HasActiveLockAsync() && LockIsStale()` true ist?

2. **Timing nach Installation:**
   - Wird die Lock-Datei vom Installer-Skript oder vom Host-Prozess gelöscht?
   - Falls vom Skript: Wird das Cleanup validiert? (Fehler zurück an Status?)
   - Falls vom Host: Wie wird das verhindert, wenn der Host nach Prozessstart beendet wird?

3. **Race Condition nach Neustart:**
   - Kann es vorkommen, dass `GetStatusAsync()` während `ReconcileInstallingAsync()` aufgerufen wird und dabei inkonsistente Zustände liefert?

4. **Error-Szenarien:**
   - Wenn das Installer-Skript bricht, wird der Status automatisch auf `Failed` gesetzt, und wird die Lock-Datei bereinigt?
   - Oder wird der Lock als "verwaist" behandelt und erst beim nächsten Neustart bereinigt?

5. **Kundenerlebnis:**
   - Ist es akzeptabel, dass Kunden nach einem fehlgeschlagenen Update-Versuch den Anwendungsdienst manuell neu starten müssen, oder sollte das System versuchen, automatisch zu recovering?

---

## Anlagen

- Dokumentation: `Docs/help/updates/beschreibung.md`, `ablauf-technisch.md`, `troubleshooting.md`
- Fehlerbehandlung: siehe `ablauf-technisch.md`, Abschnitte 5 (Lock-Reset) und 4 (Post-Update-Reconciliation)
