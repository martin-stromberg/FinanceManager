# Split / UploadGroup Flow

Describes how parent/child split drafts and upload groups are handled.

Key points:
- Parent draft with intermediary contact can be linked to child/children drafts via SplitDraftId.
- UploadGroupId groups drafts from same multi-part upload; linking may be performed across group members.
- Booking parent will book grouped children and create parent zero postings referencing child postings as parent.
