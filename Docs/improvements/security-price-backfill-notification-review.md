# Architecture Review: Backfill notification alignment

## Review summary
The planned change is feasible and low-risk. Main requirement is consistency with `SecurityPriceWorker` for notification-relevant errors.

## Findings
### Major
1. **Semantics must be explicitly mirrored**
   - Backfill must notify only on non-`RateLimit` and non-`TransientNetwork` `PriceProviderException`.
2. **Message consistency risk**
   - Without shared message-building logic, worker/backfill user text may drift.
3. **Test coverage gap**
   - Backfill currently lacks worker-equivalent error-handling tests for notification behavior.

### Minor
1. **Owner resolution robustness**
   - Prefer owner from security context (`OwnerUserId`) over implicit assumptions.

## Recommended guardrails
1. Implement notification field contract exactly (title/type/target/date/trigger).
2. Add/extend tests for:
   - relevant error => notification created
   - `RateLimit` => no notification
   - `TransientNetwork` => no notification
3. Centralize error message generation (shared helper), or document why divergence is acceptable.

## Approval checklist
- [ ] Backfill notification behavior matches worker error-class policy.
- [ ] Notification payload contract matches expected fields.
- [ ] Backfill tests cover all three core branches (relevant, RateLimit, TransientNetwork).
- [ ] No regressions in existing worker tests.

## Related planning artifacts
- [Requirements](../requirements/security-price-backfill-notification-alignment.md)
- [Architecture blueprint](../architecture/security-price-backfill-notification-alignment.md)
- [ERM analysis](../architecture/security-price-backfill-notification-erm.md)
- [Planning overview](../security-price-backfill-notification-planning-overview.md)

