# Umsetzungsplan – Schritt 1: Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung

**Eingaben:** `requirement.md`, `inventory.md` (dieses Verzeichnis), Projektkontext
`Docs/projects/task/issue-492-.../project-plan.md` (Schritt 1, verbindlich).
**Basisbranch:** `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul`

## Umsetzungselemente

### 1. Neue Join-Entity `SavingsPlanAccountLink`

Datei: `FinanceManager.Domain/Savings/SavingsPlanAccountLink.cs`

- `public sealed class SavingsPlanAccountLink : Entity` — erbt `Id`, `CreatedUtc`, `ModifiedUtc`
  (Erstellungs-/Metadaten gemäß Anforderung).
- Konstruktor `SavingsPlanAccountLink(Guid savingsPlanId, Guid accountId, Guid ownerUserId)` mit
  `Guards.NotEmpty`-Prüfung aller drei IDs.
- Properties (alle `private set`): `SavingsPlanId`, `AccountId`, `OwnerUserId`.
- Navigationen `SavingsPlan? SavingsPlan`, `Account? Account` (Vorbild `AccountLinkedIban.Account`).
- `SavingsPlanAccountLinkBackupDto`-Record + `ToBackupDto()`/`AssignBackupDto()` am selben Typ
  (Konvention; Backup-Export/-Import selbst ist Schritt 9).
- **Benennung:** `SavingsPlanAccountLink` — klar von `SavingsPlanExpectation` unterscheidbar;
  drückt die n:m-Verknüpfung aus (Alternativkandidat `SavingsPlanLinkedAccount` verworfen, da er
  wie ein Konto-Subtyp wirkt).

### 2. DbContext-Erweiterung — `FinanceManager.Infrastructure/AppDbContext.cs`

- `DbSet<SavingsPlanAccountLink> SavingsPlanAccountLinks` (Getter-Stil wie die übrigen Sets).
- Fluent-Konfiguration in `OnModelCreating` (direkt nach der `SavingsPlan`-Config):

```csharp
modelBuilder.Entity<SavingsPlanAccountLink>(b =>
{
    b.ToTable("SavingsPlanAccountLinks");
    b.HasKey(x => x.Id);
    b.Property(x => x.OwnerUserId).IsRequired();
    b.HasIndex(x => new { x.SavingsPlanId, x.AccountId }).IsUnique();
    b.HasIndex(x => x.AccountId);
    b.HasIndex(x => x.OwnerUserId);
    b.HasOne(x => x.SavingsPlan)
     .WithMany()
     .HasForeignKey(x => x.SavingsPlanId)
     .OnDelete(DeleteBehavior.Cascade);
    b.HasOne(x => x.Account)
     .WithMany()
     .HasForeignKey(x => x.AccountId)
     .OnDelete(DeleteBehavior.Cascade);
});
```

- Unique-Index `(SavingsPlanId, AccountId)` = eindeutige Paarung; `AccountId`-Index für die
  Rückwärtsabfrage (Pläne eines Kontos); `OwnerUserId`-Index für owner-scoped Abfragen.

### 3. Migration per `dotnet ef`

```
dotnet ef migrations add AddSavingsPlanAccountLinks --project FinanceManager.Infrastructure --startup-project FinanceManager.Web
```

- Erzeugt Migration + Designer + aktualisierten `AppDbContextModelSnapshot` in
  `FinanceManager.Infrastructure/Migrations/` (Ort des Snapshots = etablierter Migrationsort).
- Generiertes Up/Down wird auf Datensicherheit geprüft (nur neue Tabelle → unkritisch).

### 4. Service-Interface — `FinanceManager.Application/Savings/ISavingsPlanAccountLinkService.cs`

Dedizierter Service (statt Erweiterung von `ISavingsPlanService`): Die Verknüpfung betrifft zwei
Aggregate (Plan **und** Konto); Schritt 3 braucht beide Abfragerichtungen an API-Subressourcen.
Fehler-/Rückgabekonvention exakt wie `IAccountService`-LinkedIbans-Methoden:

- `Task AddLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct)`
  — `ArgumentException` bei nicht gefundenem/nicht dem Aufrufer gehörendem Plan oder Konto;
  `InvalidOperationException` bei Duplikat.
- `Task<bool> RemoveLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct)`
  — `false`, wenn Plan nicht (dem Aufrufer gehörend) existiert oder kein Link vorhanden.
- `Task<IReadOnlyList<Guid>?> GetLinkedAccountIdsAsync(Guid savingsPlanId, Guid ownerUserId, CancellationToken ct)`
  — `null`, wenn der Plan nicht existiert/nicht dem Aufrufer gehört; sonst Account-IDs.
- `Task<IReadOnlyList<Guid>?> GetLinkedSavingsPlanIdsAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)`
  — `null`, wenn das Konto nicht existiert/nicht dem Aufrufer gehört; sonst Plan-IDs.

### 5. Implementierung — `FinanceManager.Infrastructure/Savings/SavingsPlanAccountLinkService.cs`

- Konstruktor `SavingsPlanAccountLinkService(AppDbContext db)`.
- `AddLinkAsync`: Plan per `Id == savingsPlanId && OwnerUserId == ownerUserId` laden (`AsNoTracking`),
  sonst `ArgumentException("Savings plan not found")`; Konto analog (`ArgumentException("Account not found")`).
  Da beide gegen `ownerUserId` geprüft werden, sind Cross-Owner-Links strukturell ausgeschlossen
  (kein Info-Leak über Fremdbesitz). Duplikatprüfung per `AnyAsync` auf dem Paar →
  `InvalidOperationException("Account already linked to this savings plan")`. Dann Entity erzeugen
  (`OwnerUserId = ownerUserId`), `Add`, `SaveChangesAsync`.
- `RemoveLinkAsync`: Owner-/Existenzprüfung des Plans (`AsNoTracking().AnyAsync`), dann Link per
  Paar laden, `Remove`, `SaveChangesAsync`, `true`.
- `GetLinkedAccountIdsAsync`: Existenz-/Owner-Check des Plans → `null`, sonst
  `Where(l => l.SavingsPlanId == savingsPlanId).Select(l => l.AccountId)` (`AsNoTracking`).
- `GetLinkedSavingsPlanIdsAsync`: analog auf `AccountId`.
- Owner-Check jeweils gegen `ownerUserId`-Parameter, zusätzlich trägt die Entity `OwnerUserId`
  redundant (für spätere Backup-Einsammlung und Audits).

### 6. DI-Registrierung — `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`

`services.AddScoped<ISavingsPlanAccountLinkService, SavingsPlanAccountLinkService>();` direkt bei
den Savings-Registrierungen (Z. ~109).

### 7. Tests — `FinanceManager.Tests/Savings/`

SQLite-in-memory-Setup wie `SavingsPlanServiceTests.Create()`. Drei Testklassen
(`{Class}Tests_{Topic}`, je Topic < 5 bzw. thematisch getrennt):

**`SavingsPlanAccountLinkServiceTests_AddLink`**
- `AddLink_ValidPlanAndAccount_PersistsLink` (inkl. `OwnerUserId`, `CreatedUtc` gesetzt)
- `AddLink_SecondAccountOnSamePlan_AllowsMultipleAccountsPerPlan`
- `AddLink_SecondPlanOnSameAccount_AllowsMultiplePlansPerAccount`
- `AddLink_DuplicatePair_ThrowsInvalidOperationException`
- `AddLink_AccountOfOtherOwner_ThrowsArgumentException` (Cross-Owner abgewiesen)
- `AddLink_UnknownPlan_ThrowsArgumentException`
- `AddLink_UnknownAccount_ThrowsArgumentException`
- `AddLink_PlanOfOtherOwner_ThrowsArgumentException`

**`SavingsPlanAccountLinkServiceTests_RemoveLink`**
- `RemoveLink_ExistingLink_RemovesAndReturnsTrue`
- `RemoveLink_UnknownLink_ReturnsFalse`
- `RemoveLink_PlanOfOtherOwner_ReturnsFalse`
- `RemoveLink_DeletedPlan_CascadesLinks` (EF-Cascade-Nachweis: Plan-Delete entfernt Links)

**`SavingsPlanAccountLinkServiceTests_Queries`**
- `GetLinkedAccountIds_PlanWithLinks_ReturnsAccountIds`
- `GetLinkedAccountIds_PlanWithoutLinks_ReturnsEmptyList`
- `GetLinkedAccountIds_PlanOfOtherOwner_ReturnsNull`
- `GetLinkedSavingsPlanIds_AccountWithLinks_ReturnsPlanIds`
- `GetLinkedSavingsPlanIds_AccountOfOtherOwner_ReturnsNull`

### 8. Verifikation

- `dotnet build` der Solution (oder betroffener Projekte).
- `dotnet test FinanceManager.Tests` gefiltert auf die neuen Testklassen, danach vollständiger
  Lauf des `Savings`-Ordners (Regression `SavingsPlanService` unberührt, aber Schema geändert →
  `EnsureCreated`-Tests laufen gegen das neue Modell).
- Reale Abnahme: `dotnet ef database update` gegen eine frische SQLite-Datei → Migration läuft
  fehlerfrei durch (Persistenz-Nachweis jenseits `EnsureCreated`).

## Scope-Klärung

**Scope-Ausschlüsse:** keine (API/UI/Analyse/Backup sind vom Projektschritt selbst ausgeschlossen
und im Requirement als Nicht-Ziele dokumentiert).

## Offene Punkte

Keine.
