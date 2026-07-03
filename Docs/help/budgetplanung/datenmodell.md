← [Zurück zur Übersicht](index.md)

# Budgetplanung — Datenmodell

## Entitäten

### `BudgetCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |

### `BudgetPurpose`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Zweck-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Zweckbezeichnung |
| `CategoryId` | `Guid?` | Verknüpfte Kategorie |

### `BudgetRule`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Regel-ID |
| `BudgetPurposeId` | `Guid?` | Zielzweck |
| `BudgetCategoryId` | `Guid?` | Zielkategorie |
| `Amount` | `decimal` | Erwarteter Betrag |
| `Interval` | `BudgetIntervalType` | Intervalltyp |
| `StartDate` | `DateOnly` | Start |
| `EndDate` | `DateOnly?` | Ende |
| `PurposePattern` | `string?` | Optionales Muster |

### `BudgetOverride`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Override-ID |
| `BudgetPurposeId` | `Guid` | Betroffener Zweck |
| `Month` | `DateOnly` | Zielmonat |
| `Amount` | `decimal` | Überschriebener Betrag |

## Beziehungen

- `BudgetPurpose` kann einer `BudgetCategory` zugeordnet sein.
- Regeln und Overrides referenzieren Budgetzwecke bzw. -kategorien.
