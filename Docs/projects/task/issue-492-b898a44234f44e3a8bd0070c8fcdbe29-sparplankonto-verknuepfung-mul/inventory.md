# Bestandsaufnahme: Sparplan↔Konto-Verknüpfung (Multi-Link), informelle Beitragsbuchungen und Istmeldung

**Issue:** #492 · **Requirement:** `requirement.md` · **Stand:** Repository-Istzustand auf dem Task-Branch.

## 1. Ist-Zustand (Kurzfassung)

- `SavingsPlan` hat **keinerlei Kontobezug** — weder 1:n noch n:m. `accumulatedAmount` wird ausschließlich aus plan-verknüpften Postings (`Posting.Kind == PostingKind.SavingsPlan`, `SavingsPlanId == plan`) summiert.
- `Posting` kennt kein `informal`-Flag und keine Korrektur-/Istmeldung-Art. Plan-Postings entstehen nur indirekt beim Kontoauszug-Buchen; es gibt **keinen API-Endpunkt zum direkten Anlegen eines Postings**.
- `Account` führt `CurrentBalance` (gepflegt über `AdjustBalance` beim Buchen und Rebuild via `PostingAggregateService`) und `IsCollectionAccount` — beide direkt für das Zielmodell nutzbar.
- Budgetauswertung läuft bestätigt über `BudgetSourceType.SavingsPlan` → plan-verknüpfte Postings (`SavingsPlanId`); Bankpostings fließen nicht als Plan-Actuals ein. Ein Konto-Link hat derzeit keinerlei Einfluss auf Budgets.
- Backup/Restore serialisiert Entitäten über `*BackupDto`-Records mit ID-Remapping beim Import; eine neue Join-Entity muss explizit in Export UND Import ergänzt werden.

## 2. Domänenmodell und EF-Core-Konfiguration

### 2.1 `SavingsPlan` — `FinanceManager.Domain/Savings/SavingsPlan.cs`

- Felder: `OwnerUserId` (Z. 12), `Name`, `Type` (`SavingsPlanType`: OneTime/Recurring/Open), `TargetAmount`, `TargetDate`, `Interval` (`SavingsPlanInterval`), `IsActive`, `ArchivedUtc`, `CategoryId`, `ContractNumber`, `SymbolAttachmentId`. **Kein Account-Feld, keine Navigation.**
- Keine Navigation-Collections — das Projekt verwendet generell standalone Entities mit loser FK-Verknüpfung (Abfragen über `DbSet`, nicht über Include/Navigation).
- Backup-Pattern vorhanden: `SavingsPlanBackupDto` + `ToBackupDto()`/`AssignBackupDto()` (Z. 249–277). Neue persistierte Strukturen folgen demselben Muster.
- `AdvanceTargetDateIfDue` (Z. 176–194): Terminfortschreibung bei Buchung; wird aus `StatementDraftService` gerufen.

### 2.2 `Account` — `FinanceManager.Domain/Accounts/Account.cs`

- `CurrentBalance` (Z. 51): wird beim Buchen via `account.AdjustBalance(amount)` angepasst (`StatementDraftService.cs:2417`, `~2751`) und beim Aggregat-Rebuild aus Bank-Postings neu berechnet (`PostingAggregateService.cs` ≈ Z. 222–237: Summe aller `PostingKind.Bank`-Postings des Kontos, `AdjustBalance(delta)`).
- `SavingsPlanExpectation` (Z. 66; Enum `FinanceManager.Shared/Dtos/Accounts/SavingsPlanExpectation.cs`: None/Optional/Required) — **bereits existierende Konto↔Plan-Kopplung**, wird aber nur für Buchungsvalidierungs-Hinweise (`SAVINGSPLAN_DUE`) genutzt, nicht für die Analyse. Namens-/Konzeptkollision mit dem neuen Link beachten.
- `IsCollectionAccount` (Z. 76) + `AccountLinkedIban` (`Domain/Accounts/AccountLinkedIban.cs`, `DbSet` + Config `AppDbContext.cs:448–458`): Sammelkonto-Konzept existiert bereits; `LinkedIbans` werden auf `AccountDto` mitgeliefert.
- `AccountType` (`Shared/Dtos/Accounts/AccountType.cs`): Giro/Savings/CreditCard — Link-Kandidaten können jeden Typ haben.

### 2.3 `Posting` — `FinanceManager.Domain/Postings/Posting.cs`

- Felder: `SourceId`, `GroupId`, `Kind`, `AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId`, `BookingDate`, `ValutaDate`, `Amount`, `OriginalAmount`, `Subject`, `RecipientName`, `Description`, `SecuritySubType`, `Quantity`, `ParentId`, `LinkedPostingId`, Reversal-Felder (`ReversedByPostingId`, `ReversalForPostingId`, `ReversedByUserId`, `ReversedAtUtc`), `IsReversed`/`IsReversal` (abgeleitet), `IsPreliminary`. **Kein OwnerUserId, kein informal-/Korrektur-Flag.**
- `PostingKind` (`FinanceManager.Shared/Dtos/Postings/PostingKind.cs`): `Bank=0`, `Contact=1`, `SavingsPlan=2`, `Security=3`. Der Enum liegt in **Shared** (nicht Domain) und wird überall referenziert — ein neuer Wert ist serialisierungskompatibel (int), aber ein Add-Endpunkt-Feld/Enum-Mitglieder müssen clientseitig bekannt sein.
- `GroupId` verbindet die zu einer Buchung erzeugten Postings (Bank + Contact + ggf. SavingsPlan + Security) — der etablierte Mechanismus, um ein Plan-Posting seinem Bank-Gegenstück zuzuordnen.
- `PostingBackupDto` (Z. 488) enthält alle Persistenzfelder; neue Felder müssen hier und in `SetupImportService` ergänzt werden.

### 2.4 EF-Core-Konfiguration — `FinanceManager.Infrastructure/AppDbContext.cs`

- `Account` (Z. 429–446): Unique `(OwnerUserId, Name)`, Unique `(OwnerUserId, Iban)` gefiltert, `SavingsPlanExpectation`/`IsCollectionAccount` mit Defaults.
- `Posting` (Z. 504–510): Indizes `(AccountId, BookingDate)`, `(SecurityId, BookingDate)`; `OriginalAmount` Precision 18,2. **Kein Index auf `SavingsPlanId`** — Plan-Queries (`SavingsPlanId == X`) laufen ohne dedizierten Index; bei neuen Join-/Querypfaden prüfenswert.
- `SavingsPlan` (Z. 539–553): Unique `(OwnerUserId, Name)`, `CategoryId` → `SetNull`.
- `AccountLinkedIban` (Z. 448–458): Referenz-Muster für eine Sub-/Join-Entity: eigene Tabelle, PK `Id`, Unique-Index `(AccountId, Iban)`, FK `Cascade`. Für die neue n:m-Entity ist Unique `(SavingsPlanId, AccountId)` analog naheliegend.
- Es existiert **keine** EF-many-to-many-Shadow-Join-Konvention im Code — alle Beziehungen sind explizite Entities.

### 2.5 Migrations-Konvention (Auffälligkeit)

- Zwei parallele Migrationsverzeichnisse für denselben `AppDbContext` (SQLite): `FinanceManager.Infrastructure/Migrations/` (hier liegt auch `AppDbContextModelSnapshot.cs`; jüngste: `20261105022049_AddMcpSettings`) und `FinanceManager.Infrastructure/Data/Migrations/Identity/` (jüngste domänenbezogene: `20260221144132_Postings_OriginalAmount`). Beide werden zur Laufzeit angewendet; neue Domänen-Migrationen gingen zuletzt nach `Data/Migrations/Identity`. **Risiko:** Ort der neuen Join-Migration mit Projekt-Konvention abstimmen; Snapshot liegt in `Migrations/`.

## 3. Plananalyse — `SavingsPlanService` / `AnalyzeAsync` / `scheduleDeficit`

Datei: `FinanceManager.Infrastructure/Savings/SavingsPlanService.cs` (Interface `FinanceManager.Application/Savings/ISavingsPlanService.cs`; DI `ServiceCollectionExtensions.cs:109`).

### 3.1 `GetAsync` (Z. 63–81)

`accumulated` = `Sum(Postings.Amount)` mit `SavingsPlanId == id && Kind == PostingKind.SavingsPlan && BookingDate <= today` (Z. 70–72); `RemainingAmount` daraus. **Erste Umbau-Stelle für Konto-Saldo-Modus.**

### 3.2 `AnalyzeAsync` (Z. 210–264)

- Lädt `history` als `PostingObservation(BookingDate, Amount, IsReversed, IsReversal)` aus denselben Plan-Postings (Z. 221–226).
- `accumulated = history.Sum(Amount)`; Forecast (`TargetReachable`, `RequiredMonthly`, `MonthsRemaining`) aus Monatsdurchschnitt der history.
- Ruft `ComputeSchedule(history, plan.Interval, plan.TargetAmount, plan.TargetDate, today)` (Z. 306–477).

### 3.3 `ComputeSchedule` (Z. 306–477) — Kadenz-/Defizitlogik

- **Financing-Cycle-Modell:** Cycle-Start = Buchungsdatum der letzten fälligen Disbursement (negatives Posting ≈ TargetAmount ±20 %, sonst größte Auszahlung), sonst letztes erreichtes `TargetDate`, sonst erste Einzahlung.
- **Amount-Klassen:** positive In-Cycle-Postings clustern mit Toleranz `max(0,50, 5 %)`; dominante Klasse = häufigste, Tie-Break größerer Betrag; Mikro-Klassen (<50 % der größten Klasse, z. B. Zinsen) können nie dominieren.
- **Kadenz:** Median der Monatsabstände der Buchungsmonate der dominanten Klasse; Plan-`Interval` nur Fallback (es markiert den Fälligkeitsrhythmus der Ausgabe, nicht den Einzahlungsstrom).
- `ExpectedToDate` = DominantBetrag × voll verstrichene Kadenzperioden; `ReceivedToDate` = Summe dominante Klasse; `ScheduleDeficit` = `max(0, expected − received)`, `null` bei revolvierendem Plan (In-Cycle-Disbursements balancieren Einzahlungen ±20 %) oder nicht detektierbarem Rhythmus; `Disbursements` = Summe aller negativen effektiven Postings (Gesamt-Historie).
- **Anschlussfähigkeit:** Die Methode arbeitet rein auf `IReadOnlyList<PostingObservation>` — ein Quellwechsel auf Konto-Inflow (positive `PostingKind.Bank`-Postings der verknüpften Konten, ggf. gefiltert auf Zuflüsse) ist mit geringem Eingriff in `AnalyzeAsync` realisierbar; `IsReversed`/`IsReversal`-Informationen stehen auch auf Bank-Postings zur Verfügung.

## 4. Entstehung plan-verknüpfter Postings (Buchungsweg)

`FinanceManager.Infrastructure/Statements/StatementDraftService.cs`:

- Normale Buchung Z. 2484–2495: Hat ein Draft-Entry `SavingsPlanId` **und** `ContactId == self` (Self-Contact), entsteht zusätzlich zum Bank- (`e.Amount`, mit `AccountId`) und Contact-Posting ein `PostingKind.SavingsPlan`-Posting mit **negiertem Betrag `-e.Amount`**, **ohne** `AccountId`, im selben `GroupId`. → Diese Plan-Postings sind exakt die „Funding-Seite" der Anforderung (Giro-Abgang −40 → Plan-Posting +40).
- Split-Draft-Pfad Z. 2817: identisches Muster für Split-Entries.
- `UpdateRecurringPlanIfDueAsync` (Z. 2383–2398): Terminfortschreibung.
- Preliminary-Buchungen: `StatementDraftService.Preliminary.cs` (vorläufige Postings werden beim finalen Buchen storniert/zeroed, Z. ~60–95).
- **Weitere Summenstellen auf Plan-Postings** (aktuell alle rein posting-basiert — fachlich dasselbe „current" wie `accumulatedAmount`):
  - Validierung `SAVINGSPLAN_GOAL_REACHED_INFO`/`GOAL_EXCEEDS`/`ARCHIVE_MISMATCH` (Z. ~2127–2140),
  - `SAVINGSPLAN_DUE`-Hinweis (Z. ~2160–2169),
  - Auto-Archivierung `current == target` nach Buchung (Z. ~2610).

## 5. Budgetbericht / Budgetauswertung — Bestätigung FA-4

- `BudgetSourceType` (`FinanceManager.Shared/Dtos/Budget/BudgetSourceType.cs`): `Contact=0`, `ContactGroup=1`, `SavingsPlan=2`.
- `FinanceManager.Infrastructure/Budget/BudgetPurposeService.cs` Z. 351–370: Ist-Werte für `SavingsPlan`-Purposes = `_db.Postings.Where(p => p.SavingsPlanId ∈ purposes)` gruppiert, **`Sum(-p.Amount)`** — Vorzeichenumkehr: negative Plan-Postings (Kosten) werden positive Actuals. **Kein `Kind`-Filter** — jedes Posting mit passender `SavingsPlanId` zählt.
- `BudgetReportService.BuildRealizationsAsync` (Z. 178–267): Realizations entstehen aus **Kontakt-Postings**; `SavingsPlanId` kommt direkt vom Posting oder via `GroupId`-Fallback vom Plan-Posting derselben Buchungsgruppe (Z. 231–235). Bankpostings werden nie als Plan-Actuals verwendet.
- `BudgetImpactEvaluationService` (Z. ~147, 241–243, 276): Draft-/Posting-Matching über `SavingsPlanId` — unverändert korrekt.
- **Konsequenz:** Solange keine Budget-Query die neue Join-Tabelle nutzt, bleibt die Budgetsicht automatisch unverändert (FA-4). **Aber Achtung:** Ein Istmeldung-Posting mit gesetztem `SavingsPlanId` fließt über den reinen `SavingsPlanId`-Filter automatisch in die Budget-Actuals ein — Vorzeichen/Fachlichkeit bei der Ausgestaltung der Posting-Art beachten (s. Lücken/Risiken).

## 6. API / Shared-DTOs / ApiClient

### 6.1 Controller

- `FinanceManager.Web/Controllers/SavingsPlansController.cs` (`api/savings-plans`): `GET` Liste/`count`/`{id}`/`{id}/analysis`, `POST`/`PUT`, `POST {id}/archive`, `DELETE {id}`, Symbol-Endpunkte (`POST {id}/symbol/{attachmentId}`, `DELETE {id}/symbol`, `POST {id}/symbol` Upload). **Keine Link-/Istmeldung-Endpunkte.**
- `FinanceManager.Web/Controllers/PostingsController.cs` (`api/postings`): `GET {id}` (Ownership via Account/Contact/SavingsPlan/Security — plan-gebundene Postings ohne AccountId sind abgedeckt, Z. 66–69), `POST {id}/reverse`, `GET {id}/validate-reversal`, Kontext-Listen (`account/`, `contact/`, `savings-plan/`, `security/`, `group/`), Exporte (`.../export`), Aggregate (`api/savings-plans/{id}/aggregates`, `api/accounts/{id}/aggregates` u. a., Z. 545–603).
- `FinanceManager.Web/Controllers/AccountsController.cs` (`api/accounts`): CRUD + `GET/POST/DELETE {id}/linked-ibans` (Z. 318–393) — **Referenz-Implementierung für verwaltete Sub-Listen auf einer Entität** (analog: `{id}/linked-accounts` auf SavingsPlans denkbar).

### 6.2 DTOs (`FinanceManager.Shared/Dtos/`)

- `SavingsPlans/SavingsPlanDto.cs` (Klasse, `CurrentAmount`/`RemainingAmount` nur in `GetAsync` befüllt; `ListAsync` setzt 0), `SavingsPlanAnalysisDto.cs` (positional record mit den Schedule-Feldern), `SavingsPlanRequests.cs` (`SavingsPlanCreateRequest`, keine Account-Felder).
- `Postings/PostingServiceDto.cs`: positional record mit `IsReversed`/`IsReversal`/`IsPreliminary` — **kein `informal`**; neue Felder müssen an allen Konstruktor-Aufrufstellen nachgezogen werden (`PostingsQueryService` ×3–4, `PostingsController.GetById`).
- `Accounts/AccountDto.cs`: `IsCollectionAccount`, `LinkedIbans`, `SavingsPlanExpectation`, `CurrentBalance`.

### 6.3 Abfrageschicht

- `FinanceManager.Application/Postings/IPostingsQueryService.cs` — Anwendungsabstraktion; `FinanceManager.Web/Services/IPostingsQueryService.cs` erbt davon (leer), Implementierung `FinanceManager.Web/Services/PostingsQueryService.cs`; DI `ProgramExtensions.cs:190–191`.
- `GetSavingsPlanPostingsAsync` (Z. 325–404): `Kind == PostingKind.SavingsPlan && SavingsPlanId == planId`, Owner-Prüfung, Paging/Suche, Text-Fallback über `StatementEntries`. Hier wäre die `informal`-Kennzeichnung für API/MCP abzuleiten (Gruppen-Join auf Bank-Posting).

### 6.4 ApiClient

- `FinanceManager.Shared/ApiClient.SavingsPlans.cs` + `IApiClient` (Z. ~760–930): alle Plan-Endpunkte als Client-Methoden; neue Endpunkte (Link setzen/entfernen, Istmeldung) müssen hier gespiegelt werden — Integrationstests testen über genau diesen Client (`TestWebApplicationFactory`).

## 7. MCP-Tools (`FinanceManager.Web/Mcp/Tools/`)

- `FmPostingsQueryTool.cs` (`fm_postings_query`, lesend): Kontexte account/contact/savingsPlan/security; Ausgabeobjekt Z. 130–165 — **hier gehört `informal: true` hin**; Record-Scope-Filter via `McpRecordContext` (Anchors in `IMcpScopeEvaluator.cs:29–60`; Dimensionen: AccountTypes, Kontakt-/Wertpapier-/Sparplankategorien).
- `FmSavingsPlanAnalysisTool.cs` (`fm_savings_plan_analysis`, lesend): liefert `analysis.accumulatedAmount` etc. (Z. 126–141). **Die Descriptor-Description (Z. 34–42) dokumentiert die Posting-Semantik sehr detailliert — muss für den Konto-Link-Modus fachlich angepasst werden.**
- `FmSavingsPlansManageTool.cs` (`fm_savings_plans_manage`, schreibvorbereitend): ops create/update/archive — möglicher Ort für link/unlink (oder neues Tool); Scopes `(SavingsPlans, Create|Modify)`, `(SavingsPlanCategories, Read)`; für Link-Verwaltung zusätzlich `(Accounts, Read)` nötig.
- `FmAccountsManageTool.cs` / `FmAccountsOverviewTool.cs`: Konten-CRUD/Übersicht inkl. `currentBalance`, `isCollectionAccount` (Ausgabe Z. 105–115).
- `FmPostingReverseTool.cs`/`FmPostingReversalCheckTool.cs`: Reversal über MCP — funktioniert derzeit nur für Postings mit `AccountId` (s. 8.4).
- `McpDataType` (`Domain/Mcp/McpDataType.cs`): `Accounts`, `Postings`, `SavingsPlans` vorhanden — keine neuen Scope-Typen zwingend nötig.

## 8. Weitere betroffene Komponenten

### 8.1 `PostingAggregateService` (`Infrastructure/Aggregates/PostingAggregateService.cs`)

- `UpsertForPostingAsync`: switch auf `posting.Kind` — nur die 4 bekannten Kinds werden aggregiert (`SavingsPlan` → `SavingsPlanId`-Aggregat). Ein **neuer `PostingKind` würde ohne Erweiterung nicht in die Aggregat-/Chart-Pipeline** (`/api/savings-plans/{id}/aggregates`) einfließen; ein Flag auf `Kind == SavingsPlan` funktioniert dagegen ohne Anpassung.
- `RebuildForUserAsync`: rechnet u. a. `CurrentBalance` der Konten aus Bank-Postings neu (≈ Z. 222–237) — die Ist-Quelle für konto-basierte `accumulatedAmount`-Werte ist damit konsistent wartbar.

### 8.2 `PostingReversalService` (`Infrastructure/Postings/PostingReversalService.cs`)

- `GetPostingOwnerUserIdAsync` (Z. 338–353): Owner-Auflösung **ausschließlich über `AccountId`** — ein konto-loses Plan-Posting (Istmeldung) ist heute **nicht stornierbar** (`InvalidOperationException`); `CreateReversalStatementDraftAsync` (Z. 253–258) wirft ebenfalls ohne `AccountId`. Gruppen-Reversal würde Plan-Postings in einer Buchungsgruppe korrekt mitstornieren, sobald ein Bank-Posting der Gruppe gestartet wird.
- Reversal erzeugt Gegenposting gleichen `Kind`, negierter Betrag (Z. 216–241) — funktioniert für jede Posting-Art.

### 8.3 Reports/Exporte (sekundäre Ausgabepfade)

- `Infrastructure/Reports/PostingExportService.cs` (Z. ~147, 306): CSV/XLSX-Export filtert Plan-Postings über `SavingsPlanId`/`Kind`.
- `PostingTimeSeriesService.cs` (Z. ~71–130), `ReportAggregationService.cs` (u. a. Z. 180, 326, 439–453): Kind-basierte Gruppierungen (`SupportsCategories` erlaubt nur Contact/SavingsPlan/Security).
- `NavigationManagerExtensions.cs:198`: `PostingKind` → Route-Mapping für Posting-Detail-Navigation.

### 8.4 UI (`FinanceManager.Web`)

- `ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` (`[CardRoute("savings-plans")]`): Card-Record aus `CardField`s (Z. 425–475), Ribbon-Gruppen Navigation/Manage/Linked/Analysis (Z. 594–677), `ChartViewModel` auf `…/aggregates`. **Ort für Konto-Link-Verwaltung (Lookup/Liste) und Ribbon-Aktion „Istmeldung"** — Präzedenzfall für Sub-Entity-Verwaltung: `BankAccountCardViewModel` verwaltet `LinkedIbans` inkl. Eingabefeld (Z. ~41–55, 89).
- `ViewModels/Postings/SavingsPlanPostingsListViewModel.cs` → Route `/list/postings/savings-plan/{id}` (Factory `ListViewModelFactory.cs:175–178`); `BasePostingsListViewModel.cs:112–120` zeigt Flag-Spalten (Vorläufig ✓, Storno ✓/—) — Vorbild für eine „informal"-/Istmeldung-Kennzeichnung.
- `ViewModels/Postings/Common/PostingsCardViewModel.cs` (Z. 90–98): Posting-Detailkarte mit `Kind`-Anzeige.
- `ViewModels/SavingsPlans/SavingsPlansListViewModel.cs` + `SavingsPlanListItem.cs`: Liste nutzt `SavingsPlanAnalysisDto` pro Plan (`_analysisByPlan`, Z. ~32, 263–271) — konto-basierter `accumulatedAmount` käme hier über denselben Endpunkt automatisch an.

### 8.5 Sonstige Konsumenten

- `FinanceManager.Infrastructure/Demo/DemoDataService.cs` (Z. ~253 ff.): erzeugt Demo-Pläne/-Postings — optionaler Erweiterungspunkt (kein Muss).
- `FinanceManager.Infrastructure/Reports/HomeKpi*`: kein SavingsPlan-Bezug.
- `Attachments`: `AttachmentEntityKind.SavingsPlan` existiert; unverändert nutzbar.

## 9. Backup / Restore / Serialisierung

- `FinanceManager.Infrastructure/Backups/BackupService.cs`:
  - `RequiredDataProperties` (Z. 26–52): Pflicht-Array-Whitelist der NDJSON-Payload — ein neuer Pflichtabschnitt würde **alte Backups invalid** machen; optionaler Abschnitt (wie `McpAccesses` etc., Z. 778–784) ist der kompatible Weg.
  - `BuildBackupDataAsync` (Z. 568–786): Export je Entität über `ToBackupDto()`; Postings werden über Referenz auf Owner-Entities eingesammelt (Z. 640–645) — eine Istmeldung mit gesetztem `SavingsPlanId` würde **automatisch** exportiert.
  - Payload `Version = 3`, `AllowedBackupVersions = [3]` — neue optionale Sektionen brauchen keine Versionserhöhung.
- `FinanceManager.Infrastructure/Setup/SetupImportService.cs` (1340 Z.): Restore mit **ID-Remapping** — Pläne neu angelegt (Z. ~396–416, `savingsMap`), Accounts (`accountMap`), Postings gemappt (Z. ~576–632). Die Join-Entity braucht einen eigenen Import-Block, der beide Maps nutzt. Neues Feld am `PostingBackupDto` muss im Import-Konstruktor/`AssignBackupDto` mitgeführt werden.
- `AccountLinkedIban.AccountLinkedIbanBackupDto` existiert, wird aber **nicht exportiert** — Beleg dafür, dass ein `*BackupDto` allein nicht reicht; Export + Import müssen explizit ergänzt werden.

## 10. Vorhandene Konventionen

- **Schichten:** `Application` (Interfaces), `Infrastructure` (EF-Core-Services auf `AppDbContext`), `Domain` (reine Entities, private Setter, Guards), `Shared` (DTOs/Enums/ApiClient), `Web` (Controller, ViewModels, MCP-Tools). Domain referenziert Shared-Enums (`PostingKind`, `SavingsPlanType` …).
- **Owner-Scoping:** überall per `OwnerUserId`-Prüfung in Service-Queries; Unique-Indizes sind owner-scoped (`(OwnerUserId, Name)`).
- **Fehler:** Services werfen `ArgumentException`/`InvalidOperationException`; Controller mappen via `ApiErrorFactory` auf 400/404 (Muster in `SavingsPlansController`).
- **Neue Entities:** `Entity`-Basisklasse (`Id`, `CreatedUtc`, `ModifiedUtc`, `Touch`, `SetDates`), `*BackupDto`-Record + `ToBackupDto`/`AssignBackupDto`, `DbSet` + Fluent-Config in `OnModelCreating`, Migration, Backup-Export + SetupImport-Block.
- **MCP-Tools:** Descriptor (JSON-Schema-String, `McpToolSecurityClass`, `RequiredScopes`) + Handler (`IMcpToolHandler`), Audit-Staging via `McpWriteAudit.StageDataChangeAsync`, untrusted Wrapper für Nutztexte, Record-Scope-Filter über `McpRecordContext`.
- **Enums in Shared:** neue Enum-Werte sind abwärtskompatibel serialisierbar (int), aber ältere Clients kennen den Wert nicht → für neue Posting-Art ist ein **Enum-Member** einfacher als ein komplett neuer Typ, solange bestehende `Kind`-Filter ihn mitfassen sollen.

## 11. Lücken und Risiken

1. **Kein Posting-Create-Endpunkt:** Die Istmeldung braucht einen neuen Schreibpfad (voraussichtlich `POST /api/savings-plans/{id}/...`), der ein Plan-Posting ohne `AccountId`/`GroupId`-Bank-Gruppe erzeugt — neuer Codepfad, heute ohne Vorbild (Postings entstehen sonst nur im Buchungsprozess/Reversal).
2. **Owner-Auflösung storno-seitig nur über `AccountId`** (`PostingReversalService.cs:338–353`, `253–258`): konto-lose Plan-Postings sind aktuell nicht stornierbar (auch nicht via MCP). Anforderung fordert kein Storno der Istmeldung; falls gewünscht, Owner-Fallback über `SavingsPlanId` nötig.
3. **Buchungsvalidierung/Auto-Archiv nutzen Plan-Posting-Summen** (`StatementDraftService` Z. ~2127–2169, ~2610): Bei verknüpften Plänen weicht „current" vom Kontosaldo ab → `SAVINGSPLAN_GOAL_*`, `SAVINGSPLAN_DUE` und Auto-Archiv (`current == target`) können falsch bewerten. Fachlich klären, ob diese Checks auf den Kontosaldo umschalten oder bewusst posting-basiert bleiben.
4. **Istmeldung fließt automatisch in Budget-Actuals** (reiner `SavingsPlanId`-Filter, `BudgetPurposeService` Z. 351–370): konsistent mit „Plan-Postings = Kostensicht", aber fachlich zu entscheiden, ob eine Saldenkorrektur ein Budget-Ist verändern darf; Vorzeichen (-Amount-Konvention) beachten.
5. **Neuer `PostingKind` vs. bestehende `Kind == SavingsPlan`-Filter:** Viele Stellen filtern hart auf `PostingKind.SavingsPlan` (Analyse, PostingsQueryService, Exporte, MCP-Scope-Filter `p.Kind`, Aggregate-Switch, Navigation). Ein neuer Kind-Wert wäre überall „unsichtbar", bis alle Stellen erweitert sind — alternativ Flag/SubType auf `SavingsPlan`-Kind (s. Designoptionen).
6. **`Posting` ohne `OwnerUserId`:** Join-Entity-Design sollte `OwnerUserId` redundant führen (Owner-Prüfung, Backup-Einsammlung, Indizes) — Requirement erlaubt „ableitbar", aber redundant ist konsistenter mit allen anderen Entities.
7. **Kein `SavingsPlanId`-Index auf `Postings`:** neue Auswertungsrichtungen (Plan↔Konto-Inflow-Queries) ggf. Index-Bedarf.
8. **Doppelte Migrationsverzeichnisse** (s. 2.5): Ort für die neue Migration abstimmen.
9. **Descriptor-/Doku-Texte:** `FmSavingsPlanAnalysisTool`-Description beschreibt die Posting-Semantik verbindlich — bei geändertem `accumulatedAmount` muss der Tool-Vertragstext mitziehen (MCP-Konsumenten lesen ihn).
10. **`SavingsPlanExpectation`-Begriffskollision:** bestehende Konto↔Plan-Kopplung anderer Semantik — Naming der neuen Verknüpfung so wählen, dass kein Mix entsteht.
11. **`AccountLinkedIban` nicht im Backup** — bestehende Lücke; bei der neuen Join-Entity nicht wiederholen (Akzeptanzkriterium 10).

## 12. Wiederverwendbare Bausteine

- **`PostingObservation`/`ComputeSchedule`** (`SavingsPlanService`): komplett quellagnostisch — Konto-Inflow-Observations (positive `Bank`-Postings verknüpfter Konten) können eins-zu-eins eingespeist werden.
- **`GroupId`** auf `Posting`: verbindet Plan-Posting und Funding-Bank-Posting — die natürliche Datenbasis zur **Ableitung** von `informal` (Plan-Posting in Gruppe mit Bank-Posting auf nicht-verknüpftem Konto).
- **`Account.CurrentBalance`/`AdjustBalance` + Aggregat-Rebuild**: gepflegte Ist-Salden, exakt die geforderte Quelle.
- **`AccountLinkedIban`-Muster** (Entity + `linked-ibans`-Endpunkte + UI-Verwaltung): Blaupause für Join-Entity, REST-Subressourcen und Card-UI.
- **`SavingsPlan`-Backup-/Restore-Muster + optionale Backup-Sektionen** (MCP-Entities als Vorbild für abwärtskompatible Payload-Erweiterung).
- **`McpRecordContext.SavingsPlanId`-Anchor + Scopes**: MCP-seitig ist kein neuer Datentyp nötig.
- **`Flag`-Anzeige in Posting-Listen** (`BasePostingsListViewModel` ✓/—-Spalten): Anzeigeort für informal/Istmeldung.
- **`ISavingsPlanService`** als zentraler Erweiterungspunkt (Analyze/Get/List); `IParentAssignmentService`-Pattern für Nebenaktionen nach Create.

## 13. Designoptionen

### 13.1 „informal" (FA-2)

**Option A — Ableitung zur Auswertungszeit (empfohlen, deckt sich mit Requirement):**
Plan-Posting gilt als informell, wenn der Plan ≥1 Konto-Link hat **und** das Posting eine Funding-Buchung dokumentiert (heuristisch: `GroupId` enthält ein `Bank`-Posting, ggf. auf nicht-verknüpftem Konto).
+ Keine Schemaänderung am `Posting`, keine Migration historischer Daten (Requirement-Nichtziel erfüllt), automatisch konsistent beim Setzen **und Entfernen** des Links (Ablauf 2 fällt gratis mit).
+ „Informalität als Auswertungseigenschaft" wörtlich umgesetzt.
− Ableitungslogik muss an drei Orten konsistent sein: `AnalyzeAsync`-Filter, `PostingServiceDto.informal` (`PostingsQueryService`), MCP-Ausgabe; Grenzfälle (Plan-Posting ohne Bank-Gruppe: Istmeldung, Darlehensauszahlung ohne Gegenposting) müssen sauber als „nicht informell" klassifiziert werden.
− Gruppen-Join nötig (je Plan-Posting ein Lookup auf Gruppen-Bankposting).

**Option B — Persistiertes Flag `Posting.IsInformal`:**
+ Triviale Queries/Ausgabe.
− Widerspricht „keine Umbuchung/Auswertungseigenschaft": beim Setzen/Entfernen des Links müssten Flags rückwirkend umgeschrieben werden (genau die verbotene Datenmigration); Flag wird bei Link-Änderungen stale oder erfordert Maintenance-Logik. Nicht empfohlen.

**Option C — Neues Zuordnungs-/Rollenfeld am Plan-Posting** (z. B. `SavingsPlanPostingKind`: Contribution/Disbursement/Adjustment):
+ Klassifiziert Funding vs. Auszahlung vs. Korrektur explizit — könnte zugleich die Istmeldung markieren (Synergie mit 13.2-Option B).
+ `informal` bliebe Ableitung „Rolle == Contribution && Plan verknüpft".
− Schema-/Migrationsaufwand am Posting; Altdaten ohne Rolle → Mapping-Regeln nötig (z. B. Betrag > 0 → Contribution); komplexer als nötig, wenn nur FA-2 gefordert ist.

### 13.2 „Istmeldung" (FA-3)

**Option A — Neuer `PostingKind`-Wert (z. B. `SavingsPlanAdjustment`):**
+ In Listen/Audit/MCP sofort als eigene Art unterscheidbar (AK „klar unterscheidbare Kennzeichnung" wörtlich).
− **Hohe Kaskade:** alle `Kind == PostingKind.SavingsPlan`-Filter müssen angefasst werden, damit die Istmeldung in `AnalyzeAsync`-History, `GetSavingsPlanPostingsAsync`, Exporten, MCP-Scope-Filter und `PostingAggregateService`-Switch sichtbar bleibt — sonst zählt sie nicht zu `accumulated` (Requirement-Bruch). Zusätzliche Enum-Wert-Behandlung in `switch`-Statements überall.

**Option B — `Kind == SavingsPlan` + Marker-Flag** (z. B. `IsAdjustment`/`ActualReport` oder `SavingsPlanPostingSubType`):
+ Läuft automatisch durch **alle** bestehenden Plan-Queries (Analyse, Liste, Budget, Aggregate, Backup) — `accumulated`-Korrektur funktioniert ohne Umbau; Unterscheidbarkeit über Flag in Ausgabe/Audit; `AccountId == null` erfüllt „keine Kontobewegung" strukturell.
+ Kompatibel mit Option C aus 13.1 (ein gemeinsames SubType-/Rollenfeld).
− Flag ist weniger „sichtbar" als eigener Kind — Ausgabeschichten müssen es als Korrektur labeln; neue Spalte + Backup-DTO-Feld.

**Option C — Konvention ohne neues Feld** (z. B. `GroupId == Empty` + `SourceId`-Konvention oder Subject-Präfix):
− Implizit, fehleranfällig, nicht auditfest. Nicht empfohlen.

**Querschnitt (unabhängig von A/B):**
- Erzeugung über neuen Endpunkt am Sparplan (Service-Methode `ReportActualAsync`/`AdjustAsync` o. ä.): Betrag = `gemeldeter Ist − aktuelles accumulated`; nur erlaubt bei Plan ohne Links (Service-Validierung, owner-scoped).
- Reversal: aktuell nicht möglich (Owner nur über `AccountId`) — Korrektur erfolgt per weiterer Istmeldung; falls Reversal gewünscht: Owner-Fallback `SavingsPlanId` in `PostingReversalService` ergänzen.

## 14. Empfohlene Testorte

- **Unit/Infra (xUnit + SQLite in-memory, `EnsureCreated`):** `FinanceManager.Tests/Savings/` — Muster `SavingsPlanAnalysisTests`, `SavingsPlanScheduleDeficitTests`, `SavingsPlanServiceTests` (Factory `Create()` mit `SqliteConnection("DataSource=:memory:")` + `AppDbContext`). Geeignet für: Link-CRUD/Owner-Scoping/Duplikat-Abweisung, `accumulatedAmount` aus Kontosalden (Multi-Link, Sammelkonto), Inflow-basierte `scheduleDeficit`, informal-Ausschluss aus `accumulated`/`expectedToDate`, Istmeldung-Differenzbuchung + Verbot bei Links.
- **Backup/Restore:** `FinanceManager.Tests/Infrastructure/` — `BackupServiceTests`, `BackupServiceFullExportTests`, `SetupImportServiceTests`: Join-Verknüpfungen + Istmeldung-Postings überleben Export→Import (ID-Remapping!).
- **API-Integration:** `FinanceManager.Tests.Integration/ApiClient/` über `TestWebApplicationFactory` + `ApiClient` — `ApiClientSavingsPlansTests`, `ApiClientPostingsTests`, `ApiClientBackupsTests` als Vorlagen; neue Endpunkte (Link setzen/entfernen, Istmeldung, `informal` im Postings-Response).
- **Controller-/ViewModel-Tests:** `FinanceManager.Tests/Controllers/` bzw. `FinanceManager.Tests/ViewModels/` (`PostingsSavingsPlanViewModelTests`, `SavingsPlanEditViewModelTests`): Ribbon-Aktion „Istmeldung" (nur ohne Link aktiv), Link-Verwaltung, Anzeige der Kennzeichnung.
- **MCP-Tool-Tests:** `FinanceManager.Tests/Mcp/` (`McpReadOnlyToolsTests`, `McpMasterDataWriteToolsTests`): `informal`-Feld in `fm_postings_query`, angepasste `fm_savings_plan_analysis`-Ausgabe.
- **E2E (optional):** `FinanceManager.Tests.E2E/` (Playwright) — nur falls UI-Flows abgesichert werden sollen.
- **Budget-Regression:** `FinanceManager.Tests/Infrastructure/Budget/` — sicherstellen, dass Links/informal die Actuals aus `BudgetPurposeService`/`BudgetReportService` nicht verändern (FA-4, AK 9).

## 15. Zentrale Umbau-Stellen (Orientierung)

| Concern | Primäre Dateien |
|---|---|
| Join-Entity n:m Plan↔Konto | `Domain/Savings/` (neu), `AppDbContext.cs` (DbSet+Config), Migration, `BackupService`, `SetupImportService` |
| `accumulatedAmount`/Analyse-Umschaltung | `SavingsPlanService.cs` (`GetAsync` Z. 70–72, `AnalyzeAsync` Z. 221–264) |
| `informal`-Ableitung + Ausgabe | `PostingsQueryService.cs` (Z. 325–404), `PostingServiceDto.cs`, `FmPostingsQueryTool.cs` (Z. 130–165), ggf. `PostingExportService` |
| Istmeldung-Erfassung | `ISavingsPlanService`/`SavingsPlanService` (neue Methode), `SavingsPlansController` (neuer Endpunkt), `Posting` (Marker), `ApiClient`/`IApiClient` |
| UI | `SavingsPlanCardViewModel.cs` (Link-Verwaltung + Istmeldung-Aktion), `BasePostingsListViewModel.cs`/`PostingsCardViewModel.cs` (Kennzeichnung) |
| Budget-Unverändertheit (Kontrolle) | `BudgetPurposeService.cs` Z. 351–370, `BudgetReportService.cs` Z. 178–267 |
| Nebenwirkungen zu prüfen | `StatementDraftService.cs` Z. ~2127–2169, ~2610 (Validierung/Auto-Archiv), `PostingReversalService.cs` Z. 253–258, 338–353, `PostingAggregateService.cs` (Kind-Switch), `FmSavingsPlanAnalysisTool.cs` (Description) |
