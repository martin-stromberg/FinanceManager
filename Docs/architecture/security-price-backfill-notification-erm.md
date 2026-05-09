# ERM Analysis: Backfill error notification alignment

## Focused current model
- `Security`
  - `Id`, `OwnerUserId`
  - `HasPriceError`, `PriceErrorClass`, `PriceErrorMessage`, `PriceErrorProviderMessage`, `PriceErrorSinceUtc`
- `Notification`
  - `OwnerUserId`, `Type`, `Target`, `ScheduledDateUtc`, `TriggerEventKey`, `IsDismissed`
- Logical link from notification to security is encoded via `TriggerEventKey = "security:error:{securityId}"`.

## Observed behavior
- Worker already writes both:
  1) security error state  
  2) notification with trigger
- Backfill currently writes only security error state.

## Target data behavior
For relevant `PriceProviderException` in backfill:
1. Persist security error state (existing flow).
2. Persist notification for the owner with trigger `security:error:{securityId}`.

For `RateLimit` and `TransientNetwork`:
- No user notification.

## Consistency rules
1. Trigger format is stable and must remain parseable by dismiss flow.
2. Notification owner must match security owner.
3. Error reset after dismiss continues to work through trigger-based clear logic.

## Persistence impact
- No new entities required.
- No schema migration required for this scope.

## Idempotency and duplication notes
- Existing pattern can produce repeated notifications across repeated runs/conditions.
- This is acceptable for now (same as worker baseline), but can be hardened later with dedupe policy.

## Optional hardening (not required by current request)
- Add dedupe constraint/policy for active notifications by `(OwnerUserId, TriggerEventKey, IsDismissed=false)`.

## Related planning artifacts
- [Requirements](../requirements/security-price-backfill-notification-alignment.md)
- [Architecture blueprint](./security-price-backfill-notification-alignment.md)
- [Architecture review](../improvements/security-price-backfill-notification-review.md)
- [Planning overview](../security-price-backfill-notification-planning-overview.md)

