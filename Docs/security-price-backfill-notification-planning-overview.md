# Planning Overview: Backfill notification on fetch failure

## Requirement
In `SecurityPricesBackfillExecutor`, trigger user notification via `notifier.CreateForUserAsync` on fetch failures matching `SecurityPriceWorker` notification-relevant error cases.

## Generated planning artifacts
1. Requirements  
   - [Docs/requirements/security-price-backfill-notification-alignment.md](./requirements/security-price-backfill-notification-alignment.md)
2. Architecture blueprint  
   - [Docs/architecture/security-price-backfill-notification-alignment.md](./architecture/security-price-backfill-notification-alignment.md)
3. ERM analysis  
   - [Docs/architecture/security-price-backfill-notification-erm.md](./architecture/security-price-backfill-notification-erm.md)
4. Architecture review  
   - [Docs/improvements/security-price-backfill-notification-review.md](./improvements/security-price-backfill-notification-review.md)

## Consolidated implementation direction
- Add `INotificationWriter` usage in `SecurityPricesBackfillExecutor`.
- Mirror worker-style notification relevance:
  - notify on non-`RateLimit` and non-`TransientNetwork` `PriceProviderException`.
- Keep trigger format `security:error:{securityId}` for existing dismiss/reset compatibility.
- Use owner context (`OwnerUserId`) for `CreateForUserAsync`.
- Keep existing price error persistence (`SetPriceErrorAsync`) behavior.

## Quality and test focus
- Add backfill tests equivalent to worker error-handling matrix.
- Verify exact notification contract fields (title/type/target/date/trigger).
- Keep worker regression tests green.

## Open assumptions / decisions to confirm
1. Scope includes notification parity; it does **not** necessarily change backfill `RateLimit` stop-via-throw behavior unless explicitly requested.
2. Message text is pattern-based and should follow existing worker user-facing style.
3. Dedupe strategy for repeated notifications remains unchanged in this change set.

