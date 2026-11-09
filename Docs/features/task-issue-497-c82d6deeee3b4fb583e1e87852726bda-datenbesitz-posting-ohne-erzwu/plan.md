# Umsetzungsplan: Datenbesitz — Posting-Referenzinvariante erzwingen, globale Pools explizit dokumentieren (Issue #497)

## Übersicht

Zwei Qualitätsmaßnahmen zur Durchsetzung des Datenbesitz-Prinzips aus `AGENT.md`: (1) Die Entität `Posting` erhält die erzwungene Invariante „mindestens eine der vier Zielreferenzen (`AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId`) ist gesetzt" — abgesichert durch einen Guard im Domain-Hauptkonstruktor und einen DB-Check-Constraint per EF-Migration; der Restore-Pfad in `SetupImportService` überspringt referenzlose Backup-Postings statt sie anzulegen. (2) Die bewusst benutzerübergreifenden Entitäten `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings` und `McpSettings` erhalten explizite Globalitäts-Hinweise in XML-Dokumentation und unter `Docs/`. Betroffen sind `FinanceManager.Domain`, `FinanceManager.Infrastructure` (inkl. neuer EF-Migration), `Docs/` und `FinanceManager.Tests`. Keine UI- und keine Verhaltensänderung für Anwender.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Domänenentität `Posting` (Aggregatwurzel, Buchungszeile) sowie die globalen Einstellungs-/Sicherheitsentitäten `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings` (und die XML-Doc-Präzisierung an `McpSettings`). |
| Auslösende Ereignisse | Kein Nutzer- oder Systemereignis: die Invariante greift bei jeder Erzeugung eines `Posting` (Konstruktoraufruf in Services, Tests, EF-Materialisierung) und bei jedem `INSERT`/`UPDATE` auf der `Postings`-Tabelle (Check-Constraint). Beim Backup-Restore greift sie über die Skip-Logik in `SetupImportService.ImportAsync`. Die Dokumentationsänderung ist rein statisch. |
| Erwartete Wirkung | `new Posting(...)` mit vier `null`-Referenzen wirft sofort `ArgumentException` (Fail-fast). `INSERT`/`UPDATE` mit vier `NULL`-Referenzspalten wird auf Datenbankebene abgelehnt (`CK_Postings_HasReference`). Ein Backup-Posting, dessen sämtliche Referenzziele nicht remappt werden können, wird beim Import übersprungen. XML-Doc und `Docs/` kennzeichnen die vier globalen Entitäten explizit als benutzerübergreifenden Datenpool. |
| Umgang mit fehlenden Objekten | Keine neuen fachlichen Objekte erforderlich. **Backup-Posting ohne remappbare Referenzen → verwerfen** (Import überspringt es; ein solches Posting wäre nach Restore ein Waisendatensatz, den der Export ohnehin nie erzeugt hätte). Alle Produktiv-Erzeuger setzen bereits mindestens eine Referenz — die Invariante formalisiert den Ist-Zustand. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein `OwnerUserId` auf `Posting` | Der Besitzerpfad bleibt bewusst indirekt über die vier referenzierten Aggregate (die alle `OwnerUserId` tragen); der Issue fordert nur die Referenz-Invariante. | bestätigt (Orchestrator, 2026-11-09 — direkt aus Issue-Text und AGENT.md ableitbar) |
| 2 | Keine Änderung an `PostingAggregate`, obwohl es dasselbe optionale Vier-Referenzen-Muster trägt | Vom Orchestrator bestätigter Scope-Ausschluss — Issue benennt explizit nur `Posting`. Bekannte Folgelücke: ein (künftig unmögliches) referenzloses Posting könnte ein verwaistes Aggregat erzeugen; als Folgeaufgabe im Issue-/PR-Kommentar vermerkbar. | bestätigt (Orchestrator, 2026-11-09 — direkt aus Issue-Text und AGENT.md ableitbar) |
| 3 | Keine nachträgliche Datenbereinigung verwaister Alt-Postings | Per Analyse setzen alle bisherigen Erzeuger mindestens eine Referenz; verwaiste Bestandsdaten können damit nicht existieren. Falls eine Bestands-DB dennoch solche Zeilen enthält, schlägt die Migration fehl (gewünschtes Fail-loud statt stiller Datenkorrektur). | bestätigt (Orchestrator, 2026-11-09 — direkt aus Issue-Text und AGENT.md ableitbar) |
| 4 | Keine Erweiterung von `PostingBackupDto` / kein Backup-Schema-Change | Es kommen keine neuen persistierten Felder hinzu; die Invariante betrifft nur den zulässigen Wertebereich vorhandener Felder. Alte Backups bleiben lesbar. | bestätigt (Orchestrator, 2026-11-09 — direkt aus Issue-Text und AGENT.md ableitbar) |
| 5 | Kein Guard in `AssignBackupDto` | Die Methode bleibt bewusst ein direkter State-Mapper; die Invariante wird dort über den DB-Check-Constraint beim `SaveChanges` durchgesetzt (zweite Verteidigungslinie) — Festlegung des Orchestrators. | bestätigt (Orchestrator, 2026-11-09 — direkt aus Issue-Text und AGENT.md ableitbar) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ort des Referenz-Guards | Ein einziger Inline-Check im vollständigen `Posting`-Konstruktor (`Posting.cs` Z. 108–141) | Alle fünf Überladungen delegieren per `: this(...)` dorthin — ein Check deckt jeden Erzeugungsweg ab. Inline-`throw` statt neuem `Guards`-Helfer, weil der Check nur einmal benötigt wird und `SetGroup`/`SetParent` dasselbe Inline-Muster (`throw new ArgumentException`) verwenden. |
| Exception-Typ des Guards | `ArgumentException` | Bestehendes Muster: `Guards.NotEmpty`, `SetGroup`, `SetParent` werfen alle `ArgumentException`. |
| Invariante „mindestens eine" statt „genau eine" Referenz | OR-Bedingung über alle vier Referenzen | `BackupServiceFullExportTests.cs:183` erzeugt legitimerweise ein Posting mit allen vier Referenzen — „genau eine" wäre fachlich falsch. |
| Restore-Pfad (`SetupImportService`, Z. 723–740) | Posting überspringen, wenn alle vier remappten Referenzen `null` sind (vor dem `new Posting(...)`, mit `LogDebug`-Eintrag) | Konsistent zum `BackupService`-Exportfilter, der referenzlose Postings gar nicht exportiert — ein solcher Datensatz wäre nach Restore ein nutzloser Waisendatensatz. Überspringen statt Werfen verhindert, dass ein einzelner defekter Backup-Eintrag den kompletten Import abbricht. Festlegung des Orchestrators. |
| Zweiter Posting-Pfad in `ImportVersion3`: Apply-Block (Z. 1308–1332) | **Kein** zusätzlicher Vier-Null-Check vor `entity.AssignBackupDto(adjusted)` (Z. 1325) — der Block bleibt unverändert | Der Apply-Block remappt die vier Referenzen mit denselben Ausdrücken wie der Create-Block (Z. 1316–1319 = Z. 723–726) und greift nur auf Entitäten zu, die in `postingMap` liegen (`TryGetValue`, Z. 1322). `postingMap` wird ausschließlich im Create-Block befüllt (Z. 744 → Z. 748–749/755–756) — ein dort geskipptes DTO fehlt in `postingMap`, der Apply-Schritt überspringt es daher automatisch. Für nicht geskippte DTOs liefert das identische Remapping deterministisch denselben Zustand mit ≥ 1 Referenz; divergente Ergebnisse zwischen den Pfaden sind ausgeschlossen. Der Check-Constraint fängt theoretische Restfälle beim `SaveChanges` ab. Festlegung des Orchestrators. |
| Zweite Verteidigungslinie | `HasCheckConstraint("CK_Postings_HasReference", "[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL")` im `modelBuilder.Entity<Posting>`-Block | Verpflichtend laut Orchestrator-Festlegung: Der Constraint deckt den Guard-umgehenden `AssignBackupDto`-Pfad und die EF-Materialisierung ab und „erzwingt" die Invariante wirklich — Kern der Anforderung. SQLite unterstützt Check-Constraints; EF erzeugt den nötigen Table-Rebuild in der Migration. Eckige Klammern entsprechen dem Stil der bestehenden `HasFilter`-Ausdrücke (`AppDbContext.cs` Z. 641–652). |
| Formulierung der Globalitäts-Doku | Expliziter Satz „globaler, benutzerübergreifender Datenpool — bewusst ohne `OwnerUserId`" in `<remarks>` bzw. `<summary>` | `McpSettings` dokumentiert die Globalität nur implizit über „Singleton" — das sprachliche Vorbild sind daher `Notification.OwnerUserId` („when null the notification is global", `Notification.cs` Z. 17) und `McpAuditEntry.OwnerUserId` („null for global events", `McpAuditEntry.cs` Z. 104). `AGENT.md` verlangt die Kennzeichnung in der Entität (XML-Doc) **und** unter `Docs/` — beides wird erledigt. |

## Programmabläufe

### Posting-Erzeugung unter Referenzinvariante

1. Aufrufer (Service, Test oder EF-Materialisierung) ruft eine der fünf `Posting`-Konstruktor-Überladungen auf.
2. Alle Überladungen delegieren an den vollständigen Konstruktor (`Posting.cs` Z. 108–141).
3. Dort prüft der neue Guard nach `Guards.NotEmpty(sourceId, ...)`: Sind `accountId`, `contactId`, `savingsPlanId` **und** `securityId` alle `null`, wird `ArgumentException` geworfen — das Objekt entsteht nicht.
4. Ist mindestens eine Referenz gesetzt, läuft die Konstruktion unverändert weiter (Zuweisung der Eigenschaften Z. 126–141).

Beteiligte Klassen/Komponenten: `Posting`, `Guards` (unverändert als Vorbild), alle Erzeuger (`StatementDraftService`, `StatementDraftService.Preliminary`, `PostingReversalService`, `SavingsPlanService`, `SetupImportService`).

### Backup-Restore: Überspringen referenzloser Postings

**Create-Pfad (Z. 711–758):**

1. `SetupImportService.ImportAsync` → `ImportVersion3` erreicht die Posting-Sektion (`TryGetProperty("Postings")`, Z. 715; fehlende Sektion = keine Postings, unverändert).
2. Pro `PostingBackupDto` werden die vier Referenzen über die ID-Maps remappt (Z. 723–726); Ziele, die nicht im Backup lagen, mappen auf `null`.
3. **Neu:** Sind alle vier remappten Werte `null`, wird das DTO übersprungen (`LogDebug`/`LogWarning` mit `dto.Id`), kein `new Posting`, kein `_db.Postings.Add`, kein Eintrag in `postingMapCollection` — der Fortschrittszähler läuft normal weiter.
4. Hat mindestens eine Referenz remappt, wird das Posting wie bisher konstruiert; der neue Konstruktor-Guard greift zusätzlich als Absicherung.
5. `SaveChanges` persistiert nur die referenzierten Postings; `postingMap` erhält nur Einträge tatsächlich angelegter Postings (Z. 748–749/755–756 über `postingMapCollection`).

**Apply-Pfad (Z. 1308–1332 — zweiter Durchlauf über dieselbe `Postings`-Sektion, unverändert):**

6. Pro DTO wird dasselbe Vier-Referenzen-Remapping erneut ausgeführt (Z. 1316–1319 — identische Ausdrücke wie im Create-Pfad → deterministisch gleiches Ergebnis) und in `adjusted` (per `with`-Ausdruck, Z. 1320) gespiegelt.
7. `postingMap.TryGetValue(dto.Id)` (Z. 1322): Für im Create-Pfad geskippte DTOs existiert kein Eintrag — der Apply-Schritt wird automatisch ausgelassen, es erfolgt **kein** `AssignBackupDto` auf einer nicht existierenden Entität. Für importierte Postings schreibt `entity.AssignBackupDto(adjusted)` (Z. 1325) denselben Zustand mit ≥ 1 Referenz — `AssignBackupDto` ist damit der Guard-umgehende Pfad (Scope-Ausschluss 5), kann hier aber kein referenzloses Ergebnis erzeugen; der Check-Constraint bleibt die Absicherung für theoretische Restfälle.
8. Nebenwirkung für `StatementEntries` (unverändert, kein Eingriff): `StatementEntry.SecurityTransactionId` behält bei geskippten Postings die Original-Backup-Id (Z. 789 — `postingMap`-Miss ⇒ Fallback `dto.SecurityTransactionId`) — harmlos, da diese Spalte keine FK trägt und nur als schreibgeschützte Referenz dient.

Beteiligte Klassen/Komponenten: `SetupImportService`, `Posting`, `Posting.PostingBackupDto`, `AppDbContext`.

### Persistenz-Schranke über Check-Constraint

1. `AppDbContext.OnModelCreating` konfiguriert im `modelBuilder.Entity<Posting>`-Block (`AppDbContext.cs` Z. 513–519) zusätzlich `b.ToTable(t => t.HasCheckConstraint("CK_Postings_HasReference", ...))`.
2. `dotnet ef migrations add` erzeugt eine Migration, die unter SQLite einen Table-Rebuild der `Postings`-Tabelle mit `CHECK`-Klausel ausführt.
3. Beim Anwenden der Migration (`db.Database.Migrate()` beim App-Start, `ProgramExtensions.cs` Z. 820, oder `dotnet ef database update`): Enthält die Tabelle referenzlose Zeilen, schlägt die Migration fehl — Fail-loud gewünscht.
4. Zur Laufzeit lehnt SQLite jedes `INSERT`/`UPDATE` mit vier `NULL`-Referenzen ab — unabhängig davon, ob es über `AssignBackupDto`, Roh-SQL oder sonstige Wege am Konstruktor-Guard vorbeikam.

Beteiligte Klassen/Komponenten: `AppDbContext`, EF-Migration `AddPostingReferenceCheckConstraint`, `ProgramExtensions` (Start-Migration, unverändert).

### Dokumentation globaler Pools

1. XML-Doc von `IpBlock` (Z. 3–7), `SecurityTxtSettings` (Z. 3–6), `WellKnownSettings` (Z. 5–8) und `McpSettings` (Z. 3–10) erhält je einen expliziten Satz: globaler, benutzerübergreifender Datenpool, bewusst ohne `OwnerUserId` (Ausnahme vom `AGENT.md`-Besitzerprinzip).
2. `Docs/help/systemverwaltung-und-setup/datenmodell.md`: Bei `IpBlock` (Z. 22–29), `SecurityTxtSettings` (Z. 81–97) und `WellKnownSettings` (Z. 99–110) wird ergänzt, dass es sich um globale, benutzerübergreifende Daten handelt.
3. `Docs/help/mcp-server/datenmodell.md`: Bei `McpSettings` (Z. 108–115) wird die Globalität explizit ergänzt (aktuell nur „Singleton ohne Beziehungen").
4. `Docs/help/konten-und-buchungen/datenmodell.md`: Beim `Posting`-Abschnitt (Z. 29–42) wird die neue Invariante ergänzt, damit die Doku nicht dem erzwungenen Verhalten widerspricht.

Beteiligte Klassen/Komponenten: `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`, `McpSettings`, drei `Docs/`-Dateien.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `Posting` (`FinanceManager.Domain/Postings/Posting.cs`, Datenmodellklasse)

- **Geänderte Methoden:** Hauptkonstruktor (Z. 108–141) — nach `Guards.NotEmpty(sourceId, ...)` neuer Guard: wirft `ArgumentException`, wenn `accountId`, `contactId`, `savingsPlanId` und `securityId` alle `null` sind.
- **Geänderte Dokumentation:** `<exception>`-Blöcke der fünf Konstruktor-Überladungen um die neue Wurfbedingung ergänzen; ggf. `<remarks>` an den vier Referenz-Eigenschaften um den Invarianten-Hinweis.
- `AssignBackupDto` bleibt bewusst unverändert (Scope-Ausschluss 5 — Durchsetzung über den DB-Constraint).

### `SetupImportService` (`FinanceManager.Infrastructure/Setup/SetupImportService.cs`)

- **Geänderte Methoden:** `ImportVersion3` — in der Posting-Import-Schleife des Create-Pfads (Z. 720–753) nach dem ID-Remapping (Z. 723–726): DTO überspringen, wenn `accountId`, `contactId`, `savingsPlanId` und `securityId` alle `null` sind; optionaler `LogDebug`/`LogWarning`-Eintrag mit `dto.Id`. Fortschrittszählung (`progress.IncSub()`) bleibt gewahrt.
- **Unverändert bleibt:** der Apply-Block derselben Methode (Z. 1308–1332), der die `Postings`-Sektion ein zweites Mal iteriert, die vier Referenzen identisch remappt (Z. 1316–1319) und per `entity.AssignBackupDto(adjusted)` (Z. 1325) nachschreibt. Kein zusätzlicher Vier-Null-Check nötig — die Abdeckung ist über `postingMap.TryGetValue` (Z. 1322) deterministisch gegeben (geskippte DTOs haben keinen `postingMap`-Eintrag; siehe Designentscheidung).

### `AppDbContext` (`FinanceManager.Infrastructure/AppDbContext.cs`)

- **Geänderte Methoden:** `OnModelCreating` — `modelBuilder.Entity<Posting>`-Block (Z. 513–519) um `b.ToTable(t => t.HasCheckConstraint("CK_Postings_HasReference", "[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL"))` ergänzen.

### `IpBlock` (`FinanceManager.Domain/Security/IpBlock.cs`, Datenmodellklasse)

- **Geänderte Dokumentation:** Klassen-`<summary>`/`<remarks>` (Z. 3–7) um expliziten Globalitäts-Hinweis ergänzen.

### `SecurityTxtSettings` (`FinanceManager.Domain/Security/SecurityTxtSettings.cs`, Datenmodellklasse)

- **Geänderte Dokumentation:** Klassen-Doc (Z. 3–6) um Globalitäts-Hinweis ergänzen.

### `WellKnownSettings` (`FinanceManager.Domain/WellKnown/WellKnownSettings.cs`, Datenmodellklasse)

- **Geänderte Dokumentation:** Klassen-Doc (Z. 5–8) um Globalitäts-Hinweis ergänzen (Singleton-Dokumentation bleibt bestehen).

### `McpSettings` (`FinanceManager.Domain/Mcp/McpSettings.cs`, Datenmodellklasse)

- **Geänderte Dokumentation:** Klassen-Doc (Z. 3–10) um expliziten Globalitäts-Hinweis ergänzen — die bisherige Formulierung nennt die Globalität nur implizit („the global kill switch"); Formulierung an `Notification.OwnerUserId`/`McpAuditEntry.OwnerUserId` orientieren.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPostingReferenceCheckConstraint` | `Postings` — `AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId` | Neuer Check-Constraint `CK_Postings_HasReference`: mindestens eine der vier Referenzspalten muss `NOT NULL` sein. Unter SQLite erzeugt EF Core einen Table-Rebuild. Erstellung ausschließlich per `dotnet ef migrations add AddPostingReferenceCheckConstraint` (AGENT.md: niemals manuell schreiben). |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Posting`-Konstruktor (`accountId`, `contactId`, `savingsPlanId`, `securityId`) | Mindestens eine Referenz muss gesetzt sein | Alle vier `null` → `ArgumentException` |
| `Postings`-Tabelle (DB) | `CHECK ([AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL)` | `INSERT`/`UPDATE` mit vier `NULL`s → Constraint-Verletzung (`DbUpdateException`/`SqliteException`) |
| `PostingBackupDto` im Restore | Postings, deren remappte Referenzen allesamt `null` sind | Kein Fehler — DTO wird übersprungen (kein Import des Waisendatensatzes) |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Migration auf Bestandsdatenbanken:** Enthält eine produktive SQLite-DB referenzlose `Postings`-Zeilen, schlägt die Migration (Table-Rebuild mit `CHECK`) fehl — der App-Start bricht in `ProgramExtensions.cs` Z. 820/835 ab. Das ist bewusstes Fail-loud; die Bestandsaufnahme zeigt, dass kein Erzeuger solche Zeilen anlegen kann, Verstoß-Daten sind daher nicht zu erwarten.
- **EF-Materialisierung:** `Posting` hat keinen parameterlosen Konstruktor — EF bindet die Materialisierung an den Hauptkonstruktor. Würden referenzlose Zeilen in der DB liegen, würde bereits das *Lesen* eine `ArgumentException` werfen. Der Check-Constraint stellt sicher, dass solche Zeilen nach erfolgreicher Migration nicht existieren können.
- **Bestandstests:** ~137 `new Posting(`-Aufrufe in `FinanceManager.Tests`; Regex-Scans ergaben keinen referenzlosen Aufruf — Brüche sind nicht zu erwarten, die Suite dient als Regression. `BackupServiceFullExportTests.cs:183` (vier Referenzen) bestätigt, dass „mindestens eine" die richtige Invariante ist.
- **`EnsureCreated`-Pfade:** Nur **neu** per `EnsureCreated` erzeugte Datenbanken (alle SQLite-`:memory:`-Tests sowie Installationen, deren DB erst jetzt initial angelegt wird) erhalten den Constraint automatisch im Schema. **Bestehende** `EnsureCreated`-Datenbanken mit Daten erhalten ihn nicht — `EnsureCreated` läuft nicht erneut und `Migrate()` scheitert auf solchen Datenbanken bereits heute (`ProgramExtensions.cs` Z. 833–836); dort wirkt weiterhin der Domain-Guard. Alt-Installationen mit Migrationshistorie erhalten den Constraint regulär über die Migration. Ein Constraint-Test gegen SQLite-`EnsureCreated` ist daher für den Neuanlage-Pfad aussagekräftig.
- **`AssignBackupDto`-Umgehung:** Die Methode kann weiterhin ein referenzloses Posting im Speicher erzeugen; der Verstoß wird erst beim `SaveChanges` sichtbar (Constraint). Bewusste Festlegung — kein stiller Datenverlust, sondern verspäteter, klarer Fehler. Im Restore ist der einzige produktive `AssignBackupDto`-Aufrufer für Postings der Apply-Block (Z. 1308–1332), der über `postingMap` deterministisch nur referenzierte Entitäten erreicht (siehe Programmablauf „Backup-Restore"). Nebenwirkung ohne Eingriff: `StatementEntry.SecurityTransactionId` behält bei geskippten Postings die Original-Backup-Id (Z. 789, keine FK — schreibgeschützte Referenz, harmlos).
- **`PostingAggregate`-Lücke bleibt:** `PostingAggregateService.UpsertForPostingAsync` (Z. 89) und `SetupImportService` (Z. 1552) könnten bei `Kind`-Referenz-Inkonsistenz ein verwaistes Aggregat erzeugen — bewusster Scope-Ausschluss (Nr. 2). Durch die `Posting`-Invariante sinkt das Risiko weiter.
- **Encoding:** `PostingAggregateService.cs` ist Windows-1252-kodiert — die Datei wird in dieser Umsetzung nicht angefasst; kein Risiko.
- **Restore-Verhalten:** Ein manipuliertes Backup mit unauflösbaren Posting-Referenzen verliert diese Postings künftig still (nur Log) statt sie als Waisen zu importieren — fachlich gewünscht, aber im Changelog/PR-Kommentar erwähnenswert.

## Umsetzungsreihenfolge

1. **Guard im `Posting`-Hauptkonstruktor**
   - Voraussetzungen: Keine.
   - Beschreibung: Inline-Check (`ArgumentException`) nach `Guards.NotEmpty(sourceId, ...)` im Konstruktor `Posting.cs` Z. 108–141 einfügen; `<exception>`-Dokumentation der Konstruktor-Überladungen entsprechend ergänzen.

2. **Domain-Test für die Invariante**
   - Voraussetzungen: Schritt 1 (Guard muss existieren, damit der Test grün wird).
   - Beschreibung: Neue Testklasse `FinanceManager.Tests/Domain/PostingReferenceInvariantTests.cs`: wirft bei vier `null`-Referenzen (über die Konstruktor-Wege), erfolgreich bei je genau einer gesetzten Referenz.

3. **Skip-Logik in `SetupImportService`**
   - Voraussetzungen: Schritt 1 — ohne Guard würde der bisherige Code referenzlose Postings erzeugen; mit Guard würde der Import ohne Skip werfen.
   - Beschreibung: In der Posting-Schleife von `ImportVersion3` (Z. 720–753) DTOs mit vier `null`-remappten Referenzen überspringen (Log-Eintrag, Fortschritt beibehalten).

4. **Import-Test für den Skip-Fall**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: In `FinanceManager.Tests/Infrastructure/SetupImportServiceTests.cs` neuen Test: Backup-Payload mit einem Posting, dessen Referenz-IDs nicht exportiert sind → `ImportAsync` läuft erfolgreich durch, `db.Postings` bleibt leer. Testsetup (SQLite-In-Memory + `EnsureCreated` + Moq für `IStatementDraftService`/`IPostingAggregateService`) ist im bestehenden Muster vorhanden.

5. **Check-Constraint in `AppDbContext`**
   - Voraussetzungen: Keine (unabhängig von Schritten 1–4; sinnvoll erst nach dem Domain-Guard, damit Modell und Domain übereinstimmen).
   - Beschreibung: `HasCheckConstraint("CK_Postings_HasReference", ...)` im `modelBuilder.Entity<Posting>`-Block ergänzen.

6. **EF-Migration erzeugen**
   - Voraussetzungen: Schritt 5. `dotnet ef`-Tool auf dem Entwicklungsrechner verfügbar — per `dotnet ef --version` verifizieren (in dieser Umgebung vorhanden: **10.0.10**, verifiziert). AGENT.md verlangt die Developer PowerShell. `Microsoft.EntityFrameworkCore.Design` 10.0.14 ist in `FinanceManager.Web.csproj` (Z. 77–80) referenziert — vorhanden. Bestehende Migrationshistorie unter `FinanceManager.Infrastructure/Migrations/` ist vorhanden.
   - **Wichtig — Startup-Projekt ist zwingend:** `AppDbContext` hat nur den `DbContextOptions`-Konstruktor (Z. 40), `OnConfiguring` (Z. 957–960) setzt mit `ConfigureWarnings` nur Warnungsoptionen (kein Provider, kein Connection-String-Fallback), und es existiert **keine `IDesignTimeDbContextFactory`** im Repo. `dotnet ef` kann den Context daher nur über das Startup-Projekt `FinanceManager.Web` instanziieren (DI-Registrierung in `AddInfrastructure`, `ServiceCollectionExtensions.cs` Z. 74–81). Ohne `--startup-project FinanceManager.Web` schlägt der Befehl fehl. In dieser Umgebung verifiziert: `dotnet ef migrations list --project FinanceManager.Infrastructure --startup-project FinanceManager.Web` listet alle Migrationen erfolgreich.
   - Beschreibung: Aus dem Repo-Root in der Developer PowerShell ausführen:
     ```powershell
     dotnet ef --version   # Erwartung: 10.x (hier: 10.0.10)
     dotnet ef migrations add AddPostingReferenceCheckConstraint --project FinanceManager.Infrastructure --startup-project FinanceManager.Web
     ```
     Generierte Migration anschließend auf korrekten SQLite-Table-Rebuild der `Postings`-Tabelle mit `CHECK`-Klausel prüfen (keine manuelle Nachbearbeitung). Hinweis: Die Design-Time-Hostauflösung führt `Program.Main` nur bis zum Host-Bau aus — `ApplyMigrationsAndSeed()`/`Database.Migrate()` (Program.cs Z. 30) wird dabei nicht ausgeführt (empirisch verifiziert: bei `migrations list` entstand keine DB-Datei); es entstehen also keine Seiteneffekte auf einer Default-DB.

7. **Constraint-Integrationstest**
   - Voraussetzungen: Schritte 5–6.
   - Beschreibung: Neue Testklasse `FinanceManager.Tests/Infrastructure/PostingCheckConstraintTests.cs` (SQLite `DataSource=:memory:` + `EnsureCreated`, Muster wie `PostingAggregatesTests.CreateSqliteContext`): Posting mit Referenz anlegen, dann per `AssignBackupDto` alle vier Referenzen auf `null` setzen → `SaveChanges` muss `DbUpdateException` werfen. Ergänzend INSERT mit einer Referenz → erfolgreich.

8. **Realer Migrationsnachweis gegen SQLite-Datei**
   - Voraussetzungen: Schritte 5–6; `dotnet ef`-Tool (wie Schritt 6, inkl. `--startup-project FinanceManager.Web`).
   - **Verbindungssteuerung:** Die Web-App liest den Connection-String aus `ConnectionStrings:Default` (`ProgramExtensions.cs` Z. 100 → `AddInfrastructure` → `UseSqlite(connectionString ?? "Data Source=financemanager.db")`, `ServiceCollectionExtensions.cs` Z. 74–81). `appsettings*.json` enthält **keinen** `ConnectionStrings`-Abschnitt — ohne Übergabe würde `database update` eine `financemanager.db` im Arbeitsverzeichnis anlegen/migrieren. Übergabe per Umgebungsvariable `ConnectionStrings__Default` (durch `WebApplication.CreateBuilder` automatisch gemappt; bewährtes Repo-Muster: `PlaywrightWebAppFixture.cs` Z. 336, `scripts/screenshots/generate-screenshots.js` Z. 63). Alternativ überschreibt der EF-Parameter `--connection` die Verbindung des design-time-aufgelösten Contexts.
   - Beschreibung: Aus dem Repo-Root in der Developer PowerShell:
     ```powershell
     $tempDb = Join-Path $env:TEMP "fm-posting-check.db"
     if (Test-Path $tempDb) { Remove-Item $tempDb }
     $env:ConnectionStrings__Default = "Data Source=$tempDb"
     dotnet ef database update --project FinanceManager.Infrastructure --startup-project FinanceManager.Web
     # alternativ: dotnet ef database update --project FinanceManager.Infrastructure --startup-project FinanceManager.Web --connection "Data Source=$tempDb"
     ```
     Anschließend die Temp-Datei prüfen: `sqlite3 $tempDb ".schema Postings"` muss `CONSTRAINT "CK_Postings_HasReference" CHECK (...)` zeigen; ein `INSERT` mit vier `NULL`-Referenzspalten (sonst gültige Pflichtwerte) muss mit CHECK-Verletzung abgelehnt werden, ein `INSERT` mit einer Referenz muss gelingen (Probe per `sqlite3`-CLI oder ADO.NET). Zum Abschluss `$env:ConnectionStrings__Default` zurücksetzen (`Remove-Item Env:ConnectionStrings__Default`) und Temp-Datei löschen. Ergebnis dokumentieren.

9. **XML-Dokumentation der globalen Entitäten**
   - Voraussetzungen: Keine.
   - Beschreibung: Klassen-Docs von `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings` und `McpSettings` um den expliziten Globalitäts-Hinweis ergänzen.

10. **`Docs/`-Aktualisierung**
    - Voraussetzungen: Keine (inhaltlich sinnvoll nach Schritt 5/6, damit die dokumentierte Invariante auch existiert).
    - Beschreibung: `Docs/help/systemverwaltung-und-setup/datenmodell.md` (Globalität bei `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`), `Docs/help/mcp-server/datenmodell.md` (Globalität bei `McpSettings`) und `Docs/help/konten-und-buchungen/datenmodell.md` (neue Referenzinvariante bei `Posting`) ergänzen.

11. **Vollständige Regression**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: `dotnet build FinanceManager.sln`, `dotnet test FinanceManager.Tests`, `dotnet test FinanceManager.Tests.Integration` — alle müssen fehlerfrei bleiben (Baseline: 2139 + 165 Tests grün).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Constructor_Throws_WhenAllReferencesAreNull` | `PostingReferenceInvariantTests` (`FinanceManager.Tests/Domain/`) | `ArgumentException` bei vier `null`-Referenzen — über den Hauptkonstruktor und Stichproben der delegierenden Überladungen |
| `Constructor_Succeeds_WhenExactlyOneReferenceSet` (Theory/MemberData, 4 Fälle) | `PostingReferenceInvariantTests` | Erfolgreiche Konstruktion mit je genau einer gesetzten Referenz (`AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId`) |
| `Constructor_Succeeds_WhenMultipleReferencesSet` | `PostingReferenceInvariantTests` | Mehrere/alle Referenzen gesetzt → erlaubt (Absicherung gegen „genau eine"-Regression, vgl. `BackupServiceFullExportTests.cs:183`) |
| `ImportAsync_SkipsPosting_WhenAllRemappedReferencesAreNull` | `SetupImportServiceTests` (`FinanceManager.Tests/Infrastructure/`) | Backup-Payload mit Posting ohne importierte Referenzziele → Import schließt ab, `db.Postings` leer, keine Exception |
| `SaveChanges_Throws_WhenAllReferencesSetToNull` | `PostingCheckConstraintTests` (`FinanceManager.Tests/Infrastructure/`) | `AssignBackupDto` mit vier `null`-Referenzen auf getracktem Posting → `SaveChanges` wirft `DbUpdateException` (CHECK-Verletzung) |
| `Insert_Succeeds_WhenSingleReferenceSet` | `PostingCheckConstraintTests` | Posting mit genau einer Referenz wird persistiert (Constraint blockiert legitime Fälle nicht) |

### Betroffene bestehende Tests

Keine. Die Bestandsaufnahme (Regex-Scans über alle `new Posting(`-Stellen) ergab keinen referenzlosen Erzeuger in Tests; alle ~137 Aufrufe setzen mindestens eine Referenz. `BackupRestoreCoverageTests`, `SavingsPlanLinkBackupRestoreTests` und `BackupServiceFullExportTests` laufen unverändert als Regression.

### E2E-Tests (primärer Funktionsnachweis)

Es werden **keine E2E-Tests** geplant. Begründung: Die Invariante wirkt auf Domain-Ebene (Konstruktor-Guard) und Persistenz-Ebene (Check-Constraint) — dort ist kein UI-Element, keine Navigation und kein Nutzerfluss betroffen. Der einzige UI-erreichbare Berührungspunkt ist der Backup-Import (`SetupImportService.ImportAsync` wird über einen Upload ausgelöst); das *geänderte* Verhalten dort — Überspringen statt Waisen-Datensatz — tritt jedoch nur bei manipulierten/inkonsistenten Backups auf, die der reguläre `BackupService`-Export niemals erzeugt. Ein solches Backup ist kein normaler Benutzerfluss, und selbst im provozierten Fall ist der nutzersichtbare Effekt („Import schließt erfolgreich ab") mit und ohne Fix identisch — der Unterschied liegt ausschließlich auf DB-Ebene und ist einem E2E-Test von außen nicht verifizierbar. Damit ist kein E2E-Nachweis fachlich möglich oder erforderlich — unabhängig davon, dass `FinanceManager.Tests.E2E` in dieser Umgebung ohnehin nicht ausführbar ist (Playwright-Browser fehlen). Als realer Nachweis des Persistenz-Aspekts dient stattdessen Schritt 8 (Migration auf eine reale SQLite-Datei angewendet + INSERT-Probe gegen den Constraint) sowie der Integrationstest in Schritt 7 und der Import-Test in Schritt 4.

Welche bestehenden E2E-Tests müssen angepasst werden? Keine — kein Nutzerfluss betroffen.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: legitimes Posting bleibt unverändert erzeug- und persistierbar | Erzeugung eines `Posting` mit mindestens einer Referenz (z. B. `StatementDraftService` buchungt, oder Testcode) | Datenbank migriert (Constraint aktiv); keinerlei Voraussetzungsobjekte fehlen | `new Posting(sourceId, PostingKind.Bank, accountId, null, null, null, …)` → `_db.Postings.Add` → `SaveChanges` | Posting wird angelegt und persistiert; kein Fehler, kein Verhaltensunterschied für Anwender | Domain-Test `Constructor_Succeeds_*`, Constraint-Test `Insert_Succeeds_*`, Bestandssuite (2139 Tests) als Regression |
| 2 | Fail-fast: referenzloses Posting kann nicht erzeugt werden | `new Posting(...)` mit allen vier Referenzen `null` (Fehlprogrammierung, manipulierter Aufruf) | — | Konstruktoraufruf (beliebige der fünf Überladungen) | Sofortige `ArgumentException`; es entsteht kein Waisen-Datensatz im Speicher oder in der DB | Domain-Test `Constructor_Throws_WhenAllReferencesAreNull` |
| 3 | Restore-Robustheit: Backup mit unauflösbaren Posting-Referenzen | `SetupImportService.ImportAsync` auf einer Version-3-Backup-Datei | Backup-JSON enthält ein Posting, dessen `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` auf keine im Backup enthaltene Entität zeigen (manipuliertes/inkonsistentes Backup) | Deserialisierung → ID-Remapping liefert vier `null` → DTO wird übersprungen (kein `postingMap`-Eintrag → Apply-Durchlauf Z. 1308–1332 überspringt es automatisch) → `SaveChanges` | Import läuft vollständig durch; `Postings`-Tabelle enthält keinen Waisen; Log-Eintrag nennt die übersprungene `dto.Id` | Test `ImportAsync_SkipsPosting_WhenAllRemappedReferencesAreNull` |
| 4 | Persistenz-Schranke: referenzloser Zustand über Guard-umgehenden Pfad | `AssignBackupDto` mit vier `null`-Referenzen auf einem persistierten Posting (oder Roh-SQL-`INSERT`) | Migrierte SQLite-DB mit aktivem `CK_Postings_HasReference` | Eigenschaften via `AssignBackupDto` nullen → `SaveChanges` (bzw. Roh-`INSERT` mit vier `NULL`s) | `DbUpdateException`/`SqliteException` mit CHECK-Verletzung; keine Zeile entsteht | Test `SaveChanges_Throws_WhenAllReferencesSetToNull`; realer Nachweis in Schritt 8 (`dotnet ef database update` + INSERT-Probe auf Temp-DB-Datei) |
| 5 | Migration auf Bestandsdatenbank (Fail-loud bei Invariantenverletzung) | `dotnet ef database update` bzw. App-Start-Migration (`Database.Migrate()`, `ProgramExtensions.cs` Z. 820) | Bestands-DB ohne referenzlose Postings (Normalfall laut Analyse) — bzw. hypothetisch mit verletzten Zeilen | Migration `AddPostingReferenceCheckConstraint` wird angewendet | Normalfall: Migration erfolgreich, Tabelle hat Table-Rebuild mit `CHECK` erhalten. Verletzungsfall: Migration schlägt fehl und macht das Datenproblem sichtbar statt es still zu übergehen | Schritt 8 (reale Ausführung auf Temp-DB); dokumentierter Seiteneffekt |
| 6 | Dokumentations-Nachweis globaler Pools | Codereview / Lesen der Entitäten und `Docs/` | — | XML-Doc von `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`, `McpSettings` sowie die drei `Docs/`-Datenmodellseiten lesen | Globalität („benutzerübergreifender Datenpool, bewusst ohne `OwnerUserId`") ist an jeder Stelle explizit erkennbar; `AGENT.md`-Anforderung (Entität + `Docs/`) erfüllt | Diff-Review der vier Entitäten und drei Docs-Dateien |

## Offene Punkte

Keine.
