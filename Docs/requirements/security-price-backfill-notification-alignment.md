# Requirement: Notification alignment for `SecurityPricesBackfillExecutor`

## Context
`SecurityPriceWorker` already creates a user notification when a security price fetch fails with relevant provider errors.  
`SecurityPricesBackfillExecutor` currently sets price error state, but does not notify the user.

## Goal
Align backfill behavior with worker behavior for notification-relevant failures by triggering:

`notifier.CreateForUserAsync(...)`

## Scope
- Add user notification creation in `SecurityPricesBackfillExecutor` for the same relevant failure classes as `SecurityPriceWorker`.
- Keep existing backfill processing behavior (continue/stop semantics) unless explicitly required by implementation constraints.
- Ensure user context is available for `CreateForUserAsync`.

## Out of scope
- Redesign of notification feature/UI.
- Broad refactoring of all price-fetch pipelines.
- New notification channels or templates.

## Functional requirements
1. On `PriceProviderException` with class **not** `RateLimit` and **not** `TransientNetwork`, backfill must create a user notification.
2. Notification pattern must follow worker baseline:
   - Title: `Kursabruf fehlgeschlagen`
   - Type: `SystemAlert`
   - Target: `HomePage`
   - Scheduled date: `DateTime.UtcNow.Date`
   - Trigger: `security:error:{securityId}`
3. Backfill must still set/maintain security price error state via existing service logic.
4. Backfill must not create user notification for `RateLimit` or `TransientNetwork`.
5. User ownership for notification must be resolved safely (`OwnerUserId` in security context).

## Acceptance criteria (Given/When/Then)
1. **Relevant provider error**
   - Given a backfill run and a non-RateLimit/non-Transient `PriceProviderException`
   - When the error is handled
   - Then one notification is created for the security owner with the expected pattern.

2. **Rate limit**
   - Given backfill hits `RateLimit`
   - When the exception is handled
   - Then no user notification is created.

3. **Transient network**
   - Given backfill hits `TransientNetwork`
   - When the exception is handled
   - Then no user notification is created.

4. **Existing error state flow**
   - Given a relevant provider error in backfill
   - When processing continues
   - Then price error state remains persisted as before and processing of other securities continues according to existing logic.

## Assumptions
- “Abruf schlägt fehl” means the same notification-relevant classification as in `SecurityPriceWorker`.
- Notification text/details can follow existing worker pattern.
- Trigger format remains `security:error:{securityId}` for compatibility with dismiss/reset behavior.

## Open points
- Whether backfill `RateLimit` should remain `throw` (current) or be changed to worker-like graceful stop (`break`) is not part of the requested change, but should be explicitly confirmed in implementation review.

## Related planning artifacts
- [Architecture blueprint](../architecture/security-price-backfill-notification-alignment.md)
- [ERM analysis](../architecture/security-price-backfill-notification-erm.md)
- [Architecture review](../improvements/security-price-backfill-notification-review.md)
- [Planning overview](../security-price-backfill-notification-planning-overview.md)
