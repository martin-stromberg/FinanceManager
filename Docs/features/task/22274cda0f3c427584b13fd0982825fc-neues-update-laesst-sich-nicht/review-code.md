# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Doppelter Code** — In `StartInstallAsync` (Zeilen 113–126) wird der Erfolgspfad manuell mit `TryGetLockCreatedAtAsync(ct, LogLevel.Warning, "...")` gefolgt von `if (lockCreatedAt.HasValue) { _logger.LogWarning(...) }` und `await ReconcileLockStatusCacheAsync(lockCreatedAt, ct)` nachgebaut. Das ist praktisch derselbe Ablauf wie in `ReconcileLockStatusAsync` (Zeilen 226–234: `TryGetLockCreatedAtAsync` → bei Erfolg `ReconcileLockStatusCacheAsync`), nur um eine zusätzliche Warn-Log-Zeile ergänzt. Es existieren damit weiterhin zwei nahezu identische "hole Lock-Zustand, dann reconciliere Cache"-Blöcke, obwohl die erste Review-Runde genau diese Duplikation bereits über `TryGetLockCreatedAtAsync` konsolidieren sollte.

  Empfehlung: `ReconcileLockStatusAsync` um optionale Parameter erweitern (z. B. `LogLevel failureLogLevel = LogLevel.Debug`, `string failureLogMessage = "..."`, `bool warnIfStillLocked = false`), sodass der Erfolgspfad in `StartInstallAsync` diese eine Methode mit `warnIfStillLocked: true` und der Warning-Log-Konfiguration aufruft, statt den `TryGetLockCreatedAtAsync`/`ReconcileLockStatusCacheAsync`-Ablauf ein zweites Mal inline zu wiederholen.

- **Namenskonvention** — `TryGetLockCreatedAtAsync(CancellationToken ct, LogLevel failureLogLevel, string failureLogMessage)` (Zeile 246) platziert `CancellationToken ct` als ersten Parameter. Jede andere Methode in dieser Klasse mit `CancellationToken`-Parameter (`GetStatusAsync`, `CheckAsync`, `StartInstallAsync`, `ResetLockAsync`, `DeleteLockOrThrowAsync`, `ReconcileLockStatusCacheAsync`) führt `ct` konsistent als letzten Parameter. Das durchbricht die im gesamten File etablierte Konvention.

  Empfehlung: Parameterreihenfolge zu `TryGetLockCreatedAtAsync(LogLevel failureLogLevel, string failureLogMessage, CancellationToken ct)` ändern und die drei Aufrufstellen (`StartInstallAsync`, `ReconcileLockStatusAsync`) entsprechend anpassen, damit `ct` durchgängig zuletzt steht.

## Geprüfte Dateien

- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
