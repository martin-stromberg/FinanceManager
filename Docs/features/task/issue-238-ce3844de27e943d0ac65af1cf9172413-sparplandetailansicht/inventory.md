# Bestandsaufnahme: Sparplandetailansicht

Diese Bestandsaufnahme dokumentiert den bestehenden Stand zur Anforderung aus [requirement.md](requirement.md): Die Sparplandetailansicht soll aktuellen Saldo, Restbetrag und bedingt den durchschnittlich benoetigten Monatsbetrag bis zum Faelligkeitsdatum anzeigen.

## Zusammenfassung

**Anwendungsbereich:** ASP.NET Core / Blazor Server mit generischer Kartenansicht (`CardPage` + `GenericCardPage`) und fachlichen ViewModels.

**Aktueller Stand:**
- Die Sparplanliste zeigt bereits Zielbetrag, Saldo, Restbetrag, Zieldatum und Status an.
- Die Detailansicht eines Sparplans rendert aktuell nur Stammdatenfelder wie Name, Kategorie, Typ, Zielbetrag, Zieldatum, Vertragsnummer und Symbol.
- Die benoetigten Werte sind backendseitig weitgehend vorhanden:
  - `SavingsPlanDto.CurrentAmount` enthaelt den aktuellen Saldo.
  - `SavingsPlanDto.RemainingAmount` enthaelt den gedeckelten Restbetrag.
  - `SavingsPlanAnalysisDto.RequiredMonthly` enthaelt den erforderlichen Monatsbetrag.
  - `SavingsPlanAnalysisDto.MonthsRemaining` enthaelt die verbleibenden vollen Monate.
- Die Detailansicht laedt `SavingsPlans_GetAsync`, ruft `SavingsPlans_AnalyzeAsync` aber nur ueber die Ribbon-Aktion "Neu berechnen" auf. Der geladene Analysewert wird derzeit nicht in der Karte angezeigt.

## Detaildokumente

- [Datenmodell, API und Berechnungslogik](inventory/data-and-api.md)
- [UI und ViewModels](inventory/ui-and-viewmodels.md)
- [Tests und Testluecken](inventory/tests.md)
- [Risiken und offene Punkte](inventory/risks-and-open-points.md)

## Relevanter Datenfluss

```text
Sparplan-Detailroute /card/savings-plans/{id}
    -> CardPage.razor
    -> CardViewModelResolver
    -> SavingsPlanCardViewModel.LoadAsync(id)
    -> ApiClient.SavingsPlans_GetAsync(id)
    -> GET /api/savings-plans/{id}
    -> SavingsPlansController.GetAsync
    -> SavingsPlanService.GetAsync
    -> SavingsPlanDto(CurrentAmount, RemainingAmount)
    -> SavingsPlanCardViewModel.BuildCardRecordAsync(dto)
    -> GenericCardPage rendert CardRecord.Fields
```

Analysewerte laufen separat:

```text
Ribbon "Neu berechnen"
    -> SavingsPlanCardViewModel.LoadAnalysisAsync()
    -> ApiClient.SavingsPlans_AnalyzeAsync(id)
    -> GET /api/savings-plans/{id}/analysis
    -> SavingsPlanService.AnalyzeAsync
    -> SavingsPlanAnalysisDto(RequiredMonthly, MonthsRemaining, AccumulatedAmount)
```

## Komponenten-Uebersicht

| Komponente | Datei | Zweck | Relevanz |
|-----------|-------|-------|----------|
| Sparplan-Domain | `FinanceManager.Domain/Savings/SavingsPlan.cs` | Stammdaten, Typ, Zielbetrag, Zieldatum, Intervall | Mittel |
| Service-Interface | `FinanceManager.Application/Savings/ISavingsPlanService.cs` | Vertrag fuer CRUD und Analyse | Hoch |
| Service-Implementierung | `FinanceManager.Infrastructure/Savings/SavingsPlanService.cs` | Berechnet CurrentAmount, RemainingAmount und RequiredMonthly | Hoch |
| API-Controller | `FinanceManager.Web/Controllers/SavingsPlansController.cs` | Endpunkte `GET /api/savings-plans/{id}` und `/analysis` | Hoch |
| Shared DTO | `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanDto.cs` | Transportiert CurrentAmount und RemainingAmount | Hoch |
| Analyse-DTO | `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanAnalysisDto.cs` | Transportiert RequiredMonthly und MonthsRemaining | Hoch |
| API-Client | `FinanceManager.Shared/ApiClient.SavingsPlans.cs` | Clientzugriff auf Detail- und Analyse-Endpunkt | Hoch |
| Detail-ViewModel | `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` | Baut die sichtbaren Detailfelder | Hoch |
| Generische Karte | `FinanceManager.Web/Components/Pages/GenericCardPage.razor` | Rendert `CardRecord.Fields` | Hoch |
| Sparplanliste | `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs` | Bestehende Referenz fuer Kennzahlenanzeige | Mittel |
| Lokalisierung | `FinanceManager.Web/Resources/Components/Pages/SavingsPlanEdit.*.resx` und/oder zentrale Pages-Ressourcen | Texte fuer neue Feldlabels | Hoch |

## Erkannte Implementierungsluecke

Die Anforderung ist voraussichtlich ohne neues Datenmodell und ohne neue API-Endpunkte umsetzbar. Der groesste fehlende Teil liegt in `SavingsPlanCardViewModel.BuildCardRecordAsync`: Dort muessen nicht editierbare CardFields fuer aktuellen Saldo und Restbetrag aus `SavingsPlanDto` sowie bedingt fuer den erforderlichen Monatsbetrag aus der Analyse ergaenzt werden.

Zusaetzlich muss die Detailansicht Analysewerte automatisch laden oder den erforderlichen Monatsbetrag aus vorhandenen DTO-Daten berechnen. Da `RequiredMonthly` bereits in `SavingsPlanAnalysisDto` berechnet wird, ist die robustere Variante, die Analyse beim Laden eines bestehenden Sparplans mitzuladen und anschliessend in `BuildCardRecordAsync` auszuwerten.

## Bedingung fuer Monatsbetrag

Die Anforderung fordert Anzeige nur wenn:

- `SavingsPlanType.OneTime`
- Restbetrag > 0
- Faelligkeitsdatum liegt in der Zukunft

Der aktuelle Analyse-Endpunkt liefert fuer alle Sparplantypen `RequiredMonthly`, sofern Zielbetrag und Zieldatum vorhanden sind. Die UI muss deshalb selbst sicherstellen, dass wiederkehrende Sparplaene den Monatsbetrag nicht anzeigen.

## Vorlaeufige Zielaenderungen

- `SavingsPlanCardViewModel.LoadAsync`: Bei bestehendem Sparplan Analyse laden, bevor oder nachdem `CardRecord` aufgebaut wird.
- `SavingsPlanCardViewModel.BuildCardRecordAsync`: Nicht editierbare Waehrungsfelder fuer `CurrentAmount`, `RemainingAmount` und bedingt `RequiredMonthly` ergaenzen.
- Ressourcen: Labels fuer aktuellen Saldo, Restbetrag und durchschnittlichen Monatsbetrag in Deutsch und Englisch ergaenzen.
- Tests: ViewModel-Tests fuer Anzeige/Nichtanzeige der drei Kennzahlen erweitern; optional Service-/Integrationstest fuer Berechnungsgrenzen des Monatsbetrags.

