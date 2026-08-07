# Enumerationen

## `UpdateStatusKind`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `NoUpdate` | Keine Update verfügbar oder System im Idle-Zustand |
| `Checking` | Update-Prüfung läuft |
| `Available` | Update verfügbar und zum Download bereit |
| `Downloading` | Update wird heruntergeladen |
| `Ready` | Update heruntergeladen und zur Installation bereit |
| `Installing` | Update-Installation läuft |
| `Failed` | Update-Vorgang fehlgeschlagen |

**Mapping:** Konvertiert von `AutoUpdateState` (msTools.Updater Library):
- `AutoUpdateState.Idle` → `NoUpdate`
- `AutoUpdateState.Checking` → `Checking`
- `AutoUpdateState.UpdateAvailable` → `Available`
- `AutoUpdateState.Downloading` → `Downloading`
- `AutoUpdateState.ReadyToInstall` → `Ready`
- `AutoUpdateState.Installing` → `Installing`
- `AutoUpdateState.Success` → `NoUpdate`
- `AutoUpdateState.Failed` → `Failed`
- `AutoUpdateState.Disabled` → `NoUpdate`

---

## `UpdateLockResetFailureKind`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Klassifiziert die spezifische Ursache eines Lock-Reset-Fehlers:

| Wert | Bedeutung |
|------|-----------|
| `NoLock` | Kein aktiver Update-Lock vorhanden (von `GetLockCreatedAtAsync` liefert `null`) |
| `LockNotStale` | Lock-Datei existiert, ist aber nicht alt genug (`IsLockStale()` = false) |
| `LockDeleteFailed` | Lock-Datei konnte nicht gelöscht werden (Delete returns false oder wirft Exception) |
| `ResetFailed` | Reset fehlgeschlagen aus technischem Grund (nicht Lock-spezifisch) |

**Verwendung:** Wird von `UpdateOrchestratorAdapter.ResetLockAsync()` gesetzt und vom Controller `UpdateController.ResetLock()` gemappt auf HTTP-Statuscodes und Error-Codes.

---

## `UpdateLockResetFailureSource`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Identifiziert, wo der Reset-Fehler ursprünglich aufgetreten ist:

| Wert | Bedeutung |
|------|-----------|
| `FinanceManager` | Fehler wurde von FinanceManager-Logik erkannt (z.B. Lock nicht stale, Status-Update fehlgeschlagen) |
| `Updater` | Fehler kam von msTools.Updater Library oder Package-Store (z.B. I/O-Fehler beim Lesen/Löschen der Lock-Datei) |

**Verwendung:** Controller mappt auf HTTP-Statuscodes:
- `ResetFailed` → 500 Internal Server Error
- Alle anderen Kind → 409 Conflict

---

## Kontext: Lock-Status-Klassifizierung

Die Lock-Reset-Fehlerklassifizierung beschreibt das **zentrale Problem der Anforderung**:

1. **UI zeigt Status via `UpdateStatusMapper`**, der liest `snapshot.IsLocked` von der Library
2. **Reset-Logik prüft Lock via `IAutoUpdatePackageStore.GetLockCreatedAtAsync()`**, die möglicherweise unterschiedliche Kriterien nutzt
3. **Ergebnis:** UI kann "Lock aktiv" zeigen, während Reset "NoLock" wirft

Das `NoLock`-Kind signalisiert exakt diese Inkonsistenz: Lock-Datei ist weg, aber UI dachte, es existiert noch.
