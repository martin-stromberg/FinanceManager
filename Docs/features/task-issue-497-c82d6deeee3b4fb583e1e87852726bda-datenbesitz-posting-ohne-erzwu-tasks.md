# Tasks: Datenbesitz — Posting-Referenzinvariante erzwingen, globale Pools explizit dokumentieren (Issue #497)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `Posting`-Hauptkonstruktor (`Posting.cs` Z. 108–141) um Guard „mindestens eine der vier Referenzen gesetzt" ergänzen (`ArgumentException` bei vier `null`) | Offen | — |
| 2 | Datenmodell | `<exception>`-XML-Dokumentation der fünf `Posting`-Konstruktor-Überladungen um die neue Wurfbedingung ergänzen | Offen | — |
| 3 | Import | `SetupImportService.ImportVersion3`: Posting-DTOs mit vier `null`-remappten Referenzen überspringen (Log-Eintrag, Fortschrittszählung beibehalten; Create-Pfad Z. 720–753; Apply-Block Z. 1308–1332 bleibt unverändert — Abdeckung über `postingMap`) | Offen | — |
| 4 | Persistenz | `AppDbContext.OnModelCreating`: `HasCheckConstraint("CK_Postings_HasReference", "[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL")` im `modelBuilder.Entity<Posting>`-Block (Z. 513–519) ergänzen | Offen | — |
| 5 | Persistenz | EF-Migration `AddPostingReferenceCheckConstraint` per `dotnet ef migrations add AddPostingReferenceCheckConstraint --project FinanceManager.Infrastructure --startup-project FinanceManager.Web` erzeugen und generierten Table-Rebuild prüfen | Offen | — |
| 6 | Persistenz | Realer Nachweis: Migration via `dotnet ef database update --project FinanceManager.Infrastructure --startup-project FinanceManager.Web` auf temporäre SQLite-Datei anwenden (Connection per `ConnectionStrings__Default`-Env-Var oder `--connection`); INSERT mit vier `NULL`-Referenzen muss abgelehnt werden, INSERT mit einer Referenz muss gelingen | Offen | — |
| 7 | Dokumentation | `IpBlock` (`FinanceManager.Domain/Security/IpBlock.cs` Z. 3–7): XML-Doc um expliziten Hinweis „globaler, benutzerübergreifender Datenpool — bewusst ohne `OwnerUserId`" ergänzen | Offen | — |
| 8 | Dokumentation | `SecurityTxtSettings` (`FinanceManager.Domain/Security/SecurityTxtSettings.cs` Z. 3–6): XML-Doc um Globalitäts-Hinweis ergänzen | Offen | — |
| 9 | Dokumentation | `WellKnownSettings` (`FinanceManager.Domain/WellKnown/WellKnownSettings.cs` Z. 5–8): XML-Doc um Globalitäts-Hinweis ergänzen | Offen | — |
| 10 | Dokumentation | `McpSettings` (`FinanceManager.Domain/Mcp/McpSettings.cs` Z. 3–10): XML-Doc um expliziten Globalitäts-Hinweis ergänzen (bisher nur implizit über „Singleton") | Offen | — |
| 11 | Dokumentation | `Docs/help/systemverwaltung-und-setup/datenmodell.md`: Globalität bei `IpBlock` (Z. 22–29), `SecurityTxtSettings` (Z. 81–97) und `WellKnownSettings` (Z. 99–110) explizit kennzeichnen | Offen | — |
| 12 | Dokumentation | `Docs/help/mcp-server/datenmodell.md`: Globalität bei `McpSettings` (Z. 108–115) explizit kennzeichnen | Offen | — |
| 13 | Dokumentation | `Docs/help/konten-und-buchungen/datenmodell.md`: neue Referenzinvariante beim `Posting`-Abschnitt (Z. 29–42) dokumentieren | Offen | — |
| 14 | Tests | Testklasse `FinanceManager.Tests/Domain/PostingReferenceInvariantTests.cs` anlegen: `Constructor_Throws_WhenAllReferencesAreNull` (über Hauptkonstruktor und delegierende Überladungen) | Offen | — |
| 15 | Tests | `PostingReferenceInvariantTests`: `Constructor_Succeeds_WhenExactlyOneReferenceSet` (4 Fälle, je eine Referenz) und `Constructor_Succeeds_WhenMultipleReferencesSet` ergänzen | Offen | — |
| 16 | Tests | `SetupImportServiceTests`: `ImportAsync_SkipsPosting_WhenAllRemappedReferencesAreNull` — Backup-Payload mit Posting ohne importierte Referenzziele → Import erfolgreich, `db.Postings` leer | Offen | — |
| 17 | Tests | Testklasse `FinanceManager.Tests/Infrastructure/PostingCheckConstraintTests.cs` anlegen: `SaveChanges_Throws_WhenAllReferencesSetToNull` (via `AssignBackupDto`, SQLite `:memory:` + `EnsureCreated`) | Offen | — |
| 18 | Tests | `PostingCheckConstraintTests`: `Insert_Succeeds_WhenSingleReferenceSet` — legitimes Posting wird persistiert | Offen | — |
| 19 | Regression | `dotnet build FinanceManager.sln`, `dotnet test FinanceManager.Tests` und `dotnet test FinanceManager.Tests.Integration` vollständig fehlerfrei ausführen | Offen | — |
