# Budget Impact Evaluation Flow

Dieses Dokument beschreibt die Ablauflogik von `BudgetImpactEvaluationService` für
- Echtzeit-Hinweise je Entry (`EvaluateEntryImpactAsync`)
- Abschluss-Summary vor Buchung (`EvaluateDraftImpactAsync`)

## Mermaid-Ablauf

```mermaid
flowchart TD
  A[Start Evaluation] --> B[Load Draft + Entries by owner]
  B --> |Draft/Entry missing| C[Return null]
  B --> D[Resolve contacts + contact categories]
  D --> E[Load budget purposes]
  E --> F[Match affected purposes by SourceType]
  F --> |none| G[Return neutral hint]
  F --> H[Group entries by BudgetPeriodKey]
  H --> I[Calculate planned values via IBudgetPlanningService]
  I --> J[Load actual before from postings]
  J --> K[Simulate delta from draft entries]
  K --> L[Calculate rates before/after + delta]
  L --> M[Classify hint type]
  M --> N[Build reason + DTO rows]
  N --> O[Create fingerprint SHA-256]
  O --> P[Return evaluation/summarized result]
```

## Klassifizierung

`BudgetImpactHintType`:
- `Exceeded`
- `AlmostExhausted` (Schwellwert aktuell 90 %)
- `StronglyChanged` (Delta aktuell 20 %)
- `Neutral`

## Quellen und Berechnungsbasis

- Sollwerte: `IBudgetPlanningService.CalculatePlannedValuesAsync(...)`
- Istwerte: `Postings` je Periode und SourceType
- Zuordnung:
  - `Contact`
  - `SavingsPlan`
  - `ContactGroup` (über Kontaktkategorie)

## Besondere Fälle

- Keine zuordenbaren Budgetzwecke ⇒ neutraler Hinweistext.
- Leere Buchungsmenge (z. B. bereits gebucht/announced) ⇒ Summary mit `Neutral` und ohne Items.
- Fingerprint ermöglicht UI-seitige Vergleichbarkeit zwischen zwei Evaluierungen.

## Referenzen

- `FinanceManager.Infrastructure/Statements/BudgetImpactEvaluationService.cs`
- `FinanceManager.Application/Statements/IBudgetImpactEvaluationService.cs`
- `FinanceManager.Shared/Dtos/Statements/BudgetImpactDtos.cs`
