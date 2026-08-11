# Bestandsaufnahme: Enums

Übersicht der bestehenden Enums, die für die Portfolio-Analyse relevant sind.

## `PostingKind`

**Datei:** `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

Identifiziert die Domäne/Art einer Buchung.

| Wert | Bedeutung |
|------|-----------|
| `Bank` (0) | Bankkonten-Buchung. |
| `Contact` (1) | Kontakt-Buchung. |
| `SavingsPlan` (2) | Sparplan-Buchung. |
| `Security` (3) | Wertpapier-Buchung (relevant für Portfolio-Analyse). |

**Verwendung:** Wird in der `Posting` Klasse verwendet, um die Art der Buchung anzugeben. Für die Portfolio-Analyse sind hauptsächlich `Security`-Buchungen relevant.

---

## `SecurityPostingSubType`

**Datei:** `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs`

Detaillierte Kategorisierung von Wertpapier-Buchungen.

| Wert | Bedeutung |
|------|-----------|
| `Buy` (0) | Kauf von Wertpapieren. |
| `Sell` (1) | Verkauf von Wertpapieren. |
| `Dividend` (2) | Dividendenzahlung (relevant für Cashflow-Analyse). |
| `Fee` (3) | Gebühr (relevant für Kostenquoten). |
| `Tax` (4) | Steuern (relevant für steuerliche Auswirkungen). |

**Verwendung:** Wird in der `Posting` Klasse als optionale Eigenschaft `SecuritySubType` verwendet. Ermöglicht die Unterscheidung zwischen:
- Kauf-/Verkaufsbuchungen (Position-Management)
- Dividendenbuchungen (Cashflow-Analyse)
- Gebühren und Steuern (Kostenquoten-Berechnung)

---

## Weitere relevante Enums

Weitere Enums wie `BudgetReportDateBasis` existieren, sind aber auf Budget-Reports beschränkt und nicht direkt für die Portfolio-Analyse relevant.
