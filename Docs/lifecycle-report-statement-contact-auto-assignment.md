# Lifecycle Report: Statement Contact Auto-Assignment

## Planned

- Requirements: [statement-contact-auto-assignment-requirements](./requirements/statement-contact-auto-assignment-requirements.md)
- Architecture blueprint: [architecture-blueprint-statement-contact-auto-assignment](./architecture/architecture-blueprint-statement-contact-auto-assignment.md)
- ER model: [entity-relationship-model-statement-contact-auto-assignment](./architecture/entity-relationship-model-statement-contact-auto-assignment.md)
- Architecture review: [review-architecture-statement-contact-auto-assignment](./improvements/review-architecture-statement-contact-auto-assignment.md)
- Planning summary: [planning-statement-contact-auto-assignment](./planning/planning-statement-contact-auto-assignment.md)

## Implemented

- `ContactsController.CreateAsync` now performs mandatory parent assignment via `TryAssignAsync(..., "contacts")`.
- Parent-assignment failures return a consistent `409 Conflict` response using `Err_Conflict_ParentAssignment`.
- Compensating rollback was added: if assignment fails, the newly created contact is deleted.
- `ParentAssignmentService` now contains an idempotency guard for repeated assignment attempts on the same contact.
- Localized controller messages were updated (`Controller.en.resx`, `Controller.de.resx`).

## Tests Added

- `FinanceManager.Tests.Integration/ApiClient/ApiClientContactsTests.cs`
  - Happy path and assignment-failure contract assertions.
- `FinanceManager.Tests/Infrastructure/ParentAssignmentServiceTests.cs`
  - Guard checks, ownership/existence cases, and idempotent no-op behavior.
- `FinanceManager.Tests/Controllers/ContactsControllerTests.cs`
  - Conflict + rollback path and localization fallback behavior.

## Documentation Updated

- `docs/api/ContactsController.md`
- `docs/api/PUBLIC_API.md`
- `docs/business/features/F012-kontakte.md`
- `docs/flows/contact-create-auto-assign.md`
- `docs/flows/README.md`
- `docs/documentation-plan.md`
- `README.md`

## Open Points / Notes

- Full solution test execution remains blocked by unrelated baseline issues in `FinanceManager.Tests` (`BudgetReportService*Tests`, missing `IsReversed` parameter).
- The feature-specific implementation and targeted integration tests are complete.
