# Abnahme Schritt 3 — analyse-buttons (#603)

**Datum:** 2026-11-29
**Branch:** `task/issue-614-…-schritt-3-analyse-buttons` → gemergt in Projekt-Basisbranch

## Scope

Einheitliche relative Zeitraumnavigation: Budgetübersicht (`/list/budget/purposes`) und Budgetbericht (`/reports/budget`) bieten dieselben 5 Analyse-Buttons (Vormonat, Aktueller Monat, Nächster Monat, Vorjahr, Nächstes Jahr), die relativ zur angezeigten Ansicht verschieben und wiederholbar sind. Der Bericht war bereits konform — geändert wurde die Übersicht plus Verhärtung an beiden Stellen.

## Umgesetzte Änderungen

- `BudgetPurposeListViewModel`: Ribbon-Analyse-Gruppe neu — 6 Buttons (`PrevMonth`, `ThisMonth`, `NextMonth`, `PrevYear`, `ThisYear`, `NextYear`), `ThisYear` als behaltener Jahres-Schnellfilter auf Position 5 (symmetrische Spalten prev/this/next). Neue `ShiftRange(months, years)`: kalenderexakte Verschiebung beider Grenzen, Tages-Clamping, Monatsende-Snap, Ankerableitung bei einseitig fehlender Grenze, Inversions-Normalisierung, Überlauf-No-Op, `RaiseUiActionRequested("Reload")`.
- `BudgetReportViewModel`: Tooltips auf den 5 Buttons; Überlauf-Guards in `ShiftAsOfMonthAsync`/`ShiftAsOfYearAsync`.
- `ListPage.razor`: `LoadingBarService` injiziert; `Reload`/`ClearSearch` laufen jetzt über `LoadingBar.RunAsync` (Ladebalken deckt den Reload ab — wie im Bericht).
- `Resources/Pages.{de,en}.resx` + neutral: 6 neue `Ribbon_Tip_*`-Keys.
- `Docs/help/benutzeroberflaeche/business-rules.md`: Ribbon-Tabelle der Budgetübersicht aktualisiert.

## Nachweise

### Unit-Tests (24 grün)

`FinanceManager.Tests/ViewModels/BudgetPurposeListAnalysisNavigationTests.cs`: Verschieben relativ, Breitenerhalt, Monatsende-Snap/Clamping (31.03→28.02., 29.02→28.02.), Jahreswechsel Dez→Jan, Null-/einseitige Anker, invertierte Range, Überlauf-No-Op, Wiederholbarkeit, Button-Reihenfolge, Reload-Event, API-Verify mit verschobenem Range.

### E2E-Abnahme (2 Tests, Playwright, grün)

`FinanceManager.Tests.E2E/Tests/Budget/BudgetAnalysisNavigationPlaywrightTests.cs`:

1. **Übersicht:** Klickfolge Vormonat×2 → Nächster Monat×2 → Vorjahr → Aktuelles Jahr; Von/Bis-Felder nach jedem Schritt assertiert (ISO `yyyy-MM-dd`).
2. **Bericht:** alle 5 Buttons vorhanden; Nächster-Monat-Klick verschiebt `.budget-report-period` korrekt.

### Screenshots (sichtgeprüft, in attachmentsPath)

| Datei | Zeigt |
|---|---|
| `s3-budget-overview-prev-month.png` | Übersicht, Von/Bis = 01.10.–31.10.2026 nach Vormonat-Klick; symmetrische Button-Spalten |
| `s3-budget-overview-prev-year.png` | Übersicht, Von/Bis = 01.11.–30.11.2025 nach Vorjahr-Klick |
| `s3-budget-overview-this-year.png` | Übersicht, Von/Bis = 01.01.–31.12.2026 nach Aktuelles-Jahr-Klick |
| `s3-budget-report-analysis-buttons.png` | Bericht mit 5 Analyse-Buttons, Periode 01.01.–31.12.2026 (nach Nächster-Monat) |

## Bewusste Entscheidungen / Abweichungen

- **6. Button „Aktuelles Jahr"** behalten (kein Funktionsverlust); strikte AC-Lesart „dieselben fünf" → die 5 stehen in identischer relativer Reihenfolge.
- **Bericht „Aktueller Monat"** setzt den `AsOfDate`-Anker auf Monatsende heute; das `Months`-Fenster (Standard 12) bleibt — dokumentierte Anker-Semantik des Berichts.
- **Breitenerhaltung** bei relativem Schieben; **„Aktuell"-Buttons** sind absolute Sprünge (setzen Breite auf Monat/Jahr).
- **Reload-Guard** (überlappende Loads bei Schnellklicks) bleibt pre-existing Schwäche des Listen-Frameworks — dokumentiert, kein neuer Defekt.

## Reviews

- Plan-Check: bestanden (Marginalien übernommen)
- Code-Review: 1 Hoch (CS1572), 2 Mittel (Überlauf, Load-Guard), 4 Niedrig — behoben bzw. dokumentiert
- Usability-Review: 2 Mittel (Icon-Ambiguität, LoadingBar), Rest Niedrig/Info — behoben bzw. dokumentiert
