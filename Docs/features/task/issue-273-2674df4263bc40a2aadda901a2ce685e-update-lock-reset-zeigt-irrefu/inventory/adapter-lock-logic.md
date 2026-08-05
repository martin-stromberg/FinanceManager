# Adapter- und Lock-Logik

## Aktuelle Adapter-Verantwortung

`FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` kapselt `msTools.Updater` hinter `IUpdateOrchestrator`. Fuer Reset-Locks verwendet der Adapter:

- `_packageStore.GetLockCreatedAtAsync(ct)`
- `_packageStore.IsLockStale(lockCreatedAt.Value)`
- `_packageStore.DeleteLockAsync(ct)`
- `_statusService.UpdateAsync(...)`

## Aktuelle Reset-Implementierung

Der Adapter fuehrt bereits eine fachliche Sequenz aus:

1. Lock-Zeitpunkt lesen.
2. Wenn kein Zeitpunkt vorhanden ist: `IOException("No update lock is active.")`.
3. Wenn Lock nicht stale ist: `IOException("The update lock is not old enough to be considered stale.")`.
4. Lock loeschen.
5. Statussnapshot auf `IsLocked = false`, `LockCreatedAt = null` setzen.
6. Optional `LastError = "Lock reset: {reason}"` schreiben.

Diese Sequenz deckt die geforderten Faelle teilweise ab, aber sie transportiert sie nicht typisiert zum Controller.

## Externe Bibliothek

Die eingebundene Version `external/msTools.Updater/v0.3.0/lib/msTools.Updater.xml` dokumentiert `IAutoUpdatePackageStore` als Verwaltung der on-disk Paket-, Validierungs- und Lock-Struktur.

Relevante API-Signale:

- `GetLockCreatedAtAsync` gibt den Lock-Zeitpunkt oder `null` zurueck.
- `DeleteLockAsync` gibt `true` zurueck, wenn eine Lock-Datei geloescht wurde, und `false`, wenn keine existierte.
- `IsLockStale` entscheidet, ob ein Lock aelter als der Health-Timeout ist und als stale gelten darf.
- `LockPath` ist als Pfad der Installations-Lock-Datei verfuegbar.
- `AutoUpdateOptions.HealthTimeoutSeconds` ist Teil der Stale-Bewertung.

Damit sind alle benoetigten lokalen Eingaben fuer `NoLock`, `LockNotStale` und einen Delete-Race/Fehler vorhanden.

## Klassifizierbare Faelle

| Fall | Aktuelles Signal | Aktuelles Ergebnis | Ziel |
| --- | --- | --- | --- |
| NoLock | `GetLockCreatedAtAsync` gibt `null` | `IOException` | typisierter `NoLock` |
| LockNotStale | `IsLockStale(...) == false` | `IOException` | typisierter `LockNotStale` |
| LockDeleteFailed | `DeleteLockAsync` wirft oder gibt `false` | Exception propagiert oder Erfolg bei `false` | typisierter `LockDeleteFailed` mit technischer Ursache |
| ResetFailed | Sonstige Exception beim Lesen/Pruefen/Statusupdate | Exception propagiert | typisierter `ResetFailed` |

## Rueckgabewert von `DeleteLockAsync`

Der Adapter ignoriert aktuell das boolsche Ergebnis von `DeleteLockAsync`. Laut Bibliotheksvertrag bedeutet `false`, dass keine Lock-Datei geloescht wurde. Nach vorherigem erfolgreichem `GetLockCreatedAtAsync` ist das mindestens ein Race oder ein inkonsistenter Zustand.

Die Umsetzung sollte diesen Rueckgabewert auswerten und nicht als erfolgreichen Reset behandeln.

## Statusaktualisierung

Nach erfolgreichem Delete wird der Status ueber `AutoUpdateStatusService.UpdateAsync` konsistent gesetzt:

- `IsLocked = false`
- `LockCreatedAt = null`
- `LastError` bleibt bestehen oder wird mit dem Reset-Grund ersetzt

Ein Fehler nach erfolgreichem Delete, aber vor erfolgreichem Statusupdate waere ein Sonderfall: Die Lock-Datei ist entfernt, aber der persistierte Status kann noch stale sein. Dieser Fall sollte als `ResetFailed` geloggt werden; die UI laedt den Status ohnehin erneut nur nach Erfolg.

## Aenderungspunkte

Moegliche kleine Erweiterung im Web-Projekt:

- `UpdateLockResetFailureKind` enum
- `UpdateLockResetException : IOException` oder eigene `Exception`
- Adapter wirft diese Exception mit `Kind`, `Source`/`OriginHint`, `LockCreatedAt`, optional `LockPath`
- Controller mappt `Kind` auf API-Code und Logstruktur

Eine Result-basierte Variante waere sauberer, aendert aber `IUpdateOrchestrator.ResetLockAsync` staerker. Eine spezialisierte Exception passt besser zum bestehenden Controller-Muster.

