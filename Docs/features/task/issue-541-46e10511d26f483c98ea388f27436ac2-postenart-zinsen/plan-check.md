# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Die in `plan-check.1.md` gemeldete Lücke (fehlender Testnachweis für die Enter-Tasten-Speicherung, FA-5) wurde behoben: `ManagePostingRules_ViaUi` legt die Regel nun per `PressAsync("Enter")` im Muster-Feld an (plan.md Z. 146, 162, 171, 182; Akzeptanzszenario 1 Z. 191; Task 11 der Taskliste).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Alle Aktionen als Symbolbuttons mit Icon + lokalisiertem Tooltip, keine Textbuttons | `PostingRulesPanel`: vier `btn btn-sm`-Buttons → `icon-btn`/`icon-btn danger` mit Sprite-Icons (`#edit`, `#trash`, `#undo`, `#plus`/`#check` — alle im Sprite vorhanden, verifiziert `sprite.svg` Z. 22/26/31/33/41/86/127) und `title` aus vorhandenen `PostingRules_*_Button`-Keys (plan.md Z. 83–88) | E2E `ManagePostingRules_ViaUi` (angepasst, `icon-btn`-Selektoren via `use[href*='#<icon>']`); Akzeptanzszenario 1 assertiert „keine Textbuttons" | Abgedeckt |
| 2. Panel nicht mehr dauerhaft sichtbar | `CardPage.razor` Z. 48 entfernen; `if`-Block bleibt für `LinkedIbansPanel` (Z. 49–52, Bedingung `EffectiveIsCollectionAccount` — verifiziert) | E2E `PostingRulesOverlay_OpensAndCloses_ViaRibbon` assertiert `.posting-rules-panel` vor Klick nicht sichtbar | Abgedeckt |
| 3. Neuer Aktionsbutton in Gruppe „Buchungen" öffnet Overlay | Dritte `UiRibbonAction` `OpenPostingRules` in `bookingActions` (Z. 318–345 verifiziert), Callback raist `UiOverlaySpec(typeof(PostingRulesPanel), { Vm, Localizer, OverlayTitle })` — `OverlayHost` auf `CardPage` Z. 77 verdrahtet; kein Switch-Eintrag nötig (`UiOverlaySpec`-Payload wird allein vom `OverlayHost` verarbeitet, analog `OpenAttachments`) | Unit `GetRibbon_BookingsGroup_ContainsOpenPostingRulesAction`, `OpenPostingRules_Callback_RaisesUiOverlaySpec` (`ComponentType`, `Parameters`, `Modal`); E2E Ribbon-Klick `button#OpenPostingRules` | Abgedeckt |
| 4. Overlay schließbar, Karteninhalt bleibt | `OverlayHost.CloseOverlay()` via Schließen-`icon-btn` (`#clear`, verifiziert `OverlayHost.razor` Z. 35) oder Backdrop-Klick (Z. 10) | E2E `PostingRulesOverlay_OpensAndCloses_ViaRibbon` (öffnen → schließen → Karte intakt) | Abgedeckt |
| 5. Regeln auflisten/anlegen/bearbeiten/löschen + Validierung + Enter-Taste wie bisher | Handler unverändert (`BeginEdit`, `CancelEdit`, `SaveRuleAsync` inkl. Leer-Pattern-Vorprüfung, `RemoveRuleAsync`, `OnKeyDownAsync`) | E2E `ManagePostingRules_ViaUi` (angepasst: Ribbon-Klick, `icon-btn`-Selektoren, Edit-Schritt, **Anlage via `PressAsync("Enter")`** — deckt `OnKeyDownAsync` → `SaveRuleAsync`), `ManagePostingRules_ValidationError_ViaUi` (angepasst) | Abgedeckt |
| 6. Ribbon-Button bei neuem Konto nicht funktional | `Disabled = Account == null \|\| Account.Id == Guid.Empty` + Conditional-Tooltip `Ribbon_PostingRules_DisabledHint`; `OnItemClick` no-op bei Disabled (verifiziert `Ribbon.razor` Z. 319, zusätzlich HTML-`disabled` Z. 70/131) | Unit `OpenPostingRules_Disabled_ForNewAccount`/`_Enabled_ForSavedAccount`; E2E `PostingRulesRibbon_Disabled_OnNewAccount` auf `/card/accounts/new` | Abgedeckt |
| 7. Neue UI-Texte de + en lokalisiert | Neue Keys `Ribbon_PostingRules`, `Ribbon_PostingRules_DisabledHint` in `Pages.resx`/`de`/`en`; Tooltips nutzen vorhandene `PostingRules_*_Button`-Keys (in allen drei Dateien vorhanden, verifiziert); Overlay-Titel via `PostingRules_Section_Title` | Akzeptanzszenario 5: manuelle Abnahme/resx-Review (im Repo existiert konventionsgemäß kein automatisierter resx-Parity-Test) | Abgedeckt (manueller Nachweis) |
| 8. Übrige Kartenbereiche unverändert | `LinkedIbansPanel`, `SavingsPlanLinkedAccountsPanel`, andere Ribbon-Gruppen/Aktionen bleiben; kein Eintrag im `OnUiActionRequested`-Switch nötig | Akzeptanzszenario 4 + E2E-Assertion „kein Panel vor Klick"; `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` bleibt grün (nur Gruppenreihenfolge) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Ribbon → Overlay öffnen → Regel anlegen (per Enter) → bearbeiten → löschen → schließen (AK 1, 3, 5 inkl. FA-5 Enter) | `ManagePostingRules_ViaUi` (angepasst: `button#OpenPostingRules`-Klick vorab, `.split-dialog .posting-rules-panel`, `icon-btn`-Selektoren via `use[href*='#<icon>']`, Edit-Schritt, `PressAsync("Enter")`) | Abgedeckt |
| Overlay-Lebenszyklus: Panel vor Klick nicht sichtbar, öffnen, schließen, Karte intakt (AK 2, 4, 8) | Neu: `PostingRulesOverlay_OpensAndCloses_ViaRibbon` | Abgedeckt |
| Validierungsfehler im Overlay: leeres Muster → Fehlerzeile, keine Regel, `option[value='0']` nicht vorhanden (AK 5) | `ManagePostingRules_ValidationError_ViaUi` (angepasst: Overlay-Öffnung + `icon-btn`-Selektor) | Abgedeckt |
| Ribbon-Button disabled bei Neuanlage inkl. Tooltip-Hinweis (AK 6) | Neu: `PostingRulesRibbon_Disabled_OnNewAccount` | Abgedeckt |
| Lokalisierung de/en (AK 7) | Kein automatisierter Test; manuelle Abnahme/resx-Review | Nicht erforderlich mit Begründung — repo-weit existiert kein automatisierter resx-Parity-Test; beide neuen Keys werden in allen drei resx-Dateien angelegt (Tasks 1–2) |

## Hinweise

- **Gegenprobe bestanden:** `posting-rules-panel`-Selektoren kommen nur in `AccountPostingRulesPlaywrightTests` vor; `button.btn-primary`/`btn-danger` in `CollectionAccountPlaywrightTests` (`.linked-ibans-panel`, Z. 127/186), `CollectionAccountImportPlaywrightTests` (Z. 264/400) und `LoadingBarPlaywrightTests` (`.split-dialog` auf `/reports/budget`, Z. 242) betreffen andere Panels/Seiten. Die Plan-Aussage „keine weiteren bestehenden Tests betroffen" ist korrekt.
- **`OverlayTitle` braucht `.Value`:** Das Plan-Snippet `Parameters["OverlayTitle"] = localizer["PostingRules_Section_Title"]` (Z. 31) fehlt `.Value`. `OverlayHost.GetOverlayTitle` prüft `v is string` (Z. 106) — ein `LocalizedString` würde die Prüfung nicht bestehen, der Titel fiele auf `ComponentType.Name` („PostingRulesPanel") zurück. Alle bestehenden Aufrufer übergeben `.Value` (z. B. `UserCardViewModel` Z. 403, `SecurityPricesListViewModel` Z. 144). Bei der Umsetzung `localizer[...].Value` verwenden — analog zur dokumentierten `Title`-Falle.
- **`#undo` (Abbrechen) wird nur auf Sichtbarkeit, nicht auf Klick-Wirkung getestet** — der Handler `CancelEdit` bleibt explizit unverändert, und Szenario 1 assertiert `#undo` im Edit-Modus sichtbar (die refactor-relevante Sichtbarkeitsregel). Optional könnte der Edit-Schritt um einen `#undo`-Klick erweitert werden; aus Sicht der Planprüfung kein Mangel.
- `typeof(PostingRulesPanel)` im ViewModel benötigt `using FinanceManager.Web.Components.Shared` oder den vollqualifizierten Namen — im Plan erwähnt (Z. 79).
- `PostingRules` ist mit `[]` initialisiert (`BankAccountCardViewModel` Z. 52) und `LoadAsync` lädt Regeln nur bei bestehenden Konten (Z. 122) — die Scope-Klärung („`PostingRules` noch nicht geladen: entfällt") ist korrekt, da der Button bei Neuanlage ohnehin disabled ist.
- `EnumCoverageTests`, Backup-DTOs und EF-Migrationen sind korrekt aus dem Scope gehalten (keine neuen Enum-Mitglieder, keine persistierten Felder).
