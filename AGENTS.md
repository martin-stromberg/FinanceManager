# Project Rules

## Lifecycle Workflow

When a user provides an `issue.md` describing a requirement on a feature branch and references the `.agents/skills/lifecycle/SKILL.md` skill, or uses phrases such as:

- "Führe den Lifecycle aus"
- "Setze diese Anforderung komplett um"
- "Bearbeite diese Anforderung"
- "Mach mit der begonnenen Anforderung weiter"
- "Setze den bestehenden Feature-Branch fort"

Then follow the workflow defined in `.agents/skills/lifecycle/SKILL.md` and `.agents/skills/lifecycle/lifecycle.md`:

1. Determine the current branch and refuse work on `main`, `master`, `develop`, or `dev`.
2. Create or update `docs/features/{branchname}/todo.md` with the steps from `lifecycle.md`.
3. Determine the entry point based on existing artifacts under `docs/features/{branchname}/`.
4. Since haiku subagents are not available in this environment, perform the required steps directly:
   - Translate the requirement to `requirement.md`.
   - Perform inventory and create `inventory.md`.
   - Create a plan in `plan.md`.
   - Implement the changes, run tests, and fix issues.
   - Update `Docs/help/` and `README.md` as appropriate.
   - Commit at the end.
5. Mark completed steps in `todo.md` as you go.
6. Delete `docs/features/{branchname}/` only when all steps are complete and changes are committed.

## UI- and E2E-Test Enforcement

For every feature that exposes a user-facing UI flow (ribbons, buttons, navigation, focus):

1. In `plan.md`, list one or more concrete E2E or UI-level tests for the exact user interaction described in the requirement.
2. Do not consider the implementation complete until those tests exist and pass successfully.
3. During code review, verify that every `RaiseUiActionRequested(...)` call in a ViewModel has a matching handler in the corresponding Blazor page/component.
4. Treat missing or failing E2E coverage for a UI flow as a blocker for the final commit.
