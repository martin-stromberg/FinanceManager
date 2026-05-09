# Architecture Blueprint: Backfill notification alignment

## Objective
Align `SecurityPricesBackfillExecutor` with `SecurityPriceWorker` by creating user notifications for the same notification-relevant provider error classes.

## Current state
- `SecurityPriceWorker`:
  - `RateLimit` => stop run (no notification)
  - `TransientNetwork` => continue (no notification)
  - Other `PriceProviderException` => set price error + create notification
- `SecurityPricesBackfillExecutor`:
  - Sets price error for classified provider errors
  - Does **not** create notifications yet

## Target design
### Component changes
1. Resolve `INotificationWriter` in `SecurityPricesBackfillExecutor` scope.
2. In `catch (PriceProviderException ex)`:
   - Keep current classification behavior.
   - For non-`RateLimit` and non-`TransientNetwork`: call `CreateForUserAsync(...)`.
3. Build notification with worker-compatible defaults:
   - `title = "Kursabruf fehlgeschlagen"`
   - `type = NotificationType.SystemAlert`
   - `target = NotificationTarget.HomePage`
   - `scheduledDateUtc = DateTime.UtcNow.Date`
   - `trigger = $"security:error:{securityId}"`
4. Use security owner context (`OwnerUserId`) from processed security item.

## Suggested refactoring
Introduce a shared helper for error-user-message generation to avoid drift between worker and backfill:
- Option A (preferred): shared `SecurityPriceErrorMessageBuilder`
- Option B: duplicate worker message logic in backfill (not preferred)

## Decision record
| Decision | Choice | Rationale |
|---|---|---|
| Error class selection | mirror worker notification relevance | consistent user behavior |
| Notification trigger | `security:error:{securityId}` | compatible with existing dismiss/reset flow |
| User context source | security owner (`OwnerUserId`) | robust for future execution contexts |
| Message-building | shared helper preferred | prevents text/logic drift |

## Quality goals
- Behavior consistency between worker and backfill.
- No unintended changes to existing persistence flow.
- Stable operational logging and traceability.
- Minimal implementation risk (small, focused change).

## Test strategy (architecture level)
1. Backfill: relevant provider error => price error + notification created.
2. Backfill: `RateLimit` => no notification.
3. Backfill: `TransientNetwork` => no notification.
4. Verify notification fields exactly match expected worker baseline contract.
5. Regression: worker tests remain green.

## Rollout and migration
- No database migration expected.
- Rollout is code-only and backward compatible.
- Monitor notification volume after deployment for unexpected duplicates.

## Risks
- Potential message drift if helper is not shared.
- Potential duplicate notifications in race conditions (existing system behavior).

## Related planning artifacts
- [Requirements](../requirements/security-price-backfill-notification-alignment.md)
- [ERM analysis](./security-price-backfill-notification-erm.md)
- [Architecture review](../improvements/security-price-backfill-notification-review.md)
- [Planning overview](../security-price-backfill-notification-planning-overview.md)

