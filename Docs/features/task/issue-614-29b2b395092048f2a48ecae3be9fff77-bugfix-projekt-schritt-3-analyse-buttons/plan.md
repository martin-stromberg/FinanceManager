# Plan – Schritt 3: Analyse-Buttons vereinheitlichen (#603)

## Ziel

Ribbon-Gruppe „Analyse" der Budgetübersicht verhält sich wie die des Budgetberichts:
5 relative Navigation-Buttons in identischer Reihenfolge. `BudgetReportViewModel`
bleibt unverändert (spezifikationskonform).

## Änderungen — `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`

Im `analysis`-Tab (`GetRibbonRegisterDefinition`, Sort 40):

1. `PrevMonth` → `ShiftRange(months: -1)` statt `today`-Anker.
2. `ThisMonth` → unverändert (bereits korrekt: aktueller Kalendermonat).
3. `NextMonth` → neu, `ShiftRange(months: +1)`, Label `Ribbon_NextMonth`, Icon `sprite.svg#next`.
4. `PrevYear` → `ShiftRange(years: -1)` statt Jahressicht.
5. `NextYear` → neu, `ShiftRange(years: +1)`, Label `Ribbon_NextYear`, Icon `sprite.svg#next`.
6. `ThisYear` → unverändert, bleibt als 6. Button (entschiedener offener Punkt 1).

Neue private Hilfsmethode (kein Code-Duplikat zwischen fünf Handlern):

```csharp
private void ShiftRange(int months = 0, int years = 0)
{
    var today = DateTime.Today;
    var from = (RangeFrom?.Date ?? new DateTime(today.Year, today.Month, 1))
        .AddYears(years).AddMonths(months);
    var to = (RangeTo?.Date ?? new DateTime(today.Year, today.Month,
            DateTime.DaysInMonth(today.Year, today.Month)))
        .AddYears(years).AddMonths(months);
    SetRange(from, to);
    RaiseUiActionRequested("Reload");
}
```

- `AddMonths`/`AddYears` liefern Tages-Clamping frei Haus (31.03.→28./29.02.).
- `null`-Bounds-Anker: `RangeFrom ??` Monatsanfang heute, `RangeTo ??` Monatsende heute —
  abgedeckt für den ClearFilter-Zustand; Verschieben aus „kein Filter" landet auf dem
  verschobenen Kalendermonat (naheliegend, dokumentiert).
- Von>Bis kann nicht entstehen (beide Grenzen werden um identische Deltas verschoben;
  `GetEffectiveMonthlyRange` normalisiert zur Sicherheit ohnehin).

## Tests

### `FinanceManager.Tests/ViewModels/BudgetPurposeListAnalysisNavigationTests.cs` (neu)

Über `vm.GetRibbonRegisters(loc)` die Analyse-Aktionen auflösen, `Callback` invoken,
`RangeFrom`/`RangeTo` + `UiActionRequested("Reload")` assertieren (Mock<IApiClient> +
TestCurrentUserService; `Budgets_ListPurposesAsync` captured):

- `PrevMonth_SetsRange_OneMonthBack`: von aktuellem Monat → exakter Kalendervormonat.
- `NextMonth_SetsRange_OneMonthForward`: → Kalender-Folgemonat.
- `PrevYear_SetsRange_SameWidth_OneYearBack`: freier Zeitraum (z. B. 15.03.–20.05.)
  → 15.03.–20.05. Vorjahr (Breite bleibt!).
- `NextYear_SetsRange_OneYearForward`.
- `ThisMonth_SetsRange_CurrentCalendarMonth`.
- `ThisYear_Remains_YearQuickFilter`: 01.01.–31.12. laufendes Jahr.
- `PrevMonth_FromCustomRange_ShiftsBoundaries`: freier Zeitraum → Grenzen exakt −1 Monat.
- `PrevMonth_ClampOnShortMonth`: Bis 31.03. → Bis 28./29.02. (AddMonths-Clamping).
- `PrevMonth_NullRange_AnchorsOnCurrentMonth`: Range (null,null) → Kalendervormonat.
- `Repeated_PrevMonth_WalksBackwards`: 2× PrevMonth → 2 Monate zurück.
- `AnalysisButtons_AreFiveInOrder`: Ids PrevMonth, ThisMonth, NextMonth, PrevYear,
  NextYear in Reihenfolge + ThisYear am Ende.
- `EachButton_RaisesReload`: UiActionRequested-Event „Reload" gefeuert.

### E2E `FinanceManager.Tests.E2E/Tests/Budget/BudgetAnalysisNavigationPlaywrightTests.cs` (neu)

Login → `/list/budget/purposes` → Default-Zeitraum = aktueller Monat:
- Klick „Vormonat" → Von/Bis-Inputs zeigen Kalendervormonat (Werte aus
  `.list-filter-range input` lesen). Klick „Nächster Monat" → zurück am aktuellen
  Monat. 2× „Nächster Monat" → Folgemonat +1. „Vorjahr" → Monatsbreite bleibt,
  Jahr −1. „Aktuelles Jahr" → Jahresspanne. Screenshot je Zustand.
  (Ribbon-Button-Lokalisierung: nach Label-Text lokalisieren; Sprache der Test-User
  prüfen — `EnsureUserAsync` ohne Language-Param → vermutlich "de"; abgesichert
  über `title`-Attribut oder beide Label-Varianten.)
- Berichtsseite `/reports/budget`: Buttons der gleichen 5 vorhanden + Klick
  „Nächster Monat" verschiebt sichtbaren Zeitraum (Monatslabel im Bericht ändert
  sich); Screenshot.

## Akzeptanzszenarien

1. Übersicht: 2× „Vormonat" → Zeitraum = Monat −2 (relativ, wiederholbar).
2. Übersicht: freier Zeitraum + „Vorjahr" → gleiche Breite, Jahr −1.
3. Bericht: unverändert, Buttons bleiben funktional (Regression).
4. E2E-Screenshots sichtgeprüft → attachmentsPath.

## Risiken

- Ribbon-Selektion in E2E (Label-Text vs. `title`-Attr) → Selector vor Ort an
  RibbonTests-Muster angleichen.
- `DateTime.Today`-Abhängigkeit: Tests relativ rechnen; Monatsgrenzen (1./31.)
  robust via AddMonths-Clamping.
