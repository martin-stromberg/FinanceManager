# Planning Overview: Budget Impact Visibility during Booking

## Scope
This overview consolidates the planning artifacts for the feature request in `7c1c60b3-841d-4c4a-86b6-7a00fe6484df.copilot.task.md`.

## Linked Planning Artifacts
- Requirements: [requirements-budget-impact-booking.md](../requirements/requirements-budget-impact-booking.md)
- Architecture blueprint: [architecture-blueprint-budget-impact-booking.md](../architecture/architecture-blueprint-budget-impact-booking.md)
- ER model: [entity-relationship-model-budget-impact-booking.md](../architecture/entity-relationship-model-budget-impact-booking.md)
- Architecture review: [review-architecture-budget-impact-booking.md](../improvements/review-architecture-budget-impact-booking.md)

## End-to-End Flow
1. Analyze feature goals, scope, FR/NFR, and acceptance criteria.
2. Design architecture for evaluation triggers, shared calculation logic, and UI feedback.
3. Model entities/relations for budget-impact computation and completion summaries.
4. Review architecture quality, risks, and prioritized improvements.

## Key Cross-Document Decisions
- A server-side single calculation core should feed both immediate hints and final booking summary.
- Existing statement-draft endpoints are extended instead of introducing mandatory parallel APIs.
- Budget threshold logic should be centrally rule-driven (`BudgetRule` + impact profile strategy).

## Current Risk Focus (from review)
- Formal consistency model between immediate hint and final summary is still a blocker.
- Trigger race handling and fallback behavior need explicit implementation contracts.
- Performance target (`<500ms p95`) requires concrete query/index validation.
