# Bestandsaufnahme – Schritt 1: Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung

**Geltungsbereich:** Nur die für Schritt 1 relevanten Ist-Zustände. Die projektweite Bestandsaufnahme
liegt unter `Docs/projects/task/issue-492-...-sparplankonto-verknuepfung-mul/inventory.md`; hier sind
die schrittbezogenen Befunde mit konkreten Datei-/Zeilenreferenzen zusammengefasst.

## 1. Relevante Bestands-Entities

### `SavingsPlan` — `FinanceManager.Domain/Savings/SavingsPlan.cs`

- `Entity`-Basisklasse (`Id`, `CreatedUtc`, `ModifiedUtc`), `OwnerUserId` (Z. 12), keine
  Navigation-Collections — das Projekt verwendet standalone Entities mit loser FK-Verknüpfung.
- Fluent-Config `AppDbContext.cs:539–553`: PK `Id`, Unique `(OwnerUserId, Name)`, `CategoryId` → `SetNull`.

### `Account` — `FinanceManager.Domain/Accounts/Account.cs`

- `OwnerUserId` (Z. 31), `CurrentBalance` (Z. 51), `IsCollectionAccount` (Z. 76),
  `SavingsPlanExpectation` (Z. 66) — bestehende Konto↔Plan-Kopplung anderer Semantik
  (Buchungsvalidierung), **Namenskollision bei der Benennung der neuen Entity beachten**.
- Fluent-Config `AppDbContext.cs:429–446`.

### Referenzmuster `AccountLinkedIban` — `FinanceManager.Domain/Accounts/AccountLinkedIban.cs`

- `sealed class : Entity`, Konstruktor mit Pflicht-IDs, `private set`-Properties, Navigation zurück
  zum Parent (`Account? Account`), `*BackupDto`-Record am selben Typ (Z. 42).
- Fluent-Config `AppDbContext.cs:448–458`: `ToTable("AccountLinkedIbans")`, PK `Id`, Unique-Index
  `(AccountId, Iban)`, FK mit `DeleteBehavior.Cascade`.

## 2. Service-Konventionen

- **Schichten:** Interface in `FinanceManager.Application`, Implementierung in
  `FinanceManager.Infrastructure`, Registrierung `ServiceCollectionExtensions.cs` (scoped,
  z. B. `ISavingsPlanService` → `SavingsPlanService` Z. 109).
- **Owner-Scoping:** Jede Service-Query filtert `OwnerUserId`; fremde/nicht vorhandene Datensätze
  werden nicht unterschieden.
- **Fehlerkonvention** (Muster `AccountService.AddLinkedIbanAsync`, `AccountService.cs:424–468`):
  - Parent nicht gefunden/nicht im Besitz → `ArgumentException("... not found", nameof(id))`.
  - Duplikat → `InvalidOperationException("... already linked ...")`.
  - Entfernen → `Task<bool>` (`false` bei nicht gefundenem Parent oder nicht vorhandenem Link).
  - Auflisten → `Task<IReadOnlyList<T>?>` (`null`, wenn Parent nicht existiert/nicht im Besitz).
- `SavingsPlanService` (`Infrastructure/Savings/SavingsPlanService.cs`) arbeitet direkt auf dem
  `AppDbContext` ohne Repository-Schicht.

## 3. Persistenz / Migration

- `AppDbContext` (`FinanceManager.Infrastructure/AppDbContext.cs`): `DbSet`-Properties plus
  Fluent-Konfiguration in `OnModelCreating`; Provider ist **SQLite**
  (`ServiceCollectionExtensions.cs:76–80`).
- **Migrationsort:** Zwei Verzeichnisse existieren historisch — `Migrations/` (enthält den
  `AppDbContextModelSnapshot`, jüngste Migration `20261105022049_AddMcpSettings`) und
  `Data/Migrations/Identity/` (älterer Stamm, jüngste `20260221144132_Postings_OriginalAmount`).
  `dotnet ef migrations add` legt neue Migrationen im Verzeichnis des ModelSnapshots ab →
  **`FinanceManager.Infrastructure/Migrations/`** ist der etablierte Ort.
- `dotnet ef` 10.0.10 ist installiert; SDK 10.0.403.
- Aufrufmuster: `dotnet ef migrations add <Name> --project FinanceManager.Infrastructure
  --startup-project FinanceManager.Web`.
- SQLite unterstützt mehrere Cascade-Pfade — `Cascade`-Delete auf beiden FKs der Join-Entity ist
  unproblematisch (Löschen von Plan oder Konto räumt Links mit ab, analog `AccountLinkedIban`).

## 4. Testkonventionen

- `FinanceManager.Tests/Savings/`: xUnit + `SqliteConnection("DataSource=:memory:")` +
  `AppDbContext` + `db.Database.EnsureCreated()`; Factory-Methode `Create()` liefert
  `(sut, db, conn)` (Muster `SavingsPlanServiceTests.cs:13–22`).
- Testklassen-Struktur: `{TestedClass}Tests_{Topic}`, Split ab 5+ Tests pro Thema;
  Methodennamen `MethodName_Scenario_ExpectedOutcome`.
- `Account`-Erzeugung in Tests: Konstruktor `(ownerUserId, AccountType, name, iban, bankContactId)` —
  `bankContactId` muss kein existierender `Contact` sein (kein FK-Zwang in `EnsureCreated`-Tests),
  Konvention: `Guid.NewGuid()`.

## 5. Auswirkungen / betroffene Dateien dieses Schritts

| Concern | Dateien |
|---|---|
| Neue Join-Entity | `FinanceManager.Domain/Savings/SavingsPlanAccountLink.cs` (neu) |
| DbContext | `FinanceManager.Infrastructure/AppDbContext.cs` (DbSet + Fluent-Config) |
| Migration | `FinanceManager.Infrastructure/Migrations/` (per `dotnet ef`, inkl. Snapshot-Update) |
| Service-Interface | `FinanceManager.Application/Savings/ISavingsPlanAccountLinkService.cs` (neu) |
| Service-Implementierung | `FinanceManager.Infrastructure/Savings/SavingsPlanAccountLinkService.cs` (neu) |
| DI-Registrierung | `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` |
| Tests | `FinanceManager.Tests/Savings/SavingsPlanAccountLinkServiceTests_*.cs` (neu) |

**Bewusst unberührt:** API-Controller, Shared-DTOs/ApiClient, MCP-Tools, Web-ViewModels,
`SavingsPlanService`-Analysepfade, Backup-Service/SetupImport (Schritt 9), Budget-Queries.
