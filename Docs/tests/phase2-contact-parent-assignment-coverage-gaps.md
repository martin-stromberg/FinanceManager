# Testlückenliste – Phase 2 Kontaktanlage mit Parent-Zuordnung

## Parent-Assignment Success/Failure Contract
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::TryAssignAsync` – Rückgabe `false` bei `parent == null` ist ungetestet (Zeilen 41–43).
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::TryAssignAsync` – Rückgabe `false` bei ungültigem Parent (`ParentKind` leer/whitespace oder `ParentId == Guid.Empty`) ist ungetestet (Zeilen 46–48).
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::TryAssignAsync` – Rückgabe `false` bei ungültigem Created-Kontext (`createdKind` leer/whitespace oder `createdId == Guid.Empty`) ist ungetestet (Zeilen 51–53).
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::TryAssignAsync` – Rückgabe `false` bei nicht registriertem Handler-Key ist ungetestet (Zeilen 57–59).
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::AssignContactToStatementDraftEntryAsync` – Rückgabe `false`, wenn der erstellte Kontakt nicht (mehr) existiert/owner-fremd ist, ist ungetestet (Zeilen 103–104).
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::AssignContactToStatementDraftEntryAsync` – Rückgabe `false`, wenn Draft-Ownership-Prüfung fehlschlägt, ist ungetestet (Zeilen 120–121).

## Rollback-Verhalten bei fehlgeschlagener Assignment
- [ ] `FinanceManager.Web/Controllers/ContactsController.cs::CreateAsync` – Pfad „Assignment fehlgeschlagen + Rollback-Delete liefert `false`“ ist ungetestet (Zeilen 159–167, `RollbackSucceeded=false`).

## Idempotency / No-Op
- [ ] `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs::AssignContactToStatementDraftEntryAsync` – idempotenter No-Op bei bereits zugewiesener `ContactId` (inkl. `true`-Rückgabe ohne Re-Write) ist ungetestet (Zeilen 125–133).

## Error Contract (Conflict Code/Message)
- [ ] `FinanceManager.Tests.Integration/ApiClient/ApiClientContactsTests.cs::Contacts_Create_WithInvalidParent_ShouldReturnConflictAndRollbackContactCreate` – Contract-Prüfung der Conflict-Message (`message`/`api.LastError`) fehlt; geprüft wird nur `LastErrorCode` (Zeilen 100–119, speziell 115).
- [ ] `FinanceManager.Web/Controllers/ContactsController.cs::CreateAsync` – Fallback-Message bei fehlendem Localizer-Eintrag (`localized.ResourceNotFound`) ist ungetestet (Zeilen 169–172).
