# Architektur-Blueprint: Posting-Stornierung (Reversal)

**Status:** Draft  
**Version:** 1.0  
**Datum:** 2025-01-27  
**Autor:** Architecture Team  
**Requirements-Referenz:** [FA-POST-001_Posting_Reversal.md](../requirements/FA-POST-001_Posting_Reversal.md)

## 1. Zielbild und Scope

### 1.1 Geschäftliches Ziel
Benutzer sollen fehlerhafte Buchungen durch Erstellung von Gegen-Buchungen stornieren können, wobei volle Nachvollziehbarkeit und referentielle Integrität gewährleistet werden.

### 1.2 In Scope
- Stornierung einzelner Buchungen über UI-Aktion
- Gruppenstornierung (alle Buchungen einer Gruppe)
- Validierung (keine Stornierung bereits stornierter oder übergeordneter Buchungen)
- Bidirektionale Markierung (Original ↔ Stornierung)
- Erstellung von StatementImport-Einträgen für Stornierungsbuchungen
- Berechtigungsprüfung (nur eigene Buchungen)
- Transaktionale Atomarität aller Operationen

### 1.3 Out of Scope
- Partielle Gruppenstornierungen
- Kaskadierende Stornierung von Kind-Buchungen
- Stornierung von Stornierungen (keine Mehrfachstornierung)
- Automatische Budget-Neu-Berechnung (erfolgt über bestehende Aggregate-Services)

## 2. Systemarchitektur

```mermaid
graph TB
    subgraph Web ["Web Layer"]
        UI[Blazor Components<br/>Posting Detail Pages]
        CTRL[PostingsController<br/>POST /api/postings/{id}/reverse]
    end
    
    subgraph Application ["Application Layer"]
        IREV[IPostingReversalService]
        IAGG[IPostingAggregateService]
    end
    
    subgraph Infrastructure ["Infrastructure Layer"]
        REV[PostingReversalService]
        AGG[PostingAggregateService]
        STMT[StatementImportService]
        REPO[Repositories]
    end
    
    subgraph Domain ["Domain Layer"]
        POST[Posting Entity<br/>+ ReversedByPostingId<br/>+ ReversalForPostingId]
    end
    
    subgraph Database ["Database Layer"]
        DB[(SQLite<br/>Postings Table<br/>+ New Columns)]
    end
    
    UI -->|Action: Reverse| CTRL
    CTRL --> IREV
    REV --> POST
    REV --> IAGG
    REV --> STMT
    REV --> REPO
    REPO --> DB
    AGG --> DB
```

## 3. Komponenten und Schnittstellen

### 3.1 Domain Layer: Posting Entity Extension

**Neue Properties:**
```csharp
public Guid? ReversedByPostingId { get; private set; }
public Posting? ReversedByPosting { get; private set; }

public Guid? ReversalForPostingId { get; private set; }
public Posting? ReversalForPosting { get; private set; }
```

**Neue Methoden:**
```csharp
public void SetReversedBy(Posting reversalPosting)
{
    ReversedByPostingId = reversalPosting.Id;
    ReversedByPosting = reversalPosting;
}

public void SetReversalFor(Posting originalPosting)
{
    ReversalForPostingId = originalPosting.Id;
    ReversalForPosting = originalPosting;
}

public bool IsReversed => ReversedByPostingId.HasValue;
public bool IsReversal => ReversalForPostingId.HasValue;
```

### 3.2 Application Layer: IPostingReversalService

```csharp
public interface IPostingReversalService
{
    /// <summary>
    /// Reverses a posting or posting group by creating counter-postings.
    /// </summary>
    /// <param name="postingId">ID of the posting to reverse</param>
    /// <param name="userId">ID of the current user (for authorization)</param>
    /// <returns>Result containing IDs of reversed and created postings</returns>
    /// <exception cref="UnauthorizedAccessException">User doesn't own the posting</exception>
    /// <exception cref="InvalidOperationException">Posting cannot be reversed</exception>
    Task<ReversalResult> ReversePostingAsync(Guid postingId, Guid userId);
    
    /// <summary>
    /// Checks if a posting can be reversed.
    /// </summary>
    Task<ValidationResult> CanReverseAsync(Guid postingId, Guid userId);
    
    /// <summary>
    /// Gets all postings that would be reversed together (group members).
    /// </summary>
    Task<IReadOnlyList<Posting>> GetRelatedPostingsAsync(Guid postingId);
}

public record ReversalResult(
    IReadOnlyList<Guid> ReversedPostingIds,
    IReadOnlyList<Guid> CreatedReversalIds,
    Guid? StatementImportId
);

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);
```

### 3.3 Web Layer: API Endpoint

**Route:** `POST /api/postings/{id}/reverse`

**Request:** Empty body (postingId from route)

**Response (Success - 200 OK):**
```json
{
  "reversedPostingIds": ["guid1", "guid2"],
  "createdReversalIds": ["guid3", "guid4"],
  "statementImportId": "guid5"
}
```

**Response (Validation Error - 400 Bad Request):**
```json
{
  "errors": [
    "Posting has already been reversed",
    "Cannot reverse posting with child postings"
  ]
}
```

**Response (Authorization Error - 403 Forbidden):**
```json
{
  "error": "You are not authorized to reverse this posting"
}
```

### 3.4 DTOs (Shared Layer)

```csharp
public record ReversalResultDto(
    IReadOnlyList<Guid> ReversedPostingIds,
    IReadOnlyList<Guid> CreatedReversalIds,
    Guid? StatementImportId
);

public record ReversalValidationDto(
    bool CanReverse,
    IReadOnlyList<string> ValidationErrors,
    int AffectedPostingsCount
);
```

## 4. Technologieentscheidungen

### 4.1 Transaktions-Management
**Entscheidung:** Verwendung von EF Core Database Transactions

**Begründung:**
- Garantiert Atomarität aller Operationen (NFR-1)
- Bei Fehler erfolgt automatischer Rollback
- Bewährtes Pattern im Projekt

**Implementation:**
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // 1. Validierung
    // 2. Erstelle Gegen-Buchungen
    // 3. Setze bidirektionale Markierung
    // 4. Erstelle StatementImport
    // 5. Update Aggregates
    
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 4.2 Service Layer Pattern
**Entscheidung:** Geschäftslogik in dediziertem Service, nicht im Controller

**Begründung:**
- Trennung von Concerns (Web vs. Business Logic)
- Testbarkeit ohne HTTP-Kontext
- Wiederverwendbarkeit (z.B. für Batch-Operations)
- Konsistent mit bestehendem IPostingAggregateService-Pattern

### 4.3 Validierungs-Strategie
**Entscheidung:** Validierung vor Transaction-Beginn

**Begründung:**
- Vermeidet unnötige Transaktions-Overhead bei Validierungsfehlern
- Erlaubt strukturierte Fehler-Rückgabe
- Separate CanReverseAsync-Methode für UI-State-Management

### 4.4 Aggregate Updates
**Entscheidung:** Verwendung von bestehendem IPostingAggregateService

**Begründung:**
- Wiederverwendung bestehender Logik
- Konsistente Aggregate-Berechnung
- Aufruf für beide Original- und Stornierungsbuchungen

## 5. UI/UX-Konzept

### 5.1 Informationsarchitektur
```
Posting Detail Page
├── Header (Posting Info)
├── Ribbon Menu
│   ├── ... (existing actions)
│   └── [Stornieren] Button ← NEU
└── Details Panel
```

### 5.2 Interaktionsdesign

**Schritt 1: Button-Anzeige**
- Button "Stornieren" im Ribbon Menu
- Deaktiviert wenn:
  - Buchung bereits storniert (`IsReversed == true`)
  - Buchung hat Kind-Buchungen (Split-Buchung als Parent)
  - Benutzer ist nicht Eigentümer
- Tooltip zeigt Grund für Deaktivierung

**Schritt 2: Bestätigungs-Dialog**
- Klick öffnet Modal-Dialog
- Bei einzelner Buchung:
  - "Möchten Sie diese Buchung wirklich stornieren?"
  - Details: Betrag, Konto, Datum
- Bei Gruppe:
  - "Die folgenden X Buchungen werden storniert:"
  - Liste aller Gruppenmitglieder
- Aktionen: [Abbrechen] [Stornieren]

**Schritt 3: Ausführung & Feedback**
- Nach Bestätigung: API-Call mit Loading-Spinner
- Bei Erfolg:
  - Toast-Benachrichtigung: "Buchung erfolgreich storniert"
  - Link zur Detail-Page der Stornierungsbuchung
  - Automatisches Reload der Posting-Liste
- Bei Fehler:
  - Fehler-Dialog mit Validierungs-Fehlern
  - Button bleibt klickbar für Retry

### 5.3 State Management
- Komponente lädt Validierungs-Status über `/api/postings/{id}/validate-reversal`
- Button-State reaktiv basierend auf ValidationResult
- Nach Stornierung: Navigation zu neuer Stornierungsbuchung

## 6. Datenmodell

### 6.1 EF Core Configuration

**Datei:** `FinanceManager.Infrastructure/Data/Configurations/PostingConfiguration.cs`

```csharp
// In Configure-Methode ergänzen:

// Reversal relationships
builder.HasOne(p => p.ReversedByPosting)
    .WithOne(p => p.ReversalForPosting)
    .HasForeignKey<Posting>(p => p.ReversedByPostingId)
    .OnDelete(DeleteBehavior.Restrict)
    .IsRequired(false);

// Indexes for performance
builder.HasIndex(p => p.ReversedByPostingId)
    .HasDatabaseName("IX_Postings_ReversedByPostingId");
    
builder.HasIndex(p => p.ReversalForPostingId)
    .HasDatabaseName("IX_Postings_ReversalForPostingId");
```

### 6.2 Migration

**Erstellen:**
```bash
dotnet ef migrations add AddReversalFields -p FinanceManager.Infrastructure -s FinanceManager.Web --context AppDbContext --output-dir Data/Migrations
```

**Migration-Inhalt:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<Guid?>(
        name: "ReversedByPostingId",
        table: "Postings",
        type: "TEXT",
        nullable: true);

    migrationBuilder.AddColumn<Guid?>(
        name: "ReversalForPostingId",
        table: "Postings",
        type: "TEXT",
        nullable: true);

    migrationBuilder.CreateIndex(
        name: "IX_Postings_ReversedByPostingId",
        table: "Postings",
        column: "ReversedByPostingId");

    migrationBuilder.CreateIndex(
        name: "IX_Postings_ReversalForPostingId",
        table: "Postings",
        column: "ReversalForPostingId");

    migrationBuilder.AddForeignKey(
        name: "FK_Postings_Postings_ReversedByPostingId",
        table: "Postings",
        column: "ReversedByPostingId",
        principalTable: "Postings",
        principalColumn: "Id",
        onDelete: ReferentialAction.Restrict);

    migrationBuilder.AddForeignKey(
        name: "FK_Postings_Postings_ReversalForPostingId",
        table: "Postings",
        column: "ReversalForPostingId",
        principalTable: "Postings",
        principalColumn: "Id",
        onDelete: ReferentialAction.Restrict);
}
```

### 6.3 Datenbank-Schema

```
Postings Table (Erweiterung):
┌─────────────────────────┬──────────┬──────────┬─────────────┐
│ Column                  │ Type     │ Nullable │ Index       │
├─────────────────────────┼──────────┼──────────┼─────────────┤
│ ReversedByPostingId     │ GUID     │ Yes      │ FK, Indexed │
│ ReversalForPostingId    │ GUID     │ Yes      │ FK, Indexed │
└─────────────────────────┴──────────┴──────────┴─────────────┘

Constraints:
- FK: ReversedByPostingId → Postings.Id (ON DELETE RESTRICT)
- FK: ReversalForPostingId → Postings.Id (ON DELETE RESTRICT)
- Check: NOT (ReversedByPostingId IS NOT NULL AND ReversalForPostingId IS NOT NULL)
  (Eine Buchung kann nicht gleichzeitig Original und Stornierung sein)
```

## 7. Qualitätsziele

### 7.1 NFR-1: Atomarität
**Ziel:** Alle Stornierungsoperationen erfolgen transaktional (alles oder nichts)

**Technische Umsetzung:**
- EF Core Database Transaction mit explizitem Rollback bei Fehlern
- Validierung vor Transaction-Beginn
- Exception Handling mit strukturierten Fehlermeldungen

**Akzeptanzkriterien:**
- Bei Fehler in beliebigem Schritt: keine partiellen Änderungen in DB
- Integration Tests prüfen Rollback-Verhalten

### 7.2 NFR-2: Performance
**Ziel:** < 2 Sekunden (95. Perzentil) für Gruppen bis 10 Buchungen

**Technische Umsetzung:**
- Single Query für Laden aller Gruppenmitglieder (`.Include(p => p.Group)`)
- Batch Insert für Stornierungsbuchungen (`.AddRange()`)
- Indexierung der neuen FK-Felder
- Aggregate Updates in dedizierter Transaktion

**Akzeptanzkriterien:**
- Performance Tests mit 1, 5, 10 Buchungen pro Gruppe
- Logging der Ausführungszeit für Monitoring

### 7.3 NFR-3: Nachvollziehbarkeit
**Ziel:** 100% Traceability zwischen Original und Stornierung

**Technische Umsetzung:**
- Bidirektionale Navigation Properties
- UI zeigt Links zu verknüpften Buchungen
- Historisierung über Audit Log (sofern vorhanden)

**Akzeptanzkriterien:**
- Von Original zu Stornierung navigierbar
- Von Stornierung zu Original navigierbar
- Stornierungsbuchungen eindeutig erkennbar in UI

### 7.4 NFR-5: Testabdeckung
**Ziel:** >= 85% Code Coverage

**Technische Umsetzung:**
- Unit Tests für PostingReversalService (alle Szenarien)
- Integration Tests für API Endpoint
- UI Tests für Button-State und User Flow

**Test-Szenarien:**
- Einzelne Buchung stornieren
- Gruppe stornieren
- Validierungsfehler (bereits storniert, hat Kinder)
- Autorisierungsfehler (fremde Buchung)
- Transaction Rollback bei Fehler

## 8. Implementierungsplan

### Phase 1: Domain & Infrastructure (Foundation)

**Task 1.1: Extend Posting Entity**
- Datei: `FinanceManager.Domain/Postings/Posting.cs`
- Add Properties: `ReversedByPostingId`, `ReversalForPostingId` + Navigation Properties
- Add Methods: `SetReversedBy()`, `SetReversalFor()`, `IsReversed`, `IsReversal`
- Estimated: 1 hour

**Task 1.2: Create EF Core Migration**
- Add columns and indexes
- Add foreign key constraints
- Test migration up/down
- Estimated: 1 hour

**Task 1.3: Implement IPostingReversalService**
- Datei: `FinanceManager.Application/Services/IPostingReversalService.cs` (Interface)
- Datei: `FinanceManager.Infrastructure/Services/PostingReversalService.cs` (Implementation)
- Methods: `ReversePostingAsync`, `CanReverseAsync`, `GetRelatedPostingsAsync`
- Estimated: 4 hours

**Task 1.4: Register Service**
- Datei: `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
- Add: `services.AddScoped<IPostingReversalService, PostingReversalService>()`
- Estimated: 15 minutes

### Phase 2: API Layer

**Task 2.1: Create DTOs**
- Datei: `FinanceManager.Shared/Dtos/ReversalResultDto.cs`
- Datei: `FinanceManager.Shared/Dtos/ReversalValidationDto.cs`
- Estimated: 30 minutes

**Task 2.2: Add API Endpoint**
- Datei: `FinanceManager.Web/Controllers/PostingsController.cs`
- Add: `POST /api/postings/{id}/reverse`
- Add: `GET /api/postings/{id}/validate-reversal` (für UI)
- Error Handling & Authorization
- Estimated: 2 hours

### Phase 3: UI Layer

**Task 3.1: Add Ribbon Button**
- Datei: Blazor component for posting detail page
- Add "Stornieren" button with state management
- Estimated: 2 hours

**Task 3.2: Implement Confirmation Dialog**
- Create modal component
- Show affected postings
- Handle user confirmation
- Estimated: 2 hours

**Task 3.3: Integrate API Call**
- Call reversal endpoint
- Handle loading state
- Show success/error feedback
- Navigate to reversal posting
- Estimated: 2 hours

### Phase 4: Testing

**Task 4.1: Unit Tests (Service Layer)**
- Test scenarios: single, group, validation errors, authorization
- Mock dependencies
- Target: > 85% coverage
- Estimated: 4 hours

**Task 4.2: Integration Tests (API)**
- Test full API flow with real database
- Test transaction rollback
- Estimated: 3 hours

**Task 4.3: UI Tests**
- Test button state logic
- Test user interaction flow
- Estimated: 2 hours

### Phase 5: Documentation & Deployment

**Task 5.1: Update User Documentation**
- Add section to user manual
- Screenshots of UI
- Estimated: 1 hour

**Task 5.2: Code Review & Deployment**
- PR review
- Deploy to staging
- Smoke tests
- Estimated: 2 hours

**Total Estimated Effort:** ~27 hours

## 5. Entscheidungen aus Phase 0: Lösungen für kritische Blocker

> **Status:** ✅ RESOLVED  
> **Datum:** 2025-01-27  
> **Review-ID:** ARCH-REV-2024-POSTING-REVERSAL-001

Diese Sektion dokumentiert die Lösungen für die 4 kritischen Blocker aus dem Architecture-Review und beantwortet die offenen Fragen.

---

### 5.1 Lösung C-1: Transaktions-Konsistenz

**Problem:** Fehlende Transaktionskonsistenz bei Reversal-Operationen (Risk: Partial Reversals)

**Lösung:**

**Transaktionsstrategie:**
```csharp
public async Task<ReversalResult> ReversePostingAsync(Guid postingId, Guid userId, CancellationToken ct)
{
    // 1. Pre-Transaction Validation (schnell, kein Lock)
    var validation = await CanReverseAsync(postingId, userId, ct);
    if (!validation.IsValid)
        throw new InvalidOperationException(string.Join("; ", validation.Errors));

    // 2. Begin Transaction with appropriate isolation
    using var transaction = await _context.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.ReadCommitted, ct);
    
    try
    {
        // 3. Load Original + Related Postings with Lock (repeatable read within TX)
        var original = await _context.Postings
            .FirstOrDefaultAsync(p => p.Id == postingId, ct);
        
        if (original == null || original.ReversedByPostingId.HasValue)
            throw new InvalidOperationException("Posting not found or already reversed");

        var relatedPostings = await GetRelatedPostingsAsync(postingId, ct);

        // 4. Create Reversal Postings (negated amounts, same dates)
        var reversals = new List<Posting>();
        var newGroupId = Guid.NewGuid(); // All reversals share new GroupId

        foreach (var posting in relatedPostings.Prepend(original))
        {
            var reversal = CreateReversalPosting(posting, newGroupId);
            _context.Postings.Add(reversal);
            reversals.Add(reversal);
            
            // Mark original as reversed
            posting.SetReversedBy(reversal);
        }

        await _context.SaveChangesAsync(ct);

        // 5. Create StatementImport for reconciliation
        var statementImport = await CreateReversalStatementImportAsync(original, ct);

        // 6. Update Aggregates (both original and reversal affect aggregates)
        foreach (var reversal in reversals)
        {
            await _aggregateService.UpsertForPostingAsync(reversal, ct);
        }

        await _context.SaveChangesAsync(ct);

        // 7. Commit Transaction
        await transaction.CommitAsync(ct);

        return new ReversalResult(
            relatedPostings.Select(p => p.Id).ToList(),
            reversals.Select(r => r.Id).ToList(),
            statementImport.Id
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Reversal transaction failed for posting {PostingId}, rolling back", postingId);
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

**Entscheidungen:**
- **IsolationLevel: ReadCommitted** – Standard-Level ausreichend, da Pre-Validation vorhanden
- **Scope: Alle Operationen (Create, Mark, StatementImport, Aggregates)** innerhalb Transaction
- **Rollback: Automatisch** bei Exception → garantierte Atomarität
- **Validation: Vor Transaction** → Performance-Optimierung, vermeidet unnötige Locks

**Begründung:**
- Bestehende Codebase verwendet `BeginTransactionAsync` (siehe `StatementDraftService.BatchUpdate.cs`)
- ReadCommitted ist ausreichend, da keine Phantom Reads zu erwarten
- SaveChanges nur zweimal: nach Posting-Erstellung und nach Aggregates-Update

---

### 5.2 Lösung C-2: Audit-Trail-Mechanismen

**Problem:** Keine Spezifikation von WER WANN WAS storniert hat

**Lösung:**

**Erweiterte Posting-Entity (Domain Layer):**
```csharp
public sealed class Posting : Entity, IAggregateRoot
{
    // ... existing properties ...
    
    /// <summary>
    /// Optional reference to the reversal posting that cancels this posting.
    /// </summary>
    public Guid? ReversedByPostingId { get; private set; }
    
    /// <summary>
    /// Optional reference to the original posting that this posting reverses.
    /// </summary>
    public Guid? ReversalForPostingId { get; private set; }
    
    /// <summary>
    /// User ID who created the reversal (only set when IsReversal == true).
    /// </summary>
    public Guid? ReversedByUserId { get; private set; }
    
    /// <summary>
    /// Timestamp when the reversal was created (only set when IsReversal == true).
    /// </summary>
    public DateTime? ReversedAtUtc { get; private set; }

    public void SetReversedBy(Posting reversalPosting, Guid userId)
    {
        if (ReversedByPostingId.HasValue)
            throw new InvalidOperationException("Posting already reversed");
        
        ReversedByPostingId = reversalPosting.Id;
        ReversedByUserId = userId;
        ReversedAtUtc = DateTime.UtcNow;
        Touch();
    }
    
    public void SetReversalFor(Posting originalPosting)
    {
        if (ReversalForPostingId.HasValue)
            throw new InvalidOperationException("Already marked as reversal");
        
        ReversalForPostingId = originalPosting.Id;
    }

    public bool IsReversed => ReversedByPostingId.HasValue;
    public bool IsReversal => ReversalForPostingId.HasValue;
}
```

**Logging-Strategie (Service Layer):**
```csharp
public async Task<ReversalResult> ReversePostingAsync(Guid postingId, Guid userId, CancellationToken ct)
{
    _logger.LogInformation(
        "User {UserId} initiating reversal for posting {PostingId}", 
        userId, postingId);
    
    // ... transaction logic ...
    
    _logger.LogInformation(
        "Reversal completed: {ReversedCount} postings reversed, {CreatedCount} reversals created by user {UserId}",
        result.ReversedPostingIds.Count,
        result.CreatedReversalIds.Count,
        userId);
    
    return result;
}
```

**Entscheidungen:**
- **ReversedByUserId + ReversedAtUtc** in Posting-Entity → keine separate AuditLog-Tabelle nötig
- **Immutable Audit-Felder** → einmal gesetzt, nicht mehr änderbar
- **Logging**: Information-Level für jede Reversal-Operation mit UserId, PostingId, Counts
- **Bestehende Entity.CreatedUtc/ModifiedUtc** bleiben erhalten für allgemeine Audit-Trail

**Begründung:**
- Projekt verwendet bereits `Entity` base class mit CreatedUtc/ModifiedUtc
- Keine separate Audit-Tabelle nötig → Einfachheit
- UserId + Timestamp direkt in Domain-Entity → volle Nachvollziehbarkeit
- Konsistent mit Domain-Driven Design (Audit-Daten gehören zur Entity)

---

### 5.3 Lösung C-3: Cascading Reversals

**Problem:** "Zugehörige Posten stornieren" ist vage; Tiefe, Zirkelreferenzen, teilstornierte Gruppen unklar

**Lösung:**

**Cascading-Strategie: Gruppe-basiert (nicht rekursiv)**

```csharp
/// <summary>
/// Gets all postings that must be reversed together (same GroupId, not already reversed).
/// </summary>
public async Task<IReadOnlyList<Posting>> GetRelatedPostingsAsync(Guid postingId, CancellationToken ct)
{
    var original = await _context.Postings
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == postingId, ct);
    
    if (original == null)
        throw new ArgumentException($"Posting {postingId} not found", nameof(postingId));

    // Strategy: Reverse all postings in the same GroupId
    // Reason: GroupId represents a logical transaction (e.g., security purchase + bank withdrawal)
    var relatedPostings = await _context.Postings
        .Where(p => p.GroupId == original.GroupId 
                 && p.Id != postingId // exclude original (will be added by caller)
                 && !p.ReversedByPostingId.HasValue) // skip already reversed
        .ToListAsync(ct);

    return relatedPostings;
}
```

**Entscheidungen:**
- **Strategie: Group-based Cascading** – alle Postings mit gleichem `GroupId` werden storniert
- **Keine rekursive Stornierung** von ParentId/LinkedPostingId → vermeidet Komplexität
- **Stopkriterium: ReversedByPostingId.HasValue** → bereits stornierte Postings überspringen
- **Keine Zirkelreferenzen möglich** → GroupId ist flat (keine Hierarchie)
- **Teilstornierte Gruppen: Validierung** → bei bereits teilstornierter Gruppe → Fehler

**Validierung teilstornierte Gruppen:**
```csharp
public async Task<ValidationResult> CanReverseAsync(Guid postingId, Guid userId, CancellationToken ct)
{
    var errors = new List<string>();
    var posting = await _context.Postings
        .Include(p => p.Account)
        .FirstOrDefaultAsync(p => p.Id == postingId, ct);

    if (posting == null)
    {
        errors.Add("Posting not found");
        return new ValidationResult(false, errors);
    }

    // Authorization
    if (posting.AccountId.HasValue && posting.Account?.OwnerUserId != userId)
        errors.Add("Unauthorized: Posting does not belong to current user");

    // Already reversed?
    if (posting.ReversedByPostingId.HasValue)
        errors.Add("Posting has already been reversed");

    // Is itself a reversal?
    if (posting.ReversalForPostingId.HasValue)
        errors.Add("Cannot reverse a reversal posting");

    // Check for partially reversed group (business rule: all-or-nothing)
    var groupPostings = await _context.Postings
        .Where(p => p.GroupId == posting.GroupId)
        .ToListAsync(ct);
    
    var anyReversed = groupPostings.Any(p => p.ReversedByPostingId.HasValue);
    var allReversed = groupPostings.All(p => p.ReversedByPostingId.HasValue);
    
    if (anyReversed && !allReversed)
        errors.Add("Cannot reverse posting: Group is partially reversed. Please reverse remaining postings first.");

    return new ValidationResult(errors.Count == 0, errors);
}
```

**Begründung:**
- GroupId in Codebase existiert und wird verwendet (siehe `Posting.cs`)
- Einfaches, vorhersagbares Verhalten für Benutzer
- Keine Komplexität durch rekursive Traversierung
- Teilstornierte Gruppen werden abgelehnt → Datenintegrität

---

### 5.4 Lösung C-4: Concurrency-Strategie

**Problem:** Zwei User stornieren parallel denselben Posten → Doppel-Stornierung, Race Conditions

**Lösung:**

**Strategie: Application-Level Validation (kein Optimistic Locking)**

**Entscheidung: Verzicht auf RowVersion**

**Begründung:**
1. **Codebase-Analyse:** Kein Posting verwendet aktuell RowVersion/Timestamp
2. **Transaktions-Isolation:** ReadCommitted + Validation innerhalb Transaction ausreichend
3. **Business-Logik:** ReversedByPostingId.HasValue ist natürliche Sperre
4. **Simplicity:** Kein Breaking Change an bestehender Domain-Entity nötig

**Implementation:**
```csharp
public async Task<ReversalResult> ReversePostingAsync(Guid postingId, Guid userId, CancellationToken ct)
{
    using var transaction = await _context.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.ReadCommitted, ct);
    
    try
    {
        // Critical: Load posting with write intent (locks row in DB)
        var original = await _context.Postings
            .FirstOrDefaultAsync(p => p.Id == postingId, ct);
        
        // Double-check within transaction (prevents race condition)
        if (original == null)
            throw new InvalidOperationException("Posting not found");
        
        if (original.ReversedByPostingId.HasValue)
        {
            // Another user already reversed this posting
            _logger.LogWarning(
                "Concurrent reversal attempt detected: Posting {PostingId} already reversed by user {OtherUserId} at {ReversedAt}",
                postingId, original.ReversedByUserId, original.ReversedAtUtc);
            
            throw new InvalidOperationException("Posting has already been reversed");
        }

        // ... rest of reversal logic ...
        
        await transaction.CommitAsync(ct);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

**Concurrency-Schutz:**
- **Database Transaction** → Isolation verhindert Lost Updates
- **Check innerhalb Transaction** → ReversedByPostingId.HasValue als natürliche Sperre
- **HTTP 409 Conflict** → Bei Concurrent Access wird zweiter Request mit 409 abgelehnt
- **Client Retry-Logic** → UI zeigt Fehlermeldung "Bereits storniert" (kein automatisches Retry)

**Entscheidungen:**
- **Kein Optimistic Locking** (kein RowVersion) → Simplicity
- **Database Isolation + Application Validation** → ausreichender Schutz
- **Idempotenz-Semantik** → Mehrfache Aufrufe mit gleicher PostingId sicher (2. Aufruf schlägt fehl, aber DB bleibt konsistent)
- **Logging bei Konflikt** → Transparenz für Monitoring

**Alternative (falls zukünftig benötigt):**
Falls später höhere Concurrency-Anforderungen entstehen, kann Optimistic Locking nachgerüstet werden:
- Migration: `ALTER TABLE Postings ADD RowVersion ROWVERSION` (SQL Server) oder `BLOB` (SQLite Workaround)
- EF Core: `[Timestamp]` Attribut auf neue Property
- Konflikt-Handling: `DbUpdateConcurrencyException` → HTTP 409

**Aktuell: Nicht nötig** → Overengineering vermeiden

---

### 5.5 Antwort Q-1: StatementImport-Struktur

**Frage:** Wie wird aus einem Posting ein StatementEntry generiert?

**Antwort (basierend auf Codebase-Analyse):**

**StatementImport-Struktur:**
```csharp
public async Task<StatementImport> CreateReversalStatementImportAsync(
    Posting originalPosting, CancellationToken ct)
{
    // 1. Create StatementImport
    var import = new StatementImport(
        accountId: originalPosting.AccountId!.Value,
        format: ImportFormat.Reversal, // NEW: Kennzeichnung als Korrektur
        originalFileName: $"Reversal_{originalPosting.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.reversal"
    );
    _context.StatementImports.Add(import);
    await _context.SaveChangesAsync(ct);

    // 2. Create StatementEntry (mirrors original, but for reversal posting)
    // Note: We create entry for the REVERSAL posting, not the original
    // This allows reconciliation to match the reversal in future imports
    var entry = new StatementEntry(
        statementImportId: import.Id,
        bookingDate: originalPosting.BookingDate, // Same dates as original
        amount: -originalPosting.Amount, // Negated amount
        subject: $"REVERSAL: {originalPosting.Subject ?? "No subject"}",
        rawHash: $"reversal_{originalPosting.Id}_{Guid.NewGuid()}", // Unique hash
        recipientName: originalPosting.RecipientName,
        valutaDate: originalPosting.ValutaDate,
        currencyCode: "EUR",
        bookingDescription: $"Reversal of posting {originalPosting.Id}",
        isAnnounced: false,
        isCostNeutral: false
    );
    _context.StatementEntries.Add(entry);
    await _context.SaveChangesAsync(ct);

    return import;
}
```

**Mapping Original → StatementEntry:**
- **BookingDate** → gleich
- **ValutaDate** → gleich
- **Amount** → **negiert** (wichtig!)
- **Subject** → mit Prefix "REVERSAL: "
- **RawHash** → unique (verhindert Duplikatserkennung)
- **RecipientName** → gleich
- **BookingDescription** → Hinweis auf Original-Posting-ID

**Validierung nicht nötig:** StatementEntry wird automatisch erstellt (kein User-Input)

**Begründung:**
- `StatementImport` + `StatementEntry` existieren in Codebase (siehe `StatementImport.cs`, `StatementEntry.cs`)
- `ImportFormat` Enum kann erweitert werden um `Reversal` → eindeutige Kennzeichnung
- StatementEntry ermöglicht spätere Rekonziliation (falls Bank echte Stornierung importiert)

---

### 5.6 Antwort Q-3: Aggregate Update Scope

**Frage:** Werden Account-Balances automatisch aktualisiert? Welche Aggregate-Werte?

**Antwort:**

**Aggregate Update Scope:**
```csharp
// In ReversePostingAsync nach Erstellung der Reversals:

// Update aggregates for all REVERSAL postings
foreach (var reversal in reversals)
{
    await _aggregateService.UpsertForPostingAsync(reversal, ct);
}

// Original postings: No explicit aggregate update needed
// Reason: Aggregate service uses Amount; reversed postings have Amount unchanged
//         The reversal posting (with negated amount) updates the aggregate correctly
```

**Entscheidung: Nur Reversal-Postings**

**Begründung (Codebase-Analyse):**
- `PostingAggregateService.UpsertForPostingAsync()` berechnet Aggregate basierend auf `Posting.Amount`
- Original-Postings behalten ihre Amount (keine Änderung)
- Reversal-Postings haben negierte Amount → addieren negativ zu Aggregat
- Ergebnis: Aggregate-Summe wird korrekt auf 0 gesetzt (Original + Reversal = 0)

**Welche Aggregate werden aktualisiert:**
- **PostingAggregates** (für Reporting/KPIs) → automatisch via `UpsertForPostingAsync`
  - Month, Quarter, HalfYear, Year
  - Booking Date + Valuta Date (AggregateDateKind)
  - Nach PostingKind: Bank, Contact, Security, SavingsPlan

**Account-Balances:**
- Keine explizite "Account-Balance"-Tabelle im Projekt
- Balances werden on-the-fly berechnet über Aggregate-Summen
- Reversal-Postings aktualisieren Aggregate → Balance automatisch korrekt

**Zusammenfassung:**
- ✅ Reversal-Postings: `UpsertForPostingAsync` aufrufen
- ❌ Original-Postings: Kein Update nötig (Amount unverändert)
- ✅ Account-Balances: Automatisch durch Aggregate-Update korrekt

---

## 9. Offene Fragen

~~**ALLE OFFENEN FRAGEN WURDEN IN SEKTION 5 BEANTWORTET**~~

### Status: ✅ RESOLVED

- ~~9.1 StatementImport Creation~~ → **Beantwortet in 5.5**
- ~~9.2 UI Component Architecture~~ → **Out of Scope für Phase 0** (wird in Phase 3 adressiert)
- ~~9.3 Aggregate Update Scope~~ → **Beantwortet in 5.6**

## 10. Risiken und Mitigation

### Risiko 1: Performance bei großen Gruppen
**Beschreibung:** Gruppen > 10 Buchungen könnten Performance-Ziel verletzen
**Wahrscheinlichkeit:** Mittel
**Impact:** Mittel
**Mitigation:** 
- Implementiere Paging für große Gruppen
- Async Processing für Gruppen > 20 Buchungen

### Risiko 2: Dateninkonsistenz bei Concurrent Updates
**Beschreibung:** Zwei Benutzer stornieren gleichzeitig die gleiche Buchung
**Wahrscheinlichkeit:** Niedrig
**Impact:** Hoch
**Mitigation:**
- Optimistic Concurrency mit RowVersion/Timestamp
- Validierung innerhalb Transaction

### Risiko 3: Aggregate-Neuberechnung verlangsamt Stornierung
**Beschreibung:** Aggregate Updates könnten Performance-Budget sprengen
**Wahrscheinlichkeit:** Mittel
**Impact:** Mittel
**Mitigation:**
- Async Processing der Aggregate Updates außerhalb Main Transaction
- Eventual Consistency für Aggregates akzeptabel

---

**Änderungshistorie:**

| Version | Datum      | Autor             | Änderung                         |
|---------|------------|-------------------|----------------------------------|
| 1.0     | 2025-01-27 | Architecture Team | Initial draft based on FA-POST-001 |
