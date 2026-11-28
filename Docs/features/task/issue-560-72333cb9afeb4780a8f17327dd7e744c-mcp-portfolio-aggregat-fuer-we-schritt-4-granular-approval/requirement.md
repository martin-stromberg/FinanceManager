# Anforderung — Schritt 4 (#578): Granularer Genehmigungsmodus (Expertenmodus)

Quelle: Issue #578 + Projektplan Schritt 4.

- `McpApprovalMode.Granular` (opt-in, Default unverändert) + `McpAccessApprovalGrant` (Id, McpAccessId, OwnerUserId, DataType, Right; freigebbar: Read/Complete/Modify/Create; Suggest/Book/Delete nicht; Administration nur Admin-Besitzer).
- `McpApprovalActionCatalog` (Application): (tool, actionKey) → Profil; fm_admin_* vollständig; unbekannt → konservativ.
- Gate/Enqueue: Routine + Grant → Direct/Approved; Endwirkungen bleiben Pending. `AiActionRequest.PreApprovalKind` (None/AccessPreApproved/GrantPreApproved) als Diskriminator; Grant-Entzug → Withdrawal nur grant-preapproved Requests.
- API/UI: Grants in McpAccessDetailDto + Update-API + Audit-Event; UI „Expertenmodus"-Radio + Grant-Matrix (de/en resx, kritische Bestätigung für riskante Grants).
- Migration (Grants-Tabelle + PreApprovalKind) + Backup (Grants-Default + optionale Sektion).
