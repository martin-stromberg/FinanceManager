# Logik — Erzeuger, Konsumenten und EF-Konfiguration

## `Posting`-Erzeuger (Produktivcode, `FinanceManager.Infrastructure`)

Vollständige Liste aller `new Posting(`-Stellen im Produktivcode (Suche über `new Posting(`, `Domain.Postings.Posting(`, `FinanceManager.Domain.Postings.Posting(`; `FinanceManager.Application` und `FinanceManager.Web` enthalten keine Erzeuger):

### `StatementDraftService` — 12 Erzeugerstellen

Datei: `FinanceManager.Infrastructure/Statements/StatementDraftService.cs` (partial class, 3101 Zeilen)

| Zeile | Kind | Gesetzte Referenz | Vorbedingung |
|-------|------|-------------------|--------------|
| 2438 | `Bank` | `account.Id` | Konto zuvor geladen (nicht-null) |
| 2440 | `Contact` | `e.ContactId` | `if (e.ContactId == null) continue` (Z. 2488) |
| 2472 | `Security` | `e.SecurityId` | `if (e.SecurityId == null) return` (Z. 2464) |
| 2476 | `Security` (Fee) | `e.SecurityId` | dto., `if (fee != 0m)` |
| 2481 | `Security` (Tax) | `e.SecurityId` | dto., `if (tax != 0m)` |
| 2524 | `SavingsPlan` | `e.SavingsPlanId` | `if (e.SavingsPlanId != null && e.ContactId == self.Id)` (Z. 2522) |
| 2774 | `Bank` | `account.Id` | (Split-Buchung, `CreateBankAndContactAsync`) |
| 2776 | `Contact` | `ce.ContactId` | `if (ce.ContactId == null) continue` (Z. 2767) |
| 2812 | `Security` | `e.SecurityId` | `if (e.SecurityId == null) return` (Z. 2805) |
| 2816 | `Security` (Fee) | `e.SecurityId` | dto. |
| 2821 | `Security` (Tax) | `e.SecurityId` | dto. |
| 2854 | `SavingsPlan` | `ce.SavingsPlanId` | `if (ce.SavingsPlanId != null && ce.ContactId == self.Id)` (Z. 2852) |

Alle Stellen setzen genau eine Referenz — **alle durch lokale Guards abgesichert, nie referenzlos**.

### `StatementDraftService.Preliminary` — 1 Erzeugerstelle

Datei: `FinanceManager.Infrastructure/Statements/StatementDraftService.Preliminary.cs` (108 Zeilen)

- `ReversePreliminaryPostingsAsync` (Z. 62–107), Erzeuger Z. 81–97: Storno-Buchung mit `amount: 0m`, kopiert alle vier Referenzen transitiv vom Original (`accountId: original.AccountId` …). Transitiv sicher, solange das Original ≥ 1 Referenz trägt.

### `PostingReversalService` — 1 Erzeugerstelle

Datei: `FinanceManager.Infrastructure/Postings/PostingReversalService.cs` (355 Zeilen)

- `CreateReversalPosting` (Z. 216–…, `new Posting(` Z. 220): kopiert alle vier Referenzen transitiv vom Original, `amount: -original.Amount`. Transitiv sicher wie oben.
- Aufgerufen aus `ReversePostingAsync` (Z. 48–…) in Transaktion mit `IsolationLevel.ReadCommitted`; danach `IPostingAggregateService.UpsertForPostingAsync` je Storno (Z. 105–108).

### `SavingsPlanService` — 1 Erzeugerstelle

Datei: `FinanceManager.Infrastructure/Savings/SavingsPlanService.cs`

- „Istmeldung"-Korrekturbuchung (Z. 646–…): `new Posting(id /* sourceId = Plan-Id */, PostingKind.SavingsPlan, null, null, id /* savingsPlanId */, null, now, now, difference, "Istmeldung", …)` — setzt `SavingsPlanId` auf die Plan-ID. Vorbedingung Z. 634: nur Pläne ohne verknüpfte Konten.

### `SetupImportService` — 1 Erzeugerstelle (Auffälligkeit)

Datei: `FinanceManager.Infrastructure/Setup\SetupImportService.cs` (1586 Zeilen)

- Restore-Pfad „Postings" (Z. 711–758), `new FinanceManager.Domain.Postings.Posting(` Z. 727–740. Die vier Referenzen werden einzeln über ID-Maps remappt (Z. 723–726):
  `Guid? accountId = dto.AccountId.HasValue && accountMap.TryGetValue(dto.AccountId.Value, out var a) ? a : null;` (analog `contactMap`, `savingsMap`, `securityMap`).
- **Lücke:** Verweist ein Backup-Posting auf ein Ziel, das nicht im Backup exportiert wurde (oder beim Merge-Import nicht gemappt ist), werden alle vier Referenzen `null` → **referenzloses Posting entsteht beim Restore**. Einzige gefundene Erzeugerstelle ohne strukturelle Referenzgarantie. Der reguläre Export (`BackupService`, s. u.) filtert referenzlose Postings ohnehin heraus — die Lücke betrifft manipulierte/inkonsistente Backups.
- Nach dem Konstruktorlauf werden `SetGroup` (Z. 741) und `SetSavingsPlanSubType` (Z. 742) aufgerufen — ein Guard im Hauptkonstruktor würde den Restore-Pfad ebenfalls erfassen (dann `ArgumentException` statt stillem Waisen-Import).
- `AssignBackupDto` wird im selben Service für Postings **doch** verwendet — im zweiten Durchlauf („apply"-Block Z. 1308–1332) iteriert `ImportVersion3` die `Postings`-Sektion erneut, remappt die vier Referenzen mit denselben Ausdrücken wie im Create-Block (Z. 1316–1319 = Z. 723–726) und ruft `entity.AssignBackupDto(adjusted)` auf (Z. 1325), sofern `postingMap.TryGetValue(dto.Id)` (Z. 1322) einen Treffer liefert. `postingMap` wird ausschließlich im Create-Block befüllt (Z. 748–749/755–756) — ein dort verworfenes/nicht angelegtes Posting erreicht den Apply-Pfad nicht. Dieser Pfad umgeht einen Konstruktor-Guard; eine Vier-`null`-Konstellation kann er jedoch nur erzeugen, wenn der Create-Block zuvor eine solche zugelassen hätte (identisches Remapping). Für andere Entitäten kommt `AssignBackupDto` u. a. an Z. 495/527/563/791/885/935/967/972/991/1106–1437 zum Einsatz.

## `PostingAggregate`-Erzeuger (Produktivcode)

### `PostingAggregateService`

Datei: `FinanceManager.Infrastructure/Aggregates/PostingAggregateService.cs` (**Encoding: Windows-1252, nicht UTF-8**)

| Methode | Zeilen | Kurzbeschreibung |
|---------|--------|------------------|
| `UpsertForPostingAsync` | 45–117 | Upsert je Periode×DateKind; `switch (posting.Kind)` (Z. 95–116) übergibt pro Kind genau eine Referenz (`Bank`→`AccountId`, `Contact`→`ContactId`, `SavingsPlan`→`SavingsPlanId`, `Security`→`SecurityId`+`SecuritySubType`); `new PostingAggregate(` Z. 89. **Lücke:** ein Posting mit `Kind=Bank` aber `AccountId=null` erzeugt ein Aggregat mit allen vier Referenzen `null`. |
| `RebuildForUserAsync` | ~119–230 | Löscht Nutzer-Aggregate (Filter „mindestens eine Referenz im Nutzerscope", Z. ~134–140), lädt Postings mit demselben OR-Filter (Z. ~144–150), verdichtet in `sums`-Dictionary und erzeugt Batch-Aggregate (`new PostingAggregate(` Z. 201) — Schlüssel kommt aus tatsächlichen Posting-Referenzen, daher ≥ 1 gesetzt, solange die Quell-Postings referenziert sind. |
| `GetPeriodStart` (private static) | 27–40 | Periodenbeginn (Month/Quarter/HalfYear/Year) |

Achtung: Datei verwendet teils deutsche Kommentare; beim Lesen mit UTF-8-Tools „�"-Artefakte.

### `SetupImportService` — Aggregat-Rebuild

- `RebuildAggregatesAsync`-ähnlicher Block (Z. 1489–1579): löscht Aggregatzeilen im Nutzerscope (Z. 1493–1498), lädt Postings mit OR-Referenzfilter (Z. 1502–1506), `switch (p.Kind)`-Upsert Z. 1558–1574, `new PostingAggregate(` Z. 1552. **Dieselbe Lücke:** `Kind=Bank` + `AccountId=null` → Aggregat mit allen Referenzen `null`.

## Konsumenten des Referenz-Filters (kein Erzeuger)

### `BackupService`

Datei: `FinanceManager.Infrastructure/Backups/BackupService.cs` (853 Zeilen)

- `BuildBackupDataAsync` — Posting-Export Z. 644–652: `Where(p => (p.AccountId != null && importAccountIds.Contains(...)) || (p.ContactId != null && contactIds.Contains(...)) || (p.SavingsPlanId != null && savingsPlanIds.Contains(...)) || (p.SecurityId != null && securityIds.Contains(...)))` — **ein Posting mit vier `null`-Referenzen wird niemals exportiert** (Waisen-Problem aus dem Issue bestätigt).

### `AppDbContext`

Datei: `FinanceManager.Infrastructure/AppDbContext.cs` (1155 Zeilen)

- `DbSet<Posting> Postings` Z. 116; `DbSet<PostingAggregate> PostingAggregates` Z. 184; `DbSet<IpBlock> IpBlocks` Z. 247; `DbSet<SecurityTxtSettings>` Z. 256; `DbSet<WellKnownSettings>` Z. 265; `DbSet<McpSettings>` Z. 390.
- `modelBuilder.Entity<Posting>` (Z. 513–519): nur Indizes `{AccountId, BookingDate}`, `{SecurityId, BookingDate}` sowie `ParentId`-/`OriginalAmount`-Präzision — **kein Check-Constraint, kein `ToTable`-Block** (`HasCheckConstraint` kommt im gesamten Repo nicht vor).
- `modelBuilder.Entity<PostingAggregate>` (Z. 633–653): breiter Unique-Index über alle Schlüsselspalten (Z. 640) + vier gefilterte Unique-Indizes mit `HasFilter("[X] IS NOT NULL AND [übrige] IS NULL")` (Z. 641–652) — decken jeweils den „genau eine Referenz"-Fall ab.
- `modelBuilder.Entity<IpBlock>` (Z. 751–756): Unique-Index `IpAddress`.
- `ClearUserDataAsync` (Postings Z. 1029–1045, PostingAggregates Z. 1011–1027): vier `ExecuteDeleteAsync`-Blöcke pro Dimension (`p.X != null && <Owner-Subquery>`) — **ein Posting/Aggregat mit vier `null`-Referenzen wird beim Nutzer-Reset nicht gelöscht**.

## `SetupImportService` — Import-Überblick (nur relevante Teile)

- Posting-Sektion: `TryGetProperty("Postings", …)` Z. 715 (optional → alte Backups ohne Sektion ok); Deserialisierung in `PostingBackupDto`-Liste Z. 717.
- Fortschritt über `ProgressChanged`-Event (`ProgressChanged?.Invoke(this, progress…)`, z. B. Z. 713/719/752); `postingMap`/`postingMapCollection` für ID-Remapping (Z. 714, 744–757).

## Events

- `SetupImportService.ProgressChanged` — publiziert Fortschrittsereignisse (Sub-/Hauptschritte) beim Import.
- Keine domain-Events an `Posting`/`PostingAggregate`; Änderungen laufen direkt über Services + `SaveChanges`.
