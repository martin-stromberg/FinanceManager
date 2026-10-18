# Interfaces

## `IGoldenCrossService`
Datei: `FinanceManager.Application/Securities/GoldenCross/IGoldenCrossService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `Guid securityId, Guid ownerUserId, CancellationToken ct` | `Task<GoldenCrossDto?>` | Statistik für eigenes Wertpapier; `null` wenn nicht gefunden/fremd |
| `EvaluateAndNotifyAsync` | `Guid securityId, CancellationToken ct` | `Task<bool>` | Phasenprüfung + Home-Page-Notification (max. 1× pro Phase und Zyklus) |

Implementierung: `GoldenCrossService` (Infrastructure), registriert scoped in `ServiceCollectionExtensions` (Zeile 126).

## `IGoldenCrossLocalizer`
Datei: `FinanceManager.Application/Securities/GoldenCross/IGoldenCrossLocalizer.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Format` | `string key, string? cultureName, params object[] args` | `string` | Lokalisierte Notification-Texte |

Implementierung: `GoldenCrossLocalizer` (Web), registriert in `ProgramExtensions` (Zeile 82). `GoldenCrossOptions` wird dort via `builder.Services.Configure<GoldenCrossOptions>(...SectionName = "GoldenCross")` gebunden (Zeile 83).

## `INotificationWriter`
Datei: `FinanceManager.Application/Notifications/` — `CreateForUserAsync(userId, title, message, NotificationType, NotificationTarget, DateTime, triggerKey, ct)`. Wird von `GoldenCrossService` und `SecurityPriceWorker` verwendet.

## `IApiClient` (Shared)
Datei: `FinanceManager.Shared/IApiClient.cs`

| Methode | Zweck |
|---------|-------|
| `Securities_GetGoldenCrossAsync(Guid id, CancellationToken ct)` | Ruft `GET /api/securities/{id}/golden-cross`; `null` bei 404 |

Implementierung in `FinanceManager.Shared/ApiClient.Securities.cs` (Zeile ~297): `GetAsync` → `EnsureSuccessStatusCode` außer 404 → `ReadFromJsonAsync<GoldenCrossDto>`.

## `ICurrentUserService`
Stellt `UserId` für Owner-Scoping im Controller (u. a. `GetGoldenCrossAsync`).
