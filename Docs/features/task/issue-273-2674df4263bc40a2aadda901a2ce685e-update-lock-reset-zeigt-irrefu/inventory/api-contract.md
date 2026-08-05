# API- und Fehlervertrag

## Aktueller Controller

`FinanceManager.Web/Controllers/UpdateController.cs` definiert die Update-API unter `api/setup/update`.

Relevante Endpunkte:

- `GET /api/setup/update/status` liefert `UpdateStatusDto`.
- `POST /api/setup/update/install/start` mappt mehrere Exception-Typen gezielt:
  - `FileNotFoundException` -> `404 Err_Update_NotReady`
  - `IOException` -> `409 Err_Update_Locked`
  - `ArgumentException` -> `400 Err_Update_InvalidRequest`
  - `InvalidOperationException` -> `400 Err_Update_InvalidState`
- `POST /api/setup/update/lock/reset` liefert bei Erfolg `204 NoContent`, faengt aber nur `IOException` und gibt immer `409 Err_Update_InstallRunning` zurueck.

Damit ist der Installationsstart bereits differenzierter als der Lock-Reset.

## Aktuelles Reset-Verhalten

Der Reset-Endpunkt:

```csharp
try
{
    ...
    await _orchestrator.ResetLockAsync(request.Reason, ct);
    return NoContent();
}
catch (IOException ex)
{
    return Conflict(ApiErrorDto.Create(Origin, "Err_Update_InstallRunning", ex.Message));
}
```

Folgen:

- Jede lokale Adapter-`IOException` wird als laufende Installation gemeldet.
- Es gibt keinen eigenen Fehlercode fuer `NoLock`, `LockNotStale`, `LockDeleteFailed` oder `ResetFailed`.
- Der Fehlerfall wird nicht geloggt; nur die Reset-Anforderung selbst wird vorab als Warning geloggt.

## API-Fehlerformat

Der Controller nutzt `FinanceManager.Shared.Dtos.Common.ApiErrorDto.Create(origin, code, message)`. Der API-Client kennt das Format und liest `code` sowie `message` aus der JSON-Antwort in `LastErrorCode` und `LastError`.

Die UI kann neue Fehlercodes ohne API-Client-Aenderung anzeigen, sofern:

- der Controller `ApiErrorDto` mit neuem `code` zurueckgibt
- die Ressourcen passende Eintraege enthalten
- `BaseViewModel.SetError` den Code ueber `IStringLocalizer<Pages>` aufloesen kann

## Vertragliche Luecke

`IUpdateOrchestrator.ResetLockAsync(string? reason, CancellationToken ct)` gibt nur `Task` zurueck und dokumentiert "Resets a stale installation lock". Fehler sind nicht typisiert.

Fuer die Anforderung sollte der API-Vertrag nicht von frei formulierten Exception-Messages abhaengen. Der Controller braucht eine stabile Information wie:

- `UpdateLockResetException.Kind`
- oder `UpdateLockResetResult.Success/FailureKind`

## Empfohlene Fehlercodes

Die folgenden Codes passen zur bestehenden Namenskonvention:

| Fehlerfall | HTTP | Code | Bemerkung |
| --- | --- | --- | --- |
| NoLock | 409 oder 404 | `Err_Update_Reset_NoLock` | Kein aktiver Lock vorhanden; 409 ist konsistent mit Konfliktstatus, 404 waere semantisch ebenfalls moeglich. |
| LockNotStale | 409 | `Err_Update_Reset_LockNotStale` | Lock existiert, darf aber noch nicht manuell entfernt werden. |
| LockDeleteFailed | 409 oder 500 | `Err_Update_Reset_DeleteFailed` | Datei konnte nicht geloescht werden oder war trotz vorherigem Lock-Check nicht geloescht. |
| ResetFailed | 500 oder 409 | `Err_Update_Reset_Failed` | Uebrige technische Fehler. |

## Diagnosebedarf

Die API-Antwort muss fuer Anwender verstaendlich sein. Technische Details sollten eher in Logs als direkt in der UI stehen. Mindestinformationen im Log:

- Reset-Fehlerfall
- `LockCreatedAt`, falls verfuegbar
- `LockPath`, falls ueber `IAutoUpdatePackageStore.LockPath` verfuegbar
- Quelle: lokal erkannter Fehler vs. Exception aus `msTools.Updater`
- Exception-Typ und Message bei technischen Fehlern

