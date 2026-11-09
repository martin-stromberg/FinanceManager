# Übersetzte Anforderung — Issue #497: Datenbesitz: Posting ohne erzwungenen Benutzerbezug, globale Pools nur implizit dokumentiert

## Fachliche Zusammenfassung

Zwei Qualitätsmaßnahmen zur Durchsetzung des Datenbesitz-Prinzips aus `AGENT.md` („Datensätze sind stets an das Benutzerkonto zu binden; ein globaler Datenpool ist nur bei expliziter Anforderung und entsprechender Dokumentation zulässig"):

1. Die Entität `Posting` erhält eine erzwungene Invariante „mindestens eine Zielreferenz ist gesetzt" (`AccountId`, `ContactId`, `SavingsPlanId` oder `SecurityId`), damit kein verwaistes Posting entstehen kann, das weder im Backup exportiert noch in `ClearUserDataAsync` gelöscht noch in der UI sichtbar wäre. Die Invariante wird im Domain-Konstruktor per Guard abgesichert und optional zusätzlich als DB-Check-Constraint in der EF-Konfiguration verankert.
2. Die drei bewusst benutzerübergreifenden (globalen) Entitäten `IpBlock`, `SecurityTxtSettings` und `WellKnownSettings` erhalten im XML-Doc einen expliziten Hinweis auf ihre Globalität — analog zur bereits vorhandenen Dokumentation bei `McpSettings`.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Domänenentität `Posting` (Aggregatwurzel, Buchungszeile) sowie die drei globalen Einstellungs-/Sicherheitsentitäten `IpBlock`, `SecurityTxtSettings`, `WellKnownSettings`.
- **Auslösende Ereignisse:** Kein Nutzer- oder Systemereignis — die Invariante greift bei jeder Erzeugung eines `Posting` (Konstruktoraufruf in Services, Tests, Restore-Pfad) sowie bei jedem `SaveChanges` gegen die Datenbank (Check-Constraint). Die Dokumentationsänderung ist rein statisch.
- **Erwartete Wirkung:**
  - Ein `new Posting(...)` ohne jede der vier Referenzen schlägt sofort mit einer `ArgumentException` fehl (Fail-fast statt still erzeugtem Waisen-Datensatz).
  - Ein `INSERT`/`UPDATE`, das alle vier Referenzspalten auf `NULL` setzt, wird — falls der Check-Constraint umgesetzt wird — auf Datenbankebene abgelehnt.
  - Leser des Codes erkennen an der XML-Dokumentation der drei globalen Entitäten unmittelbar, dass der fehlende `OwnerUserId` bezahlter/geduldeter Projektstandard ist (Annahme: Formulierung analog `McpSettings`).
  - Kein Verhaltensänderung für Nutzer; keine UI-Änderung.
- **Umgang mit fehlenden Objekten:** Keine neuen fachlichen Objekte erforderlich. Bestehende Erzeuger setzen nach Prüfung ausnahmslos mindestens eine Referenz — die Invariante formalisiert nur den Ist-Zustand (`PostingReversalService.CreateReversalPosting` kopiert die Referenzen transitiv vom Original; `SavingsPlanService` setzt bei „Istmeldung"-Korrekturen `SavingsPlanId`; `StatementDraftService`/`.Preliminary` setzen jeweils Konto-/Kontakt-/Wertpapier-Bezüge).
- **Scope-Ausschlüsse:**
  - Kein `OwnerUserId` auf `Posting` — der Besitzerpfad bleibt bewusst indirekt über die vier referenzierten Aggregate (die alle `OwnerUserId` tragen); nur die Referenz-Invariante wird erzwungen.
  - Keine Änderung an `PostingAggregate`, obwohl es dasselbe optionale Vier-Referenzen-Muster hat (gefilterte Unique-Indizes in `AppDbContext.cs` ca. Z. 633–653) — vom Issue nicht gefordert, siehe Offene Fragen.
  - Keine nachträgliche Datenbereinigung: verwaiste Alt-Postings können per Definition nicht existieren, solange alle bisherigen Erzeuger Referenzen setzten; eine DB-Migration prüft dies ggf. beim Anlegen des Constraints.
  - Keine neuen persistierten Felder → keine `*BackupDto`-Erweiterung und keine Backup-/Restore-Anpassung nötig.

## Betroffene Klassen und Komponenten

- **Datenmodell:**
  - `FinanceManager.Domain/Postings/Posting.cs` — Hauptkonstruktor (ca. Z. 108–141; alle Überladungen delegieren dorthin) erhält den Guard; ggf. neuer Helfer in `FinanceManager.Domain/Guards.cs`.
  - `FinanceManager.Domain/Security/IpBlock.cs` (Klassen-Doc Z. 3–7) — XML-Doc-Hinweis „globaler, benutzerübergreifender Datenpool".
  - `FinanceManager.Domain/Security/SecurityTxtSettings.cs` (Klassen-Doc Z. 3–6) — dito.
  - `FinanceManager.Domain/WellKnown/WellKnownSettings.cs` (Klassen-Doc Z. 5–8) — dito.
  - Referenz-/Vorbild-Implementierung der Dokumentation: `FinanceManager.Domain/Mcp/McpSettings.cs` (Z. 3–10).
- **EF-Konfiguration / Infrastruktur:**
  - `FinanceManager.Infrastructure/AppDbContext.cs` — `modelBuilder.Entity<Posting>`-Block (Z. 513–519): optionaler `HasCheckConstraint` (z. B. `"CK_Postings_HasReference"`: `[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL`); Provider ist SQLite (`ServiceCollectionExtensions.cs` → `UseSqlite`).
  - Neue EF-Migration per `dotnet ef migrations add <Name>` (laut `AGENT.md` niemals manuell schreiben).
  - `FinanceManager.Infrastructure/Backups/BackupService.cs` (Posting-Exportfilter Z. 646–651) und `AppDbContext.ClearUserDataAsync` (Z. 1029–1045): nur betroffen als Konsumenten des Filtermusters — kein Code-Change erwartet, die Invariante macht den Waisen-Fall strukturell unmöglich.
- **Logikklassen / Services:** Keine — alle Erzeuger erfüllen die Invariante bereits (`FinanceManager.Infrastructure/Statements/StatementDraftService*.cs`, `FinanceManager.Infrastructure/Postings/PostingReversalService.cs`, `FinanceManager.Infrastructure/Savings/SavingsPlanService.cs`, `FinanceManager.Infrastructure/Setup/SetupImportService.cs`).
- **Interfaces / Enums:** Keine Änderung (kein neues Enum-Mitglied → `EnumCoverageTests` nicht betroffen).
- **UI-Komponenten / Controller:** Keine.
- **Tests:**
  - Neuer Domain-Test (z. B. unter `FinanceManager.Tests/Domain/`): Konstruktor wirft `ArgumentException`, wenn alle vier Referenzen `null` sind; mindestens ein Referenztyp gesetzt → erfolgreich.
  - Ggf. Infrastrukturtest, dass `SaveChanges` den Check-Constraint durchsetzt (falls Constraint umgesetzt).
  - Bestandssuite als Regression: ~100 `new Posting(`-Aufrufe in `FinanceManager.Tests*`, die bereits Referenzen setzen (Stichprobe: `BackupServiceFullExportTests.cs:183` erzeugt sogar ein Posting mit allen vier Referenzen — die Invariante muss daher „mindestens eine" lauten, nicht „genau eine").

## Implementierungsansatz

1. **Guard im `Posting`-Hauptkonstruktor:** Da alle fünf Konstruktor-Überladungen per `: this(...)` in den vollständigen Konstruktor (Z. 108–141) delegieren, genügt ein einziger Check dort:
   `if (accountId is null && contactId is null && savingsPlanId is null && securityId is null) throw new ArgumentException(...)`.
   Optional als wiederverwendbarer Helfer in `Guards` (interne Klasse in `FinanceManager.Domain`). `ArgumentException` als Exception-Typ entspricht dem bestehenden Muster (`Guards.NotEmpty`, `SetGroup`, `SetParent`).
2. **Optionaler Check-Constraint:** Im `Posting`-Konfigurationsblock `b.ToTable(t => t.HasCheckConstraint("CK_...", "<SQL>"))` ergänzen; SQLite unterstützt Check-Constraints, die eckige-Klammern-Schreibweise der bestehenden `HasFilter`-Ausdrücke ist SQLite-kompatibel. Migration via `dotnet ef migrations add` in der Developer PowerShell.
3. **XML-Doc:** `<summary>`/`<remarks>` der drei Entitäten um einen Satz ergänzen, der die Entität explizit als globalen, benutzerübergreifenden Datenpool kennzeichnet und auf die Ausnahme vom `OwnerUserId`-Prinzip verweist (Vorbild: `McpSettings`).
4. **Abhängigkeiten:** Keine neuen Pakete/Interfaces; Risiko besteht nur, falls ein übersehener Erzeuger oder der Restore-Pfad (`AssignBackupDto` in `Posting.cs` Z. 523–550 setzt die Referenzen direkt, also nach dem Konstruktor-Guard) ein Posting ganz ohne Referenz erzeugt — Stichproben zeigen keine solche Stelle.

## Konfiguration

Keine Konfigurierbarkeit erforderlich — die Invariante ist ein fester Domänenvertrag; die Dokumentationsänderung ist statisch.

## Offene Fragen

1. **`PostingAggregate` mitgleichen?** Es trägt dasselbe Vier-Referenzen-Muster und wäre bei völlig leeren Referenzen ebenfalls verwaist. Issue nennt nur `Posting` — Soll die Invariante (Guard + ggf. Constraint) auch auf `PostingAggregate` angewendet werden oder bewusst nicht?
2. **Restore-Pfad absichern?** `AssignBackupDto` umgeht den Konstruktor-Guard; ein manipuliertes Backup könnte theoretisch ein referenzloses Posting einschleusen (der reguläre Export filtert solche aber ohnehin heraus). Soll `AssignBackupDto`/der `SetupImportService` die Invariante ebenfalls prüfen?
3. **Check-Constraint verpflichtend oder nur optional?** Der Issue formuliert „plus optionaler Check-Constraint/Index". Klären, ob der DB-Constraint tatsächlich per Migration eingeführt werden soll (empfohlen als zweite Verteidigungslinie) oder der Domain-Guard genügt.
4. **Dokumentation zusätzlich unter `Docs/`?** `AGENT.md` verlangt für globale Pools Dokumentation „in der Entität (XML-Dokumentation) und unter `Docs/`". Der Issue nennt nur das XML-Doc — soll ergänzend ein Eintrag in `Docs/` (z. B. unter `Docs/features/` oder einer Bestandsseite) erfolgen?
