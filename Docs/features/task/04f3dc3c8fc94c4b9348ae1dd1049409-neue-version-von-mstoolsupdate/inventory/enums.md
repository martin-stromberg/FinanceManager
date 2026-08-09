# Enumerationen

## Lokal definierte Enums

### `UpdateLockResetFailureKind`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Klassifiziert den Grund, warum ein Update-Lock-Reset fehlgeschlagen ist.

| Wert | Bedeutung |
|------|-----------|
| `NoLock` | Es existiert keine aktive Update-Sperr |
| `LockNotStale` | Die aktive Sperr ist nicht alt genug um als veraltet zu gelten |
| `LockDeleteFailed` | Die aktive Sperr konnte nicht gelöscht werden |
| `ResetFailed` | Das Reset ist aus einem anderen technischen Grund fehlgeschlagen |

---

### `UpdateLockResetFailureSource`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Gibt an, wo ein Reset-Fehler erkannt wurde.

| Wert | Bedeutung |
|------|-----------|
| `FinanceManager` | FinanceManager hat den Fehler aus lokalem Zustand oder Invarianten erkannt |
| `Updater` | Der Updater Package-Store oder eine andere Updater-Komponente hat den Fehler gemeldet |

---

## Aus msTools.Updater importierte Enums

### `AutoUpdateState`
Aus msTools.Updater v0.3.0

Repräsentiert den aktuellen Zustand des Update-Prozesses. Wird durch `UpdateStatusMapper.MapState` zu `UpdateStatusKind` gemapped.

| Wert | Mapping zu UpdateStatusKind | Bedeutung |
|------|-----------|---------|
| `Idle` | `NoUpdate` | Keine Aktivität |
| `Checking` | `Checking` | Quellprüfung läuft |
| `UpdateAvailable` | `Available` | Update gefunden, aber nicht heruntergeladen |
| `Downloading` | `Downloading` | Download läuft |
| `ReadyToInstall` | `Ready` | Download abgeschlossen, bereit zur Installation |
| `Installing` | `Installing` | Installation läuft |
| `Success` | `NoUpdate` | Erfolgreich abgeschlossen |
| `Failed` | `Failed` | Fehler aufgetreten |
| `Disabled` | `NoUpdate` | Auto-Updates deaktiviert |

---

### `AutoUpdateOutcome`
Aus msTools.Updater v0.3.0

Ergebnis einer Update-Operation (Check, Install, etc.).

Verwendete Werte in `UpdateOrchestratorAdapter`:
- `Success` — Operation erfolgreich
- `Failed` — Operation fehlgeschlagen

---

### `UpdateStatusKind`
Aus FinanceManager.Shared.Dtos.Update

FinanceManager-spezifische Update-Status-Klassifizierung für die API und UI.

| Wert | Beschreibung |
|------|-----------|
| `NoUpdate` | Keine Update-Aktivität |
| `Checking` | Quellprüfung läuft |
| `Available` | Update verfügbar |
| `Downloading` | Download läuft |
| `Ready` | Installation bereit |
| `Installing` | Installation läuft |
| `Failed` | Fehler |

---

## Verwendung in Conditional Logic

In `AutoUpdateOptionsMapper`:
- `DayOfWeek` — Standard-.NET-Enum für Wochentag der Quellprüfungs-Zeitfenster

In `UpdateStatusMapper`:
- `AutoUpdateState` wird zu `UpdateStatusKind` via `MapState()` Schalter gemappt
