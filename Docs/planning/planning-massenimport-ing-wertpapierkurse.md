# Planungsübersicht: Massenimport ING Wertpapierkurse (Startseite)

> **Quelle:** [`../../issue.md`](../../issue.md)  
> **Status:** ✅ Umsetzung und Tests ergänzt  
> **Version:** 1.1  
> **Datum:** 2026-07-03  
> **Koordination:** planning-orchestrator

## 1. Zweck

Diese Übersicht konsolidiert den vollständigen Orchestrator-Ablauf (Anforderungsanalyse → Architektur-Blueprint → ERM → Architektur-Review) und verlinkt die erstellten Artefakte.

## 2. Verlinkte Artefakte

- Anforderungen: [`../requirements/massenimport-ing-wertpapierkurse-requirements.md`](../requirements/massenimport-ing-wertpapierkurse-requirements.md)
- Architektur-Blueprint: [`../architecture/architecture-blueprint-massenimport-ing-wertpapierkurse.md`](../architecture/architecture-blueprint-massenimport-ing-wertpapierkurse.md)
- ERM: [`../architecture/entity-relationship-model-massenimport-ing-wertpapierkurse.md`](../architecture/entity-relationship-model-massenimport-ing-wertpapierkurse.md)
- Architektur-Review: [`../improvements/review-architecture-massenimport-ing-wertpapierkurse.md`](../improvements/review-architecture-massenimport-ing-wertpapierkurse.md)

## 3. Orchestrator-Sequenz (durchgeführt)

1. **Anforderungsanalyse** erstellt (FR/NFR, Akzeptanzkriterien, Scope, Use Cases, Annahmen).
2. **Architektur-Blueprint** erstellt (2-Phasen-Import, Dialog-/Skip-Logik, UI/UX, Fehlerbehandlung, Qualitätsziele).
3. **ERM** erstellt (Persistenzabgrenzung, Batch-/Dateimodell, Service-Anzeigename, Security-Zuordnung).
4. **Architektur-Review** durchgeführt (priorisierte Findings und Maßnahmen, Freigabeempfehlung).
5. **Konsolidierung** in dieser Planungsübersicht abgeschlossen.

## 4. Konsolidierte Kernentscheidungen

1. Startseiten-Import unterstützt gemischte Batches aus Kontoauszugs- und Kursdateien.
2. Erkennung erfolgt je Datei über ImportFactory mit Dateityp und Service-Anzeigename.
3. Für Kursdateien erfolgt eine Dateiname-basierte Wertpapier-Vorbelegung mit manueller Korrekturmöglichkeit.
4. Importausführung erfolgt nach Bestätigung im Dialog, außer bei regelkonformem Dialog-Skip über Einstellungen.
5. Ausschluss einzelner Dateien ist verpflichtender Bestandteil des Dialogflows.

## 5. Explizite Annahmen

- Pflichtangaben für Dialog-Skip: Dateityp, Service und bei Kursdateien valide Wertpapierzuordnung.
- Wertpapiererkennung aus Dateinamen wird initial regelbasiert umgesetzt.
- Service-Anzeigenamen werden zentral an Importservices gepflegt und in der Erkennung mitgeliefert.
- Unbekannte Dateien bleiben sichtbar, sind standardmäßig nicht direkt importierbar.

## 6. Review-Ergebnis und Umsetzung

- Ursprüngliche Freigabeempfehlung: **Conditional Go** (siehe Review).
- Geschlossen in der Umsetzung:
  1. Dialog-Skip-Matrix inkl. Unit-/Integrationstests.
  2. Re-Validierung der Wertpapierzuordnung unmittelbar vor Persistierung.
  3. Auditierbares Logging pro Datei mit Batch-/Trace-Korrelation.

## 7. Implementierungsnachweis (Code + Tests)

### Technische Umsetzung
- `FinanceManager.Infrastructure/Statements/MassImportOrchestrator.cs`
- `FinanceManager.Application/Statements/IMassImportOrchestrator.cs`
- `FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs`
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` (`POST /api/statement-drafts/mass-import`)
- `FinanceManager.Web/Controllers/UserSettingsController.cs` (`GET/PUT /api/user/settings/import-split`)
- `FinanceManager.Web/ViewModels/Home/HomeViewModel.cs`
- `FinanceManager.Web/Components/Pages/Home.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupStatementTab.razor`

### Persistenz für Policy
- `FinanceManager.Domain/Users/User.cs` (`MassImportDialogPolicy`)
- `FinanceManager.Infrastructure/AppDbContext.cs` (Mapping)
- `FinanceManager.Infrastructure/Migrations/20260703061917_202607030850_AddMassImportDialogPolicy.cs`

### Testabdeckung (Feature-Scope)
- `FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs`
- `FinanceManager.Tests/ViewModels/HomeViewModelTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs`

## 8. Änderungshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0 | 2026-07-03 | planning-orchestrator | Vollständige Planungskonsolidierung für Startseiten-Massenimport von ING-Wertpapierkursen erstellt |
| 1.1 | 2026-07-03 | documentation-orchestrator | Planungsstatus auf umgesetzt aktualisiert, Implementierungs- und Testnachweise ergänzt |
