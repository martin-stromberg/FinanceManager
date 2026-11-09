# Bestandsaufnahme: Datenbesitz — Posting ohne erzwungenen Benutzerbezug, globale Pools nur implizit dokumentiert

Bestandsaufnahme zur Anforderung Issue #497 (siehe `requirement.md`): Invariante „mindestens eine Zielreferenz" für `Posting` sowie explizite Globalitäts-Dokumentation der Entitäten `IpBlock`, `SecurityTxtSettings` und `WellKnownSettings`. Analysiert wurden die betroffenen Domänenentitäten, alle `Posting`-/`PostingAggregate`-Erzeugerstellen, die EF-Konfiguration, Backup-/Restore-Pfade und die vorhandene Dokumentation unter `Docs/`.

## Zusammenfassung

- **`Posting` besitzt aktuell keine Referenz-Invariante.** Alle fünf öffentlichen Konstruktor-Überladungen delegieren per `: this(...)` an den vollständigen Konstruktor (`Posting.cs` Z. 108–141) — ein einziger Guard dort würde alle Wege abdecken. `AssignBackupDto` (Z. 523–550) setzt die vier Referenzen dagegen direkt nach der Konstruktion und umgeht jeden Konstruktor-Guard.
- **Alle Produktiv-Erzeuger setzen bereits mindestens eine Referenz.** Geprüft wurden 17 Erzeugerstellen in `FinanceManager.Infrastructure` (12× `StatementDraftService`, 1× `StatementDraftService.Preliminary`, 1× `PostingReversalService`, 1× `SavingsPlanService`, 1× `SetupImportService`, 2× `PostingAggregateService` für `PostingAggregate`). Die Vererbungsrichtung ist durchweg „mindestens eine" — `BackupServiceFullExportTests.cs:183` erzeugt sogar ein Posting mit allen vier Referenzen.
- **Auffälligkeit — Restore-Pfad:** `SetupImportService.cs` Z. 723–740 remappt die vier Referenzen einzeln über ID-Maps (`TryGetValue` … sonst `null`). Ein Backup-Eintrag, dessen Referenzziele nicht exportiert wurden, erzeugt faktisch ein referenzloses `Posting` — die einzige real erreichbare Lücke zur Invariante (siehe Offene Frage 2 in `requirement.md`).
- **`PostingAggregate` trägt dasselbe Vier-Referenzen-Muster** ohne Guard (Scope-Ausschluss des Issues); ein referenzloses Posting würde über `PostingAggregateService.UpsertForPostingAsync` (Z. 89) bzw. `SetupImportService` (Z. 1552) zusätzlich ein völlig verwaistes Aggregat erzeugen.
- **Kein `HasCheckConstraint` im gesamten Codebestand** — ein DB-Constraint wäre neu einzuführen (Migration per `dotnet ef migrations add`, Provider SQLite).
- **Globale Entitäten ohne Globalitäts-Hinweis:** `IpBlock` (Z. 3–7), `SecurityTxtSettings` (Z. 3–6) und `WellKnownSettings` (Z. 5–8) tragen kein `OwnerUserId`, dokumentieren die Ausnahme aber nicht. Vorbild `McpSettings` (Z. 3–10) dokumentiert das Singleton-Muster, verwendet aber ebenfalls nicht wörtlich „globaler Datenpool" — nächstliegende Muster für „global" sind `Notification.OwnerUserId` („when null the notification is global", `Notification.cs` Z. 17) und `McpAuditEntry.OwnerUserId` („null for global events", `McpAuditEntry.cs` Z. 104).
- **Docs/-Fundstellen für Globalitäts-Doku:** `Docs/help/systemverwaltung-und-setup/datenmodell.md` (Z. 22–29 `IpBlock`, Z. 81–110 `SecurityTxtSettings`/`WellKnownSettings`, jeweils mit Singleton-Hinweis aber ohne „globaler Pool") und `Docs/help/mcp-server/datenmodell.md` Z. 108–115 (`McpSettings`). `Docs/help/konten-und-buchungen/datenmodell.md` Z. 29–42 dokumentiert `Posting` inkl. der vier optionalen Referenzen.
- **Encoding-Fallstrick:** `FinanceManager.Infrastructure/Aggregates/PostingAggregateService.cs` ist Windows-1252-kodiert (kein UTF-8) — bei späteren Edits beachten.

**Test-Ausgangszustand:** `dotnet build FinanceManager.sln` erfolgreich (0 Fehler, 65 Warnungen); `dotnet test FinanceManager.Tests` → **2139 bestanden, 0 fehlgeschlagen, 0 übersprungen**; `dotnet test FinanceManager.Tests.Integration` → **165 bestanden, 0 fehlgeschlagen**. E2E-Suite nicht ausführbar (Playwright-Browser nicht installiert). Nachweis und Details: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `Posting`, `PostingAggregate`, `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`, `McpSettings`, `Guards`, `Entity`/`IAggregateRoot`
- [Logik / Erzeuger & Konsumenten](inventory/logic.md) — alle `new Posting(`/`new PostingAggregate(`-Stellen, `BackupService`, `AppDbContext` (EF-Konfig + `ClearUserDataAsync`), Restore-Pfad
- [Enums](inventory/enums.md) — `PostingKind`, `SecurityPostingSubType`, `SavingsPlanPostingSubType`, `AggregatePeriod`, `AggregateDateKind`
- [Interfaces](inventory/interfaces.md) — `IAggregateRoot`, `IPostingAggregateService`, `IPostingReversalService`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, bestehende Testklassen, Hilfsmethoden, Testlücken
