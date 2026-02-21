# Pattern: Inline Create & Assign for Lookup-Linked Records (Cards)

This document describes the **standard way** to implement the UX and technical flow where a user, while editing a card, can **create a new linked entity directly from a lookup field** (via an **Add/New** button) and have the **newly created record assigned immediately by the server** to the originating entity.

The goal is to make this behavior consistent across the application (Budgets, Statement Drafts, etc.).

---

## User flow (UX)

1. User opens a card that contains a lookup field (e.g. Budget Purpose ? Category).
2. In the lookup input, the user clicks the **Add/New** button.
3. App navigates to the **create card** for the linked entity.
   - The create card receives a `back` query parameter so it can return to the originating card.
   - Optionally, a `prefill` query parameter is provided to suggest an initial name.
   - Additionally, the navigation includes **parent context** so the server can assign on save:
     - `parentKind` (string)
     - `parentId` (Guid)
     - `parentField` (string, optional)
4. User fills out the linked entity and clicks **Save**.
5. The linked entity is created and **assigned server-side** to the parent.
6. App navigates back to the originating card (from `back`). No client-side assignment is required.

---

## Technical flow (architecture)

### A) Originating card: enable inline create

**Where:** The originating card view model builds a `CardField` for the lookup.

**Requirements:**
- The lookup field must set:
  - `LookupType` (e.g. `"BudgetCategory"`, `"Contact"`, ...)
  - `LookupField` (usually `"Name"`)
  - `AllowAdd = true`
  - `RecordCreationNameSuggestion` (optional)

### B) Generic UI: Add button navigation

**Where:** `GenericCardPage.razor` (lookup rendering).

**Responsibilities:**
- Render an Add/New button when `CardField.AllowAdd` is true.
- When clicked, call `OpenCreateForLookup(field)`.

**OpenCreateForLookup must:**
1. Map the `LookupType` to the correct card route.
2. Build a create URL of the form:
   - `/card/{routeKind}/new?back={...}&prefill={...}&parentKind={...}&parentId={...}&parentField={...}`

**Important:**
- `LookupType` is *not always* a valid route segment.
- Maintain an explicit mapping table for known lookup types.

### C) Create card: sending parent context to the API

**Where:** the create card’s ViewModel (e.g. `ContactCardViewModel`, `SavingsPlanCardViewModel`, `SecurityCardViewModel`, etc.)

**Responsibilities:**
- Read `parentKind/parentId/parentField` from the current URL.
- Populate the create request’s `Parent` property (`ParentLinkRequest`).
  - In this solution this is standardized via `BaseCardViewModel.TryGetParentLinkFromQuery()`.

### D) API: create + assign

**Where:** the entity’s create endpoint (controller/service)

**Responsibilities:**
- Create the entity normally.
- If `req.Parent` is present, call the central assignment service.

In this solution:
- Controllers call `IParentAssignmentService.TryAssignAsync(...)` after successful create.
- The assignment is resolved through a registry of allowed links (typed resolvers / handlers).

---

## Implementation checklist

### Originating card (ViewModel)
- [ ] Lookup field sets `lookupType`.
- [ ] Lookup field sets `lookupField` (usually `Name`).
- [ ] `allowAdd: true`.
- [ ] Optional: `recordCreationNameSuggestion`.

### Generic UI (`GenericCardPage`)
- [ ] Add button is rendered when `AllowAdd`.
- [ ] `OpenCreateForLookup` maps `LookupType` ? card route.
- [ ] Create URL includes `back` and optional `prefill`.
- [ ] Create URL includes `parentKind/parentId` (and optional `parentField`) for supported origins.

### Create card ViewModel
- [ ] On create/save, include `Parent = TryGetParentLinkFromQuery()` in the create request.

### API
- [ ] Controller calls `IParentAssignmentService.TryAssignAsync(...)` after create when `req.Parent != null`.
- [ ] `ParentAssignmentService` has a registered handler for this parent/child combination.
- [ ] Ownership/authorization is verified server-side before the assignment is applied.

---

## Error handling & edge cases

- If no valid `parentKind/parentId` is present: behave like a normal create.
- If parent assignment is not supported for the given combination: ignore and still return created entity.
- Never allow assignments across users: ownership must be validated in the handler.

---

## Testing guidance

- Unit/Service tests:
  - A registered handler assigns the created entity to the parent.
  - Ownership is enforced.

- Integration/UI flow tests (happy path):
  1. Open origin card
  2. Click Add in lookup
  3. Create new linked record
  4. Return to origin
  5. Verify server-side assignment is visible after reload
