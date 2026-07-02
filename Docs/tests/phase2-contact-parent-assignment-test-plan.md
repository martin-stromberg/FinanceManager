# Testplan: Phase 2 – Contact Creation + Parent Assignment

> **Basis:** `docs/tests/phase2-contact-parent-assignment-coverage-gaps.md`  
> **Stand:** 2026-07-01  
> **Ziel:** Hochrisiko-Lücken mit minimal redundanten Tests schließen (Datenintegrität, Ownership, API-Contract)

---

## 1) Scope und Priorität

**Im Scope**
- `ContactsController.CreateAsync` (Assignment + Rollback + Conflict-Contract)
- `ParentAssignmentService.TryAssignAsync` und `AssignContactToStatementDraftEntryAsync`
- API-Client-Contract für Conflict-Code **und** Message

**Nicht im Scope**
- Neue Feature-Implementierung (nur Testabdeckung)
- Low-Risk-Duplikate bereits indirekt abgedeckter Happy-Path-Szenarien

---

## 2) Konkreter Umsetzungsplan (nicht-redundant)

## Prio 1 – Datenintegrität & Security (zuerst umsetzen)

### A. Neue Unit-Tests für `ParentAssignmentService` (neu)
**Datei:** `FinanceManager.Tests/Infrastructure/ParentAssignmentServiceTests.cs`

1. `TryAssignAsync_ShouldReturnFalse_WhenParentIsNull`
2. `TryAssignAsync_ShouldReturnFalse_WhenParentKindMissingOrParentIdEmpty` *(Theory mit 2 InlineData-Fällen)*
3. `TryAssignAsync_ShouldReturnFalse_WhenCreatedKindMissingOrCreatedIdEmpty` *(Theory mit 2 InlineData-Fällen)*
4. `TryAssignAsync_ShouldReturnFalse_WhenHandlerIsNotRegistered`
5. `AssignContactToStatementDraftEntryAsync_ShouldReturnFalse_WhenCreatedContactNotOwnedOrMissing`
6. `AssignContactToStatementDraftEntryAsync_ShouldReturnFalse_WhenDraftOwnershipCheckFails`
7. `AssignContactToStatementDraftEntryAsync_ShouldReturnTrueWithoutRewrite_WhenContactAlreadyAssigned` *(idempotenter No-Op; SaveChanges nicht erneut für Feldänderung)*

**Warum Prio 1:** Diese Fälle schützen vor unzulässiger Verknüpfung und stiller Datenkorruption bei Inline-Erstellung.

---

### B. Neue Controller-Unit-Tests für Rollback-/Fallback-Contract (neu)
**Datei:** `FinanceManager.Tests/Controllers/ContactsControllerTests.cs`

8. `CreateAsync_ShouldReturnConflict_WhenParentAssignmentFails_AndRollbackDeleteFails`
   - Arrange: `_contacts.CreateAsync` liefert Contact, `_parentAssign.TryAssignAsync` => `false`, `_contacts.DeleteAsync` => `false`
   - Assert: `ConflictObjectResult` mit `Error = "Err_Conflict_ParentAssignment"`; Pfad mit `RollbackSucceeded=false` ist abgedeckt.

9. `CreateAsync_ShouldUseFallbackConflictMessage_WhenLocalizedResourceMissing`
   - Arrange: Localizer gibt `ResourceNotFound=true` für `API_Contacts_Err_Conflict_ParentAssignment`
   - Assert: Message = `"Contact creation could not be completed because assignment to the requested entry failed."`

**Warum Prio 1:** Fehlervertrag und Rollback-Verhalten sind API-kritisch und regressionsanfällig.

---

## Prio 2 – Integrations-Contract vervollständigen

### C. Integrationstest erweitern (bestehende Datei)
**Datei:** `FinanceManager.Tests.Integration/ApiClient/ApiClientContactsTests.cs`

10. Bestehenden Test `Contacts_Create_WithInvalidParent_ShouldReturnConflictAndRollbackContactCreate` erweitern:
   - zusätzlich prüfen:
   - `api.LastErrorCode == "Err_Conflict_ParentAssignment"` *(bereits vorhanden, beibehalten)*
   - `api.LastError` ist nicht leer
   - `api.LastError` enthält die erwartete Conflict-Message (lokalisierter oder Fallback-Text, robust mit `Contain(...)`)

**Warum Prio 2:** Schließt Contract-Lücke ohne neuen schweren End-to-End-Test.

---

## 3) Reihenfolge (empfohlen)

1. `ParentAssignmentServiceTests` (A1–A7)  
2. `ContactsControllerTests` (B8–B9)  
3. `ApiClientContactsTests` erweitern (C10)  
4. Gezielt ausführen, dann Full-Run

---

## 4) Minimaler Redundanz-Ansatz

- Guard-Varianten als **Theory** statt separater Einzeltests.
- Ownership-/Missing-Fälle in je **einem** gezielten Test zusammenfassen, wenn derselbe Rückgabepfad (`false`) geprüft wird.
- Integration nur für den extern sichtbaren Fehlervertrag; interne Branches primär in Unit-Tests.

---

## 5) Ausführung/Verifikation

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~ParentAssignmentServiceTests|FullyQualifiedName~ContactsControllerTests"
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter "FullyQualifiedName~ApiClientContactsTests"
dotnet test FinanceManager.sln
```

---

## 6) Definition of Done

- Alle 10 oben genannten Testfälle sind implementiert (inkl. Theory-Fälle).
- Alle Tests grün in beiden Testprojekten.
- Die in der Gap-Liste genannten Lücken für Contact-Create + Parent-Assignment sind für die oben priorisierten Hochrisiko-Pfade geschlossen.
