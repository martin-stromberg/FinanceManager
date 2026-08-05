# Risiken und offene Punkte

## Geklaerte Punkte

### Datenquelle fuer Saldo und Restbetrag

Die Datenquelle ist `SavingsPlanService.GetAsync`:

- `CurrentAmount` wird aus bisherigen Sparplan-Postings berechnet.
- `RemainingAmount` wird aus Zielbetrag minus aktuellem Saldo berechnet und bei negativen Ergebnissen auf `0` begrenzt.

Damit ist fuer die Detailansicht kein neuer Endpunkt notwendig.

### Datenquelle fuer Monatsbetrag

Die Datenquelle ist `SavingsPlanService.AnalyzeAsync`:

- `RequiredMonthly` wird aus offenem Restbetrag und verbleibenden vollen Monaten berechnet.
- Bei Faelligkeitsdatum heute oder Vergangenheit liefert die Analyse `0`.

## Offene fachliche Annahme

Die Anforderung fragt, ob der durchschnittliche Monatsbetrag monatsgenau, kalendertagsgenau oder anhand voller verbleibender Monate berechnet werden soll. Die bestehende Implementierung nutzt volle verbleibende Kalendermonate:

```csharp
((endDate.Year - today.Year) * 12 + endDate.Month - today.Month)
```

Das ignoriert Tagesanteile. Beispiel: Vom 31. Januar bis 1. Februar ergibt die Formel einen Monat, obwohl nur ein Tag verbleibt. Fuer die Planung sollte festgehalten werden, ob diese bestehende Logik akzeptiert wird. Aus Bestandsaufnahme-Sicht ist "volle verbleibende Monate" die aktuelle Systemlogik.

## Technische Risiken

### Analyse wird bisher nicht automatisch geladen

`SavingsPlanCardViewModel.LoadAsync` laedt aktuell nur `SavingsPlans_GetAsync`. `LoadAnalysisAsync` existiert, wird aber nur ueber die Ribbon-Aktion "Neu berechnen" angestossen. Wenn der erforderliche Monatsbetrag direkt beim Oeffnen sichtbar sein soll, muss `LoadAsync` die Analyse automatisch laden oder `BuildCardRecordAsync` nach Analyse-Ladung erneut ausfuehren.

### Doppelte Saldoquellen

`SavingsPlanDto.CurrentAmount` und `SavingsPlanAnalysisDto.AccumulatedAmount` beschreiben fachlich denselben Wert. Die Liste nutzt Analysewerte, die Detailansicht kann DTO-Werte nutzen. Bei zukuenftigen Aenderungen besteht ein Konsistenzrisiko, wenn beide Berechnungen auseinanderlaufen.

### Typfilter fuer Monatsbetrag liegt in der UI

`AnalyzeAsync` berechnet `RequiredMonthly` nicht nur fuer einmalige Sparplaene. Die Anforderung verlangt aber explizit, dass wiederkehrende Sparplaene den Monatsbetrag nicht anzeigen. Die Detailansicht muss deshalb den Typfilter selbst anwenden.

### Sichtbarkeit fuer neue Sparplaene

Bei `Guid.Empty` gibt es noch keine Buchungen und keinen sinnvollen Saldo. Die Kennzahlen sollten nur fuer bestehende Sparplaene angezeigt werden.

### Lokalisierungskey-Ziel

Das Projekt nutzt mehrere Ressourcenbereiche. Die vorhandenen `SavingsPlanEdit.*.resx` enthalten passende Analyse-Labels, die generische Kartenansicht arbeitet aber mit `Card_Caption_*`-Keys. Vor Implementierung sollte kurz geprueft werden, welche Ressourcendatei die bestehenden Card-Caption-Keys enthaelt, damit neue Keys am richtigen Ort landen.

## Umsetzungsempfehlung

- Bestehende DTOs und Endpunkte wiederverwenden.
- `SavingsPlanCardViewModel` als einzigen fachlichen UI-Erweiterungspunkt nutzen.
- Neue Kennzahlen als read-only `CardFieldKind.Currency` in `CardRecord.Fields` aufnehmen.
- Analyse automatisch fuer bestehende Plaene laden, damit der Monatsbetrag ohne manuellen Recalculate-Klick sichtbar ist.
- Unit-Tests auf `CardRecord.Fields` schreiben, weil dort die generische UI ihre Anzeigequelle hat.

