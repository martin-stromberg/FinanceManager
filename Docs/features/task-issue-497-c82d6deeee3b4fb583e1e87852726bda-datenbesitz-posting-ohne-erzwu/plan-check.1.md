# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `new Posting(...)` ohne jede der vier Referenzen schlägt sofort mit `ArgumentException` fehl (Fail-fast) | Schritt 1: Inline-Guard im Hauptkonstruktor `Posting.cs` Z. 108–141; alle fünf Überladungen delegieren per `: this(...)` dorthin — verifiziert am Code (Z. 23, 54, 88, 161) | `PostingReferenceInvariantTests.Constructor_Throws_WhenAllReferencesAreNull` (Schritt 2) | Abgedeckt |
| Invariante ist „mindestens eine", nicht „genau eine" Referenz | Designentscheidung begründet mit `BackupServiceFullExportTests.cs:183` (Posting mit allen vier Referenzen) | `Constructor_Succeeds_WhenMultipleReferencesSet` | Abgedeckt |
| `INSERT`/`UPDATE` mit vier `NULL`-Referenzen wird auf DB-Ebene abgelehnt (Check-Constraint) | Schritte 5–6: `HasCheckConstraint("CK_Postings_HasReference", "[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL")` in `AppDbContext` Z. 513–519 + EF-Migration. SQLite-Syntax mit eckigen Klammern korrekt; EF Core erzeugt für `AddCheckConstraint` unter SQLite tatsächlich einen Table-Rebuild | `PostingCheckConstraintTests` (Schritt 7) + realer Nachweis Schritt 8 (`dotnet ef database update` + INSERT-Probe) | Abgedeckt |
| Restore-Pfad erzeugt kein referenzloses Posting (Lücke aus inventory/logic.md) | Schritt 3: Skip in `ImportVersion3` Z. 720–753 bei vier `null`-remappten Referenzen (verifiziert am Code Z. 723–726) | `SetupImportServiceTests.ImportAsync_SkipsPosting_WhenAllRemappedReferencesAreNull` (Schritt 4) — Testsetup (SQLite `:memory:` + `EnsureCreated` + Moq für `IStatementDraftService`/`IPostingAggregateService`) exakt wie vorhandene Tests in `SetupImportServiceTests.cs` Z. 35–98 vorhanden und machbar | Abgedeckt |
| `AssignBackupDto` umgeht Guard → Durchsetzung über DB-Constraint (Scope-Ausschluss 5) | Bewusster Ausschluss mit Status `bestätigt`; Constraint als zweite Verteidigungslinie | `SaveChanges_Throws_WhenAllReferencesSetToNull`: `AssignBackupDto` ist `public` (Posting.cs Z. 523), `PostingBackupDto` ist positional record — DTO per `ToBackupDto() with { AccountId = null, … }` erzeugbar, Referenzen-Nullsetzen via `AssignBackupDto` technisch machbar | Abgedeckt |
| XML-Doc: `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings` als globaler Datenpool gekennzeichnet | Schritt 9 (Z. 177–179) + Klassenabschnitte; AGENT.md-konform | Diff-Review (Akzeptanzszenario 6) | Abgedeckt |
| Globalitäts-Dokumentation zusätzlich unter `Docs/` (AGENT.md-Anforderung) | Schritt 10: drei `Docs/`-Dateien (`systemverwaltung-und-setup/datenmodell.md`, `mcp-server/datenmodell.md`, `konten-und-buchungen/datenmodell.md` inkl. Posting-Invariante) | Diff-Review (Akzeptanzszenario 6) | Abgedeckt |
| Kein `OwnerUserId` auf `Posting`; Besitzerpfad bleibt indirekt | Scope-Ausschluss 1, Status `bestätigt` | — | Abgedeckt |
| Keine `PostingAggregate`-Änderung | Scope-Ausschluss 2, Status `bestätigt`; Folgelücke (Kind/Referenz-Inkonsistenz → verwaistes Aggregat) als Seiteneffekt benannt | — | Abgedeckt |
| Keine Datenbereinigung; Migration schlägt bei verletzten Bestandsdaten fehl (Fail-loud) | Scope-Ausschluss 3 + Seiteneffekt + Akzeptanzszenario 5; `db.Database.Migrate()` beim App-Start (`ProgramExtensions.cs` Z. 820, verifiziert) | Schritt 8 | Abgedeckt |
| Keine BackupDTO-/Schema-Erweiterung; alte Backups lesbar | Scope-Ausschluss 4, Status `bestätigt`; Posting-Sektion ist `TryGetProperty`-guarded (Z. 715, verifiziert) | Bestandssuite als Regression | Abgedeckt |
| `EnumCoverageTests` nicht betroffen (kein neues Enum-Mitglied) | implizit — inventory/enums.md stellt fest, keiner der fünf betroffenen Enums ist im Coverage-Test | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — alle Akzeptanzkriterien haben konkrete Testnachweise; Testsetups wurden am Bestandscode verifiziert (SQLite-`:memory:` + `EnsureCreated` in `SetupImportServiceTests.cs` Z. 35–39; `AssignBackupDto` public, DTO via `with` manipulierbar).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Posting-Erzeugung, Check-Constraint, XML-Doc | — | Nicht erforderlich: kein UI-Element, keine Navigation, kein Nutzerfluss betroffen — Begründung im Plan trägt |
| Backup-Restore mit referenzlosem Posting (Skip-Verhalten) | — | Grenzwertig, siehe Hinweis: Der Setup-Import (`SetupImportService.ImportAsync`) wird über einen UI-Upload ausgelöst, der Fehlerpfad ist also streng genommen „über Nutzeraktion erreichbar". Die Begründung trägt aber in der Sache: Der sichtbare Effekt (Import schließt erfolgreich ab) ist mit und ohne Fix identisch; der Unterschied liegt nur auf DB-Ebene und ist einem E2E-Test nicht von außen verifizierbar. Zudem sind Playwright-Browser in dieser Umgebung nicht installiert. Integrationstest `ImportAsync_SkipsPosting_…` ist der fachlich sinnvolle Nachweis. |

## Fehlende oder unvollständige Planbestandteile

- [ ] **`dotnet ef`-Schritte ohne Startup-Projekt (Schritte 6 und 8):** `AppDbContext` hat nur den Konstruktor `AppDbContext(DbContextOptions<AppDbContext>)` (Z. 40), `OnConfiguring` setzt keinen Provider (Z. 957–960, nur Warnungskonfiguration), und es existiert **keine `IDesignTimeDbContextFactory`** im Repo. `dotnet ef` kann den Context damit ohne Startup-Projekt (vermutlich `FinanceManager.Web`, das `FinanceManager.Infrastructure` referenziert) nicht instanziieren — die angegebenen Befehle `dotnet ef migrations add … --project FinanceManager.Infrastructure` und `dotnet ef database update …` schlagen ohne `--startup-project` bzw. ohne Ausführung aus dem Web-Projektverzeichnis fehl. Notwendige Voraussetzung fehlt im Plan.
- [ ] **Zweiter Posting-Pfad in `ImportVersion3` nicht analysiert (Z. 1309–1332):** Der Plan beschreibt den Restore-Pfad nur als Erzeuger-Block Z. 720–753. Es existiert jedoch ein „apply"-Block, der dieselbe `Postings`-Sektion erneut iteriert, die vier Referenzen identisch einzeln remappt (Z. 1316–1319, null bei nicht gemapptem Ziel) und auf dem geladenen Entity `entity.AssignBackupDto(adjusted)` aufruft (Z. 1322–1325) — also genau der Guard-umgehende Pfad, den Scope-Ausschluss 5 und die „zweite Verteidigungslinie" adressieren. Zudem widerspricht die Stelle der Behauptung in `inventory/logic.md` Z. 55 („`AssignBackupDto` wird im selben Service für Postings nicht verwendet"). Faktisch bleibt die geplante Umsetzung korrekt — das Remapping ist in beiden Blöcken identisch (≥1 Referenz in Block 1 ⇒ ≥1 Referenz in `adjusted`), und ein in Block 1 geskipptes DTO fehlt in `postingMap`, sodass `TryGetValue` (Z. 1322) den apply-Schritt automatisch überspringt —, aber der Plan sollte diese Konsistenz explizit feststellen und benennen, dass für geskippte Postings auch kein apply-Update erfolgt (gewünscht) sowie dass `StatementEntry.SecurityTransactionId` bei geskippten Postings die Original-Backup-Id behält (Z. 789, harmlos da keine FK — dennoch erwähnenswert).

## Hinweise

- Die E2E-Begründung sollte präzisiert werden: nicht „kein über UI erreichbarer Ablauf geändert" (der Import ist UI-erreichbar), sondern „das geänderte Defensiv-Verhalten erzeugt keinen nutzersichtbaren Unterschied und ist nur auf DB-Ebene verifizierbar".
- Seiteneffekt-Aussage „`EnsureCreated`-Pfade: … Alt-Installationen ohne Migrationshistorie erhalten den Constraint beim Schemaaufbau automatisch" gilt nur für **neu** erzeugte DBs. Bestehende `EnsureCreated`-DBs mit Daten erhalten den Constraint nicht (`EnsureCreated` läuft nicht erneut; `Migrate()` scheitert dort bereits heute, vgl. `ProgramExtensions.cs` Z. 833–836) — fachlich unkritisch, da der Domain-Guard greift, aber die Formulierung ist ungenau.
- Positiv verifiziert (keine Befunde): Check-Constraint-SQL ist valides SQLite; EF Core erzeugt unter SQLite für `HasCheckConstraint` auf einer Bestandstabelle einen Table-Rebuild; die EF-Materialisierungs-Analyse (EF bindet den Hauptkonstruktor — `Posting` hat keinen parameterlosen Konstruktor, verifiziert Z. 22–161 — und der Guard würde beim Lesen verletzter Zeilen werfen) ist korrekt und der Seiteneffekt dokumentiert; `PostingBackupDto` hat Default-Werte für neue Parameter (Z. 509) — alte Backups bleiben deserialisierbar; `postingMap` wird ausschließlich im Erzeuger-Block befüllt (Z. 238 deklariert, Z. 748/755 gefüllt), sodass kein Merge-Pfad existiert, der Skip und apply auseinanderlaufen lässt.
