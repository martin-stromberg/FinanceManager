← [Zurück zur Übersicht](index.md)

# Sparpläne — Business Rules

## Wiederkehrende Ziele werden automatisch fortgeschrieben

**Beschreibung:** Bei wiederkehrenden Sparplänen wird ein überfälliges Zieldatum automatisch in die Zukunft verschoben.

**Bedingungen:**
- `Type = Recurring`
- `Interval` und `TargetDate` sind gesetzt.

**Verhalten:**
- Zieltermin wird in Intervallschritten erhöht, bis er nach dem Stichtag liegt.

**Umsetzung:** `SavingsPlan.AdvanceTargetDateIfDue`.

## Monatsende bleibt Monatsende

**Beschreibung:** Intervallfortschreibung bewahrt Monatsend-Semantik.

**Bedingungen:**
- Ursprungsdatum liegt am Monatsende.

**Verhalten:**
- Neues Datum wird ebenfalls auf das Monatsende des Zielmonats gesetzt.

**Umsetzung:** `SavingsPlan.AddIntervalWithMonthEndRule`.

## Detailansicht zeigt Sparfortschritt

**Beschreibung:** Die Detailansicht eines bestehenden Sparplans zeigt aktuelle Fortschrittskennzahlen an.

**Bedingungen:**
- Ein bestehender Sparplan wurde geladen.

**Verhalten:**
- `CurrentAmount` wird als aktueller Saldo angezeigt.
- `RemainingAmount` wird als Restbetrag angezeigt.
- Beide Felder sind nicht editierbar.

**Umsetzung:** `SavingsPlanCardViewModel.BuildCardRecordAsync`.

## Benötigter Monatsbetrag nur für offene Einmalziele

**Beschreibung:** Der benötigte Monatsbetrag wird nur angezeigt, wenn er für die Planung eines einmaligen Sparziels relevant ist.

**Bedingungen:**
- `Type = OneTime`
- `RemainingAmount > 0`
- `TargetDate` liegt nach dem heutigen Datum.
- Die Analyse liefert `RequiredMonthly > 0`.

**Verhalten:**
- In der Detailansicht erscheint das nicht editierbare Feld `Card_Caption_SavingsPlan_RequiredMonthly`.
- Bei wiederkehrenden Sparplänen, vollständig angesparten Zielen oder Zieldatum heute beziehungsweise in der Vergangenheit wird das Feld ausgeblendet.

**Umsetzung:** `SavingsPlanCardViewModel.BuildCardRecordAsync` mit Daten aus `SavingsPlanService.AnalyzeAsync`.
