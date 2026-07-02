# AttachmentsController

Path: `FinanceManager.Web.Controllers.AttachmentsController`

Purpose:
- Upload, list, download and manage owner-scoped attachments (binary and URL-based).

## Key endpoints

- `POST /api/attachments` - upload attachment (`multipart/form-data`)
- `GET /api/attachments/{entityKind}/{entityId}` - paged list by parent entity
- `POST /api/attachments/{id}/download-token` - create short-lived token
- `GET /api/attachments/{id}/download` - download binary attachment
- `DELETE /api/attachments/{id}` - delete attachment

## SQLite download issue and mitigation

### Observed error mechanism
- In production, attachment download occasionally failed with SQLite errors in the area of:
  - active statements / reader overlap during concurrent operations
  - collation-sequence conflicts (`SqliteErrorCode = 5`, message contains `collation sequence`)
- Symptom: sporadic failure path in attachment retrieval despite valid ownership and attachment id.

### Implemented technical mitigation
- `AttachmentService.DownloadAsync(...)` now uses an isolated read path via `IDbContextFactory<AppDbContext>` (fresh context per read).
- On SQLite collation conflict (`SqliteException`, error code 5 + `collation sequence`), the service logs a warning and retries the download via the isolated path.
- `AddInfrastructure(...)` registers `AddDbContextFactory<AppDbContext>(...)`, so the mitigation is available in the standard DI setup.

### Effect on API contract
- No endpoint shape changed (`GET /api/attachments/{id}/download` unchanged).
- Improvement is runtime stability and reduced transient download failures under concurrent load.

## Notes

- All operations are owner-scoped and authorization-protected.
- Attachments can be reassigned or referenced across entities without blob duplication.
