# Review – Teil A: Domain-/Persistenz-Fundament

## Status: umgesetzt, bereit für Commit/Push

### Geliefert
- `FinanceManager.Domain/Mcp/`:
  - `McpAccess.cs` — Aggregate mit Lebenszyklus-Methoden (`RotateToken`, `Activate`,
    `Deactivate`, `Revoke`, `RecordUse`, `IsCurrentlyUsable`, `IsExpired`,
    `ReplaceIndividualScopes`, `Rename`, `Update`) und `McpAccessBackupDto`
    (ohne `TokenHash`/`AccessKeyHint`, `ToBackupDto` → `Status = Revoked`).
  - `McpAccessScope.cs` — Scope-Tupel `DataType`×`Right` + 5 Filter-CSVs, FK `McpAccessId`,
    denormalisiertes `OwnerUserId`, `McpAccessScopeBackupDto`.
  - `McpAuditEntry.cs` — append-only, `SequenceNo`, `PrevHash`/`EntryHash`,
    `Changes`-Navigation + `AddChange`, `McpAuditEntryBackupDto`.
  - `McpAuditEntryChange.cs` — Feld-Deltas, `McpAuditEntryChangeBackupDto`.
  - 6 Enum-Dateien mit normativen Wertelisten (datenmodell §2.2).
- `AppDbContext`: 4 DbSets + Konfiguration (Unique `AccessKeyHint`, `(OwnerUserId,Name)`,
  `(OwnerUserId,SequenceNo)`; Indizes exakt datenmodell §7; Cascade Scopes/Changes;
  Enum→`short`).
- EF-Migration `20261104174706_AddMcpFoundation` (4 Tabellen, 14 Indizes); verifiziert via
  `dotnet ef database update` auf temporärer SQLite-DB.
- `EnumCoverageTests`: 6 neue `[Fact]`; `enum-coverage-check.py --all --strict` OK.
- Lifecycle-Artefakte in diesem Verzeichnis.

### Abweichungen/Entscheidungen
- `McpAccessScope.OwnerUserId`: denormalisiert hinzugefügt (Lieferumfang Vorgabe); erleichtert
  Owner-scoped Queries und `ClearUserDataAsync`-Muster; kein Konflikt zum datenmodell
  (Besitz weiterhin über `McpAccessId` abgeleitet).
- `RotateToken` reaktiviert einen widerrufenen Zugang bewusst (Status→`Active`,
  Revokationsdaten geleert) — das ist der normative Reaktivierungspfad nach Backup-Restore
  (datenmodell §10). `Activate`/`Deactivate` verweigern bei `Revoked`.
- `McpAuditEntry`/`McpAccess` wurden als `IAggregateRoot` markiert; Scopes/Changes sind
  Kind-Entitäten (Cascade, kein eigener Aggregate-Status) — wie im datenmodell festgelegt.
- Unique `(OwnerUserId, SequenceNo)`: unter SQLite erlaubt `NULL`-Owner weiterhin Duplikate;
  die globale Kette wird applikatorisch abgesichert (datenmodell §6.1, dokumentierte
  Einschränkung — Teil B muss dies beim Audit-Writer beachten).

## Risiken / offene Punkte für Teil B
1. **Backup-Service-Registrierung fehlt bewusst** — `IBackupService`/`BackupService` ist noch
   nicht erweitert (war nicht Teil dieses Laufs). Teil B muss Export (Accesses, Scopes, Audit
   inkl. Changes) + Restore einbauen. Restore braucht Platzhalter-`TokenHash`/`AccessKeyHint`
   (required, unique) — Empfehlung: zufällige, nie ausgegebene Werte generieren oder direkt
   rotieren; `AssignBackupDto` setzt `Status=Revoked` zwangsweise.
2. **`AccessKeyHint`-Unique bei Restore:** Bestands-Id/Hinweis könnten kollidieren — der
   Platzhalter-Hint muss global eindeutig sein (z. B. aus neuer Id ableiten).
3. **Audit-Writer (F-0301/F-0302):** Hash-Ketten-Kanonisierung, `SequenceNo`-Vergabe je Kette
   (Owner/global) und das NULL-Owner-Unique-Problem sind Service-Logik — hier ist Sorgfalt
   nötig (Transaktion, monotone Vergabe unter Nebenläufigkeit).
4. **`Mcp:Enabled`-Flag:** noch nicht angebunden — `appsettings`-Default `false` in Teil B
   setzen, damit Migration ≠ Aktivierung bleibt.
5. **`ClearUserDataAsync`:** neue Tabellen sind dort noch nicht berücksichtigt (Löschung
   Benutzerdaten) — Teil B ergänzen (Scopes/Audit via OwnerUserId bzw. Cascade).
6. **Ältere EF-Tools-Version** (10.0.10 vs. Runtime 10.0.14): nur Warnung, funktional ok.
7. **Enum-Coverage-Tests** prüfen nur Namen — Wertebelegung (0..n) wird vom normativen
   Dokument sichergestellt; Änderungen an Enum-Werten sind konzeptpflichtig.
