# Exceptions und Fehlerklassifizierung

## `UpdateLockResetException`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Typisierte Exception für klassifizierte Lock-Reset-Fehler. Erbt von `IOException`.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Kind` | `UpdateLockResetFailureKind` | Klassifizierung des Reset-Fehlers (NoLock, LockNotStale, LockDeleteFailed, ResetFailed) |
| `FailureSource` | `UpdateLockResetFailureSource` | Ursprung des Fehlers (FinanceManager oder Updater) |
| `LockCreatedAt` | `DateTimeOffset?` | Lock-Erstellungszeit (optional; null bei NoLock-Fehler) |
| `LockPath` | `string?` | Dateipfad der Lock-Datei (optional) |
| `Message` | `string` | Diagnostische Fehlermeldung (geerbt von `IOException`) |
| `InnerException` | `Exception?` | Zugrunde liegende technische Exception (geerbt; z.B. I/O-Fehler) |

### Konstruktor
```csharp
public UpdateLockResetException(
    UpdateLockResetFailureKind kind,
    UpdateLockResetFailureSource failureSource,
    string message,
    DateTimeOffset? lockCreatedAt = null,
    string? lockPath = null,
    Exception? innerException = null)
```

### Verwendung

**Geworfen von:**
- `UpdateOrchestratorAdapter.ResetLockAsync()`

**Gefangen von:**
- `UpdateController.ResetLock()` — mappt `Kind` auf HTTP-Statuscode und Error-Code

### HTTP-Status-Mapping (in UpdateController)

| Kind | HTTP-Status | Error-Code |
|------|-------------|-----------|
| `NoLock` | 409 Conflict | "Err_Update_Reset_NoLock" |
| `LockNotStale` | 409 Conflict | "Err_Update_Reset_LockNotStale" |
| `LockDeleteFailed` | 409 Conflict | "Err_Update_Reset_DeleteFailed" |
| `ResetFailed` | 500 Internal Server Error | "Err_Update_Reset_Failed" |

---

## `UpdateLockResetFailureKind` (Enum)

Klassifiziert die **spezifische Ursache** eines Lock-Reset-Fehlers auf fachlicher Ebene.

| Wert | Auslöser | Bedeutung für den Benutzer |
|------|---------|-----|
| `NoLock` | `GetLockCreatedAtAsync()` gibt `null` zurück | Kein aktiver Lock vorhanden; Button hätte gar nicht aktiviert sein dürfen — **signalisiert UI-Inkonsistenz** |
| `LockNotStale` | `IsLockStale()` gibt `false` zurück | Lock existiert, ist aber zu jung; zu schnell Reset versucht |
| `LockDeleteFailed` | `DeleteLockAsync()` gibt `false` zurück oder wirft I/O-Exception | Lock-Datei konnte nicht gelöscht werden (Permission, Sperrung, etc.) |
| `ResetFailed` | Andere Exception außerhalb Lock-Logik | Generischer Reset-Fehler (z.B. Status-Update schlug fehl) |

---

## `UpdateLockResetFailureSource` (Enum)

Identifiziert, **wo** der Fehler aufgetreten ist — hilft beim Debugging.

| Wert | Bedeutung |
|------|-----------|
| `FinanceManager` | Fehler wurde in FinanceManager-Logik (`ResetLockAsync`) erkannt: Lock nicht stale, Status-Update fehlgeschlagen |
| `Updater` | Fehler kam von der msTools.Updater Library oder `IAutoUpdatePackageStore`: I/O-Fehler, Permission-Fehler |

### Logging-Implications

Der `UpdateController.ResetLock()` mappt `FailureSource`:
- `Updater` → LogLevel.Warning
- `ResetFailed` (Kind) → LogLevel.Error
- Andere → LogLevel.Warning

---

## Fehlerbehandlungs-Pfad in `ResetLockAsync`

```
ResetLockAsync()
  ├─ GetLockCreatedAtAsync()
  │  └─ null → throw UpdateLockResetException(NoLock, FinanceManager)
  │  └─ IOException → throw UpdateLockResetException(ResetFailed, Updater)
  │
  ├─ IsLockStale(lockCreatedAt)
  │  └─ false → throw UpdateLockResetException(LockNotStale, FinanceManager)
  │
  ├─ DeleteLockAsync()
  │  ├─ false → throw UpdateLockResetException(LockDeleteFailed, FinanceManager)
  │  ├─ IOException/UnauthorizedAccessException → throw UpdateLockResetException(LockDeleteFailed, Updater)
  │  └─ Other Exception → throw UpdateLockResetException(ResetFailed, Updater)
  │
  └─ UpdateAsync() (Status-Update)
     └─ Any Exception → throw UpdateLockResetException(ResetFailed, FinanceManager)
```

---

## Zentrale Problem-Signatur für die Anforderung

Die **`NoLock` Failure Kind** ist die **Symptomatik des Inkonsistenz-Problems:**

1. **UI liest Status:** `UpdateStatusMapper.MapAsync()` liest `snapshot.IsLocked = true`
2. **UI zeigt:** "Lock aktiv seit [Zeit]", Button zum Zurücksetzen wird aktiviert
3. **Benutzer klickt Reset**
4. **Reset-Logik prüft:** `GetLockCreatedAtAsync()` → `null`
5. **Result:** `NoLock` Exception → "Es ist kein aktiver Update-Lock vorhanden"

Dies bedeutet, dass zwischen Status-Abfrage und Reset-Versuch die Lock-Datei verschwindet oder die beiden Methoden unterschiedliche Quellen nutzen.
