# Testergebnisse — Schritt 4 (#578): Granularer Genehmigungsmodus

## Ausgeführte Tests

- `dotnet build FinanceManager.sln` — 0 Fehler (nur bekannte Warnungen).
- `dotnet test FinanceManager.Tests` — **2511/2511 bestanden**.
- Pre-Commit-Checks (gestaged): Translation, xmldoc, razor-l10n, stub, enum-coverage, U+FFFD — alle grün.

## Neue Tests (21)

- Gate-Matrix (6): Klasse × Modus × Grant — Granular + Routine-Op + Grant → direkt;
  unbekannt/fehlender Grant → RequiresApproval; Genehmigungspflichtig queued stets.
- Approval-Service (7): Enqueue-Klassifizierung (`PreApprovalKind` Access/Grant/None),
  Withdrawal-Matrix bei Grant-Entzug (nur offene GrantPreApproved-Requests),
  unbekannte Aktionen fail-closed, Backup-Roundtrip.
- Access-Service (8): Grant-CRUD, Validierung (grantbare Rechte, Administration nur
  Admins, Duplikate), Owner-Isolation, Audit-Delta, End-to-End-Withdrawal.

## Abgedeckte Akzeptanzfälle

- `Granular` + Grant `(Administration, Read)` → `fm_admin_updates:status/check`,
  `fm_admin_backups:list/download`, `fm_admin_notifications:list`,
  `fm_admin_settings:get` laufen direkt; Schreib-/Löschoperationen bleiben Pending.
- Grant `(Administration, Modify)` → `dismiss` auto-Approved (dokumentiert).
- Genehmigungspflichtige Ops mit Grant erzeugen Approved-Queue-Eintrag (Audit-Instanz).
- Multi-Datentyp-Tools bewusst unmapped → bleiben genehmigungspflichtig (fail-closed).
- UI: Wizard „Expertenmodus", Grant-Matrix (nur grantbare Zellen), kritische
  Bestätigung bei riskanten Grants — de/en resx.
