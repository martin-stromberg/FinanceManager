# Bestandsaufnahme: MassImportDialogPolicy

## Ergebnis

Die Warnung entsteht durch die Kombination aus einem Datenbankdefault auf `User.MassImportDialogPolicy`, fehlender Sentinel-Konfiguration und dem gueltigen Enumwert `AlwaysConfirm = 0`. Der fachliche und Datenbankdefault ist `OnMissingInformation = 1`. Damit besteht ein konkretes Insert-Risiko: Ein neuer User mit explizit gesetztem `AlwaysConfirm` kann von EF als unveraendert interpretiert werden; die Datenbank liefert anschliessend den Default `OnMissingInformation`.

Eine bestehende gespeicherte Policy wird beim normalen Lesen nicht veraendert. Die Einfuehrungsmigration hat vorhandene Zeilen mit `1` initialisiert; es gibt keine spaetere Migration, die Policywerte umdeutet. Die Bestandsaufnahme spricht daher fuer eine lokale Modellkonfigurationskorrektur und gezielte Insert-/Metadatentests, nicht fuer eine Datenbereinigung.

## Detaildokumente

- [Policy und User](inventory/policy-and-user.md)
- [EF-Core-Konfiguration und Persistenz](inventory/ef-persistence.md)
- [Migrationen und gespeicherte Daten](inventory/migrations.md)
- [Tests und Abdeckung](inventory/tests.md)

## Relevante Fundstellen

| Bereich | Fundstelle | Befund |
|---|---|---|
| Enum | `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs:8-17` | `AlwaysConfirm = 0`, `OnMissingInformation = 1` |
| User | `FinanceManager.Domain/Users/User.cs:22-143` | Default `OnMissingInformation`; mehrere Konstruktorpfade |
| Setter | `FinanceManager.Domain/Users/User.cs:232-235` | Policy wird direkt gesetzt, ohne Enum-Validierung |
| EF-Modell | `FinanceManager.Infrastructure/AppDbContext.cs:122` | DB-Default `OnMissingInformation`, kein Sentinel |
| Migration | `FinanceManager.Infrastructure/Migrations/20260703061917_202607030850_AddMassImportDialogPolicy.cs:8-22` | Nicht nullable INTEGER, Default `1` |
| API | `FinanceManager.Web/Controllers/UserSettingsController.cs:248-304` | Direkter Read/Update-Pfad |
| Tests | `FinanceManager.Tests/Controllers/UserImportSplitSettingsControllerTests.cs` | Default und Persistenz abgedeckt |
| Integrationstests | `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs:163-194` | API-Default und Update abgedeckt |

## Offene technische Entscheidung fuer die Planung

Der fachliche Standard ist aufgrund von Enum-, Entity-, DTO-, Migration- und API-Tests eindeutig `OnMissingInformation`. Fuer die Umsetzung ist festzulegen, ob der DB-Default beibehalten und ein passender Sentinel gesetzt wird, oder ob die Property nicht mehr als datenbankgeneriert behandelt wird. Beide Varianten muessen den expliziten Wert `AlwaysConfirm` bei Inserts erhalten und die Startwarnung beseitigen.
