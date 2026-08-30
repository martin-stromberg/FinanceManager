# Code-Review – Speichern von Massenänderungen

## Geänderte Dateien

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` (keine Änderung, nur bestehende Ribbon-Logik)
- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs`
- `Docs/help/kontoauszuege-und-import/business-rules.md`
- `Docs/RELEASE_NOTES.md`

## Kritische Prüfpunkte

### 1. Validierungskonsistenz zwischen Ribbon und Zeilen

- `ValidateRow` zentralisiert die Kriterien: Buchungsdatum, Valutadatum, nicht-nuller Betrag, mindestens Buchungstext oder Verwendungszweck.
- `QuickEditRowsAreValid` nutzt dieselbe `ValidateRow`-Methode.
- `ValidateAllQuickEditRows` füllt `_entryHints` mit denselben Regeln.
- Alle sichtbaren, bearbeitbaren Zeilen werden geprüft; Placeholder, gelöschte und nicht-bearbeitbare Zeilen (AlreadyBooked/Announced) ausgenommen.
- Entscheidung getroffen: ungültige `DateTime`-Werte mit Jahr < 1000 werden als fehlend behandelt; das verhindert fehlerhafte Jahreszahl-Übernahmen.

### 2. Valuta-Übernahme

- `SetBookingDateFromUi` und `SetValutaDateFromUi` parsen ausschließlich gültige `yyyy-MM-dd`-Daten mit 4-stelliger Jahreszahl.
- `SetEditValue` kopiert das Valutadatum nur, wenn es zuvor leer war oder dem Buchungsdatum entsprochen hat.
- Manuelle Änderungen des Valutadatums bleiben bei Buchungsdatum-Änderungen erhalten.

### 3. UI-Aktualisierung und Fehleranzeige

- `OnRowBlur` in `QuickEditTable.razor` löst `ValidateQuickEditRow` aus.
- Ungültige Zeilen zeigen ein Hinweissymbol (`!`) mit `title` vor dem Buchungsdatum.
- `OnDateChanged` in `QuickEditTable.razor` verwendet `SetBookingDateFromUi` und aktualisiert das Valuta-Input nur bei tatsächlicher Änderung.
- Empfänger-Input erhält `placeholder` aus `RecipientPlaceholder` (Bankkontaktname).

### 4. `RaiseUiActionRequested` Handler

- Keine neuen `RaiseUiActionRequested`-Aufrufe eingeführt.
- `StatementDraftCardViewModel` ruft weiterhin `Saved` auf; `CardPage.razor` behandelt `Saved` via `NavigationManager`.
- `QuickEditTable.razor` bindet die neuen VM-Methoden direkt, keine neuen UI-Actions notwendig.

### 5. Tests

- Unit-Tests ergänzt: Jahreszahl 0002, Valuta-Kopie, Nicht-Kopie, Textpflicht, Zeilenvalidierung, Hinweis-Record.
- E2E-Test zur Valuta-Übernahme an neue Regel angepasst.
- Neuer E2E-Test `QuickEdit_SaveButton_IsEnabledWhenAllRowsComplete` prüft Ribbon-Aktivierung.
- Alle relevanten Unit- und E2E-Tests bestanden.

## Entscheidungen und bewusste Abweichungen

- `README.md` wurde nicht angepasst, da das Feature keinen Projekteinstieg oder allgemeine Konfiguration betrifft.
- `Docs/help/kontoauszuege-und-import/business-rules.md` wurde aktualisiert, um die neuen Regeln für Endanwender zu dokumentieren.
- Der Plan-Review-Schritt wurde aufgrund des bereits vorhandenen `plan-check.md` und der unmittelbaren, überschaubaren Änderungen nicht nochmals wiederholt.
