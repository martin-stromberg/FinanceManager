# Tasks: Kontierungsregeln-Panel als Overlay mit Ribbon-Aufruf und Symbolbuttons

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Lokalisierung | resx-Key `Ribbon_PostingRules` in `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` anlegen (de „Kontierungsregeln", en „Posting rules") | Offen | — |
| 2 | Lokalisierung | resx-Key `Ribbon_PostingRules_DisabledHint` in `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` anlegen (Tooltip-Hinweis bei ungespeichertem Konto) | Offen | — |
| 3 | UI | `PostingRulesPanel.razor`: Zeilenbuttons Bearbeiten/Entfernen auf `icon-btn` (+`danger`) mit Sprite-Icons (`#edit`/`#trash`) und `title`-Tooltips umstellen | Offen | — |
| 4 | UI | `PostingRulesPanel.razor`: Eingabezeilen-Buttons Abbrechen (`#undo`, nur im Edit-Modus) und Hinzufügen/Speichern (`#plus`/`#check`) auf `icon-btn` mit `title`-Tooltips umstellen | Offen | — |
| 5 | UI | `PostingRulesPanel.razor`: interne Titelzeile (`PostingRules_Section_Title`-`<tr>`) und `margin-top` am Panel-Div entfernen | Offen | — |
| 6 | Logik | `BankAccountCardViewModel.GetRibbonRegisterDefinition`: `UiRibbonAction` `OpenPostingRules` (Icon `#filters`, `Small`, Disabled bei ungespeichertem Konto, Conditional-Tooltip) in `bookingActions` ergänzen; Callback raist `UiOverlaySpec(typeof(PostingRulesPanel), { Vm, Localizer, OverlayTitle })` | Offen | — |
| 7 | UI | `CardPage.razor`: eingebettetes `<PostingRulesPanel>` (Z. 48) entfernen; `if`-Block mit `LinkedIbansPanel` unverändert erhalten | Offen | — |
| 8 | Tests | `AccountDetailViewModelTests`: Test `GetRibbon_BookingsGroup_ContainsOpenPostingRulesAction` (Button vorhanden, `Small`) | Offen | — |
| 9 | Tests | `AccountDetailViewModelTests`: Tests für Disabled-Verhalten (neu vs. gespeichert) und Tooltip | Offen | — |
| 10 | Tests | `AccountDetailViewModelTests`: Test `OpenPostingRules_Callback_RaisesUiOverlaySpec` (`ComponentType`, `Parameters` mit `Vm`/`Localizer`/`OverlayTitle`, `Modal`) | Offen | — |
| 11 | E2E-Tests | `ManagePostingRules_ViaUi` anpassen: Ribbon-Klick `button#OpenPostingRules` vorab, `.split-dialog .posting-rules-panel` abwarten, `icon-btn`-Selektoren (via `use[href*='#<icon>']`), Edit-Schritt ergänzen; Regel-Anlage via `PressAsync("Enter")` im Muster-Feld auslösen (Nachweis Enter-Speicherung, FA-5) | Offen | — |
| 12 | E2E-Tests | `ManagePostingRules_ValidationError_ViaUi` anpassen: Overlay-Öffnung + `icon-btn`-Selektor für Hinzufügen | Offen | — |
| 13 | E2E-Tests | Neuer Test `PostingRulesOverlay_OpensAndCloses_ViaRibbon`: Panel vor Klick nicht sichtbar → Ribbon-Klick → Overlay sichtbar → Schließen-Button → Overlay weg, Karte intakt | Offen | — |
| 14 | E2E-Tests | Neuer Test `PostingRulesRibbon_Disabled_OnNewAccount`: `button#OpenPostingRules` auf `/card/accounts/new` disabled inkl. Tooltip-Hinweis | Offen | — |
