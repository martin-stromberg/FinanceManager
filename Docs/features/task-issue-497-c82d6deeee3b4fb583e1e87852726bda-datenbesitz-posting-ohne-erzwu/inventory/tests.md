# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-09, ca. 05:08–05:20 UTC
- **Branch und Commit-ID:** `task/issue-497-c82d6deeee3b4fb583e1e87852726bda-datenbesitz-posting-ohne-erzwu`, HEAD `e7c3b22fdc8a1ac3f2cd7327d4dab72018103faf` („feat: backup/restore - vollstaendige import- und dto-abdeckung (#500)")
- **Uncommittete Änderungen im getesteten Stand:** nur `Docs/features/task-issue-497-…/requirement.md` (untracked; Feature-Arbeitsmappe). Keine Änderungen an Produktiv- oder Testcode.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows, Git Bash; .NET SDK 10.0.403 (`net10.0`); xUnit v3 (4.0.2) + `xunit.runner.visualstudio` 3.1.5, FluentAssertions 8.11.0, Moq 4.21.0, bUnit 2.14.0; EF Core 10.0.14 (`Microsoft.EntityFrameworkCore.Sqlite` + `InMemory` — 270 `UseInMemoryDatabase`/`UseSqlite`-Vorkommen in `FinanceManager.Tests`).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Projekte `FinanceManager.Tests` (Baseline laut Arbeitsauftrag), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E` (alle in `FinanceManager.sln`). Befehl aus Arbeitsauftrag: `dotnet test FinanceManager.Tests` nach `dotnet build FinanceManager.sln`. Kein dediziertes Testskript/CI-Workflow im Repo als Quelle herangezogen.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build FinanceManager.sln` | Repo-Root | 0 | — (0 Fehler, 65 Warnungen, ~30,6 s) | — | — | [build-baseline.log](test-results/build-baseline.log) |
| Unit | `dotnet test FinanceManager.Tests` | Repo-Root | 0 | 2139 | 0 | 0 | [test-baseline.log](test-results/test-baseline.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration --no-build` | Repo-Root | 0 | 165 | 0 | 0 | [test-integration.log](test-results/test-integration.log) |

### Nachgewiesene bestehende Testfehler

Keine. Beide ausgeführten Suiten liefen vollständig fehlerfrei (0 Fehlschläge, 0 Übersprungen).

### Testlücken und Ausführungsprobleme

- **`FinanceManager.Tests.E2E` nicht ausgeführt:** Die Suite referenziert `Microsoft.Playwright` 1.64.1; unter `~/.cache/ms-playwright` existiert kein Browser-Cache → Playwright-Browser sind in dieser Umgebung nicht installiert. Ausführung per Arbeitsauftrag bei fehlender Infrastruktur unterlassen (Umfang: gesamte E2E-Suite).
- **`FinanceManager.Tests.Integration.ApiClient` ist kein Projekt:** Das Verzeichnis `FinanceManager.Tests.Integration.ApiClient/` im Repo-Root enthält genau eine Testdatei (`ApiClientBudgetReportUnbudgetedMirrorTests.cs`), aber **keine `.csproj`** und steht **nicht in `FinanceManager.sln`** — die Datei wird von keinem Testprojekt kompiliert und läuft nie. (Auffälligkeit, nicht durch diese Anforderung verursacht.)
- **Warnungen im Build:** 65 Warnungen (u. a. xUnit-Analyser-Empfehlungen, `NU1510`-Paketkurzhinweise) — keine Fehler, keine für die Anforderung relevanten Hinweise.

## Testklassen (anforderungsrelevante Bereiche)

### `PostingReversalDomainTests` (`FinanceManager.Tests/Domain/PostingReversalDomainTests.cs`)
Reine Domain-Tests (keine Infrastruktur); Hilfsmethode `CreatePosting` erzeugt Bank-Posting mit `accountId` gesetzt.
- `SetReversedBy_ShouldSetReversedProperties` — Storno-Marker setzt Felder
- `SetReversalFor_ShouldSetReversalProperties` — Reversal-Marker
- `IsReversed_ShouldBeFalse_ForFreshPosting` / `IsReversal_ShouldBeFalse_ForFreshPosting` — Defaults
- `SetReversedBy_ShouldThrow_WhenAlreadyReversed` / `SetReversalFor_ShouldThrow_WhenAlreadyAReversal` — Doppel-Storno-Schutz (`InvalidOperationException`)

### `PostingBackupDtoReversalTests` (`FinanceManager.Tests/Domain/PostingBackupDtoReversalTests.cs`)
- `ToBackupDto_AndAssignBackupDto_ShouldPreserveReversalFields` — Backup-/Restore-Roundtrip der Storno-Felder über `AssignBackupDto`

### `PostingReversalServiceTests` (`FinanceManager.Tests/Infrastructure/Postings/PostingReversalServiceTests.cs`)
Infrastrukturtests zum Storno-Service (28 `new Posting(`-Aufrufe, alle mit `accountId`/`contactId` gesetzt).

### `PostingAggregatesTests` / `PostingAggregateService_SubtypeAggregationTests` / `PostingAggregateServiceSubtypeSplitTests` (`FinanceManager.Tests/Aggregates/`)
Aggregat-Upserts und Subtyp-Aufschlüsselung; Postings immer mit `accountId`/`securityId`.

### `BackupServiceFullExportTests` (`FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs`)
- Voll-Export-Roundtrip; Z. 183: `new Posting(Guid.NewGuid(), pk, account.Id, contact.Id, savings.Id, security.Id, …)` — **erzeugt ein Posting mit allen vier Referenzen** → die Invariante muss „mindestens eine" lauten, nicht „genau eine".

### `BackupRestoreCoverageTests` / `SavingsPlanLinkBackupRestoreTests` (`FinanceManager.Tests/Infrastructure/`)
Backup-/Restore-Abdeckung inkl. Postings (Z. 378 bzw. Z. 83) — setzen Referenzen.

### `SecurityTxtSettingsTests_Initialization` (`FinanceManager.Tests/Domain/SecurityTxtSettingsTests_Initialization.cs`)
- `Constructor_Throws_WhenExpiresIsNotInFuture` — Domain-Guard (`ArgumentOutOfRangeException`)

### `WellKnownSettingsTests_Initialization` (`FinanceManager.Tests/Domain/WellKnownSettingsTests_Initialization.cs`)
- `CreateDefault_HasChangePasswordUrl`, `Update_AcceptsLocalPath`, `Update_AcceptsAbsoluteHttpUrl` (Theory), `Update_RejectsInvalid` (Theory, 6 Fälle) — Singleton-Default + URL-Guard

### Weitere `new Posting(`-Erzeuger in `FinanceManager.Tests` (137 Treffer `new Posting(`/`Postings.Posting(`, Stichproben alle referenziert)
`SavingsPlanServiceTests`/`SavingsPlanServiceOwnAmountTests`/`SavingsPlanServiceActualReportTests`/`SavingsPlanLinkedAnalysisTests`/`SavingsPlanAnalysisTests` (SavingsPlan-Referenz), `PostingsQueryServiceTests`/`PostingsQueryServiceAdditionalTests`/`PostingsQueryServiceInformalTests` (Konto-/Kontakt-Referenz), `PostingExportServiceInformalTests`, `ReportAggregationProjectionTests`/`ReportAggregationService*Tests`/`ContactPostingsValutaDateTests`/`ReportAggregation_ValutaBookingTests`, `ReturnAnalysisServiceTests` (23×, Security-Referenz), `PortfolioAnalysisReportServiceTests`, `BudgetPurposeServiceSavingsPlanActualsTests`, `BudgetImpactEvaluationServiceTests`, `AccountServiceTests`, `ContactServiceMergeTests` (auch 3× `new PostingAggregate(`), `SecurityDividendNetYtdSimpleTests`, `McpReadOnlyToolsTests` (Z. 729–731: Bank-Posting mit `accountId`), `PostingAggregatesTests`.

**Prüfung auf referenzlose Erzeuger in Tests:** Regex-Scans (`null, null, null, null` als Positionsargumente sowie benannte `…Id: null`-Kombinationen über alle `new Posting(`-Stellen, einzeilig und mehrzeilig) ergaben **keinen Treffer** — kein Test erzeugt ein `Posting` ganz ohne Referenz.

## Hilfsmethoden (`FinanceManager.Tests/TestHelpers/`)

### `TestEntityHelper`
- `SetEntityId(object entity, Guid id)` — setzt `Id` per Reflection (private Setter); genutzt z. B. in `BackupServiceFullExportTests` (`SetGuid`-Wrapper)

### `SecurityTxtSettingsTestData`
- `ValidRequest(...)` / `ValidRequestWithCanonical(...)` / `MinimalRequest(...)` — Fixture-Fabriken für `SecurityTxtSettingsUpdateRequest`

### Weitere Helfer
`CapturingLogger`, `CultureTestHelper`, `StubAccountService`, `TestLoggerHelper`, `TestReturnAnalysisLocalizer` — Logging-/Kultur-/Service-Stubs ohne direkten Posting-Bezug.

## Fehlende Tests für die Anforderung (Ist-Lücke)

- Kein Domain-Test, der die Konstruktor-Invariante „mindestens eine Referenz" prüft (existiert noch nicht als Vertrag).
- Kein Infrastrukturtest zu Check-Constraints (es gibt keine Constraints; `HasCheckConstraint` kommt repo-weit nicht vor).
