# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Alle Aktionen als Symbolbuttons mit Icon + lokalisiertem Tooltip, keine Textbuttons | `PostingRulesPanel`: vier `btn btn-sm`-Buttons → `icon-btn`/`icon-btn danger` mit Sprite-Icons (`#edit`, `#trash`, `#undo`, `#plus`/`#check`) und `title` aus vorhandenen `PostingRules_*_Button`-Keys (plan.md Z. 83–88) | E2E `ManagePostingRules_ViaUi` (angepasst, `icon-btn`-Selektoren via `use[href*='#<icon>']`); Akzeptanzszenario 1 assertiert „keine Textbuttons, nur icon-btn mit Tooltips" | Abgedeckt |
| 2. Panel nicht mehr dauerhaft sichtbar | `CardPage.razor` Z. 48 entfernen; `if`-Block für `LinkedIbansPanel` bleibt (plan.md Z. 92) | E2E `PostingRulesOverlay_OpensAndCloses_ViaRibbon` assertiert `.posting-rules-panel` vor Klick nicht sichtbar | Abgedeckt |
| 3. Neuer Aktionsbutton in Gruppe „Buchungen" öffnet Overlay | Dritte `UiRibbonAction` `OpenPostingRules` in `bookingActions` (Z. 318–345), Callback raist `UiOverlaySpec(typeof(PostingRulesPanel), { Vm, Localizer, OverlayTitle })` — `OverlayHost` auf `CardPage` Z. 77 verdrahtet | Unit `GetRibbon_BookingsGroup_ContainsOpenPostingRulesAction`, `OpenPostingRules_Callback_RaisesUiOverlaySpec` (`ComponentType`, `Parameters`, `Modal`); E2E Ribbon-Klick `button#OpenPostingRules` | Abgedeckt |
| 4. Overlay schließbar, Karteninhalt bleibt | `OverlayHost.CloseOverlay()` via Schließen-`icon-btn` (`#clear`) oder Backdrop-Klick (verifiziert `OverlayHost.razor` Z. 10, 35) | E2E `PostingRulesOverlay_OpensAndCloses_ViaRibbon` (öffnen → schließen → Karte intakt) | Abgedeckt |
| 5. Regeln auflisten/anlegen/bearbeiten/löschen + Validierung wie bisher | Handler unverändert (`BeginEdit`, `CancelEdit`, `SaveRuleAsync` inkl. Leer-Pattern-Vorprüfung, `RemoveRuleAsync`) | E2E `ManagePostingRules_ViaUi` (angepasst + Edit-Schritt), `ManagePostingRules_ValidationError_ViaUi` (angepasst) | **Lücke** — Enter-Tasten-Speicherung (FA-5) ohne Testnachweis, siehe unten |
| 6. Ribbon-Button bei neuem Konto nicht funktional | `Disabled = Account == null \|\| Account.Id == Guid.Empty` + Conditional-Tooltip `Ribbon_PostingRules_DisabledHint`; `OnItemClick` no-op bei Disabled (verifiziert `Ribbon.razor` Z. 319) | Unit `OpenPostingRules_Disabled_ForNewAccount`/`_Enabled_ForSavedAccount`; E2E `PostingRulesRibbon_Disabled_OnNewAccount` auf `/card/accounts/new` | Abgedeckt |
| 7. Neue UI-Texte de + en lokalisiert | Neue Keys `Ribbon_PostingRules`, `Ribbon_PostingRules_DisabledHint` in `Pages.resx`/`de`/`en`; Tooltips nutzen vorhandene `PostingRules_*_Button`-Keys (in allen drei Dateien vorhanden, verifiziert) | Akzeptanzszenario 5: manuelle Abnahme/resx-Review (kein automatisierter Test — im Repo existiert konventionsgemäß kein resx-Parity-Test) | Abgedeckt (manueller Nachweis, siehe Hinweise) |
| 8. Übrige Kartenbereiche unverändert | `LinkedIbansPanel`, `SavingsPlanLinkedAccountsPanel`, andere Ribbon-Gruppen bleiben; kein Eintrag im `OnUiActionRequested`-Switch nötig | Akzeptanzszenario 4 + E2E-Assertion „kein Panel vor Klick"; `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` bleibt grün (Gruppenreihenfolge unverändert) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Enter-Tasten-Speicherung (FA-5, letzter Punkt):** Die Anforderung verlangt explizit, dass Enter in der Muster-Eingabe weiterhin Speichern auslöst (`@onkeydown="OnKeyDownAsync"`, `PostingRulesPanel.razor` Z. 79, 198–201). Weder die bestehenden noch die geplanten Tests drücken Enter — alle speichern per Button-Klick. Da die Eingabezeile im umgebauten Markup-Bereich liegt, bliebe ein versehentlicher Verlust des Handlers unentdeckt. Empfehlung: In `ManagePostingRules_ViaUi` einen Anlege-Schritt über `PressAsync("Enter")` im Muster-Feld ergänzen (oder dedizierte Assertion) sowie einen Task-Eintrag in der Taskliste.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Ribbon → Overlay öffnen → Regel anlegen/bearbeiten/löschen (AK 1, 3, 5) | `ManagePostingRules_ViaUi` (angepasst: Ribbon-Klick vorab, `.split-dialog .posting-rules-panel`, `icon-btn`-Selektoren, Edit-Schritt) | Abgedeckt — Enter-Pfad fehlt (s. o.) |
| Overlay-Lebenszyklus: öffnen, schließen, Karte darunter intakt; Panel vor Klick nicht sichtbar (AK 2, 4, 8) | Neu: `PostingRulesOverlay_OpensAndCloses_ViaRibbon` | Abgedeckt |
| Validierungsfehler im Overlay (AK 5) | `ManagePostingRules_ValidationError_ViaUi` (angepasst) | Abgedeckt |
| Ribbon-Button disabled bei Neuanlage inkl. Tooltip (AK 6) | Neu: `PostingRulesRibbon_Disabled_OnNewAccount` | Abgedeckt |
| Lokalisierung de/en (AK 7) | Kein automatisierter Test; manuelle Abnahme/resx-Review | Nicht erforderlich mit Begründung — repo-weit existiert kein automatisierter resx-Parity-Test; beide neuen Keys werden in allen drei resx-Dateien angelegt (Tasks 1–2) |

## Fehlende oder unvollständige Planbestandteile

- [ ] Testschritt/Test für Enter-Tasten-Speicherung ergänzen (siehe Testanforderungen); entsprechenden Task in `...-tasks.md` nachziehen.

## Hinweise

- Gegenprobe bestanden: `CollectionAccountPlaywrightTests` (`.linked-ibans-panel`, `button.btn-primary`/`btn-danger` Z. 127/186) und `LoadingBarPlaywrightTests` (`.split-dialog` auf `/reports/budget`, Z. 242) betreffen andere Panels/Seiten — die Plan-Aussage „keine weiteren bestehenden Tests betroffen" ist korrekt.
- Die Designentscheidung `OverlayTitle` statt `Title` ist zwingend richtig: `OverlayHost.razor` Z. 41–43 filtert nur `OverlayTitle`/`Visible` aus den `DynamicComponent`-Parametern; `Title` würde ungefiltert durchgereicht und ohne `[Parameter] string? Title` am Panel eine Ausnahme werfen.
- `typeof(PostingRulesPanel)` im ViewModel benötigt `using FinanceManager.Web.Components.Shared` oder den vollqualifizierten Namen (Muster: `SavingsPlanCardViewModel` Z. 341 verwendet FQN) — im Plan erwähnt.
- AK 7 stützt sich auf manuelle Abnahme; optional ließe sich der Disabled-Tooltip-Unit-Test nutzen, um das Vorhandensein beider neuen Keys zumindest strukturell mit abzusichern (DummyLocalizer liefert Key-Namen, deckt echte resx-Inhalte aber nicht ab).
- `EnumCoverageTests`, Backup-DTOs und EF-Migrationen sind korrekt aus dem Scope gehalten (keine neuen Enum-Mitglieder, keine persistierten Felder).
