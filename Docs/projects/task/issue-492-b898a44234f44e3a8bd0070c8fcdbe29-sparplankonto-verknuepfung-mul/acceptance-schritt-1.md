# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul...HEAD` (14 Dateien, ausschließlich Additions) gegen Schritt 1 in `project-plan.md` (Zeilen 49–62) sowie `requirement.md` (FA-1, Konfiguration/Datenmodell) und `project-plan-check.md`.

- **AK 1 – Join-Entity in eigener Tabelle, eindeutige Paarung:** `SavingsPlanAccountLink` (`FinanceManager.Domain/Savings/SavingsPlanAccountLink.cs`) als explizite n:m-Entity mit `SavingsPlanId`, `AccountId`, `OwnerUserId` und Metadaten über `Entity`-Basisklasse (`CreatedUtc`/`ModifiedUtc`). EF-Konfiguration in `FinanceManager.Infrastructure/AppDbContext.cs` (Zeilen 564–580): eigene Tabelle `SavingsPlanAccountLinks`, Unique-Index auf `(SavingsPlanId, AccountId)`, Indizes auf `AccountId` und `OwnerUserId`, Cascade-Delete zu beiden Eltern. Migration `20261107212617_AddSavingsPlanAccountLinks` im Scaffold-Format per `dotnet ef` erzeugt, inkl. aktualisiertem `AppDbContextModelSnapshot`.

- **AK 2 – Setzen/Auflisten/Entfernen:** `ISavingsPlanAccountLinkService` (`FinanceManager.Application/Savings/ISavingsPlanAccountLinkService.cs`) mit `AddLinkAsync`, `RemoveLinkAsync`, `GetLinkedAccountIdsAsync` (Konten eines Plans) und `GetLinkedSavingsPlanIdsAsync` (Pläne eines Kontos); Implementierung `FinanceManager.Infrastructure/Savings/SavingsPlanAccountLinkService.cs`, DI-Registrierung in `ServiceCollectionExtensions.cs` (Zeile 110).

- **AK 3 – Owner-Scope und Duplikat-Abweisung:** `AddLinkAsync` validiert Plan und Konto jeweils gegen `ownerUserId` (fremde Entitäten werden wie nicht vorhandene mit `ArgumentException` abgewiesen — keine Existenz-Preisgabe) und weist Duplikate mit `InvalidOperationException` ab; Unique-Index sichert zusätzlich auf DB-Ebene.

- **AK 4 – n:m-Kardinalität:** Tests belegen mehrere Konten pro Plan (`AddLink_SecondAccountOnSamePlan_AllowsMultipleAccountsPerPlan`) und mehrere Pläne pro Konto/Sammelkonto (`AddLink_SecondPlanOnSameAccount_AllowsMultiplePlansPerAccount`).

- **Benennung:** `SavingsPlanAccountLink` ist klar von der bestehenden `SavingsPlanExpectation`-Kopplung unterscheidbar — Anforderung aus der Schrittanforderung eingehalten.

- **Testnachweis:** `dotnet test FinanceManager.Tests --filter "FullyQualifiedName~SavingsPlanAccountLinkServiceTests"` → **17/17 Tests bestanden** (3 neue Testdateien: `..._AddLink.cs` 8 Tests, `..._Queries.cs` 5 Tests, `..._RemoveLink.cs` 4 Tests; In-Memory-SQLite, abgedeckt: Persistenz, Owner-Scoping in beide Richtungen, Duplikat, Unbekannte/Fremde, Leermenge, Kaskaden-Löschung beim Plan-Remove). Build ohne Fehler, nur vorbestehende Analyzer-Warnungen.

- **Migrationsort:** Die Migration liegt in `FinanceManager.Infrastructure/Migrations/` neben dem Snapshot — entgegen der Plan-Anmerkung zu `Data/Migrations/Identity` ist dies die aktuell gültige Konvention (jüngste Migrationen dort vom 04./05.11.2026; `Data/Migrations/Identity` zuletzt 21.02.2026). Plan-Formulierung „beim Anlegen mit der Projekt-Konvention abstimmen" wurde damit korrekt umgesetzt; funktional ohne Belang (EF-Discovery ist attributbasiert).

- **Vorgriff auf Schritt 9:** Die Entity enthält bereits `SavingsPlanAccountLinkBackupDto`, `ToBackupDto()` und `AssignBackupDto()` — Export/Import-Verdrahtung ist explizit Schritt 9 und nicht Teil dieses Diff. Konsistent mit Vorgehensentscheidung 8 (Vermeidung der `AccountLinkedIban`-Lücke), kein Manko.

- **Einschränkung:** Service-Methoden `GetLinked*Async` liefern `null` bei unbekanntem/fremdem Elternteil statt leerer Liste — dokumentierter und getesteter Vertrag; die API-Schicht (Schritt 3) muss dieses `null`-Ergebnis geeignet auf Fehlercodes mappen.

- **Weiterer Diff-Inhalt:** Hilfe-Dokumentation (`Docs/help/sparplaene/datenmodell.md`, `Docs/help/konten-und-buchungen/datenmodell.md`) und `Docs/RELEASE_NOTES.md` (EN+DE) wurden konsistent ergänzt. Keine ungeplanten Änderungen außerhalb des Schritt-Umfangs (keine API-/UI-/Analyse-Berührung — korrekt für Schritt 1).
