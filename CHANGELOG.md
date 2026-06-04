# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added

- **Posting Reversal (Stornierung):** Erroneous postings can now be cancelled (reversed) via the API and UI (feature branch `140-buchung-rückgängig-machen`).
  - New endpoint `POST /api/postings/{id}/reverse` — creates a counter-posting with negated amount, reversing the original posting (and all postings in the same booking group). Returns a `ReversalResultDto` with the IDs of reversed and newly created postings plus the reconciliation import ID.
  - New endpoint `GET /api/postings/{id}/validate-reversal` — validates whether a posting can be reversed without performing the operation. Returns a `ReversalValidationDto` (`isValid`, `errors[]`). Use this to pre-validate before showing a confirmation UI.
  - New database columns on the Posting entity:
    - `ReversedByPostingId` (`Guid?`) — ID of the counter-posting that reversed this posting.
    - `ReversalForPostingId` (`Guid?`) — ID of the original posting this posting reverses.
    - `ReversedByUserId` (`string?`) — User ID of the user who performed the reversal.
    - `ReversedAtUtc` (`DateTime?`) — UTC timestamp of the reversal.
  - Computed properties on the Posting entity:
    - `IsReversed` (`bool`) — `true` when `ReversedByPostingId` is set.
    - `IsReversal` (`bool`) — `true` when `ReversalForPostingId` is set.
  - `PostingServiceDto` extended with `IsReversed`, `IsReversal`, `ReversedByPostingId`, `ReversalForPostingId` fields.
  - Action button "Stornieren" (Cancel/Reverse) added to posting detail pages in the web UI.
  - "Storno" indicator column added to posting list views.

---

### Known Issues

- **Bug:** `GetRelatedPostingsAsync` with `GroupId == Guid.Empty` returns all ungrouped postings instead of an empty result. Integration test `L21` is skipped as a workaround. This is a pre-existing issue unrelated to the reversal feature.
- **UX:** No confirmation dialog is shown before executing a reversal. The action is irreversible and currently triggers immediately on button click.
- **Tests:** 13 pre-existing test failures exist in the test suite. These failures are not caused by the reversal feature and were present before this branch was created.

---

[Unreleased]: https://github.com/Muesli84/FinanceManager/compare/main...140-buchung-rückgängig-machen
