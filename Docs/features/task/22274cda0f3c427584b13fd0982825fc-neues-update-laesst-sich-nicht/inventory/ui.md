# UI-Komponenten und Controller

## `UpdateController`
Datei: `FinanceManager.Web/Controllers/UpdateController.cs`

REST API Controller für Update-Verwaltung, geschützt mit Admin-Rolle.

| Endpoint | HTTP-Methode | Kurzbeschreibung |
|----------|------------|------------------|
| `/api/setup/update/status` | GET | Liest aktuellen Update-Status |
| `/api/setup/update/settings` | GET | Liest aktuelle Einstellungen |
| `/api/setup/update/settings` | PUT | Speichert geänderte Einstellungen |
| `/api/setup/update/services` | GET | Listet verfügbare Windows Services / systemd-Units |
| `/api/setup/update/check` | POST | Triggert manuelle Update-Prüfung |
| `/api/setup/update/schedule` | POST | Setzt geplante Installationszeit |
| `/api/setup/update/install/start` | POST | Startet Update-Installation |
| `/api/setup/update/lock/reset` | POST | **Lock-Reset-Endpoint** |

### `ResetLock` Endpoint (zentral für Anforderung)

```csharp
[HttpPost("lock/reset")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> ResetLock([FromBody] UpdateLockResetRequest request, CancellationToken ct)
```

**Verarbeitung:**
1. Ruft `_orchestrator.ResetLockAsync(request.Reason, ct)` auf
2. Fängt `UpdateLockResetException`
3. Mappt `Kind` auf HTTP-Statuscode:
   - `ResetFailed` → 500 Internal Server Error
   - Andere Kinds → 409 Conflict
4. Mappt `Kind` auf Error-Code: NoLock/LockNotStale/LockDeleteFailed/ResetFailed
5. Loggt Fehler mit `Kind`, `FailureSource`, `LockCreatedAt`, Benutzer
6. Gibt 204 No Content bei Erfolg zurück

**Fehlerbehandlung Hierarchie:**
- `UpdateLockResetException` → gezielt gehandhabt
- `IOException` (nicht typisiert) → 500 Internal Server Error
- Andere Exceptions → nicht behandelt (propagieren)

---

## `SetupUpdateViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`

Blazor-ViewModel backing der Update-Setup-Seite, implementiert Ribbon-Actions für UI-Buttons.

### Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Settings` | `UpdateSettingsDto?` | Geladene Update-Einstellungen |
| `Status` | `UpdateStatusDto?` | Aktueller Update-Status (enthält `IsLocked`, `LockCreatedAt`) |
| `ServiceSuggestions` | `IReadOnlyList<string>` | Service-Namen für Autocomplete |
| `ConfirmInstallAsync` | `Func<ValueTask<bool>>?` | Callback für Downtime-Bestätigung |
| `Busy` | `bool` | Action läuft gerade |
| `Dirty` | `bool` | Einstellungen wurden seit dem Laden geändert |
| `Installing` | `bool` | Update-Installation läuft |
| `InstallPhase` | `string?` | Lokalisierungsschlüssel für aktuelle Install-Phase |

### Methoden (API-Aufrufe)

| Methode | Zweck |
|---------|-------|
| `LoadAsync()` | Lädt Settings und Status vom API |
| `SaveAsync()` | Speichert geänderte Settings |
| `CheckAsync()` | Triggert Update-Prüfung |
| `StartInstallAsync(confirmDowntime)` | Startet Installation |
| `StartInstallWithConfirmationAsync()` | Fordert Downtime-Bestätigung, dann Install |
| `ResetLockAsync()` | **Lock-Reset-Aufrufe** |
| `UpdateSettings(settings)` | Aktualisiert In-Memory-Settings (Form-Binding) |
| `Reset()` | Verwirft Änderungen |
| `LoadServiceSuggestionsAsync(query)` | Lädt Service-Namen für Autocomplete |

### Events

- **`InstallStarted`** — Ereignis nach erfolgreicher Install-Start; UI kann Health-Polling beginnen

### Ribbon-Buttons (UI-Aktivierungsbedingungen)

```csharp
protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
{
    return new List<UiRibbonRegister>
    {
        new(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
        {
            new("Setup_Section_Update", new List<UiRibbonAction>
            {
                new("UpdateCheckNow", ..., disabled: Busy || Status is null),
                new("UpdateInstall", ..., disabled: Busy || Status is null || Status.Status != UpdateStatusKind.Ready || ConfirmInstallAsync is null),
                new("UpdateResetLock", ..., disabled: Busy || Status is null || !Status.IsLocked)  // ← KRITISCH
            })
        })
    };
}
```

**Kritischer Button: "UpdateResetLock"**
- Aktivierungsbedingung: `!Busy && Status != null && Status.IsLocked`
- Verwendete Quelle: `Status.IsLocked` (kommt aus `UpdateStatusMapper`)
- **Problem:** Wenn `GetStatusAsync()` sagt "Locked", aber `GetLockCreatedAtAsync()` sagt "null", wird trotzdem eine Exception beim Reset geworfen

---

## Error-Handling im ViewModel

Die `ResetLockAsync()` Methode:
1. Ruft `ApiClient.Updates_ResetLockAsync()` auf
2. Bei Fehler fängt `RunBusyAsync()` Exception
3. Setzt `ApiClient.LastErrorCode` und `ApiClient.LastError`
4. ViewModel zeigt Error in UI

Fehlercodes die getrieben werden:
- `Err_Update_Reset_NoLock` — Kein Lock vorhanden
- `Err_Update_Reset_LockNotStale` — Lock nicht alt genug
- `Err_Update_Reset_DeleteFailed` — Delete fehlgeschlagen
- `Err_Update_Reset_Failed` — Generischer Reset-Fehler

---

## Razer-Component: `SetupUpdateTab.razor`

Datei: `FinanceManager.Web/.../ (nicht gelesen, aber erwähnt in Anforderung)`

**Verwendung:**
- Bindet ViewModel `SetupUpdateViewModel`
- Zeigt Status-Display mit `Status.IsLocked`, `Status.LockCreatedAt`
- Zeigt Ribbon-Buttons mit Bedingungen aus ViewModel
- Triggert Health-Polling nach `InstallStarted` Event

---

## API-Client Wrapper (generiert oder in ApiClient.Update.cs)

Datei: `FinanceManager.Shared/ApiClient.Update.cs` (falls vorhanden)

Stellt Methoden bereit:
- `Updates_GetStatusAsync(CancellationToken)` → `UpdateStatusDto`
- `Updates_GetSettingsAsync(CancellationToken)` → `UpdateSettingsDto`
- `Updates_ResetLockAsync(UpdateLockResetRequest, CancellationToken)` → wirft bei 409/500
- Weitere CRUD-Methoden

**Error-Handling:**
- Setzt `LastErrorCode` und `LastError` Properties
- Wirft `HttpRequestException` bei HTTP-Fehler
