# Umsetzungsplan: Speichern von Massenänderungen

## Ziel
Die Quick-Edit-Ansicht für Kontoauszugszeilen soll die Konsistenz zwischen Ribbon-Speichern-Button, Zeilenvalidierung und Valuta-Datum-Übernahme herstellen.

## Akzeptanzkriterien und Tests

### AC-1: Speichern-Button genau dann aktiviert, wenn alle sichtbaren Zeilen gültig
- **E2E/Unit-Happy-Path:**
  - `RibbonSaveQuickEdit_IsEnabled_WhenAllVisibleRowsValid` (ViewModel): Fülle alle sichtbaren Zeilen mit Buchungsdatum, Valutadatum, Betrag, Buchungsbeschreibung/Verwendungszweck -> `SaveQuickEdit`-Action ist nicht Disabled.
  - `E2E_SaveQuickEditButton_Enabled_AfterCompletingAllVisibleRows` (Playwright): Öffne Quick-Edit, fülle alle sichtbaren Zeilen vollständig, prüfe `aria-disabled`/`disabled` des Save-Buttons.
- **Negative/Edge:**
  - `RibbonSaveQuickEdit_IsDisabled_WhenAnyVisibleRowInvalid` (ViewModel): Eine sichtbare Zeile hat kein Buchungsdatum oder Betrag oder weder Buchungsbeschreibung noch Verwendungszweck -> Save-Button disabled.
  - `RibbonSaveQuickEdit_IsDisabled_WhenValutaMissing` (ViewModel): Eine sichtbare Zeile hat leeres Valutadatum (und Buchungsdatum) -> disabled.

### AC-2: Live-Datenprüfung bei Fokuswechsel
- **E2E:**
  - `E2E_QuickEdit_Blur_LeavingInvalidRow_ShowsWarningIconAndHint` (Playwright): Verlasse eine unvollständige Zeile per Tab -> Warnsymbol vor Buchungsdatum sichtbar und Hinweis erscheint.
- **Unit:**
  - `ValidateQuickEditRow_UpdatesHint_ForCurrentRow` (ViewModel): Aufruf `ValidateQuickEditRow(id)` füllt `_entryHints` für diese ID.

### AC-3: Symbol vor Buchungsdatum für unvollständige Zeilen
- **E2E:**
  - `E2E_QuickEdit_InvalidRow_ShowsIconBeforeBookingDate` (Playwright): Prüft, dass ein `!`-Symbol (oder gleichwertiges Warnsymbol) in der Buchungsdatum-Zelle vor dem Input erscheint.

### AC-4: Vollständigkeitsregeln pro Zeile
- **Unit-Happy:**
  - `ValidateRow_AllowsRow_WithOnlyBookingDescriptionAndNoSubject` (ViewModel): Buchungsbeschreibung vorhanden, Verwendungszweck leer -> gültig.
  - `ValidateRow_AllowsRow_WithOnlySubjectAndNoBookingDescription` (ViewModel): Verwendungszweck vorhanden, Buchungsbeschreibung leer -> gültig.
- **Unit-Negative:**
  - `ValidateRow_Fails_WhenBookingDescriptionAndSubjectMissing` (ViewModel): beide leer -> Fehler.
  - `ValidateRow_Fails_WhenBookingDateInvalidOrTooOld` (ViewModel): `DateTime.MinValue` oder Jahr < 1000 -> Fehler.
  - `ValidateRow_Fails_WhenValutaDateMissing` (ViewModel): Valutadatum null oder Jahr < 1000 -> Fehler.
  - `ValidateRow_Fails_WhenAmountMissingOrZero` (ViewModel): bereits teilweise abgedeckt, wird erweitert.

### AC-5: Erlaubte optionale Felder
- **Unit/E2E-Happy:**
  - `ValidateRow_AllowsMissingRecipient` (ViewModel): leerer Empfänger -> gültig.
  - `ValidateRow_AllowsMissingPurpose` (ViewModel): leerer Verwendungszweck (wenn Buchungsbeschreibung vorhanden) -> gültig.
- **UI:**
  - `E2E_QuickEdit_EmptyRecipientInput_ShowsBankPlaceholder` (Playwright): Empfänger-Eingabefeld leer zeigt `placeholder` mit Bankname.

### AC-6: Konsistenz Ribbon-Action / Einzeilprüfung
- **Unit:**
  - `QuickEditRowsAreValid_UsesSameRulesAsValidateRow` (ViewModel): Füge Fehler via `ValidateRow` ein und stelle sicher, dass `QuickEditRowsAreValid` exakt den gleichen booleschen Wert liefert.

### AC-7: Valutadatum-Übernahme
- **E2E (Bestand erhalten):**
  - `QuickEdit_BookingDateChange_ShouldCopyToEmptyValutaDateOnly` (bereits vorhanden) muss weiterhin passen.
  - `QuickEdit_BookingDateChange_DoesNotOverwriteDifferentValuta` (bereits vorhanden) muss weiterhin passen.
- **E2E-Negative/Edge:**
  - `E2E_QuickEdit_PartialYearInput_DoesNotCopyToValuta` (Playwright): Tippe in Buchungsdatum nacheinander `2`, `0`, `0`, `2` und wechsle bei Bedarf Fokus -> Valuta bleibt leer, bis die Jahreszahl vollständig und >= 1000 ist.
- **Unit:**
  - `SetBookingDateFromUi_DoesNotAcceptYear0002` (ViewModel): Eingabe `"0002-01-01"` wird nicht übernommen, Valuta bleibt unverändert.
  - `SetBookingDateFromUi_CopiesToEmptyValuta` (ViewModel): gültiges Datum, leere Valuta -> Valuta wird gleichgesetzt.
  - `SetBookingDateFromUi_CopiesWhenValutaEqualsOldBooking` (ViewModel): Valuta war gleich altem Buchungsdatum -> wird auf neues Buchungsdatum übernommen.
  - `SetBookingDateFromUi_KeepsDifferentValuta` (ViewModel): Valuta ist anders als Buchungsdatum -> bleibt erhalten.

## Implementierungsschritte

1. **`StatementDraftEntriesListViewModel` erweitern**
   - `_bankContactName` Feld und `BankContactName` Property hinzufügen; in `LoadPageAsync` aus `ContactNames` oder per `Contacts_GetAsync` befüllen.
   - `ValidateRow` anpassen:
     - Buchungsdatum: gültig, Jahr >= 1000, kein `DateTime.MinValue`.
     - Valutadatum: gültig, Jahr >= 1000, nicht null.
     - Betrag: Pflicht, ungleich 0.
     - Buchungsbeschreibung **oder** Verwendungszweck: mindestens einer muss angegeben sein (Trim).
     - Empfänger optional.
   - `QuickEditRowsAreValid` so anpassen, dass **alle** sichtbaren Quick-Edit-Zeilen (außer Placeholder) validiert werden, nicht nur geänderte/neue.
   - `ValidateQuickEditRow(Guid id)` hinzufügen: führt `ValidateRow` für eine Zeile aus, aktualisiert `_entryHints` und ruft `BuildRecords` + `RaiseStateChanged` auf.
   - `SetBookingDateFromUi(Guid id, string? rawDate)` und `SetValutaDateFromUi(Guid id, string? rawDate)` hinzufügen:
     - Parse mit `DateTime.TryParseExact("yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)`.
     - Nur übernehmen, wenn `dt.Year >= 1000`.
     - Für Buchungsdatum: Valuta nur kopieren, wenn Valuta leer war **oder** dem bisherigen Buchungsdatum entsprochen hat (Vergleich mit Wert vor der Änderung).
   - `GetRecipientPlaceholder(Guid id)` oder `RecipientPlaceholder` Property anbieten.

2. **`QuickEditTable.razor` anpassen**
   - `OnDateChanged` und `OnValutaChanged` delegieren an `vm.SetBookingDateFromUi` bzw. `vm.SetValutaDateFromUi`.
   - `@onblur` auf alle editierbaren Inputs in einer Zeile setzen und `OnRowBlur(item.Id)` aufrufen, der `vm.ValidateQuickEditRow` aufruft.
   - In der Buchungsdatum-Zelle vor dem Input ein Warnsymbol anzeigen, wenn `rec.Hint` nicht leer ist.
   - Empfänger-Input `placeholder` mit `vm.RecipientPlaceholder` befüllen.

3. **`StatementDraftCardViewModel` (Ribbon)**
   - Keine Änderung an der Ribbon-Definition nötig, da `QuickEditRowsAreValid` jetzt alle sichtbaren Zeilen prüft.

4. **Tests**
   - Unit-Tests in `StatementDraftCardViewModelTests.cs` erweitern (Validierungsregeln, Save-Button-Logik, Valuta-Übernahme).
   - E2E-Tests in `StatementDraftQuickEditValueTakeoverE2ETests.cs` erweitern (Partiell-Jahres-Input, Blur-Validierung, Warnsymbol).

5. **Lokalisierung**
   - Keine neuen Ressourcen nötig; Fehlertexte werden aus `ValidateRow` englisch zurückgegeben (wie bisher) und ggf. über `localizer` aufgelöst. Warnsymbol-Tooltip/Alt optional.

## Offene Punkte

Keine.
