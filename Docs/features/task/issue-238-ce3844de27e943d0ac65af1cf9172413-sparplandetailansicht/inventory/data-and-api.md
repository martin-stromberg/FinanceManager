# Datenmodell, API und Berechnungslogik

## Domain-Modell

`FinanceManager.Domain/Savings/SavingsPlan.cs` enthaelt die fachlichen Stammdaten:

- `Type`: `SavingsPlanType` mit u. a. `OneTime`, `Recurring`, `Open`
- `TargetAmount`: optionaler Zielbetrag
- `TargetDate`: optionales Zieldatum
- `Interval`: Intervall fuer wiederkehrende Plaene
- `IsActive` und `ArchivedUtc`: Archivstatus

Die Domain-Entitaet speichert keinen Saldo und keinen Restbetrag. Diese Werte sind abgeleitet aus Buchungen (`Postings`) und werden in der Service-Schicht berechnet.

## DTOs

`FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanDto.cs` enthaelt bereits:

- `RemainingAmount` ab Zeile 82: Restbetrag bis zum Ziel, bei fehlendem Ziel aktuell `0`.
- `CurrentAmount` ab Zeile 87: aktuell akkumuliertes Sparplanvolumen.

`FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanAnalysisDto.cs` enthaelt:

- `AccumulatedAmount`
- `RequiredMonthly`
- `MonthsRemaining`

Damit existieren alle fuer die Anforderung benoetigten Transportfelder bereits.

## API-Endpunkte

`FinanceManager.Web/Controllers/SavingsPlansController.cs` bietet:

- `GET /api/savings-plans/{id}`: liefert `SavingsPlanDto`.
- `GET /api/savings-plans/{id}/analysis`: liefert `SavingsPlanAnalysisDto`.

Der Shared API-Client spiegelt diese Endpunkte in `FinanceManager.Shared/ApiClient.SavingsPlans.cs`:

- `SavingsPlans_GetAsync(Guid id)`
- `SavingsPlans_AnalyzeAsync(Guid id)`

## Berechnung in `SavingsPlanService.GetAsync`

`FinanceManager.Infrastructure/Savings/SavingsPlanService.cs` berechnet in `GetAsync`:

- Zeile 70: Summe aller `Postings` mit `SavingsPlanId == id`, `Kind == PostingKind.SavingsPlan` und `BookingDate <= DateTime.Today`.
- Zeile 77: `RemainingAmount = Math.Max(0m, TargetAmount - accumulated)`.
- Rueckgabe: `SavingsPlanDto(..., remaining, accumulated)`.

Wichtig: `ListAsync` setzt diese beiden Werte aktuell auf `0m`; die Liste nutzt stattdessen den Analyse-Endpunkt. Fuer die Detailansicht ist `GetAsync` ausreichend, weil sie einen einzelnen Sparplan laedt.

## Berechnung in `SavingsPlanService.AnalyzeAsync`

`AnalyzeAsync` berechnet:

- Zeile 225: `accumulated` als Summe der bisherigen Buchungen.
- Zeile 234: `monthsRemaining` als Differenz voller Kalendermonate zwischen heute und Zieldatum.
- Zeile 255: `remaining = Math.Max(0m, target - accumulated)`.
- Zeile 256: `requiredMonthly = remaining / monthsRemaining`, wenn Monate verbleiben.

Bei Zieldatum heute oder Vergangenheit wird `monthsRemaining <= 0`, und der Service liefert `RequiredMonthly = 0m`.

## Bewertung zur Anforderung

Die Datenquelle fuer aktuellen Saldo und Restbetrag ist geklaert: `SavingsPlanService.GetAsync` liefert beide Werte im `SavingsPlanDto`.

Die Berechnung des durchschnittlichen Monatsbetrags ist ebenfalls vorhanden, aber ihre Methodik ist konkret: volle verbleibende Kalendermonate, ohne Tagesanteile. Das beantwortet den offenen Punkt aus der Anforderung technisch, muss aber fachlich akzeptiert oder im Plan als Annahme festgehalten werden.

